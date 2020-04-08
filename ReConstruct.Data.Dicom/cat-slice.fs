namespace ReConstruct.Data.Dicom

open System

open ReConstruct.Core
open ReConstruct.Core.Numeric

open ReConstruct.Data.Dicom.DicomTree

module Hounsfield =
    
    [<Literal>]
    let BONES_ISOVALUE = 500.0f

    [<Literal>]
    let SKIN_ISOVALUE = 30.0f

    // Image parameters used to calculate a Hounsfield gradient.
    type HounsfieldCoordinates =
        {
            RescaleIntercept: int;
            RescaleSlope: int;
            PixelPadding: uint16 option;
            PixelPaddingRangeLimit: uint16 option;
            PixelRepresentation: uint16 option;
            StreamPosition: Int64;
        }

    let hounsfieldCoordinates root =
        let pixelData = Tags.PixelData |> findNode root |> Option.map(fun t -> t.Marker.StreamPosition |> Convert.ToInt64) |> Option.defaultValue -1L

        {
            RescaleIntercept = Tags.RescaleIntercept |> findTagValueAsNumber root int |> Option.defaultValue 0;
            RescaleSlope = Tags.RescaleSlope|> findTagValueAsNumber root int |> Option.defaultValue 0;
            PixelPadding = Tags.PixelPadding|> findTagValueAsNumber root uint16;
            PixelPaddingRangeLimit = Tags.PixelPaddingRangeLimit|> findTagValueAsNumber root uint16;
            PixelRepresentation = Tags.PixelRepresentation |> findTagValueAsNumber root uint16;
            StreamPosition = pixelData
        }

    let getImage (buffer: byte[], coordinates: HounsfieldCoordinates, sliceParams: SliceParams) =
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

        let getHounsfieldValue =
            match coordinates.PixelPadding with
            | Some pixelPadding -> pixelPadding |> int |> capValue
            | _ -> rescale

        let hounsfieldValues = Array.create (sliceParams.Dimensions.Rows * sliceParams.Dimensions.Columns) (int 0)

        let mutable index = coordinates.StreamPosition |> int
        let limit = buffer.Length - 2

        let setHounsfieldValue n = 
            if (index < limit) then
                let pixelValue = (int buffer.[index]) + ((int buffer.[index + 1]) <<< 8)
                hounsfieldValues.[n] <- pixelValue |> getHounsfieldValue
                index <- index + 2

        hounsfieldValues |> Array.iteri(fun i _ -> setHounsfieldValue i)

        // TODO: Unrolled loop. Keeping it here as a reminder to profile performance of plain loops vs. Array2D iterator.
//        for row in 0..sliceParams.Dimensions.Rows - 1 do
//            for column in 0..sliceParams.Dimensions.Columns-1 do
//                if (index < limit) then
//                    let pixelValue = Convert.ToInt32(((int pixels.[index+1]) <<< 8) + (int pixels.[index]))                    
//                    buffer.[row, column] <- pixelValue |> hounsfieldValue
//                    index <- index + 2

        hounsfieldValues

    let getBitmap (buffer: int[]) sliceParams =

        let windowLeftBorder = sliceParams.WindowCenter - (sliceParams.WindowWidth / 2)

        let normalizePixelValue pixelValue =
            let normalizedValue = (255 * (pixelValue - windowLeftBorder))/sliceParams.WindowWidth
            match normalizedValue with
            | underMinimum when underMinimum <= 0   -> byte 0
            | overMaximum when overMaximum >= 255   -> byte 255
            | _                                     -> Convert.ToByte(normalizedValue)

        let imageBuffer = Array.create (sliceParams.Dimensions.Rows*sliceParams.Dimensions.Columns*4) (byte 0)

        let mutable position, index = 0, 0
        for row in 0..sliceParams.Dimensions.Rows-1 do
            for column in 0..sliceParams.Dimensions.Columns-1 do
                let grayValue = buffer.[index] |> normalizePixelValue
                imageBuffer.[position] <- grayValue
                imageBuffer.[position + 1] <- grayValue
                imageBuffer.[position + 2] <- grayValue
                imageBuffer.[position + 3] <- byte 255
                position <- position + 4
                index <- index + 1

        (sliceParams.Dimensions.Columns, sliceParams.Dimensions.Rows, imageBuffer)

module Cat =
    open Hounsfield

    let sliceParams (root: DicomTree) =

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

    let slice (buffer, dicomTree) =
        let sliceParams = dicomTree |> sliceParams
        let coordinates = dicomTree |> hounsfieldCoordinates
        let hounsfieldImage = Hounsfield.getImage(buffer, coordinates, sliceParams)

        {
            SliceParams = sliceParams;
            HounsfieldBuffer = hounsfieldImage;
            GetRawImage = fun() -> sliceParams |> Hounsfield.getBitmap hounsfieldImage;
        }