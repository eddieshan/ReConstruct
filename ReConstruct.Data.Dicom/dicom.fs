﻿namespace ReConstruct.Data.Dicom

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
        TopLeft: Vector3;
        PixelSpacing: Vector2;
        WindowCenter: int;
        WindowWidth: int;
    }    

module ImageSlice =
    let inline adjustToCenter center slice = { slice with TopLeft = slice.TopLeft - center }

type DicomNode = 
    {
        Tag: DicomDataElement;
        Children: IDictionary<uint16*uint16, DicomNode>;
    }

module DicomNode =    
    let newNode tag = { Tag = tag; Children = new Dictionary<uint16*uint16, DicomNode>(); }

    let rec find (node: DicomNode) key =
        match node.Tag.Tag with
        | v when v = key -> node.Tag |> Some
        | _ -> node.Children.Values |> Seq.tryFind(fun v -> key |> find v |> Option.isSome) |> Option.map(fun n -> n.Tag)

    let findValue root = find root >> Option.map(fun v -> v.ValueField)
    let findNumericValue root f = find root >> Option.map(fun v -> v.ValueField |> Utils.parseLastDouble |> f)

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