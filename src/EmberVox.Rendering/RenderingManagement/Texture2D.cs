using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.RenderingManagement;

public class Texture2D : IRenderable
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }

    private readonly DeviceContext _deviceContext;
    private readonly CommandContext _commandContext;
    private readonly uint _mipLevels;
    private readonly Image _textureImage;
    private readonly DeviceMemory _textureImageMemory;
    private readonly MemoryRequirements _memoryRequirements;

    public Texture2D(
        DeviceContext deviceContext,
        CommandContext commandContext,
        TextureData textureData
    )
    {
        _deviceContext = deviceContext;
        _commandContext = commandContext;

        uint textureStride = textureData.Width * textureData.Height * 4;
        _mipLevels =
            (uint)Math.Floor(Math.Log2(Math.Max(textureData.Width, textureData.Height))) + 1;

        BufferContext stagingBuffer = new BufferContext(
            _deviceContext,
            textureStride,
            BufferUsageFlags.TransferSrcBit
        );
        textureData.PixelData.CopyTo(stagingBuffer.MappedMemory);

        _textureImage = ImageUtils.CreateImage(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            textureData.Width,
            textureData.Height,
            _mipLevels,
            Format.R8G8B8A8Srgb,
            ImageTiling.Optimal,
            ImageUsageFlags.TransferSrcBit
                | ImageUsageFlags.TransferDstBit
                | ImageUsageFlags.SampledBit
        );

        _memoryRequirements = _deviceContext.Api.GetImageMemoryRequirements(
            _deviceContext.LogicalDevice,
            _textureImage
        );
        _textureImageMemory = _deviceContext.AllocateMemory(
            _memoryRequirements,
            MemoryPropertyFlags.DeviceLocalBit
        );

        _deviceContext.Api.BindImageMemory(
            _deviceContext.LogicalDevice,
            _textureImage,
            _textureImageMemory,
            0
        );

        _commandContext.TransitionImageLayout(
            _textureImage,
            _mipLevels,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal
        );
        _commandContext.CopyBufferToImage(
            stagingBuffer,
            _textureImage,
            (uint)textureData.Width,
            (uint)textureData.Height
        );

        ImageUtils.GenerateMipmaps(
            _deviceContext.Api,
            _commandContext,
            _deviceContext,
            _textureImage,
            Format.R8G8B8A8Srgb,
            textureData.Width,
            textureData.Height,
            _mipLevels
        );

        ImageView = ImageUtils.CreateImageView(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            _textureImage,
            _mipLevels,
            Format.R8G8B8A8Srgb,
            ImageAspectFlags.ColorBit
        );
        Sampler = CreateTextureSampler();

        stagingBuffer.Dispose();
    }

    public void Dispose()
    {
        _deviceContext.Api.DestroySampler(
            _deviceContext.LogicalDevice,
            Sampler,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyImageView(
            _deviceContext.LogicalDevice,
            ImageView,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.FreeMemory(
            _deviceContext.LogicalDevice,
            _textureImageMemory,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyImage(
            _deviceContext.LogicalDevice,
            _textureImage,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        GC.SuppressFinalize(this);
    }

    private Sampler CreateTextureSampler()
    {
        PhysicalDeviceProperties properties = _deviceContext.Api.GetPhysicalDeviceProperties(
            _deviceContext.PhysicalDevice
        );
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MipLodBias = 0.0f,
            AnisotropyEnable = Vk.True,
            MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
            CompareEnable = Vk.False,
            CompareOp = CompareOp.Always,
            MinLod = 0.0f,
            MaxLod = Vk.LodClampNone,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = Vk.False,
        };

        Sampler sampler = default;
        _deviceContext.Api.CreateSampler(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<SamplerCreateInfo>(ref samplerInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<Sampler>(ref sampler)
        );

        return sampler;
    }
}
