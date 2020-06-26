namespace ReConstruct.Data.Dicom

open System
open System.Numerics

open ReConstruct.Core
open ReConstruct.Core.Numeric

open ReConstruct.Data.Dicom.DicomNode

// Image parameters used to calculate a Hounsfield gradient.
type private HounsfieldCoordinates =
    {
        RescaleIntercept: int16;
        RescaleSlope: int16;
        PixelPadding: int16 option;
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
        let pixelData = Tags.PixelData |> find root |> Option.map(fun t -> t.Marker.StreamPosition |> Convert.ToInt64) |> Option.defaultValue -1L

        {
            RescaleIntercept = Tags.RescaleIntercept |> findNumericValue root int16 |> Option.defaultValue 0s;
            RescaleSlope = Tags.RescaleSlope|> findNumericValue root int16 |> Option.defaultValue 0s;
            PixelPadding = Tags.PixelPadding|> findNumericValue root int16;
            PixelPaddingRangeLimit = Tags.PixelPaddingRangeLimit|> findNumericValue root uint16;
            PixelRepresentation = Tags.PixelRepresentation |> findNumericValue root uint16;
            StreamPosition = pixelData
        }

    let private getHField (buffer: byte[]) (rows, columns) (coordinates: HounsfieldCoordinates) =
        let rescale =
            match coordinates.RescaleSlope with
            | 0s -> id
            | _ -> fun pixelValue -> pixelValue*coordinates.RescaleSlope + coordinates.RescaleIntercept

        let defaultHounsfieldValue = 
            match coordinates.RescaleSlope with
            | 0s -> Int16.MinValue
            | _ -> 
                // Test possible underflow of minimum value, cap to Int16.MinValue.
                let minValue = int Int16.MinValue
                /// TODO: find a way to avoid repeated rescale calculation. 
                /// For now there is no other way, casting to int is needed to check underflow.
                let testMinOverflow = minValue*(int coordinates.RescaleSlope) + (int coordinates.RescaleIntercept)
                if testMinOverflow < minValue then
                    Int16.MinValue
                else
                    testMinOverflow |> int16

        let capValue max = 
            fun pixelValue -> 
                if pixelValue = max then 
                    defaultHounsfieldValue
                else
                    pixelValue |> rescale 

        let mapToLayout =
            match coordinates.PixelPadding with
            | Some pixelPadding -> pixelPadding |> capValue
            | _ -> rescale

        let mutable index = coordinates.StreamPosition |> int
        let limit = buffer.Length - 2

        let getHounsfieldValue() = 
            if (index < limit) then
                let pixelValue = (int16 buffer.[index]) + ((int16 buffer.[index + 1]) <<< 8)
                index <- index + 2
                pixelValue |> mapToLayout
            else
                0s

        Array.init (rows * columns) (fun _ -> getHounsfieldValue())

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
        let normalizePixelValue = pixelMapper slice
        let numPixels = slice.Rows*slice.Columns
        let imageBuffer = Array.create (numPixels*4) (byte 0)

        let mutable position = 0
        slice.HField |> Array.iter(fun v -> 
                                    let grayValue = v |> int |> normalizePixelValue
                                    imageBuffer.[position] <- grayValue
                                    imageBuffer.[position + 1] <- grayValue
                                    imageBuffer.[position + 2] <- grayValue
                                    imageBuffer.[position + 3] <- maxValue
                                    position <- position + 4)

        (slice.Columns, slice.Rows, imageBuffer)

    let slice (buffer, root) =

        let parseDoubles (value: string option) = 
            match value with 
            | Some s -> s.Split('\\') |> Array.map (parseDouble >> float32)
            | None -> [||]

        let imagePosition =  Tags.Position|> findValue root |> parseDoubles
        let pixelSpacing = Tags.PixelSpacing |> findValue root |> parseDoubles
        let spacingX, spacingY =
            match pixelSpacing.Length with
            | 0 -> 0.0f, 0.0f
            | _ -> float32 pixelSpacing.[0], float32 pixelSpacing.[1]

        let rows =  Tags.Rows|> findNumericValue root int |> Option.defaultValue 0
        let columns =  Tags.Columns|> findNumericValue root int |> Option.defaultValue 0
        let hField = root |> hounsfieldCoordinates |> getHField buffer (rows, columns)

        {
            HField = hField;
            Rows = rows;
            Columns = columns;
            TopLeft = Vector3(imagePosition.[0], imagePosition.[1], imagePosition.[2]);
            PixelSpacing = Vector2(spacingX, spacingY);
            WindowCenter = Tags.WindowCenter |> findNumericValue root int |> Option.defaultValue 0;
            WindowWidth =  Tags.WindowWidth |> findNumericValue root int |> Option.defaultValue 1;
        }

    let getValuesCount slice =  
        let zeroValue = 0s
        slice.HField |> Seq.filter(fun v -> v > zeroValue) |> Seq.countBy id |> Seq.toArray