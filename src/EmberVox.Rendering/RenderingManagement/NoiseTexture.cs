using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.Utils;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.RenderingManagement;

public class NoiseTexture : IRenderable
{
    public Sampler Sampler { get; }
    public ImageView ImageView { get; }

    private readonly DeviceContext _deviceContext;
    private readonly CommandContext _commandContext;
    private readonly uint _mipLevels;
    private readonly Image _textureImage;
    private readonly DeviceMemory _textureImageMemory;
    private readonly MemoryRequirements _memoryRequirements;

    public NoiseTexture(
        DeviceContext deviceContext,
        CommandContext commandContext,
        int width,
        int height
    )
    {
        _deviceContext = deviceContext;
        _commandContext = commandContext;

        _mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;
        uint imageSize = (uint)(width * height * 4);

        BufferContext stagingBuffer = new BufferContext(
            _deviceContext,
            imageSize,
            BufferUsageFlags.TransferSrcBit
        );

        FastNoiseLite noise = new FastNoiseLite();
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.05f);

        byte[] noiseData = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float noiseValue = noise.GetNoise(x, y);
                byte mappedValue = (byte)((noiseValue + 1f) / 2f * 255f);

                int i = (x + y * width) * 4;
                noiseData[i] = mappedValue;
                noiseData[i + 1] = mappedValue;
                noiseData[i + 2] = mappedValue;
                noiseData[i + 3] = 255;
            }
        }

        noiseData.CopyTo(stagingBuffer.MappedMemory);

        _textureImage = ImageUtils.CreateImage(
            _deviceContext.Api,
            _deviceContext.LogicalDevice,
            (uint)width,
            (uint)height,
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
        _commandContext.CopyBufferToImage(stagingBuffer, _textureImage, (uint)width, (uint)height);

        ImageUtils.GenerateMipmaps(
            _deviceContext.Api,
            _commandContext,
            _deviceContext,
            _textureImage,
            Format.R8G8B8A8Srgb,
            width,
            height,
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
