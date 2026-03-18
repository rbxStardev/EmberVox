using EmberVox.Core.Types;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.GraphicsPipeline;
using Silk.NET.Vulkan;
using DescriptorType = Silk.NET.SPIRV.Reflect.DescriptorType;

namespace EmberVox.Rendering.Utils;

public static class ShaderUtils
{
    public static unsafe ShaderModule LoadShaderModule(
        DeviceContext deviceContext,
        byte[] shaderCode
    )
    {
        using ManagedPointer<byte> shaderCodeInfo = new(shaderCode.Length);
        shaderCode.CopyTo(shaderCodeInfo.Span);

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)shaderCodeInfo.Length,
            PCode = (uint*)shaderCodeInfo.Pointer,
        };

        ShaderModule shaderModule = default;
        if (
            deviceContext.Api.CreateShaderModule(
                deviceContext.LogicalDevice,
                new ReadOnlySpan<ShaderModuleCreateInfo>(ref createInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<ShaderModule>(ref shaderModule)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create shader module");
        }

        return shaderModule;
    }

    public static ShaderBindingType ShaderBindingTypeFromDescriptorType(
        DescriptorType descriptorType
    ) =>
        descriptorType switch
        {
            DescriptorType.UniformBuffer => ShaderBindingType.UniformBuffer,
            DescriptorType.CombinedImageSampler => ShaderBindingType.CombinedImageSampler,
            DescriptorType.StorageBuffer => ShaderBindingType.StorageBuffer,
            _ => ShaderBindingType.Unknown,
        };
}
