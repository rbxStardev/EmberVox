using System.Numerics;

namespace EmberVox.Engine.Voxels.Utils;

internal static class VoxelTextureUtils
{
    //TODO -> try texture arrays instead of texture atlases
    private const int AtlasSize = 16;

    private static Vector2[] GetUnmappedUVs()
    {
        return [new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)];
    }

    private static Vector2[] GetAtlasUVs(int column, int row)
    {
        float size = 1.0f / AtlasSize;

        float u = column * size;
        float v = 1.0f - (row + 1) * size;

        return
        [
            new Vector2(u, v),
            new Vector2(u + size, v),
            new Vector2(u + size, v + size),
            new Vector2(u, v + size),
        ];
    }

    private static readonly Dictionary<VoxelType, Vector2[]> DefaultVoxelUVs = new()
    {
        { VoxelType.Dirt, GetAtlasUVs(2, 0) },
        { VoxelType.Grass, GetAtlasUVs(3, 0) },
        { VoxelType.Bedrock, GetAtlasUVs(1, 1) },
    };

    private static readonly Dictionary<
        VoxelType,
        Dictionary<VoxelFace, Vector2[]>
    > VoxelFaceUvOverrides = new()
    {
        {
            VoxelType.Grass,
            new Dictionary<VoxelFace, Vector2[]>()
            {
                { VoxelFace.Top, GetAtlasUVs(7, 2) },
                { VoxelFace.Bottom, GetAtlasUVs(2, 0) },
            }
        },
    };

    public static Vector2[] GetUVs(VoxelType voxelType, VoxelFace voxelFace, bool mapped)
    {
        if (!mapped)
            return GetUnmappedUVs();

        if (
            VoxelFaceUvOverrides.TryGetValue(voxelType, out var faceDict)
            && faceDict.TryGetValue(voxelFace, out var overrideUVs)
        )
            return overrideUVs;

        if (DefaultVoxelUVs.TryGetValue(voxelType, out var defaultUv))
            return defaultUv;

        return GetAtlasUVs(15, 2);
    }
}
