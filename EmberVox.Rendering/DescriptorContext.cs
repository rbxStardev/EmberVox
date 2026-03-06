using System.Runtime.CompilerServices;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering;

internal sealed class DescriptorContext : IDisposable
{
    public DescriptorSet this[int index] => _descriptorSets[index];

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly GraphicsPipelineContext _graphicsPipelineContext;
    private readonly DescriptorPool _descriptorPool;
    private List<DescriptorSet> _descriptorSets;

    public DescriptorContext(
        Vk vk,
        DeviceContext deviceContext,
        GraphicsPipelineContext graphicsPipelineContext,
        uint descriptorCount,
        List<BufferContext> uniformBuffers
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _graphicsPipelineContext = graphicsPipelineContext;

        _descriptorPool = CreateDescriptorPool(descriptorCount);
        _descriptorSets = [];
        CreateDescriptorSets(descriptorCount, uniformBuffers);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private unsafe DescriptorPool CreateDescriptorPool(uint descriptorCount)
    {
        DescriptorPoolSize poolSize = new(DescriptorType.UniformBuffer, descriptorCount);
        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = descriptorCount,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
        };

        DescriptorPool descriptorPool = default;
        _vk.CreateDescriptorPool(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<DescriptorPoolCreateInfo>(ref poolInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<DescriptorPool>(ref descriptorPool)
        );

        return descriptorPool;
    }

    private unsafe void CreateDescriptorSets(
        uint descriptorCount,
        List<BufferContext> uniformBuffers
    )
    {
        DescriptorSetLayout[] layouts = new DescriptorSetLayout[descriptorCount];
        Array.Fill(layouts, _graphicsPipelineContext.DescriptorSetLayout);

        fixed (DescriptorSetLayout* pLayouts = layouts)
        {
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _descriptorPool,
                DescriptorSetCount = (uint)layouts.Length,
                PSetLayouts = pLayouts,
            };

            _descriptorSets.Clear();

            Span<DescriptorSet> descriptorSets = new DescriptorSet[descriptorCount];

            _vk.AllocateDescriptorSets(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<DescriptorSetAllocateInfo>(ref allocateInfo),
                descriptorSets
            );

            _descriptorSets.AddRange(descriptorSets);

            for (int i = 0; i < descriptorCount; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = uniformBuffers[i].Buffer,
                    Offset = 0,
                    Range = (ulong)Unsafe.SizeOf<UniformBufferObject>(),
                };
                WriteDescriptorSet descriptorWrite = new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = _descriptorSets[i],
                    DstBinding = 0,
                    DstArrayElement = 0,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.UniformBuffer,
                    PBufferInfo = &bufferInfo,
                };

                _vk.UpdateDescriptorSets(
                    _deviceContext.LogicalDevice,
                    new ReadOnlySpan<WriteDescriptorSet>(ref descriptorWrite),
                    ReadOnlySpan<CopyDescriptorSet>.Empty
                );
            }
        }
    }
}
