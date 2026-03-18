using EmberVox.Core.Types;
using EmberVox.Rendering.Contexts;
using Silk.NET.Vulkan;

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
}
