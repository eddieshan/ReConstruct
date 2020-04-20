namespace ReConstruct.Data.Dicom

open System

open ReConstruct.Core
open ReConstruct.Core.Numeric

open ReConstruct.Data.Dicom.DicomTree

// Image parameters used to calculate a Hounsfield gradient.
type private HounsfieldCoordinates =
    {
        RescaleIntercept: int;
        RescaleSlope: int;
        PixelPadding: uint16 option;
        PixelPaddingRangeLimit: uint16 option;
        PixelRepresentation: uint16 option;
        StreamPosition: Int64;
    }

module Imaging =
    
    [<Literal>]
    let BONES_ISOVALUE = 500s

    [<Literal>]
    let SKIN_ISOVALUE = 30s

    let private hounsfieldCoordinates root =
        let pixelData = Tags.PixelData |> findNode root |> Option.map(fun t -> t.Marker.StreamPosition |> Convert.ToInt64) |> Option.defaultValue -1L

        {
            RescaleIntercept = Tags.RescaleIntercept |> findTagValueAsNumber root int |> Option.defaultValue 0;
            RescaleSlope = Tags.RescaleSlope|> findTagValueAsNumber root int |> Option.defaultValue 0;
            PixelPadding = Tags.PixelPadding|> findTagValueAsNumber root uint16;
            PixelPaddingRangeLimit = Tags.PixelPaddingRangeLimit|> findTagValueAsNumber root uint16;
            PixelRepresentation = Tags.PixelRepresentation |> findTagValueAsNumber root uint16;
            StreamPosition = pixelData
        }

    let private getHField(buffer: byte[], coordinates: HounsfieldCoordinates, sliceParams: SliceLayout) =
        let rescale =
            match coordinates.RescaleSlope with
            | 0 -> id
            | _ -> fun pixelValue -> pixelValue*coordinates.RescaleSlope + coordinates.RescaleIntercept

        let defaultHounsfieldValue = Int16.MinValue |> int |> rescale

        let capValue max = 
            fun pixelValue -> 
                if pixelValue >= max then 
                    defaultHounsfieldValue
                else
                    pixelValue |> rescale 

        let mapToLayout =
            match coordinates.PixelPadding with
            | Some pixelPadding -> pixelPadding |> int |> capValue
            | _ -> rescale

        let mutable index = coordinates.StreamPosition |> int
        let limit = buffer.Length - 2

        let getHounsfieldValue() = 
            if (index < limit) then
                let pixelValue = (int buffer.[index]) + ((int buffer.[index + 1]) <<< 8)                
                index <- index + 2
                pixelValue |> mapToLayout |> int16
            else
                0s

        Array.init (sliceParams.Dimensions.Rows * sliceParams.Dimensions.Columns) (fun _ -> getHounsfieldValue())

    let intMinValue, intMaxValue = 0, 255
    let minValue, maxValue = 0uy, byte intMaxValue

    let pixelMapper sliceLayout =
        let windowLeftBorder = sliceLayout.WindowCenter - (sliceLayout.WindowWidth / 2)

        fun pixelValue ->
            let normalizedValue = (intMaxValue * (pixelValue - windowLeftBorder))/sliceLayout.WindowWidth
            match normalizedValue with
            | underMinimum when underMinimum <= intMinValue -> minValue
            | overMaximum when overMaximum >= intMaxValue   -> maxValue
            | _                                             -> Convert.ToByte(normalizedValue)
        
    let getBitmap slice =
        let normalizePixelValue = pixelMapper slice.Layout
        let numPixels = slice.Layout.Dimensions.Rows*slice.Layout.Dimensions.Columns
        let imageBuffer = Array.create (numPixels*4) (byte 0)

        let mutable position = 0
        slice.HField |> Array.iter(fun v -> 
                                    let grayValue = v |> int |> normalizePixelValue
                                    imageBuffer.[position] <- grayValue
                                    imageBuffer.[position + 1] <- grayValue
                                    imageBuffer.[position + 2] <- grayValue
                                    imageBuffer.[position + 3] <- maxValue
                                    position <- position + 4)

        (slice.Layout.Dimensions.Columns, slice.Layout.Dimensions.Rows, imageBuffer)

    let private parseSliceLayout (root: DicomTree) =

        let parseDoubles (value: string option) = 
            match value with 
            | Some s -> s.Split('\\') |> Array.map parseDouble
            | None -> [||]

        let imagePosition =  Tags.Position|> findTagValue root |> parseDoubles
        let pixelSpacing = Tags.PixelSpacing |> findTagValue root |> parseDoubles
        let spacingX, spacingY =
            match pixelSpacing.Length with
            | 0 -> 0.0, 0.0
            | _ -> pixelSpacing.[0], pixelSpacing.[1]

        {
            Dimensions = 
                { 
                    Rows =  Tags.Rows|> findTagValueAsNumber root int |> Option.defaultValue 0;
                    Columns =  Tags.Columns|> findTagValueAsNumber root int |> Option.defaultValue 0;
                };
            UpperLeft = imagePosition;
            PixelSpacing = 
                {
                    X = spacingX; 
                    Y = spacingY; 
                };
            WindowCenter = Tags.WindowCenter |> findTagValueAsNumber root int |> Option.defaultValue 0;
            WindowWidth =  Tags.WindowWidth |> findTagValueAsNumber root int |> Option.defaultValue 0;
        }

    let getValuesCount slice =  
        let zeroValue = 0s
        slice.HField |> Seq.filter(fun v -> v > zeroValue) |> Seq.countBy id |> Seq.toArray

    let slice (buffer, dicomTree) =
        let sliceParams = dicomTree |> parseSliceLayout
        let coordinates = dicomTree |> hounsfieldCoordinates
        let hField = getHField(buffer, coordinates, sliceParams)        

        {
            Layout = sliceParams;
            HField = hField;            
        }