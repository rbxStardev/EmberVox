using System.Numerics;
using EmberVox.Core.Types;

namespace EmberVox.Engine.VoxelUtils;

public static class VoxelDataUtils
{
    public static readonly Dictionary<VoxelFace, List<VertexData>> VoxelRawFaceData = new()
    {
        {
            VoxelFace.Front,
            [
                new VertexData(new Vector3(-0.5f, -0.5f, -0.5f)),
                new VertexData(new Vector3(0.5f, -0.5f, -0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, -0.5f)),
                new VertexData(new Vector3(-0.5f, 0.5f, -0.5f)),
            ]
        },
        {
            VoxelFace.Right,
            [
                new VertexData(new Vector3(0.5f, -0.5f, -0.5f)),
                new VertexData(new Vector3(0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, 0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, -0.5f)),
            ]
        },
        {
            VoxelFace.Back,
            [
                new VertexData(new Vector3(0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(-0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(-0.5f, 0.5f, 0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, 0.5f)),
            ]
        },
        {
            VoxelFace.Left,
            [
                new VertexData(new Vector3(-0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(-0.5f, -0.5f, -0.5f)),
                new VertexData(new Vector3(-0.5f, 0.5f, -0.5f)),
                new VertexData(new Vector3(-0.5f, 0.5f, 0.5f)),
            ]
        },
        {
            VoxelFace.Top,
            [
                new VertexData(new Vector3(-0.5f, 0.5f, -0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, -0.5f)),
                new VertexData(new Vector3(0.5f, 0.5f, 0.5f)),
                new VertexData(new Vector3(-0.5f, 0.5f, 0.5f)),
            ]
        },
        {
            VoxelFace.Bottom,
            [
                new VertexData(new Vector3(-0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(0.5f, -0.5f, 0.5f)),
                new VertexData(new Vector3(0.5f, -0.5f, -0.5f)),
                new VertexData(new Vector3(-0.5f, -0.5f, -0.5f)),
            ]
        },
    };

    public static readonly HashSet<VoxelType> TransparentBlockSet = Enum.GetValues<VoxelType>()
        .Where(voxel => voxel.HasFlag(VoxelType.Transparent))
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

    public static VertexData[] GetVoxelFaceVertices(
        VoxelType type,
        VoxelFace face,
        Vector3 position
    )
    {
        Vector2[] uvs = VoxelTextureUtils.GetUVs(type, face);

        return VoxelRawFaceData[face]
            .Select(
                (vertex, i) =>
                    new VertexData(position + vertex.Position, uvs[i], new Vector4(1, 1, 1, 1))
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
