using EmberVox.Core.Types;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public sealed class NewDescriptorContext : IResource
{
    private readonly DeviceContext _deviceContext;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private readonly DescriptorPool _descriptorPool;
    private DescriptorSet[] _descriptorSets = [];

    public DescriptorSet GetDescriptorSet(int index) => _descriptorSets[index];

    public NewDescriptorContext(
        DeviceContext deviceContext,
        DescriptorSetLayout descriptorSetLayout,
        uint descriptorSetCount
    )
    {
        _deviceContext = deviceContext;
        _descriptorSetLayout = descriptorSetLayout;

        _descriptorPool = CreateDescriptorPool(descriptorSetCount);
        CreateDescriptorSets(descriptorSetCount);
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
        DescriptorPoolSize[] poolSizeArray =
        [
            new(DescriptorType.UniformBuffer, descriptorCount),
            new(DescriptorType.CombinedImageSampler, descriptorCount),
        ];

        using ManagedPointer<DescriptorPoolSize> poolSize = new(poolSizeArray.Length);
        poolSizeArray.CopyTo(poolSize.Span);

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = descriptorCount,
            PoolSizeCount = (uint)poolSize.Length,
            PPoolSizes = poolSize.Pointer,
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

    private unsafe void CreateDescriptorSets(uint descriptorSetCount)
    {
        var layoutArray = new DescriptorSetLayout[descriptorSetCount];
        Array.Fill(layoutArray, _descriptorSetLayout);

        using ManagedPointer<DescriptorSetLayout> layout = new(layoutArray.Length);
        layoutArray.CopyTo(layout.Span);

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = (uint)layout.Length,
            PSetLayouts = layout.Pointer,
        };

        _descriptorSets = new DescriptorSet[descriptorSetCount];

        _deviceContext.Api.AllocateDescriptorSets(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<DescriptorSetAllocateInfo>(ref allocateInfo),
            new Span<DescriptorSet>(_descriptorSets)
        );
    }
}
