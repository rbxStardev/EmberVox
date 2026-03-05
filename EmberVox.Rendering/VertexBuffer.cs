using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace EmberVox.Rendering;

internal sealed class VertexBuffer : IDisposable
{
    private static readonly Vertex[] Vertices =
    [
        new() { Position = new Vector2(0.0f, -0.5f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f) },
        new() { Position = new Vector2(0.5f, 0.5f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f) },
        new() { Position = new Vector2(-0.5f, 0.5f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f) },
    ];

    public Buffer Buffer { get; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;

    private readonly DeviceMemory _vertexBufferMemory;
    private readonly MemoryRequirements _memoryRequirements;

    public unsafe VertexBuffer(Vk vk, DeviceContext deviceContext)
    {
        _vk = vk;
        _deviceContext = deviceContext;

        Buffer = CreateBuffer();

        _memoryRequirements = _vk.GetBufferMemoryRequirements(_deviceContext.LogicalDevice, Buffer);
        _vertexBufferMemory = AllocateMemory();

        _vk.BindBufferMemory(_deviceContext.LogicalDevice, Buffer, _vertexBufferMemory, 0);

        void* data;
        _vk.MapMemory(
            _deviceContext.LogicalDevice,
            _vertexBufferMemory,
            0,
            (ulong)(Unsafe.SizeOf<Vertex>() * Vertices.Length),
            MemoryMapFlags.None,
            &data
        );

        fixed (Vertex* pVertices = Vertices)
        {
            uint size = (uint)(Unsafe.SizeOf<Vertex>() * Vertices.Length);
            void* source = pVertices;
            Unsafe.CopyBlock(data, source, size);
        }

        _vk.UnmapMemory(_deviceContext.LogicalDevice, _vertexBufferMemory);
    }

    private Buffer CreateBuffer()
    {
        BufferCreateInfo bufferInfo = new BufferCreateInfo()
        {
            SType = StructureType.BufferCreateInfo,
            Size = (ulong)(Unsafe.SizeOf<Vertex>() * Vertices.Length),
            Usage = BufferUsageFlags.VertexBufferBit,
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
        {
            throw new Exception("Failed to create Vertex Buffer");
        }

        return buffer;
    }

    private DeviceMemory AllocateMemory()
    {
        uint memoryTypeIndex = _deviceContext.GetMemoryType(
            _memoryRequirements.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
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
            throw new Exception("Failed to allocate memory for Vertex Buffer");

        return deviceMemory;
    }

    public void Dispose()
    {
        _vk.FreeMemory(
            _deviceContext.LogicalDevice,
            _vertexBufferMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyBuffer(
            _deviceContext.LogicalDevice,
            Buffer,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
    }
}
