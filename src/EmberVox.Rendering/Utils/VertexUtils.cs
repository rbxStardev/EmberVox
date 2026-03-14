using System.Runtime.CompilerServices;
using EmberVox.Core.Extensions;
using EmberVox.Core.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Utils;

public static class VertexUtils
{
    public static VertexInputBindingDescription[] GetBindingDescription()
    {
        return
        [
            new VertexInputBindingDescription
            {
                Binding = 0,
                Stride = (uint)Unsafe.SizeOf<VertexData>(),
                InputRate = VertexInputRate.Vertex,
            },
        ];
    }

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        VertexData v = default;

        return
        [
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Format = Format.R32G32B32Sfloat,
                Location = 0,
                Offset = (uint)Unsafe.OffsetOf(ref v, ref v.Position),
            },
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Format = Format.R32G32B32A32Sfloat,
                Location = 1,
                Offset = (uint)Unsafe.OffsetOf(ref v, ref v.Color),
            },
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Location = 2,
                Offset = (uint)Unsafe.OffsetOf(ref v, ref v.TexCoord),
            },
        ];
    }
}
