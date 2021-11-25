using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

public struct VoxelData
{
    public Vector3 position;
    public uint fill;
    public uint front;

    public bool IsFrontFace()
    {
        return fill > 0 && front > 0;
    }

    public bool IsBackFace()
    {
        return fill > 0 && front < 1;
    }

    public bool IsEmpty()
    {
        return fill < 1;
    }
};

[RequireComponent(typeof(MeshFilter))]
public class SolidVoxel : MonoBehaviour
{
    [SerializeField] public ComputeShader computeShader;
    [SerializeField] public int resolution = 64;         //精度
    [SerializeField] public Mesh mesh;
    

    const string kStartKey = "_Start", kEndKey = "_End", kSizeKey = "_Size";
    const string kUnitKey = "_Unit", kInvUnitKey = "_InvUnit", kHalfUnitKey = "_HalfUnit";
    const string kWidthKey = "_Width", kHeightKey = "_Height", kDepthKey = "_Depth";
    const string kTriCountKey = "_TrianglesCount", kTriIndexesKey = "_TriangleIndexes";
    const string kVertBufferKey = "_VertBuffer", kTriBufferKey = "_TriBuffer";
    const string kVoxelBufferKey = "_VoxelBuffer";

    private void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 200, 50), "Voxel"))
        {
            var data = Voxelize(mesh, resolution, computeShader);
            GetComponent<MeshFilter>().sharedMesh = Build(data.GetData(), data.UnitLength);
            data.Dispose();
        }
    }

    //Voxelize：传递compute shader参数，得到voxelbuffer
    public static GPUVoxelData Voxelize(
        Mesh mesh,
        int resolution,
        ComputeShader voxelizer
    )
    {
        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;

        var vertices = mesh.vertices;
        var vertBuffer = new ComputeBuffer(vertices.Length, Marshal.SizeOf(typeof(Vector3)));
        vertBuffer.SetData(vertices);


        var triangles = mesh.triangles;
        var triBuffer = new ComputeBuffer(triangles.Length, Marshal.SizeOf(typeof(int)));
        triBuffer.SetData(triangles);

        var maxLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        var unit = maxLength / resolution;
        var hunit = unit * 0.5f;


        var start = bounds.min - new Vector3(hunit, hunit, hunit);
        var end = bounds.max + new Vector3(hunit, hunit, hunit);
        var size = end - start;

        int w, h, d;

        w = Mathf.CeilToInt(size.x / unit);
        h = Mathf.CeilToInt(size.y / unit);
        d = Mathf.CeilToInt(size.z / unit);

        var voxelBuffer = new ComputeBuffer(w * h * d, Marshal.SizeOf(typeof(VoxelData)));
        var voxels = new VoxelData[voxelBuffer.count];
        voxelBuffer.SetData(voxels); // initialize voxels explicitly

        //设置包围盒
        voxelizer.SetVector(kStartKey, start);
        voxelizer.SetVector(kEndKey, end);
        voxelizer.SetVector(kSizeKey, size);


        //单个体素的大小，长宽高(有多少个体素)
        voxelizer.SetFloat(kUnitKey, unit);
        voxelizer.SetFloat(kInvUnitKey, 1f / unit);
        voxelizer.SetFloat(kHalfUnitKey, hunit);
        voxelizer.SetInt(kWidthKey, w);
        voxelizer.SetInt(kHeightKey, h);
        voxelizer.SetInt(kDepthKey, d);

        //三角形buffer
        voxelizer.SetInt(kTriCountKey, triBuffer.count);
        var indexes = triBuffer.count / 3;
        voxelizer.SetInt(kTriIndexesKey, indexes);

        // 表面体素化 前面
        voxelizer.SetBuffer(0, kVertBufferKey, vertBuffer);
        voxelizer.SetBuffer(0, kTriBufferKey, triBuffer);
        voxelizer.SetBuffer(0, kVoxelBufferKey, voxelBuffer);
        voxelizer.Dispatch(0, indexes / 17, 1, 1);

        // 表面体素化 背面
        voxelizer.SetBuffer(1, kVertBufferKey, vertBuffer);
        voxelizer.SetBuffer(1, kTriBufferKey, triBuffer);
        voxelizer.SetBuffer(1, kVoxelBufferKey, voxelBuffer);
        voxelizer.Dispatch(1, indexes / 17, 1, 1);

        // solid voxel
        voxelizer.SetBuffer(2, kVoxelBufferKey, voxelBuffer);
        voxelizer.Dispatch(2, w / 17, h / 17, 1);

 
        vertBuffer.Release();
        triBuffer.Release();
        return new GPUVoxelData(voxelBuffer, w, h, d, unit);
    }


    //
    public static Mesh Build(VoxelData[] voxels, float unit, bool useUV = false)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        var centers = new List<Vector4>();

        var up = Vector3.up * unit;
        Debug.Log("Vector3.up的值是：" + Vector3.up);

        var hup = up * 0.5f;
        var hbottom = -hup;

        var right = Vector3.right * unit;
        Debug.Log("Vector3.right的值是：" + Vector3.right);
        var hright = right * 0.5f;

        var left = -right;
        var hleft = left * 0.5f;

        var forward = Vector3.forward * unit;
        Debug.Log("Vector3.forward的值是：" + Vector3.forward);
        var hforward = forward * 0.5f;

        var back = -forward;
        var hback = back * 0.5f;

        for (int i = 0, n = voxels.Length; i < n; i++)
        {
            var v = voxels[i];
            if (v.fill > 0)
            {
                // back
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hback, right, up, Vector3.back
                );

                // right
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hright, forward, up, Vector3.right
                );

                // forward
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hforward, left, up, Vector3.forward
                );

                // left
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hleft, back, up, Vector3.left
                );

                // up
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hup, right, forward, Vector3.up
                );

                // down
                CalculatePlane(
                    vertices, normals, centers, triangles,
                    v, hbottom, right, back, Vector3.down
                );

            }
        }
        var mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.tangents = centers.ToArray();
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.RecalculateBounds();
        return mesh;
    }


    static void CalculatePlane(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> centers,
        List<int> triangles,
        VoxelData voxel,
        Vector3 offset,
        Vector3 right,
        Vector3 up,
        Vector3 normal,
        int rSegments = 2,
        int uSegments = 2
        )
    {
        float rInv = 1f / (rSegments - 1);
        float uInv = 1f / (uSegments - 1);

        int triangleOffset = vertices.Count;
        var center = voxel.position;

        var transformed = center + offset;
        for (int y = 0; y < uSegments; y++)
        {
            float ru = y * uInv;
            for (int x = 0; x < rSegments; x++)
            {
                float rr = x * rInv;
                vertices.Add(transformed + right * (rr - 0.5f) + up * (ru - 0.5f));
                normals.Add(normal);
                centers.Add(center);
            }

            if (y < uSegments - 1)
            {
                var ioffset = y * rSegments + triangleOffset;
                for (int x = 0, n = rSegments - 1; x < n; x++)
                {
                    triangles.Add(ioffset + x);
                    triangles.Add(ioffset + x + rSegments);
                    triangles.Add(ioffset + x + 1);
                    triangles.Add(ioffset + x + 1);
                    triangles.Add(ioffset + x + rSegments);
                    triangles.Add(ioffset + x + 1 + rSegments);
                }
            }
        }
    }
}






