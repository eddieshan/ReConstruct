namespace ReConstruct.Data.Dicom

open System
open System.Numerics
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
        TopLeft: double[];
        PixelSpacing: Vector2;
        WindowCenter: int;
        WindowWidth: int;
    }    

module ImageSlice =
    let inline adjustToCenter (cx, cy, cz) slice =
        slice.TopLeft.[0] <- slice.TopLeft.[0] - cx
        slice.TopLeft.[1] <- slice.TopLeft.[1] - cy
        slice.TopLeft.[2] <- slice.TopLeft.[2] - cz

type DicomNode = 
    {
        Tag: DicomDataElement;
        Children: IDictionary<uint16*uint16, DicomNode>;
    }

module DicomNode =    
    let newNode tag = { Tag = tag; Children = new Dictionary<uint16*uint16, DicomNode>(); }

    let rec find (node: DicomNode) key =
        match node.Tag.Tag, node.Children.TryGetValue key with
        | v, (_, _) when v = key -> node.Tag |> Some
        | _, (true, child) -> child.Tag |> Some
        | _, (false, _) -> node.Children.Values |> Seq.tryFind(fun v -> key |> find v |> Option.isSome) |> Option.map(fun n -> n.Tag)

    let findValue root key = key |> find root |> Option.map(fun v -> v.ValueField)
    let findNumericValue root f key = key |> find root |> Option.map(fun v -> v.ValueField |> Utils.parseFloat |> f)

type DicomInstance =
    {
        DicomTree: DicomNode;
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