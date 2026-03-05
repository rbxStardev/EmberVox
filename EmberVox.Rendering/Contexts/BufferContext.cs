using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering.Contexts;

internal sealed class BufferContext : IDisposable
{
    public Buffer Buffer { get; }
    public unsafe Span<byte> MappedMemory =>
        _mappedPointer != null ? new Span<byte>(_mappedPointer, (int)_size) : [];

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly ulong _size;
    private readonly DeviceMemory _deviceMemory;
    private readonly MemoryRequirements _memoryRequirements;
    private readonly unsafe void* _mappedPointer;

    public unsafe BufferContext(
        Vk vk,
        DeviceContext deviceContext,
        ulong size,
        BufferUsageFlags bufferUsage,
        MemoryPropertyFlags memoryPropertyFlags =
            MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _size = size;

        Buffer = CreateBuffer(bufferUsage);

        _memoryRequirements = _vk.GetBufferMemoryRequirements(_deviceContext.LogicalDevice, Buffer);
        _deviceMemory = AllocateMemory(memoryPropertyFlags);

        _vk.BindBufferMemory(_deviceContext.LogicalDevice, Buffer, _deviceMemory, 0);

        if (memoryPropertyFlags.HasFlag(MemoryPropertyFlags.HostVisibleBit))
            _vk.MapMemory(
                _deviceContext.LogicalDevice,
                _deviceMemory,
                0,
                _size,
                MemoryMapFlags.None,
                ref _mappedPointer
            );
    }

    public unsafe void Dispose()
    {
        if (_mappedPointer != null)
            _vk.UnmapMemory(_deviceContext.LogicalDevice, _deviceMemory);

        _vk.FreeMemory(
            _deviceContext.LogicalDevice,
            _deviceMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyBuffer(
            _deviceContext.LogicalDevice,
            Buffer,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
    }

    private Buffer CreateBuffer(BufferUsageFlags usage)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = _size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        Buffer buffer = default;
        if (
            _vk.CreateBuffer(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<BufferCreateInfo>(ref bufferInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Buffer>(ref buffer)
            ) != Result.Success
        )
            throw new Exception("Failed to create buffer");

        return buffer;
    }

    private DeviceMemory AllocateMemory(MemoryPropertyFlags memoryPropertyFlags)
    {
        uint memoryTypeIndex = _deviceContext.GetMemoryType(
            _memoryRequirements.MemoryTypeBits,
            memoryPropertyFlags
        );

        MemoryAllocateInfo memoryAllocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = _memoryRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        DeviceMemory deviceMemory = default;
        if (
            _vk.AllocateMemory(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<MemoryAllocateInfo>(ref memoryAllocateInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DeviceMemory>(ref deviceMemory)
            ) != Result.Success
        )
            throw new Exception("Failed to allocate buffer memory");

        return deviceMemory;
    }
}
