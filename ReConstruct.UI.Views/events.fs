namespace ReConstruct.UI.View

type Axis =
| X
| Y
| Z

module Events =
    let Progress = new Event<bool>()
    let Status = new Event<string>()