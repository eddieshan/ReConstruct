#Introduction

ReConstruct is an F# application that renders 3D reconstructions from Radiology scans.
Radiology scans contain series of 2D images (slices) that are essentially scalar fields.
Hence, scalar field algorithms can be applied to series of slices to extract an iso surface.
Different iso values on the scalar field can be used to filter different types of tissue.

The final goal is to build an application to build 3D reconstructions from any source of scalar field data, not just Radiology scans.
Scalar field analysis is widely used in many areas of Science and Engineering.

#Iso surface calculation

Iso surfaces are surfaces that contains points that have a value, typically within a threshold, in the scalar field.
They are approximated by calculating a triangle mesh, the challenge here is generating a mesh that is topologically correct with the minimal amount of points.

Extracting the mesh from a slice series is done in two steps,

- Normalizing 2D images. Each pixel value is mapped to a range of black and white values.
- Polygonizing pairs of consecutive normalized images.

Polygonization is parallelized with a render agent though this creates coupling between geometry calculation and rendering.
Ideally it would be better to separate geometry calculation from render but for now doing both together seems more efficient.
Each pair of slices can be processed with a parallel task but a scan with a good level of detail typically contains from hundreds to thousands of slices.
This means that parallel tasks are throttled to avoid exhausting the thread pool.
That is, if there are n CPU cores and 1 is already used to run the UI thread, the render agent only has n - 1 jobs at any time.

#UI Design

The application is written in F# including the WPF front end.
An MVC pattern is used instead of MVVM, this is an experiment on alternatives to MVVM.
In this case, F# and MVC are a good match since MVC is immutable by default and fits well in a Funcional Programming approach.

Styling is done in XAML but Views are built with code instead of XAML. 
Admittedly, this goes against the grain of WPF but there is a rationale behind.
It allows binding Models to Views statically so broken data bindings do not happen.
It also offer a lot of flexibility and Views can actually be written this way just as fast as XAML or even faster.

#Credits

Many thanks to the creators of,

- Oswald Font https://github.com/googlefonts/OswaldFont
- Material Design Iconic Font http://zavoloklom.github.io/material-design-iconic-font/license.html

Both fonts are available under the SIL Open Font License (OFL) http://scripts.sil.org/OFL
