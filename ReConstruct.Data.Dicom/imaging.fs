namespace ReConstruct.Data.Dicom

open System
open System.Numerics

open ReConstruct.Core

open ReConstruct.Data.Dicom.DicomNode

// Image parameters used to calculate a Hounsfield gradient.
type private HounsfieldCoordinates =
    {
        RescaleIntercept: int16;
        RescaleSlope: int16;
        PixelPadding: int16 option;
        PixelPaddingRangeLimit: uint16 option;
        PixelRepresentation: uint16 option;
        StreamPosition: int64;
    }

module Imaging =
    
    [<Literal>]
    let BONES_ISOVALUE = 500s

    [<Literal>]
    let SKIN_ISOVALUE = 30s

    let private hounsfieldCoordinates root =
        let pixelData = Tags.PixelData |> find root |> Option.map(fun t -> t.Marker.StreamPosition) |> Option.defaultValue -1L

        {
            RescaleIntercept = Tags.RescaleIntercept |> findNumericValue root int16 |> Option.defaultValue 0s;
            RescaleSlope = Tags.RescaleSlope |> findNumericValue root int16 |> Option.defaultValue 0s;
            PixelPadding = Tags.PixelPadding |> findNumericValue root int16;
            PixelPaddingRangeLimit = Tags.PixelPaddingRangeLimit |> findNumericValue root uint16;
            PixelRepresentation = Tags.PixelRepresentation |> findNumericValue root uint16;
            StreamPosition = pixelData
        }

    let private getHField (buffer: byte[]) (rows, columns) (coordinates: HounsfieldCoordinates) =
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

        let inline rescalePixel value = value*coordinates.RescaleSlope + coordinates.RescaleIntercept

        let rescale =
            match coordinates.RescaleSlope with
            | 0s -> id
            | _ -> rescalePixel

        let inline mapPixel max value = 
            if value = max then 
                defaultHounsfieldValue
            else
                value |> rescale

        let mapToLayout = coordinates.PixelPadding |> Option.map(mapPixel) |> Option.defaultValue rescale

        let start = coordinates.StreamPosition |> int
        let limit = buffer.Length - 2

        let hField = Array.create (rows * columns) 0s

        let mutable n = 0

        for i in start..2..limit do
            let pixelValue = (int16 buffer.[i]) + ((int16 buffer.[i + 1]) <<< 8)
            hField.[n] <- pixelValue |> mapToLayout
            n <- n + 1

        hField

    let pixelMapper sliceLayout =
        let windowLeftBorder = sliceLayout.WindowCenter - (sliceLayout.WindowWidth / 2)

        fun pixelValue ->
            ((Byte.MaxAsInt * (pixelValue - windowLeftBorder))/sliceLayout.WindowWidth) |> Byte.clamp
        
    let getBitmap slice =
        let normalizePixelValue = pixelMapper slice
        let numPixels = slice.Rows*slice.Columns
        let imageBuffer = Array.create (numPixels*4) 0uy

        let mutable position = 0
        slice.HField |> Array.iter(fun v -> 
                                    let grayValue = v |> int |> normalizePixelValue
                                    imageBuffer.[position] <- grayValue
                                    imageBuffer.[position + 1] <- grayValue
                                    imageBuffer.[position + 2] <- grayValue
                                    imageBuffer.[position + 3] <- Byte.Max
                                    position <- position + 4)

        (slice.Columns, slice.Rows, imageBuffer)

    let slice (buffer, root) =

        let findDoubles f v = findValue root >> Option.map(Utils.parseDoubles >> f) >> Option.defaultValue v
        let findInt v = findNumericValue root int >> Option.defaultValue v

        let rows, columns =  Tags.Rows|> findInt 0, Tags.Columns|> findInt 0
        let hField = root |> hounsfieldCoordinates |> getHField buffer (rows, columns)

        {
            HField = hField;
            Rows = rows;
            Columns = columns;
            TopLeft = Tags.Position |> findDoubles Vector3.fromDoubles Vector3.Zero;
            PixelSpacing = Tags.PixelSpacing |> findDoubles Vector2.fromDoubles Vector2.Zero;
            WindowCenter = Tags.WindowCenter |> findInt 0;
            WindowWidth =  Tags.WindowWidth |> findInt 1;
        }

    let getValuesCount slice =  
        let zeroValue = 0s
        slice.HField |> Seq.filter(fun v -> v > zeroValue) |> Seq.countBy id |> Seq.toArray