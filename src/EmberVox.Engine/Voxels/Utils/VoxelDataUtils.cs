using System.Numerics;
using EmberVox.Rendering.Types;

namespace EmberVox.Engine.Voxels.Utils;

public static class VoxelDataUtils
{
    public static readonly Dictionary<VoxelFace, List<Vertex>> VoxelRawFaceData = new()
    {
        {
            VoxelFace.Front,
            [
                new Vertex { Position = new Vector3(-0.5f, -0.5f, -0.5f) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, -0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, -0.5f) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, -0.5f) },
            ]
        },
        {
            VoxelFace.Right,
            [
                new Vertex { Position = new Vector3(0.5f, -0.5f, -0.5f) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, 0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, -0.5f) },
            ]
        },
        {
            VoxelFace.Back,
            [
                new Vertex { Position = new Vector3(0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(-0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, 0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, 0.5f) },
            ]
        },
        {
            VoxelFace.Left,
            [
                new Vertex { Position = new Vector3(-0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(-0.5f, -0.5f, -0.5f) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, -0.5f) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, 0.5f) },
            ]
        },
        {
            VoxelFace.Top,
            [
                new Vertex { Position = new Vector3(-0.5f, 0.5f, -0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, -0.5f) },
                new Vertex { Position = new Vector3(0.5f, 0.5f, 0.5f) },
                new Vertex { Position = new Vector3(-0.5f, 0.5f, 0.5f) },
            ]
        },
        {
            VoxelFace.Bottom,
            [
                new Vertex { Position = new Vector3(-0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, 0.5f) },
                new Vertex { Position = new Vector3(0.5f, -0.5f, -0.5f) },
                new Vertex { Position = new Vector3(-0.5f, -0.5f, -0.5f) },
            ]
        },
    };

    public static readonly HashSet<VoxelType> TransparentBlockSet = Enum.GetValues<VoxelType>()
        .Where(voxel => (voxel & VoxelType.Transparent) == VoxelType.Transparent)
        .ToHashSet();

    public static readonly Dictionary<VoxelFace, Vector3> VoxelFaceNormals = new()
    {
        { VoxelFace.Front, -Vector3.UnitZ },
        { VoxelFace.Right, Vector3.UnitX },
        { VoxelFace.Back, Vector3.UnitZ },
        { VoxelFace.Left, -Vector3.UnitX },
        { VoxelFace.Top, Vector3.UnitY },
        { VoxelFace.Bottom, -Vector3.UnitY },
    };

    public static Vertex[] GetVoxelFaceVertices(
        VoxelFace face,
        Vector3 position,
        VoxelType type = VoxelType.Air,
        bool includeUv = false
    )
    {
        Vector2[] uvs = VoxelTextureUtils.GetUVs(type, face, includeUv);

        return VoxelRawFaceData[face]
            .Select(
                (vertex, i) =>
                    new Vertex
                    {
                        Position = position + vertex.Position,
                        Color = new Vector4(1, 1, 1, 1),
                        TexCoord = uvs[i],
                    }
            )
            .ToArray();
    }

    public static uint[] GetVoxelFaceIndices(int existingFaces)
    {
        uint offset = (uint)(existingFaces * 4);
        return
        [
            /* -> Bottom Right Triangle <- */
            0 + offset,
            1 + offset,
            2 + offset,
            /* -> Top Left Triangle <- */
            0 + offset,
            2 + offset,
            3 + offset,
        ];
    }
}
