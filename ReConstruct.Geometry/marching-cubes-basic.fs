namespace ReConstruct.Geometry

open System
open System.Numerics

open ReConstruct.Core

open ReConstruct.Data.Dicom

open ReConstruct.Geometry.MarchingCubesTables

module MarchingCubesBasic =

    let private marchCube addPoint (cube: Cube) =

        let cubeIndexAsInt = cube.GetIndex()

        if EdgeTable.[cubeIndexAsInt] <> 0 then
            let vertlist = Array.zeroCreate<Vector3> 12
            
            for i in 0..EdgeTraversal.Length-1 do
                if (EdgeTable.[cubeIndexAsInt] &&& (1 <<< i)) > 0 then
                    let index1, index2 = int EdgeTraversal.[i].[0], int EdgeTraversal.[i].[1]
                    let v1, v2 = cube.Values.[index1], cube.Values.[index2]
                    let delta = v2 - v1
                    let mu =
                        if delta = 0s then
                            0.5f
                        else
                            float32(cube.IsoValue - v1) / (float32 delta)

                    vertlist.[i] <- Vector3.Lerp(cube.Vertices.[index1], cube.Vertices.[index2], mu)

            let triangles = TriTable2.[cubeIndexAsInt]

            for triangle in triangles do
                let v0 = vertlist.[triangle.[0]]
                let v1 = vertlist.[triangle.[1]]
                let v2 = vertlist.[triangle.[2]]

                let normal = Vector3.Cross(v2 - v0, v1 - v0) |> Vector3.Normalize

                addPoint v0
                addPoint normal
                addPoint v1
                addPoint normal
                addPoint v2
                addPoint normal

    let polygonize isoLevel (slices: ImageSlice[]) partialRender = 

        let polygonizeSection (front, back) =
            let bufferChain = BufferChain()
            CubesIterator.iterate (front, back) isoLevel (marchCube bufferChain.Add)
            bufferChain

        let addPoints (bufferChain: BufferChain) = bufferChain.Dump partialRender

        let queueJob = RenderAgent.renderQueue()
        let polygonizeJob i = async { return polygonizeSection (slices.[i - 1], slices.[i]) }
        
        seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob >> (queueJob addPoints))

        /// NOT TO BE REMOVED. Single-thread version for testing.
        //let polygonizeJob i = polygonizeSection (slices.[i - 1], slices.[i]) |> addPoints
        //seq { 1..slices.Length - 1 } |> Seq.iter(polygonizeJob)