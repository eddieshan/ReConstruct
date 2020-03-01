namespace ReConstruct.Services

open System.IO

open ReConstruct.Core
open ReConstruct.Services

module DicomService =

    let availableAnalysis() = DatasetRepository.all() |> Seq.map(fun (id, directory) -> (id, Path.GetFileName directory))

    let getDataset id = Time.clock(fun _ -> id |> DatasetRepository.byId)

    let getIods = DatasetRepository.datasetIods

    let getVolume = DatasetRepository.datasetSlices

    let getIod (id, index) = id |> getIods |> Array.item index

    let getTags id = id |> DatasetRepository.byId