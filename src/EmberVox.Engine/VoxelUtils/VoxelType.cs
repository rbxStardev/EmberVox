namespace EmberVox.Engine.VoxelUtils;

[Flags]
public enum VoxelType : uint
{
    Block = 1 << 0,
    Air = 1 << 1 | Transparent,
    Godot = 1 << 2,
    Dirt = 1 << 3,
    Grass = 1 << 4,
    Bedrock = 1 << 5,
    Transparent = 1 << 30,
}
