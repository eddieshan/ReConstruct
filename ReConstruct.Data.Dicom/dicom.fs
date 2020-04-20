namespace ReConstruct.Data.Dicom

open System
open System.Collections.Generic

type TagMarker = 
    {
        ValueLength: int64;
        StreamPosition: int64;
    }

// Dicom data element. Contains parsed dicom tag data.
type DicomDataElement = 
    {
        Tag: uint16*uint16;
        TransferSyntax: TransferSyntaxType;
        VR: VRType;
        VM: int64 option;
        Marker: TagMarker;
        ValueField: string;
    }

type SliceDimensions =
    {
        Columns: int;
        Rows: int;
    }

// Encapsulate CAT slice geometry params.
type ImageSlice =
    {
        HField: int16[];
        Dimensions: SliceDimensions;
        UpperLeft: double[];
        PixelSpacingX: double;
        PixelSpacingY: double;
        //PixelSpacing: PixelSpacing;
        WindowCenter: int;
        WindowWidth: int;
    } 
    member x.AdjustToCenter (cx, cy, cz) =
        x.UpperLeft.[0] <- x.UpperLeft.[0] - cx
        x.UpperLeft.[1] <- x.UpperLeft.[1] - cy
        x.UpperLeft.[2] <- x.UpperLeft.[2] - cz

type DicomTree = 
    {
        Tag: DicomDataElement;
        Children: IDictionary<uint16*uint16, DicomTree>;
    }

module DicomTree =    
    let newNode tag = { Tag = tag; Children = new Dictionary<uint16*uint16, DicomTree>(); }

    let rec findNode (node: DicomTree) key =
        if node.Tag.Tag = key then
            node.Tag |> Some
        else if node.Children.Values |> Seq.isEmpty then
            None
        else
            match node.Children.TryGetValue key with
            | true, childNode -> childNode.Tag |> Some
            | false, _ -> node.Children.Values |> Seq.tryFind(fun v -> key |> findNode v |> Option.isSome) |> Option.map(fun n -> n.Tag)

    let findTagValue root key = key |> findNode root |> Option.map(fun v -> v.ValueField)
    let findTagValueAsNumber root f key = key |> findNode root |> Option.map(fun v -> v.ValueField |> Utils.parseFloat |> f)

// A Dicom instance.
type DicomInstance =
    {
        DicomTree: DicomTree; // Tree of Dicom tags represented by an XML document.
        FileName: string;
        StudyInstanceUID: string;
        SeriesInstanceUID: string;
        SOPInstanceUID: string;
        SOPClassUID: string;
        SOPClassName: string;
        PatientName: string;
        TransferSyntaxUID: string;
        SortOrder: double;
        Slice: ImageSlice option; // CAT slice image, whenever there is one.
    }

// A dataset contains analysis metadata and an array of Iods.
type DatasetEntry =
    {
        Id: int;
        Name: string;
        SopClass: string;
        Study: string;
        Series: string;
        HasPixelData: bool;
        Iods: DicomInstance[];
    }