using System.Runtime.CompilerServices;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.RenderingManagement;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public sealed class DescriptorContext : IResource
{
    public DescriptorSet this[int index] => _descriptorSets[index];

    private readonly DeviceContext _deviceContext;
    private readonly GraphicsPipelineContext _graphicsPipelineContext;
    private readonly DescriptorPool _descriptorPool;
    private DescriptorSet[] _descriptorSets = [];
    private readonly IRenderable _renderTarget;

    public DescriptorContext(
        DeviceContext deviceContext,
        GraphicsPipelineContext graphicsPipelineContext,
        uint descriptorCount,
        List<BufferContext> uniformBuffers,
        IRenderable renderTarget
    )
    {
        _deviceContext = deviceContext;
        _graphicsPipelineContext = graphicsPipelineContext;
        _renderTarget = renderTarget;

        _descriptorPool = CreateDescriptorPool(descriptorCount);
        CreateDescriptorSets(descriptorCount, uniformBuffers);
    }

    public void Dispose()
    {
        _deviceContext.Api.FreeDescriptorSets(
            _deviceContext.LogicalDevice,
            _descriptorPool,
            new ReadOnlySpan<DescriptorSet>(_descriptorSets)
        );

        _deviceContext.Api.DestroyDescriptorPool(
            _deviceContext.LogicalDevice,
            _descriptorPool,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    private unsafe DescriptorPool CreateDescriptorPool(uint descriptorCount)
    {
        DescriptorPoolSize[] poolSize =
        [
            new(DescriptorType.UniformBuffer, descriptorCount),
            new(DescriptorType.CombinedImageSampler, descriptorCount),
        ];
        fixed (DescriptorPoolSize* pPoolSize = poolSize)
        {
            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = descriptorCount,
                PoolSizeCount = (uint)poolSize.Length,
                PPoolSizes = pPoolSize,
            };

            DescriptorPool descriptorPool = default;
            _deviceContext.Api.CreateDescriptorPool(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<DescriptorPoolCreateInfo>(ref poolInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DescriptorPool>(ref descriptorPool)
            );

            return descriptorPool;
        }
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

            _descriptorSets = new DescriptorSet[descriptorCount];

            Span<DescriptorSet> descriptorSets = new DescriptorSet[descriptorCount];

            _deviceContext.Api.AllocateDescriptorSets(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<DescriptorSetAllocateInfo>(ref allocateInfo),
                descriptorSets
            );

            descriptorSets.CopyTo(new Span<DescriptorSet>(_descriptorSets));

            for (int i = 0; i < descriptorCount; i++)
            {
                DescriptorBufferInfo bufferInfo = new()
                {
                    Buffer = uniformBuffers[i].Buffer,
                    Offset = 0,
                    Range = (ulong)Unsafe.SizeOf<UniformBufferObject>(),
                };
                DescriptorImageInfo imageInfo = new()
                {
                    Sampler = _renderTarget.Sampler,
                    ImageView = _renderTarget.ImageView,
                    ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                };
                WriteDescriptorSet[] descriptorWrites =
                [
                    new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = _descriptorSets[i],
                        DstBinding = 0,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.UniformBuffer,
                        PBufferInfo = &bufferInfo,
                    },
                    new()
                    {
                        SType = StructureType.WriteDescriptorSet,
                        DstSet = _descriptorSets[i],
                        DstBinding = 1,
                        DstArrayElement = 0,
                        DescriptorCount = 1,
                        DescriptorType = DescriptorType.CombinedImageSampler,
                        PImageInfo = &imageInfo,
                    },
                ];

                _deviceContext.Api.UpdateDescriptorSets(
                    _deviceContext.LogicalDevice,
                    new ReadOnlySpan<WriteDescriptorSet>(descriptorWrites),
                    ReadOnlySpan<CopyDescriptorSet>.Empty
                );
            }
        }
    }
}
