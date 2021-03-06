﻿namespace ReConstruct.Services

open System
open System.IO
open System.Threading.Tasks

open ReConstruct.Core
open ReConstruct.Core.IO
open ReConstruct.Core.Patterns

open ReConstruct.Data.Dicom
open ReConstruct.Data.Dicom.DicomNode

module internal DatasetRepository =

    let private bufferizeFile filePathName = (filePathName, File.ReadAllBytes(filePathName))

    let private datasetsEntries = Directory.GetDirectories(Config.DataPath) |> Array.indexed |> Map.ofArray

    let mutable private currentDataset = None

    let private setCurrent dataSet = currentDataset <- dataSet |> Some

    [<Literal>]
    let private UNKNOWN_VALUE = "???"

    let private newSlice (filePathName, buffer) =
        let root = DicomParser.getDicomTree buffer

        let sopClassUID =  Tags.SopClassUID |> findValue root
        let syntaxKey, syntaxData = 
            match Tags.TransferSyntaxUID|> find root with
            | Some t -> (Some t.Tag, Some t.ValueField)
            | None -> (None, None)

        let findSortOrder() = 
            let positionTag, locationTag =  Tags.Position|> findValue root, Tags.Location |> findValue root

            match(positionTag, locationTag) with
            | (Some position, _)    -> position |> Utils.splitNumbers |> Array.item 2 |> Numeric.parseDouble
            | (None, Some location) -> location |> Utils.parseLastDouble
            | (_, _)                -> 0.0

        let nonCompressedSlice() = syntaxData |> TransferSyntax.notCompressed |> Option.fromTrue(fun _ -> Imaging.slice(buffer, root))

        {
            DicomTree = root;
            FileName = filePathName |> Path.GetFileNameWithoutExtension;
            TransferSyntaxUID = syntaxKey |> Option.map Tags.getTagName |> Option.defaultValue UNKNOWN_VALUE;
            StudyInstanceUID =  Tags.StudyInstanceUID |> findValue root |> Option.defaultValue UNKNOWN_VALUE;
            SeriesInstanceUID = Tags.SeriesInstanceUID |> findValue root |> Option.defaultValue UNKNOWN_VALUE;
            SOPInstanceUID =  Tags.SopInstanceUID |> findValue root |> Option.defaultValue UNKNOWN_VALUE;
            SOPClassUID = sopClassUID |> Option.defaultValue UNKNOWN_VALUE;
            SOPClassName = sopClassUID |> Option.bind SopClass.Dictionary.TryFind |> Option.defaultValue UNKNOWN_VALUE;
            PatientName = Tags.PatientName |> findValue root |> Option.defaultValue UNKNOWN_VALUE;
            SortOrder = findSortOrder();
            Slice = Tags.PixelData |> findValue root |> Option.bind(fun _ -> nonCompressedSlice());
        }

    let private loadSlices id =
        datasetsEntries 
            |> Map.find id 
            |> directoryFiles "*.*" SearchOption.AllDirectories
            |> Array.map (bufferizeFile >> newSlice)
            |> Array.sortBy(fun iod -> iod.SortOrder)

    let private loadSlicesParallel id =
        // Image files are processed in a sequential fork-join pattern.
        // Files are read sequentially to avoid contention on disk reads.
        // After each file is loaded, an async task is started to process the image.
        let asAsyncImageProcess content =
            async {
                return content |> newSlice
            }

        datasetsEntries 
        |> Map.find id 
        |> directoryFiles "*.*" SearchOption.AllDirectories
        |> Array.map (bufferizeFile >> asAsyncImageProcess)
        |> Async.parallelThrottledByProcessor 
        |> Async.RunSynchronously 
        |> Array.sortBy(fun iod -> iod.SortOrder)

    let private loadDataset id =
        let iods = loadSlicesParallel id

        let (patient, sopClass, study, series), _ = iods |> Array.groupBy(fun iod -> (iod.PatientName, iod.SOPClassName, iod.StudyInstanceUID, iod.SeriesInstanceUID)) 
                                                         |> Array.exactlyOne

        let pixelSlicesCount = iods |> Array.choose(fun iod -> iod.Slice)

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
        dataset.Iods |> Array.choose(fun iod -> iod.Slice)

    let datasetIods id = 
        let dataset = id |> byId
        dataset.Iods