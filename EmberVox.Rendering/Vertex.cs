using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EmberVox.Core;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering;

public struct Vertex
{
    public Vector2 Position;
    public Vector4 Color;

    public static VertexInputBindingDescription GetBindingDescription()
    {
        return new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)Unsafe.SizeOf<Vertex>(),
            InputRate = VertexInputRate.Vertex,
        };
    }

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        Vertex defaultVertex = default;

        return
        [
            new VertexInputAttributeDescription(
                0,
                0,
                Format.R32G32Sfloat,
                (uint)Unsafe.OffsetOf(ref defaultVertex, ref defaultVertex.Position)
            ),
            new VertexInputAttributeDescription(
                1,
                0,
                Format.R32G32B32A32Sfloat,
                (uint)Unsafe.OffsetOf(ref defaultVertex, ref defaultVertex.Color)
            ),
        ];
    }
}
