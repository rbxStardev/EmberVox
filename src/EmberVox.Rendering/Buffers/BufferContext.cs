using EmberVox.Core.Logging;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.ResourceManagement;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering.Buffers;

public sealed class BufferContext : IResource
{
    public Buffer Buffer { get; }
    public unsafe Span<byte> MappedMemory =>
        _mappedPointer != null ? new Span<byte>(_mappedPointer, (int)_size) : [];

    private readonly DeviceContext _deviceContext;
    private readonly ulong _size;
    private readonly DeviceMemory _deviceMemory;
    private readonly MemoryRequirements _memoryRequirements;
    private readonly unsafe void* _mappedPointer;

    public unsafe BufferContext(
        DeviceContext deviceContext,
        ulong size,
        BufferUsageFlags bufferUsage,
        MemoryPropertyFlags memoryPropertyFlags =
            MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostVisibleBit
    )
    {
        _deviceContext = deviceContext;
        _size = size;

        Buffer = CreateBuffer(bufferUsage);

        _memoryRequirements = _deviceContext.Api.GetBufferMemoryRequirements(
            _deviceContext.LogicalDevice,
            Buffer
        );
        _deviceMemory = AllocateMemory(memoryPropertyFlags);

        _deviceContext.Api.BindBufferMemory(_deviceContext.LogicalDevice, Buffer, _deviceMemory, 0);

        if (memoryPropertyFlags.HasFlag(MemoryPropertyFlags.HostVisibleBit))
        {
            _deviceContext.Api.MapMemory(
                _deviceContext.LogicalDevice,
                _deviceMemory,
                0,
                _size,
                MemoryMapFlags.None,
                ref _mappedPointer
            );

            Logger.Metric?.WriteLine("-> Buffer created is visible to host");
        }
        Console.WriteLine();
    }

    public unsafe void Dispose()
    {
        if (_mappedPointer != null)
            _deviceContext.Api.UnmapMemory(_deviceContext.LogicalDevice, _deviceMemory);

        _deviceContext.Api.FreeMemory(
            _deviceContext.LogicalDevice,
            _deviceMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyBuffer(
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
            _deviceContext.Api.CreateBuffer(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<BufferCreateInfo>(ref bufferInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Buffer>(ref buffer)
            ) != Result.Success
        )
            throw new Exception("Failed to create buffer");

        Logger.Metric?.WriteLine("Created buffer, listing properties...");
        Logger.Metric?.WriteLine($"-> Buffer size: {_size} bytes");
        Logger.Metric?.WriteLine($"-> Buffer usage: {usage}");

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
            _deviceContext.Api.AllocateMemory(
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
