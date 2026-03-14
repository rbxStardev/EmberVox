using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;
using StbImageSharp;

namespace EmberVox.Rendering.Types;

public class Texture2D : IResource, IRenderable
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }

    private readonly DeviceContext _deviceContext;
    private readonly CommandContext _commandContext;
    private readonly uint _mipLevels;
    private readonly Image _textureImage;
    private readonly DeviceMemory _textureImageMemory;
    private readonly MemoryRequirements _memoryRequirements;

    public Texture2D(DeviceContext deviceContext, CommandContext commandContext, string texturePath)
    {
        _deviceContext = deviceContext;
        _commandContext = commandContext;

        StbImage.stbi_set_flip_vertically_on_load(1);
        ImageResult imageResult = ImageResult.FromStream(
            File.OpenRead(texturePath),
            ColorComponents.RedGreenBlueAlpha
        );
        _mipLevels =
            (uint)Math.Floor(Math.Log2(Math.Max(imageResult.Width, imageResult.Height))) + 1;
        uint imageSize = (uint)(imageResult.Width * imageResult.Height * 4);

        BufferContext stagingBuffer = new BufferContext(
            _deviceContext,
            imageSize,
            BufferUsageFlags.TransferSrcBit
        );
        imageResult.Data.CopyTo(stagingBuffer.MappedMemory);

        _textureImage = ImageUtils.CreateImage(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            (uint)imageResult.Width,
            (uint)imageResult.Height,
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
            (uint)imageResult.Width,
            (uint)imageResult.Height
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
            (uint)imageResult.Width,
            (uint)imageResult.Height
        );

        ImageUtils.GenerateMipmaps(
            _deviceContext.Api,
            _commandContext,
            _deviceContext,
            _textureImage,
            Format.R8G8B8A8Srgb,
            imageResult.Width,
            imageResult.Height,
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
