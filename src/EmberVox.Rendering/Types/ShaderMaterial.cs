using System.Runtime.InteropServices;
using EmberVox.Core.Logging;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.ShaderReflection;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.Vulkan;
using DescriptorType = Silk.NET.Vulkan.DescriptorType;

namespace EmberVox.Rendering.Types;

public class ShaderMaterial : IResource
{
    private readonly DeviceContext _deviceContext;
    private readonly IDictionary<(uint binding, uint set), ShaderDescriptor> _shaderDescriptors;
    private readonly SwapChainContext _swapChainContext;
    private readonly BufferContext[] _uniformBuffers;

    public unsafe ShaderMaterial(
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        ReadOnlySpan<byte> vertexShaderCode,
        ReadOnlySpan<byte> fragmentShaderCode,
        PrimitiveTopology primitiveTopology,
        TargetInfo targetInfo,
        VertexInputRate inputRate,
        RasterizerState rasterizerState,
        MultisampleState multisampleState,
        DepthStencilState depthStencilState
    )
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;

        using var reflect = Reflect.GetApi();
        using var vertReflector = new ShaderReflector(reflect, vertexShaderCode);
        vertReflector.Dump();
        using var fragReflector = new ShaderReflector(reflect, fragmentShaderCode);
        fragReflector.Dump();

        _shaderDescriptors = new Dictionary<(uint binding, uint set), ShaderDescriptor>();

        foreach (var shaderDescriptor in vertReflector.GetShaderDescriptors())
            _shaderDescriptors[(shaderDescriptor.BindingIndex, shaderDescriptor.SetIndex)] =
                shaderDescriptor;

        Logger.Debug?.WriteLine("=== PRE-MERGE VERT DESCRIPTORS ===");
        foreach (var (key, val) in _shaderDescriptors)
            Logger.Debug?.WriteLine(
                $"  ({key.Item1},{key.Item2}) => type={val.BindingType}, stage={val.StageFlags}"
            );

        Logger.Debug?.WriteLine("=== PRE-MERGE FRAG DESCRIPTORS ===");
        foreach (var d in fragReflector.GetShaderDescriptors())
            Logger.Debug?.WriteLine(
                $"  ({d.BindingIndex},{d.SetIndex}) => type={d.BindingType}, stage={d.StageFlags}"
            );

        foreach (var shaderDescriptor in fragReflector.GetShaderDescriptors())
            if (
                _shaderDescriptors.TryGetValue(
                    (shaderDescriptor.BindingIndex, shaderDescriptor.SetIndex),
                    out var existingShaderDescriptor
                )
                && existingShaderDescriptor.BindingType == shaderDescriptor.BindingType
            )
                _shaderDescriptors[(shaderDescriptor.BindingIndex, shaderDescriptor.SetIndex)] =
                    existingShaderDescriptor with
                    {
                        StageFlags =
                            existingShaderDescriptor.StageFlags | shaderDescriptor.StageFlags,
                    };
            else
                _shaderDescriptors[(shaderDescriptor.BindingIndex, shaderDescriptor.SetIndex)] =
                    shaderDescriptor;

        Logger.Metric?.WriteLine($"Descriptors reflected: {_shaderDescriptors.Count}");
        Logger.Metric?.WriteLine("Merged Descriptors:");
        foreach (var (key, value) in _shaderDescriptors)
            Logger.Metric?.WriteLine($"-> {key}: {value}");

        DescriptorContext = new NewDescriptorContext(
            deviceContext,
            _shaderDescriptors,
            swapChainContext.SwapChainImages.Length
        );

        GraphicsPipeline = new NewGraphicsPipeline(
            deviceContext,
            vertReflector,
            fragReflector,
            DescriptorContext.DescriptorSetLayouts.Values.ToArray(),
            primitiveTopology,
            targetInfo,
            inputRate,
            rasterizerState,
            multisampleState,
            depthStencilState
        );

        uint vertUboSize = (uint)
            vertReflector
                .GetShaderDescriptors()
                .ToArray()
                .Where(binding => binding.BindingType == DescriptorType.UniformBuffer)
                .Sum(binding => binding.Stride);
        uint fragUboSize = (uint)
            fragReflector
                .GetShaderDescriptors()
                .ToArray()
                .Where(binding => binding.BindingType == DescriptorType.UniformBuffer)
                .Sum(binding => binding.Stride);
        uint totalUboSize = vertUboSize + fragUboSize;

        Logger.Metric?.WriteLine($"Creating uniform buffers of size: {totalUboSize} bytes");
        _uniformBuffers = CreateUniformBuffers(deviceContext, swapChainContext, totalUboSize);

