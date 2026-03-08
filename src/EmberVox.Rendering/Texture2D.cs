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
    private readonly ImageView _textureImageView;
    private readonly DeviceMemory _textureImageMemory;
    private readonly MemoryRequirements _memoryRequirements;
    private readonly Sampler _textureSampler;

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

        _textureImageView = CreateImageView(_textureImage, Format.R8G8B8A8Srgb);
        _textureSampler = CreateTextureSampler();
    }

    public void Dispose()
    {
        _vk.DestroySampler(
            _deviceContext.LogicalDevice,
            _textureSampler,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyImageView(
            _deviceContext.LogicalDevice,
            _textureImageView,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
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

    private ImageView CreateImageView(Image image, Format format)
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        ImageView imageView = default;
        _vk.CreateImageView(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<ImageViewCreateInfo>(ref viewInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<ImageView>(ref imageView)
        );

        return imageView;
    }

    private Sampler CreateTextureSampler()
    {
        PhysicalDeviceProperties properties = _vk.GetPhysicalDeviceProperties(
            _deviceContext.PhysicalDevice
        );
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = Vk.True,
            MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
            CompareEnable = Vk.False,
            CompareOp = CompareOp.Always,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = Vk.False,
        };

        Sampler sampler = default;
        _vk.CreateSampler(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<SamplerCreateInfo>(ref samplerInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<Sampler>(ref sampler)
        );

        return sampler;
    }
}
