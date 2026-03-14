using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.Buffers;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.RenderingManagement;
using EmberVox.Rendering.ResourceManagement;
using EmberVox.Rendering.Utils;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public sealed class GraphicsPipelineContext : IResource
{
    public Pipeline GraphicsPipeline { get; }
    public DescriptorSetLayout DescriptorSetLayout { get; }
    public PipelineLayout PipelineLayout { get; }
    public IRenderable RenderTarget { get; }
    public DescriptorContext DescriptorContext { get; }

    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly DepthContext _depthContext;
    private readonly List<ShaderModule> _shaderModules = [];

    public unsafe GraphicsPipelineContext(
        IRenderable renderTarget,
        DeviceContext deviceContext,
        SwapChainContext swapChainContext,
        DepthContext depthContext,
        List<BufferContext> uniformBuffers
    )
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _depthContext = depthContext;
        RenderTarget = renderTarget;

        // BIG TODO - Make shaders modular
        byte[] shaderCode = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Shaders", "slang.spv")
        );
        ShaderModule shaderModule = CreateShaderModule(shaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("vertMain"),
        };
        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("fragMain"),
        };

        PipelineShaderStageCreateInfo[] shaderStages = [vertShaderStageInfo, fragShaderStageInfo];

        DescriptorSetLayout = CreateDescriptorSetLayout();
        PipelineLayout = CreatePipelineLayout();
        GraphicsPipeline = CreateGraphicsPipeline(shaderStages);

        DescriptorContext = new DescriptorContext(
            _deviceContext,
            this,
            (uint)_swapChainContext.SwapChainImages.Length,
            uniformBuffers,
            RenderTarget
        );

        SilkMarshal.FreeString((nint)vertShaderStageInfo.PName);
        SilkMarshal.FreeString((nint)fragShaderStageInfo.PName);
    }

    public void Dispose()
    {
        DescriptorContext.Dispose();

        _deviceContext.Api.DestroyDescriptorSetLayout(
            _deviceContext.LogicalDevice,
            DescriptorSetLayout,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyPipeline(
            _deviceContext.LogicalDevice,
            GraphicsPipeline,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyPipelineLayout(
            _deviceContext.LogicalDevice,
            PipelineLayout,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        foreach (ShaderModule shaderModule in _shaderModules)
            _deviceContext.Api.DestroyShaderModule(
                _deviceContext.LogicalDevice,
                shaderModule,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );

        GC.SuppressFinalize(this);
    }

    private unsafe DescriptorSetLayout CreateDescriptorSetLayout()
    {
        DescriptorSetLayoutBinding[] bindingsArray =
        [
            new()
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.VertexBit,
            },
            new()
            {
                Binding = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
            },
        ];
        //fixed (DescriptorSetLayoutBinding* pBindings = bindings)
        //{
        using ManagedPointer<DescriptorSetLayoutBinding> bindingsInfo = new(bindingsArray.Length);
        bindingsArray.CopyTo(bindingsInfo.Span);

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = (uint)bindingsInfo.Length,
            PBindings = bindingsInfo.Pointer,
        };

        DescriptorSetLayout descriptorSetLayout = default;
        _deviceContext.Api.CreateDescriptorSetLayout(
            _deviceContext.LogicalDevice,
            new ReadOnlySpan<DescriptorSetLayoutCreateInfo>(ref layoutInfo),
            ReadOnlySpan<AllocationCallbacks>.Empty,
            new Span<DescriptorSetLayout>(ref descriptorSetLayout)
        );

        return descriptorSetLayout;
        //}
    }

    private unsafe PipelineLayout CreatePipelineLayout()
    {
        DescriptorSetLayout layout = DescriptorSetLayout;
        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &layout,
            PushConstantRangeCount = 0,
        };

        PipelineLayout pipelineLayout = default;
        if (
            _deviceContext.Api.CreatePipelineLayout(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<PipelineLayoutCreateInfo>(ref pipelineLayoutInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<PipelineLayout>(ref pipelineLayout)
            ) != Result.Success
        )
            throw new Exception("Failed to create pipeline layout.");

        return pipelineLayout;
    }

    private unsafe Pipeline CreateGraphicsPipeline(PipelineShaderStageCreateInfo[] shaderStages)
    {
        //DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];

        //fixed (DynamicState* pDynamicStates = dynamicStates)
        //{
        /*
        PipelineDynamicStateCreateInfo dynamicState = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length,
            PDynamicStates = pDynamicStates,
        };
        */

        // -> Dynamic State
        DynamicState[] dynamicStatesArray = [DynamicState.Viewport, DynamicState.Scissor];

        using ManagedPointer<DynamicState> dynamicStates = new(dynamicStatesArray.Length);
        dynamicStatesArray.CopyTo(dynamicStates.Span);

        PipelineDynamicStateCreateInfo dynamicStateInfo = Initializers.CreateDynamicStateInfo(
            dynamicStates
        );

        // -> Vertex Input State
        VertexInputBindingDescription[] bindingDescriptionsArray =
            VertexUtils.GetBindingDescription();
        VertexInputAttributeDescription[] attributeDescriptionsArray =
            VertexUtils.GetAttributeDescriptions();
        /*
        fixed (VertexInputAttributeDescription* pAttributeDescriptions = attributeDescriptionsArray)
        {
            fixed (VertexInputBindingDescription* pBindingDescriptions = bindingDescriptionsArray)
            {
                PipelineVertexInputStateCreateInfo vertexInputInfo = new()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = (uint)bindingDescriptionsArray.Length,
                    PVertexBindingDescriptions = pBindingDescriptions,
                    VertexAttributeDescriptionCount = (uint)attributeDescriptionsArray.Length,
                    PVertexAttributeDescriptions = pAttributeDescriptions,
                };
                */

        // TODO - Binding and Attribute description as params
        using ManagedPointer<VertexInputBindingDescription> bindingDescriptions = new(
            bindingDescriptionsArray.Length
        );
        bindingDescriptionsArray.CopyTo(bindingDescriptions.Span);
        using ManagedPointer<VertexInputAttributeDescription> attributeDescriptions = new(
            attributeDescriptionsArray.Length
        );
        attributeDescriptionsArray.CopyTo(attributeDescriptions.Span);

        PipelineVertexInputStateCreateInfo vertexInputStateInfo =
            Initializers.CreateVertexInputStateInfo(bindingDescriptions, attributeDescriptions);

        // -> Input Assembly State
        // TODO - Topology as param
        PipelineInputAssemblyStateCreateInfo inputAssemblyInfo =
            Initializers.CreateInputAssemblyStateInfo(PrimitiveTopology.TriangleList);

        // -> Viewport State
        PipelineViewportStateCreateInfo viewportStateInfo = Initializers.CreateViewportStateInfo();

        // -> Rasterizer State
        /*PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = Vk.False,
            RasterizerDiscardEnable = Vk.False,
            PolygonMode = PolygonMode.Fill,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.CounterClockwise,
            DepthBiasEnable = Vk.False,
            DepthBiasSlopeFactor = 1.0f,
            LineWidth = 1.0f,
        };*/

        /*
        PipelineRasterizationStateCreateInfo rasterizer = new(
            StructureType.PipelineRasterizationStateCreateInfo,
            null,
            null,
            Vk.False,
            Vk.False,
            PolygonMode.Fill,
            CullModeFlags.BackBit,
            FrontFace.Clockwise,
            Vk.False,
            0.0f,
            0.0f,
            1.0f,
            1.0f
        );
        */
        PipelineRasterizationStateCreateInfo rasterizationStateInfo =
            Initializers.CreateRasterizationStateInfo();

        // -> Multisample State
        /*
        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            SampleShadingEnable = Vk.False,
        };
        */
        // TODO - Add params
        PipelineMultisampleStateCreateInfo multisampleStateInfo =
            Initializers.CreateMultisampleStateInfo();

        // -> Color Blend State
        /*
        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask =
                ColorComponentFlags.RBit
                | ColorComponentFlags.GBit
                | ColorComponentFlags.BBit
                | ColorComponentFlags.ABit,
            BlendEnable = Vk.True,
            SrcColorBlendFactor = BlendFactor.SrcAlpha,
            DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add,
        };
        */
        // TODO -- Get from params
        PipelineColorBlendAttachmentState[] colorBlendAttachmentStateArray =
        [
            new()
            {
                ColorWriteMask =
                    ColorComponentFlags.RBit
                    | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit
                    | ColorComponentFlags.ABit,
                // TODO - Set blending off if not using transparency
                BlendEnable = Vk.True,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
                AlphaBlendOp = BlendOp.Add,
            },
        ];

        using ManagedPointer<PipelineColorBlendAttachmentState> colorBlendAttachmentStates = new(
            colorBlendAttachmentStateArray.Length
        );
        colorBlendAttachmentStateArray.CopyTo(colorBlendAttachmentStates.Span);

        PipelineColorBlendStateCreateInfo colorBlendStateInfo =
            Initializers.CreateColorBlendStateInfo(colorBlendAttachmentStates);

        // -> Depth Stencil State
        PipelineDepthStencilStateCreateInfo depthStencil =
            Initializers.CreateDepthStencilStateInfo();

        // -> Rendering Info
        Format swapChainImageFormat = _swapChainContext.SwapChainImageFormat;
        PipelineRenderingCreateInfo pipelineRenderingCreateInfo = new()
        {
            SType = StructureType.PipelineRenderingCreateInfo,
            ColorAttachmentCount = 1,
            PColorAttachmentFormats = &swapChainImageFormat,
            DepthAttachmentFormat = _depthContext.DepthImageFormat,
        };

        // -> Pipeline Creation
        using ManagedPointer<PipelineShaderStageCreateInfo> shaderStageInfo = new(
            shaderStages.Length
        );
        shaderStages.CopyTo(shaderStageInfo.Span);

        //fixed (PipelineShaderStageCreateInfo* pShaderStages = shaderStages)
        //{
        GraphicsPipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            PNext = &pipelineRenderingCreateInfo,
            StageCount = (uint)shaderStageInfo.Length,
            PStages = shaderStageInfo.Pointer,
            PVertexInputState = &vertexInputStateInfo,
            PInputAssemblyState = &inputAssemblyInfo,
            PViewportState = &viewportStateInfo,
            PDynamicState = &dynamicStateInfo,
            PRasterizationState = &rasterizationStateInfo,
            PMultisampleState = &multisampleStateInfo,
            PColorBlendState = &colorBlendStateInfo,
            PDepthStencilState = &depthStencil,
            Layout = PipelineLayout,
        };

        Pipeline pipeline = default;
        if (
            _deviceContext.Api.CreateGraphicsPipelines(
                _deviceContext.LogicalDevice,
                default,
                1,
                new ReadOnlySpan<GraphicsPipelineCreateInfo>(ref pipelineInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<Pipeline>(ref pipeline)
            ) != Result.Success
        )
            throw new Exception("Failed to create graphics pipeline.");

        return pipeline;
        //}
        //}
        //}
        //}
    }

    private unsafe ShaderModule CreateShaderModule(byte[] shaderCode)
    {
        //fixed (byte* pShaderCode = shaderCode)
        //{

        using ManagedPointer<byte> shaderCodeInfo = new(shaderCode.Length);
        shaderCode.CopyTo(shaderCodeInfo.Span);

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)shaderCodeInfo.Length,
            PCode = (uint*)shaderCodeInfo.Pointer,
        };

        ShaderModule shaderModule = default;
        if (
            _deviceContext.Api.CreateShaderModule(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<ShaderModuleCreateInfo>(ref createInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<ShaderModule>(ref shaderModule)
            ) != Result.Success
        )
            throw new Exception("Failed to create shader module.");
        //}

        _shaderModules.Add(shaderModule);
        Logger.Metric?.WriteLine($"Created shader module of size: {shaderCode.Length} bytes");

        return shaderModule;
    }
}
