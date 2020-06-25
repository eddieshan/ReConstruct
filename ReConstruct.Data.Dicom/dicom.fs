namespace ReConstruct.Data.Dicom

open System
open System.Collections.Generic

type TagMarker = 
    {
        ValueLength: int64;
        StreamPosition: int64;
    }

type DicomDataElement = 
    {
        Tag: uint16*uint16;
        TransferSyntax: TransferSyntaxType;
        VR: VRType;
        VM: int64 option;
        Marker: TagMarker;
        ValueField: string;
    }

type ImageSlice =
    {
        HField: int16[];
        Columns: int;
        Rows: int;
        UpperLeft: double[];
        PixelSpacingX: double;
        PixelSpacingY: double;
        WindowCenter: int;
        WindowWidth: int;
    }    

module ImageSlice =
    let inline adjustToCenter (cx, cy, cz) slice =
        slice.UpperLeft.[0] <- slice.UpperLeft.[0] - cx
        slice.UpperLeft.[1] <- slice.UpperLeft.[1] - cy
        slice.UpperLeft.[2] <- slice.UpperLeft.[2] - cz

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

type DicomInstance =
    {
        DicomTree: DicomTree;
        FileName: string;
        StudyInstanceUID: string;
        SeriesInstanceUID: string;
        SOPInstanceUID: string;
        SOPClassUID: string;
        SOPClassName: string;
        PatientName: string;
        TransferSyntaxUID: string;
        SortOrder: double;
        Slice: ImageSlice option;
    }

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