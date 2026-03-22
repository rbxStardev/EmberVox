using System.Runtime.InteropServices;
using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.ShaderReflection;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.Vulkan;
using DescriptorType = Silk.NET.Vulkan.DescriptorType;
using Result = Silk.NET.Vulkan.Result;

namespace EmberVox.Rendering.Types;

public class ShaderMaterial : IResource
{
    public NewGraphicsPipeline GraphicsPipeline { get; }
    public NewDescriptorContext DescriptorContext { get; }

    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private readonly BufferContext[] _uniformBuffers;
    private readonly ShaderDescriptor[] _shaderDescriptors;

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

        using var descriptorSetLayoutBindings = Initializers.CreateDescriptorSetLayoutBindings(
            vertReflector,
            fragReflector
        );
        Logger.Metric?.WriteLine(
            $"-> Descriptor bindings reflected: {descriptorSetLayoutBindings.Length}"
        );
        _descriptorSetLayout = CreateDescriptorSetLayout(
            deviceContext,
            descriptorSetLayoutBindings
        );

        GraphicsPipeline = new NewGraphicsPipeline(
            deviceContext,
            vertReflector,
            fragReflector,
            _descriptorSetLayout,
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

        DescriptorContext = new NewDescriptorContext(
            deviceContext,
            _descriptorSetLayout,
            (uint)swapChainContext.SwapChainImages.Length
        );

        _shaderDescriptors = vertReflector.GetShaderDescriptors().ToArray();
        foreach (var shaderDescriptor in vertReflector.GetShaderDescriptors())
        {
            if (shaderDescriptor.BindingType == DescriptorType.UniformBuffer)
            {
                for (int i = 0; i < swapChainContext.SwapChainImages.Length; i++)
                {
                    DescriptorBufferInfo bufferInfo = new()
                    {
                        Buffer = _uniformBuffers[i].Buffer,
                        Offset = shaderDescriptor.Offset,
                        Range = shaderDescriptor.Stride,
                    };
                    var writeDescriptorSet = new WriteDescriptorSet
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = DescriptorContext.GetDescriptorSet(i),
                        // TODO - get binding for reflector ayjwhdauwjdh
                        DstBinding = shaderDescriptor.BindingIndex,
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
        }
    }

    public void Dispose()
    {
        DescriptorContext.Dispose();
        _deviceContext.Api.DestroyDescriptorSetLayout(
            _deviceContext.LogicalDevice,
            _descriptorSetLayout,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        foreach (var uniformBuffer in _uniformBuffers)
        {
            uniformBuffer.Dispose();
        }
        GraphicsPipeline.Dispose();
    }

    public unsafe void SetTexture(Sampler sampler, ImageView imageView, ImageLayout imageLayout)
    {
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
                DstSet = DescriptorContext.GetDescriptorSet(i),
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &imageInfo,
            };

            _deviceContext.Api.UpdateDescriptorSets(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<WriteDescriptorSet>(ref writeDescriptorSet),
                ReadOnlySpan<CopyDescriptorSet>.Empty
            );
        }
    }

    public T GetShaderProperty<T>(int bufferIndex, string propertyName)
        where T : unmanaged
    {
        var binding = _shaderDescriptors.First(descriptor => descriptor.Name == propertyName);
        return MemoryMarshal.Read<T>(
            _uniformBuffers[bufferIndex].MappedMemory[(int)binding.Offset..]
        );
    }

    public void SetShaderProperty<T>(int bufferIndex, string propertyName, T value)
        where T : unmanaged
    {
        var binding = _shaderDescriptors.First(descriptor => descriptor.Name == propertyName);
        MemoryMarshal.Write(
            _uniformBuffers[bufferIndex].MappedMemory[(int)binding.Offset..],
            in value
        );
    }

    private static unsafe DescriptorSetLayout CreateDescriptorSetLayout(
        DeviceContext deviceContext,
        ManagedPointer<DescriptorSetLayoutBinding> descriptorSetLayoutBindings
    )
    {
        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)descriptorSetLayoutBindings.Length,
            PBindings = descriptorSetLayoutBindings.Pointer,
        };

        DescriptorSetLayout descriptorSetLayout = default;
        if (
            deviceContext.Api.CreateDescriptorSetLayout(
                deviceContext.LogicalDevice,
                new ReadOnlySpan<DescriptorSetLayoutCreateInfo>(ref layoutInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DescriptorSetLayout>(ref descriptorSetLayout)
            ) != Result.Success
        )
        {
            Logger.Error?.WriteLine("Failed to create descriptor set layout.");
            throw new Exception("Failed to create descriptor set layout.");
        }

        return descriptorSetLayout;
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
