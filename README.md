# ReConstruct

ReConstruct is an F# / WPF application that renders 3D reconstructions from Radiology CAT or MRI scans.  
Radiology scans contain a stack of 2D images (slices) that are essentially point clouds, or scalar fields.  
Iso surfaces representing tissue can be extracted from a set of slices and different iso values can be used  
to filter different types of tissue. This is an oversimplification of course, the actual process is more nuanced.  

Medical 3D volume reconstruction in general has inherent difficulties. To name a few,

- input scalar fields may have noise,  
- geometry reconstructions algorithms sometimes generate artifacts (shapes that do not exist), 
- geometry reconstruction by itself does not always yield precise tissue differentiation,  
- etc.

![Arm](Screenshot_Arm_Bone.png)

![Arm](Screenshot_Arm.png)

![Abdomen](Screenshot_Abdomen.png)

# Iso surface calculation

An Iso Surface is the geometrical place of points with a specific value in a scalar field.  
It can be approximated by extracting an analytical function that represents it or by calculating a discrete mesh.  

The discrete mesh approach is the most common, some of its challenges are, 

- generating a mesh that is topologically correct,
- achieving both acceptable performance and quality of the mesh.

The 3D reconstruction algorithms in this project are based in well known 3D discrete reconstruction techniques,

- Marching cubes, the standard implementation plus a couple experimental variations.
- Marching tetrahedra.
- Dual contouring.

Standard marching cubes and dual contouring proved to be the most effective in terms of speed and mesh quality.  
Dual contouring yields a better mesh quality but is slower.  

Both are optimized using parallel processing, SIMD, mem tweaks, etc.

# Design

The app is quite modular, consisting of a pipeline of,

- images dataset processing,
- geometry calculation,
- rendering,
- GUI.

I made emphasis on decoupling the render backend from geometry calculation so the backend can be swapped easily.  
The render backend is OpenGL at the moment, with a small wrapper so it can be swapped.  
I would like to provide at least another backend option, Vulkan would be ideal.

## GUI

All of the application is written in F# including the WPF front end.  
An MVC pattern is used instead of MVVM, this is an experiment on alternatives to MVVM.  
In this case, F# and MVC are a good match since MVC is immutable by default and fits well in an FP approach.  

Styling is done in XAML but Views are built with code instead of XAML.   
Admittedly, this goes against the grain of WPF but there is a rationale behind,  

- Binding Models to Views statically. Bindings are checked in compile time so broken bindings do not happen.  
- It offers a lot of flexibility and control when creating Views.  
- Views can be written this way just as fast as XAML or even faster.  
- In my opinion, MVC provides enforces separation of concerns better than MVVM.  
- I have not used this pattern in big projects yet but I believe it scales well.

# Credits

Many thanks to the creators of,

- Oswald Font https://github.com/googlefonts/OswaldFont
- Material Design Iconic Font http://zavoloklom.github.io/material-design-iconic-font/license.html

Both fonts are available under the SIL Open Font License (OFL) http://scripts.sil.org/OFL
