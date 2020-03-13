namespace ReConstruct.Render

type Axis =
| X
| Y
| Z

module Events =
    let OnRotation = new Event<Axis*float32>()
    let OnCameraMoved = new Event<float32>()
    let OnScale = new Event<float32>()
    let RenderStatus = new Event<string>()