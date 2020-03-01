namespace ReConstruct.Core

open System
open System.Configuration

module Config =
    let DataPath = ConfigurationManager.AppSettings.["DataPath"]

    



