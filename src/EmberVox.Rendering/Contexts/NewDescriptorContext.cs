using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.ShaderReflection;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

public sealed class NewDescriptorContext : IResource
{
    private readonly DescriptorPool _descriptorPool;

    private readonly DeviceContext _deviceContext;
    private DescriptorSet[] _descriptorSets = [];

    // TODO - Separate by layout set too
    public NewDescriptorContext(
        DeviceContext deviceContext,
        IDictionary<(uint set, uint binding), ShaderDescriptor> shaderDescriptors,
        int setMultiplier
    )
    {
        _deviceContext = deviceContext;

        var groupedBindings = shaderDescriptors
            .GroupBy(kvp => kvp.Key.set)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Select(g => g.Value).ToArray());

        DescriptorSetLayouts = new Dictionary<uint, DescriptorSetLayout>();
        foreach (var groupedSet in groupedBindings)
        {
            using var descriptorSetLayoutBindings = Initializers.CreateDescriptorSetLayoutBindings(
                groupedSet.Value
            );

            DescriptorSetLayouts[groupedSet.Key] = CreateDescriptorSetLayout(
                deviceContext,
                descriptorSetLayoutBindings
            );
        }

        _descriptorPool = CreateDescriptorPool(
            groupedBindings.Values.SelectMany(x => x).ToArray(),
            (uint)setMultiplier
        );
        CreateDescriptorSets(setMultiplier);
    }

    public IDictionary<uint, DescriptorSetLayout> DescriptorSetLayouts { get; }

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

        foreach (var kvp in DescriptorSetLayouts)
            _deviceContext.Api.DestroyDescriptorSetLayout(
                _deviceContext.LogicalDevice,
                kvp.Value,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );

        GC.SuppressFinalize(this);
    }

    public DescriptorSet GetDescriptorSet(int bufferIndex, uint setIndex)
    {
        return _descriptorSets[bufferIndex * DescriptorSetLayouts.Count + setIndex];
    }

    public DescriptorSet[] GetDescriptorSets(int bufferIndex)
    {
        return _descriptorSets
            .Skip(bufferIndex * DescriptorSetLayouts.Count)
            .Take(DescriptorSetLayouts.Count)
            .ToArray();
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

    private unsafe DescriptorPool CreateDescriptorPool(
        ShaderDescriptor[] shaderDescriptors,
        uint setMultiplier
    )
    {
        var poolSizeArray = shaderDescriptors
            .Select(x => new DescriptorPoolSize(x.BindingType, setMultiplier))
            .ToArray();

        using ManagedPointer<DescriptorPoolSize> poolSize = new(poolSizeArray.Length);
        poolSizeArray.CopyTo(poolSize.Span);

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
            MaxSets = (uint)(DescriptorSetLayouts.Values.Count * setMultiplier),
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

    private unsafe void CreateDescriptorSets(int setMultiplier)
    {
        var layouts = Enumerable
            .Range(0, setMultiplier)
            .SelectMany(_ => DescriptorSetLayouts.Values)
            .ToArray();

        using ManagedPointer<DescriptorSetLayout> layout = new(layouts.Length);
        layouts.CopyTo(layout.Span);

        DescriptorSetAllocateInfo allocateInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = (uint)layout.Length,
            PSetLayouts = layout.Pointer,
        };

        _descriptorSets = new DescriptorSet[layouts.Length];

        _deviceContext.Api.AllocateDescriptorSets(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<DescriptorSetAllocateInfo>(ref allocateInfo),
            new Span<DescriptorSet>(_descriptorSets)
        );
    }
}
