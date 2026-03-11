using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;
using StbImageSharp;

namespace EmberVox.Rendering;

internal class Texture2D : IDisposable
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }

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

        StbImage.stbi_set_flip_vertically_on_load(1);
        ImageResult imageResult = ImageResult.FromStream(
            File.OpenRead(texturePath),
            ColorComponents.RedGreenBlueAlpha
        );
        uint imageSize = (uint)(imageResult.Width * imageResult.Height * 4);

        BufferContext stagingBuffer = new BufferContext(
            _vk,
            _deviceContext,
            imageSize,
            BufferUsageFlags.TransferSrcBit
        );
        imageResult.Data.CopyTo(stagingBuffer.MappedMemory);

        _textureImage = ImageUtils.CreateImage(
            _vk,
            _deviceContext.LogicalDevice,
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
        _textureImageMemory = _deviceContext.AllocateMemory(
            _memoryRequirements,
            MemoryPropertyFlags.DeviceLocalBit
        );

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

        ImageView = ImageUtils.CreateImageView(
            _vk,
            _deviceContext.LogicalDevice,
            _textureImage,
            Format.R8G8B8A8Srgb,
            ImageAspectFlags.ColorBit
        );
        Sampler = CreateTextureSampler();
    }

    public void Dispose()
    {
        _vk.DestroySampler(
            _deviceContext.LogicalDevice,
            Sampler,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _vk.DestroyImageView(
            _deviceContext.LogicalDevice,
            ImageView,
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
