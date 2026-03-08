using EmberVox.Rendering.Contexts;
using Silk.NET.Vulkan;
using StbImageSharp;

namespace EmberVox.Rendering;

internal class Texture2D : IDisposable
{
    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly CommandContext _commandContext;
    private readonly Image _textureImage;
    private readonly DeviceMemory _textureImageMemory;
    private readonly MemoryRequirements _memoryRequirements;

    public Texture2D(
        Vk vk,
        DeviceContext deviceContext,
        CommandContext commandContext,
        string texturePath
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _commandContext = commandContext;

        Span<byte> imageSource = File.ReadAllBytes(texturePath);
        ImageResult imageResult = ImageResult.FromMemory(
            imageSource.ToArray(),
            ColorComponents.RedGreenBlueAlpha
        );
        uint imageSize = (uint)(imageResult.Width * imageResult.Height * 4);

        BufferContext stagingBuffer = new BufferContext(
            _vk,
            _deviceContext,
            imageSize,
            BufferUsageFlags.TransferSrcBit
        );
        imageSource.CopyTo(stagingBuffer.MappedMemory);

        _textureImage = CreateImage(
            (uint)imageResult.Width,
            (uint)imageResult.Height,
            Format.R8G8B8A8Srgb,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit
        );

        _memoryRequirements = _vk.GetImageMemoryRequirements(
            _deviceContext.LogicalDevice,
            _textureImage
        );
        _textureImageMemory = AllocateMemory(MemoryPropertyFlags.DeviceLocalBit);

        _vk.BindImageMemory(_deviceContext.LogicalDevice, _textureImage, _textureImageMemory, 0);

        _commandContext.TransitionImageLayout(
            _textureImage,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal
        );
        _commandContext.CopyBufferToImage(
            stagingBuffer,
            _textureImage,
            (uint)imageResult.Width,
            (uint)imageResult.Height
        );
        stagingBuffer.Dispose();

        _commandContext.TransitionImageLayout(
            _textureImage,
            ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal
        );
    }

    public void Dispose()
    {
        _vk.FreeMemory(
            _deviceContext.LogicalDevice,
            _textureImageMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyImage(
            _deviceContext.LogicalDevice,
            _textureImage,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    private Image CreateImage(
        uint width,
        uint height,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage
    )
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = format,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = tiling,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined,
        };

        Image image = default;
        _vk.CreateImage(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<ImageCreateInfo>(ref imageInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<Image>(ref image)
        );

        return image;
    }

    private DeviceMemory AllocateMemory(MemoryPropertyFlags properties)
    {
        uint memoryTypeIndex = _deviceContext.GetMemoryType(
            _memoryRequirements.MemoryTypeBits,
            properties
        );

        MemoryAllocateInfo memoryAllocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = _memoryRequirements.Size,
            MemoryTypeIndex = memoryTypeIndex,
        };

        DeviceMemory imageMemory = default;
        if (
            _vk.AllocateMemory(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<MemoryAllocateInfo>(ref memoryAllocateInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<DeviceMemory>(ref imageMemory)
            ) != Result.Success
        )
            throw new Exception("Failed to allocate buffer memory");

        return imageMemory;
    }
}
