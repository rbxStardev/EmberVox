using System.Runtime.CompilerServices;
using EmberVox.Rendering.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

internal sealed class DescriptorContext : IDisposable
{
    public DescriptorSet this[int index] => _descriptorSets[index];

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly GraphicsPipelineContext _graphicsPipelineContext;
    private readonly DescriptorPool _descriptorPool;
    private readonly List<DescriptorSet> _descriptorSets;
    private readonly Texture2D _texture2D;

    public DescriptorContext(
        Vk vk,
        DeviceContext deviceContext,
        GraphicsPipelineContext graphicsPipelineContext,
        uint descriptorCount,
        List<BufferContext> uniformBuffers,
        Texture2D texture2D
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _graphicsPipelineContext = graphicsPipelineContext;
        _texture2D = texture2D;

        _descriptorPool = CreateDescriptorPool(descriptorCount);
        _descriptorSets = [];
        CreateDescriptorSets(descriptorCount, uniformBuffers);
    }

    public void Dispose()
    {
        _vk.DestroyDescriptorPool(
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
            _vk.CreateDescriptorPool(
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
                DescriptorImageInfo imageInfo = new()
                {
                    Sampler = _texture2D.Sampler,
                    ImageView = _texture2D.ImageView,
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

                _vk.UpdateDescriptorSets(
                    _deviceContext.LogicalDevice,
                    new ReadOnlySpan<WriteDescriptorSet>(descriptorWrites),
                    ReadOnlySpan<CopyDescriptorSet>.Empty
                );
            }
        }
    }
}
