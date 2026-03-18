namespace EmberVox.Rendering.Utils;

public static class FormatUtils
{
    public static uint GetVulkanFormatSize(Silk.NET.Vulkan.Format format)
    {
        throw new NotImplementedException();
    }

    public static uint GetSpirvFormatSize(Silk.NET.SPIRV.Reflect.Format format) =>
        format switch
        {
            Silk.NET.SPIRV.Reflect.Format.R32Sfloat => 4,
            Silk.NET.SPIRV.Reflect.Format.R32G32Sfloat => 8,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32Sfloat => 12,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32A32Sfloat => 16,
            Silk.NET.SPIRV.Reflect.Format.R32Sint => 4,
            Silk.NET.SPIRV.Reflect.Format.R32G32Sint => 8,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32Sint => 12,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32A32Sint => 16,
            Silk.NET.SPIRV.Reflect.Format.R32Uint => 4,
            Silk.NET.SPIRV.Reflect.Format.R32G32Uint => 8,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32Uint => 12,
            Silk.NET.SPIRV.Reflect.Format.R32G32B32A32Uint => 16,
            Silk.NET.SPIRV.Reflect.Format.R64Sfloat => 8,
            Silk.NET.SPIRV.Reflect.Format.R64G64Sfloat => 16,
            Silk.NET.SPIRV.Reflect.Format.R64G64B64Sfloat => 24,
            Silk.NET.SPIRV.Reflect.Format.R64G64B64A64Sfloat => 32,
            _ => throw new NotImplementedException($"Format {format} not implemented"),
        };
}
