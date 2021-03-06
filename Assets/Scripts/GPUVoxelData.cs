using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUVoxelData : System.IDisposable
{

    public ComputeBuffer Buffer { get { return buffer; } }

    public int Width { get { return width; } }
    public int Height { get { return height; } }
    public int Depth { get { return depth; } }
    public float UnitLength { get { return unitLength; } }

    int width, height, depth;
    float unitLength;

    ComputeBuffer buffer;
    VoxelData[] voxels;

    public GPUVoxelData(ComputeBuffer buf, int w, int h, int d, float u)
    {
        buffer = buf;
        width = w;
        height = h;
        depth = d;
        unitLength = u;
    }

    public VoxelData[] GetData()
    {
        // cache
        if (voxels == null)
        {
            voxels = new VoxelData[Buffer.count];
            Buffer.GetData(voxels);
        }
        return voxels;
    }

    public void Dispose()
    {
        buffer.Release();
    }

}