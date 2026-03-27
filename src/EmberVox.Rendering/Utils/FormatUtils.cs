using Silk.NET.SPIRV.Reflect;

namespace EmberVox.Rendering.Utils;

public static class FormatUtils
{
    public static uint GetSpirvFormatSize(Format format)
    {
        return format switch
        {
            Format.R32Sfloat => 4,
            Format.R32G32Sfloat => 8,
            Format.R32G32B32Sfloat => 12,
            Format.R32G32B32A32Sfloat => 16,
            Format.R32Sint => 4,
            Format.R32G32Sint => 8,
            Format.R32G32B32Sint => 12,
            Format.R32G32B32A32Sint => 16,
            Format.R32Uint => 4,
            Format.R32G32Uint => 8,
            Format.R32G32B32Uint => 12,
            Format.R32G32B32A32Uint => 16,
            Format.R64Sfloat => 8,
            Format.R64G64Sfloat => 16,
            Format.R64G64B64Sfloat => 24,
            Format.R64G64B64A64Sfloat => 32,
            _ => throw new NotImplementedException($"Format {format} not implemented"),
        };
    }
}
