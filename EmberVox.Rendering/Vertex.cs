using System.Numerics;
using System.Runtime.CompilerServices;
using EmberVox.Core;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering;

public struct Vertex
{
    public Vector2 Position;
    public Vector4 Color;

    public static VertexInputBindingDescription GetBindingDescription() =>
        new()
        {
            Binding = 0,
            Stride = (uint)Unsafe.SizeOf<Vertex>(),
            InputRate = VertexInputRate.Vertex,
        };

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        Vertex v = default;

        return
        [
            new(0, 0, Format.R32G32Sfloat, (uint)Unsafe.OffsetOf(ref v, ref v.Position)),
            new(1, 0, Format.R32G32B32A32Sfloat, (uint)Unsafe.OffsetOf(ref v, ref v.Color)),
        ];
    }
}
