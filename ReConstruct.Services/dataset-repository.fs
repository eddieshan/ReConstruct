namespace ReConstruct.Services

open System
open System.Globalization
open System.IO
open System.Threading.Tasks

open ReConstruct.Core
open ReConstruct.Core.IO
open ReConstruct.Core.Patterns

open ReConstruct.Data.Dicom
open ReConstruct.Data.Dicom.DicomTree

module internal DatasetRepository =

    let private bufferizeFile filePathName = (filePathName, File.ReadAllBytes(filePathName))

    let private datasetsEntries = Directory.GetDirectories(Config.DataPath) |> Array.indexed |> Map.ofArray

    let mutable private currentDataset = None

    let private setCurrent dataSet = currentDataset <- dataSet |> Some

    let private newIod (filePathName, buffer) =
        let root = DicomParser.getDicomTree buffer

        let sopClassUID =  Tags.SopClassUID |> findTagValue root
        let syntaxKey, syntaxData = 
            match Tags.TransferSyntaxUID|> findNode root with
            | Some t -> (Some t.Tag, Some t.ValueField)
            | None -> (None, None)

        let findSortOrder() = 
            let positionTag, locationTag =  Tags.Position|> findTagValue root, Tags.Location |> findTagValue root

            let parsePosition (value: string) =
                let split = value.Split [| '\\' |]
                Convert.ToDouble(split.[2], CultureInfo.InvariantCulture)

            match(positionTag, locationTag) with
            | (Some position, _)    -> position |> parsePosition
            | (None, Some location) -> location |> Utils.parseFloat |> double
            | (_, _)                -> 0.0

        // The viewer can only process the pixel data, if:
        // - the IOD contains pixel data.
        // - the image is not compressed.
        //let pixelData =  Tags.PixelData|> findTagValue

        //let hasPixels = match (sopClassUID, pixelData) with
        //                | (Some SopClass.CAT_UID, Some _) -> syntaxData |> TransferSyntax.notCompressed
        //                | (_, _)                          -> false

        let catSlice = match Tags.PixelData|> findTagValue root with
                        | Some _ -> syntaxData |> TransferSyntax.notCompressed |> Option.fromTrue(fun _ -> Cat.slice(buffer, root))
                        | _      -> None
        {
            DicomTree = root;
            FileName = filePathName |> Path.GetFileNameWithoutExtension;
            TransferSyntaxUID = syntaxKey |> Option.map Tags.getTagName |> Option.defaultValue "???";
            StudyInstanceUID =  Tags.StudyInstanceUID |> findTagValue root |> Option.defaultValue "???";
            SeriesInstanceUID = Tags.SeriesInstanceUID |> findTagValue root |> Option.defaultValue "???";
            SOPInstanceUID =  Tags.SopInstanceUID |> findTagValue root |> Option.defaultValue "???";
            SOPClassUID = sopClassUID |> Option.defaultValue "???";
            SOPClassName = sopClassUID |> Option.bind SopClass.Dictionary.TryFind |> Option.defaultValue "???";
            PatientName = Tags.PatientName |> findTagValue root |> Option.defaultValue "???";
            SortOrder = findSortOrder();
            CatSlice = catSlice;
        }

    let private loadDataset id =
        // Image files are processed in a sequential fork-join pattern.
        // Files are read sequentially to avoid contention on disk reads.
        // After each file is loaded, an async task is started to process the image.
        let asAsyncImageProcess content =
            async {
                return content |> newIod
            }

        let tasks = datasetsEntries 
                    |> Map.find id 
                    |> directoryFiles "*.*" SearchOption.AllDirectories
                    |> Array.map (bufferizeFile >> asAsyncImageProcess >> Async.StartAsTask)

        // Processed images are available when all async tasks have finished.
        let iods = Task.WhenAll(tasks).Result |> Array.sortBy(fun iod -> iod.SortOrder)

        //let iods = datasetsEntries 
        //            |> Map.find id 
        //            |> directoryFiles "*.dcm" SearchOption.AllDirectories
        //            |> Array.map (buffer >> newIod)
        //            |> Array.sortBy(fun iod -> iod.SortOrder)

        let (patient, sopClass, study, series), _ = iods |> Array.groupBy(fun iod -> (iod.PatientName, iod.SOPClassName, iod.StudyInstanceUID, iod.SeriesInstanceUID)) 
                                                         |> Array.exactlyOne

        let pixelSlicesCount = iods |> Array.choose(fun iod -> iod.CatSlice)

        {
            Id = id;
            Name = patient;
            SopClass = sopClass;
            Study = study;
            Series = series;
            HasPixelData = (pixelSlicesCount.Length = iods.Length);
            Iods = iods;
        }

    let all() = datasetsEntries |> Map.toSeq

    let byId id = 
        match currentDataset with
        | Some dataset when (dataset.Id = id)  -> dataset
        | _ -> id |> loadDataset |> branch setCurrent
        
    let datasetSlices id = 
        let dataset = id |> byId
        dataset.Iods |> Array.choose(fun iod -> iod.CatSlice)

    let datasetIods id = 
        let dataset = id |> byId
        dataset.Iods