        foreach (var mergedDescriptor in _shaderDescriptors.Values)
            if (mergedDescriptor.BindingType == DescriptorType.UniformBuffer)
                for (int i = 0; i < swapChainContext.SwapChainImages.Length; i++)
                {
                    DescriptorBufferInfo bufferInfo = new()
                    {
                        Buffer = _uniformBuffers[i].Buffer,
                        Offset = mergedDescriptor.Offset,
                        Range = mergedDescriptor.Stride,
                    };
                    var writeDescriptorSet = new WriteDescriptorSet
                    {
                        SType = StructureType.WriteDescriptorSet,
                        // TODO - replace this somehow with mergedDescriptor.Value.SetIndex
                        DstSet = DescriptorContext.GetDescriptorSet(i, mergedDescriptor.SetIndex),
                        DstBinding = mergedDescriptor.BindingIndex,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        PBufferInfo = &bufferInfo,
                    };

                    _deviceContext.Api.UpdateDescriptorSets(
                        _deviceContext.LogicalDevice,
                        new ReadOnlySpan<WriteDescriptorSet>(ref writeDescriptorSet),
                        ReadOnlySpan<CopyDescriptorSet>.Empty
                    );
                }
    }

    public NewGraphicsPipeline GraphicsPipeline { get; }
    public NewDescriptorContext DescriptorContext { get; }

    public void Dispose()
    {
        DescriptorContext.Dispose();
        foreach (var uniformBuffer in _uniformBuffers)
            uniformBuffer.Dispose();
        GraphicsPipeline.Dispose();
    }

    public T? GetShaderUniform<T>(int bufferIndex, string propertyName)
        where T : unmanaged
    {
        var binding = _shaderDescriptors.Values.First(descriptor =>
            descriptor.Name == propertyName
        );
        if (binding.BindingType != DescriptorType.UniformBuffer)
        {
            Logger.Warning?.WriteLine(
                $"Shader property [{binding.BindingType}]{propertyName} is not an UniformBuffer"
            );
            return null;
        }

        return MemoryMarshal.Read<T>(
            _uniformBuffers[bufferIndex].MappedMemory[(int)binding.Offset..]
        );
    }

    public void SetShaderUniform<T>(int bufferIndex, string propertyName, T value)
        where T : unmanaged
    {
        var binding = _shaderDescriptors.Values.First(descriptor =>
            descriptor.Name == propertyName
        );
        if (binding.BindingType != DescriptorType.UniformBuffer)
        {
            Logger.Warning?.WriteLine(
                $"Shader property [{binding.BindingType}]{propertyName} is not an UniformBuffer"
            );
            return;
        }

        MemoryMarshal.Write(
            _uniformBuffers[bufferIndex].MappedMemory[(int)binding.Offset..],
            in value
        );
    }

    public unsafe void SetShaderCombinedImageSampler(
        string propertyName,
        Sampler sampler,
        ImageView imageView,
        ImageLayout imageLayout
    )
    {
        var binding = _shaderDescriptors.Values.First(descriptor =>
            descriptor.Name == propertyName
        );
        if (binding.BindingType != DescriptorType.CombinedImageSampler)
        {
            Logger.Warning?.WriteLine(
                $"Shader property [{binding.BindingType}]{propertyName} is not a CombinedImageSampler"
            );
            return;
        }

        for (int i = 0; i < _swapChainContext.SwapChainImages.Length; i++)
        {
            DescriptorImageInfo imageInfo = new()
            {
                Sampler = sampler,
                ImageView = imageView,
                ImageLayout = imageLayout,
            };

            WriteDescriptorSet writeDescriptorSet = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = DescriptorContext.GetDescriptorSet(i, binding.SetIndex),
                DstBinding = binding.BindingIndex,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = binding.BindingType,
                PImageInfo = &imageInfo,
            };

            _deviceContext.Api.UpdateDescriptorSets(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<WriteDescriptorSet>(ref writeDescriptorSet),
                ReadOnlySpan<CopyDescriptorSet>.Empty
            );
        }
    }

    private BufferContext[] CreateUniformBuffers(
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        uint bufferSize
    )
    {
        var uniformBuffers = new BufferContext[swapChainContext.SwapChainImages.Length];

        for (int i = 0; i < swapChainContext.SwapChainImages.Length; i++)
        {
            var uniformBuffer = new BufferContext(
                deviceContext,
                bufferSize,
                BufferUsageFlags.UniformBufferBit
            );
            uniformBuffers[i] = uniformBuffer;
        }

        return uniformBuffers;
    }
}
