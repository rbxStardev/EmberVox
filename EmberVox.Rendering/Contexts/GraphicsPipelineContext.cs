using EmberVox.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Contexts;

internal sealed class GraphicsPipelineContext : IDisposable
{
    public Pipeline GraphicsPipeline { get; }

    private readonly Vk _vk;
    private readonly DeviceContext _deviceContext;
    private readonly SwapChainContext _swapChainContext;
    private readonly List<ShaderModule> _shaderModules;

    private readonly PipelineLayout _pipelineLayout;

    public unsafe GraphicsPipelineContext(
        Vk vk,
        DeviceContext deviceContext,
        SwapChainContext swapChainContext
    )
    {
        _vk = vk;
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;
        _shaderModules = [];

        byte[] shaderCode = File.ReadAllBytes("Shaders/slang.spv");
        ShaderModule shaderModule = CreateShaderModule(shaderCode);
        Logger.Metric?.WriteLine($"Created shader module of size: {shaderCode.Length} bytes");

        PipelineShaderStageCreateInfo vertShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("vertMain"),
        };
        PipelineShaderStageCreateInfo fragShaderStageInfo = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("fragMain"),
        };

        PipelineShaderStageCreateInfo[] shaderStages = [vertShaderStageInfo, fragShaderStageInfo];

        _pipelineLayout = CreatePipelineLayout();
        GraphicsPipeline = CreateGraphicsPipeline(shaderStages);

        SilkMarshal.FreeString((nint)vertShaderStageInfo.PName);
        SilkMarshal.FreeString((nint)fragShaderStageInfo.PName);
    }

    private unsafe PipelineLayout CreatePipelineLayout()
    {
        PipelineLayoutCreateInfo pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PushConstantRangeCount = 0,
        };

        PipelineLayout pipelineLayout = default;
        if (
            _vk.CreatePipelineLayout(
                _deviceContext.LogicalDevice,
                new ReadOnlySpan<PipelineLayoutCreateInfo>(ref pipelineLayoutInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<PipelineLayout>(ref pipelineLayout)
            ) != Result.Success
        )
        {
            throw new Exception("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }

    private unsafe Pipeline CreateGraphicsPipeline(PipelineShaderStageCreateInfo[] shaderStages)
    {
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];
        fixed (DynamicState* pDynamicStates = dynamicStates)
        {
            PipelineDynamicStateCreateInfo dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = pDynamicStates,
            };

            PipelineVertexInputStateCreateInfo vertexInputInfo =
                new PipelineVertexInputStateCreateInfo
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                };

            PipelineInputAssemblyStateCreateInfo inputAssembly =
                new PipelineInputAssemblyStateCreateInfo
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                };

            Viewport viewport = new Viewport(
                0.0f,
                0.0f,
                _swapChainContext.SwapChainExtent.Width,
                _swapChainContext.SwapChainExtent.Height,
                0.0f,
                1.0f
            );

            Rect2D rect2D = new Rect2D(new Offset2D(0, 0), _swapChainContext.SwapChainExtent);

            PipelineViewportStateCreateInfo viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            PipelineRasterizationStateCreateInfo rasterizer =
                new PipelineRasterizationStateCreateInfo
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = Vk.False,
                    RasterizerDiscardEnable = Vk.False,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.BackBit,
                    FrontFace = FrontFace.Clockwise,
                    DepthBiasEnable = Vk.False,
                    DepthBiasSlopeFactor = 1.0f,
                    LineWidth = 1.0f,
                };

            PipelineMultisampleStateCreateInfo multisampling =
                new PipelineMultisampleStateCreateInfo
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                    SampleShadingEnable = Vk.False,
                };

            PipelineColorBlendAttachmentState colorBlendAttachment =
                new PipelineColorBlendAttachmentState
                {
                    BlendEnable = Vk.False,
                    ColorWriteMask =
                        ColorComponentFlags.RBit
                        | ColorComponentFlags.GBit
                        | ColorComponentFlags.BBit
                        | ColorComponentFlags.ABit,
                };
            colorBlendAttachment.BlendEnable = Vk.True;
            colorBlendAttachment.SrcColorBlendFactor = BlendFactor.SrcAlpha;
            colorBlendAttachment.DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha;
            colorBlendAttachment.ColorBlendOp = BlendOp.Add;
            colorBlendAttachment.SrcAlphaBlendFactor = BlendFactor.One;
            colorBlendAttachment.DstAlphaBlendFactor = BlendFactor.Zero;
            colorBlendAttachment.AlphaBlendOp = BlendOp.Add;

            PipelineColorBlendStateCreateInfo colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = Vk.False,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            Format swapChainImageFormat = _swapChainContext.SwapChainImageFormat;
            PipelineRenderingCreateInfo pipelineRenderingCreateInfo =
                new PipelineRenderingCreateInfo
                {
                    SType = StructureType.PipelineRenderingCreateInfo,
                    ColorAttachmentCount = 1,
                    PColorAttachmentFormats = &swapChainImageFormat,
                };

            fixed (PipelineShaderStageCreateInfo* pShaderStages = shaderStages)
            {
                GraphicsPipelineCreateInfo pipelineInfo = new GraphicsPipelineCreateInfo
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    PNext = &pipelineRenderingCreateInfo,
                    StageCount = 2,
                    PStages = pShaderStages,
                    PVertexInputState = &vertexInputInfo,
                    PInputAssemblyState = &inputAssembly,
                    PViewportState = &viewportState,
                    PRasterizationState = &rasterizer,
                    PMultisampleState = &multisampling,
                    PColorBlendState = &colorBlending,
                    PDynamicState = &dynamicState,
                    Layout = _pipelineLayout,
                };

                Pipeline pipeline = default;
                if (
                    _vk.CreateGraphicsPipelines(
                        _deviceContext.LogicalDevice,
                        default,
                        1,
                        new ReadOnlySpan<GraphicsPipelineCreateInfo>(ref pipelineInfo),
                        ReadOnlySpan<AllocationCallbacks>.Empty,
                        new Span<Pipeline>(ref pipeline)
                    ) != Result.Success
                )
                {
                    throw new Exception("Failed to create graphics pipeline.");
                }

                return pipeline;
            }
        }
    }

    private unsafe ShaderModule CreateShaderModule(byte[] shaderCode)
    {
        ShaderModule shaderModule;
        fixed (byte* pShaderCode = shaderCode)
        {
            ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)shaderCode.Length,
                PCode = (uint*)pShaderCode,
            };

            if (
                _vk.CreateShaderModule(
                    _deviceContext.LogicalDevice,
                    &createInfo,
                    null,
                    out shaderModule
                ) != Result.Success
            )
                throw new Exception("Failed to create shader module.");
        }
        _shaderModules.Add(shaderModule);

        return shaderModule;
    }

    public void Dispose()
    {
        _vk.DestroyPipeline(
            _deviceContext.LogicalDevice,
            GraphicsPipeline,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        _vk.DestroyPipelineLayout(
            _deviceContext.LogicalDevice,
            _pipelineLayout,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );

        foreach (ShaderModule shaderModule in _shaderModules)
        {
            _vk.DestroyShaderModule(
                _deviceContext.LogicalDevice,
                shaderModule,
                ReadOnlySpan<AllocationCallbacks>.Empty
            );
        }

        GC.SuppressFinalize(this);
    }
}
