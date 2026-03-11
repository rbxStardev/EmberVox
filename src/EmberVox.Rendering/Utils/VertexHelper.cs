using System.Runtime.CompilerServices;
using EmberVox.Core.Extensions;
using EmberVox.Core.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Utils;

public static class VertexHelper
{
    public static VertexInputBindingDescription GetBindingDescription() =>
        new()
        {
            Binding = 0,
            Stride = (uint)Unsafe.SizeOf<VertexData>(),
            InputRate = VertexInputRate.Vertex,
        };

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        VertexData v = default;

        return
        [
            new VertexInputAttributeDescription(
                0,
                0,
                Format.R32G32B32Sfloat,
                (uint)Unsafe.OffsetOf(ref v, ref v.Position)
            ),
            new VertexInputAttributeDescription(
                1,
                0,
                Format.R32G32B32A32Sfloat,
                (uint)Unsafe.OffsetOf(ref v, ref v.Color)
            ),
            new VertexInputAttributeDescription(
                2,
                0,
                Format.R32G32Sfloat,
                (uint)Unsafe.OffsetOf(ref v, ref v.TexCoord)
            ),
        ];
    }
}
