using System.Collections.Immutable;
using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.ShaderReflection;
using EmberVox.Rendering.Utils;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Format = Silk.NET.Vulkan.Format;
using Result = Silk.NET.Vulkan.Result;

namespace EmberVox.Rendering.GraphicsPipeline;

public class NewGraphicsPipeline : IDisposable
{
    private readonly DeviceContext _deviceContext;

    public unsafe NewGraphicsPipeline(
        DeviceContext deviceContext,
        ShaderReflector vertexReflector,
        ShaderReflector fragmentReflector,
        ReadOnlySpan<DescriptorSetLayout> descriptorSetLayoutSpan,
        PrimitiveTopology primitiveTopology,
        TargetInfo targetInfo,
        VertexInputRate inputRate,
        RasterizerState rasterizerState,
        MultisampleState multisampleState,
        DepthStencilState depthStencilState
    )
    {
        _deviceContext = deviceContext;

        Logger.Info?.WriteLine("-----> Creating GraphicsPipeline... <-----");

        ShaderReflector[] reflectors = [vertexReflector, fragmentReflector];

        PipelineLayout = CreatePipelineLayout(deviceContext, descriptorSetLayoutSpan);

        var vertShaderInputs = vertexReflector.GetInputVariables().ToArray();
        Logger.Metric?.WriteLine($"-> Vertex inputs: {vertShaderInputs.Length}");

        Span<ShaderModule> modules = stackalloc ShaderModule[reflectors.Length];

        using var attachmentStates = SetupAttachmentStates(targetInfo.ColorTargetDescriptions);
        using var formats = GetFormats(targetInfo.ColorTargetDescriptions);

        using ManagedPointer<PipelineShaderStageCreateInfo> createInfos = new(reflectors.Length);
        using ManagedPointer<VertexInputAttributeDescription> attributeDescriptions = new(
            vertShaderInputs.Length
        );
        using ManagedPointer<VertexInputBindingDescription> bindingDescriptions = new(1);

        DynamicState[] dynamicStateArray = [DynamicState.Viewport, DynamicState.Scissor];
        using ManagedPointer<DynamicState> dynamicStates = new(dynamicStateArray.Length);
        dynamicStateArray.CopyTo(dynamicStates.Span);

        uint totalStride = 0;
        for (int i = 0; i < vertShaderInputs.Length; i++)
        {
            var current = vertShaderInputs[i];

            if (
                current.Format == (uint)Format.Undefined
                || current.Stride == int.MaxValue
                || current.Location == uint.MaxValue
            )
            {
                Logger.Warning?.WriteLine(
                    $"Skipping vertex input at index {i} — undefined format, invalid stride or location."
                );
                continue;
            }

            attributeDescriptions[i] = Initializers.CreateVertexInputDescription(
                0,
                current.Format,
                current.Location,
                totalStride
            );
            totalStride += current.Stride;
        }

        Logger.Metric?.WriteLine(
            $"-> Total vertex stride: {totalStride} bytes, {attributeDescriptions.Length} attribute(s)."
        );

        bindingDescriptions[0] = Initializers.CreateVertexInputBindingDescription(
            0,
            inputRate,
            totalStride
        );

        for (int i = 0; i < reflectors.Length; i++)
        {
            var reflector = reflectors[i];
            Logger.Info?.WriteLine($"Loading shader stage: {reflector.StageFlags}");
            Logger.Metric?.WriteLine($"-> Entry point: {reflector.EntryPoint}");
            modules[i] = ShaderUtils.LoadShaderModule(deviceContext, reflector.CompiledShaderCode);
            createInfos[i] = Initializers.CreatePipelineShaderStageCreateInfo(
                modules[i],
                reflector.EntryPoint,
                reflector.StageFlags
            );
        }

        var renderingInfo = Initializers.CreatePipelineRenderingInfo(
            formats,
            targetInfo.DepthAttachmentFormat
        );
        var vertexInputStateInfo = Initializers.CreateVertexInputStateInfo(
            attributeDescriptions,
            bindingDescriptions
        );
        var inputAssemblyStateInfo = Initializers.CreateInputAssemblyStateInfo(primitiveTopology);
        var viewportStateInfo = Initializers.CreateViewportStateInfo();
        var rasterizationStateInfo = Initializers.CreateRasterizationStateInfo(rasterizerState);
        var multisampleStateInfo = Initializers.CreateMultisampleStateInfo(multisampleState);
        var colorBlendStateInfo = Initializers.CreateColorBlendStateInfo(attachmentStates);
        var depthStencilStateInfo = Initializers.CreateDepthStencilStateInfo(depthStencilState);
        var dynamicStateInfo = Initializers.CreateDynamicStateInfo(dynamicStates);
        try
        {
            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &renderingInfo,
                StageCount = (uint)createInfos.Length,
                PStages = createInfos.Pointer,
                PVertexInputState = &vertexInputStateInfo,
                PInputAssemblyState = &inputAssemblyStateInfo,
                PViewportState = &viewportStateInfo,
                PRasterizationState = &rasterizationStateInfo,
                PMultisampleState = &multisampleStateInfo,
                PColorBlendState = &colorBlendStateInfo,
                PDepthStencilState = &depthStencilStateInfo,
                PDynamicState = &dynamicStateInfo,
                Layout = PipelineLayout,
            };

            Pipeline pipeline = default;
            if (
                deviceContext.Api.CreateGraphicsPipelines(
                    deviceContext.LogicalDevice,
                    new PipelineCache(),
                    1,
                    new ReadOnlySpan<GraphicsPipelineCreateInfo>(ref pipelineInfo),
                    ReadOnlySpan<AllocationCallbacks>.Empty,
                    new Span<Pipeline>(ref pipeline)
                ) != Result.Success
            )
            {
                Logger.Error?.WriteLine("Failed to create graphics pipeline.");
                throw new Exception("Failed to create graphics pipeline.");
            }

            Pipeline = pipeline;
        }
        finally
        {
            foreach (var info in createInfos)
                SilkMarshal.Free((nint)info.PName);

            foreach (var module in modules)
                _deviceContext.Api.DestroyShaderModule(
                    _deviceContext.LogicalDevice,
                    module,
                    ReadOnlySpan<AllocationCallbacks>.Empty
                );
        }

        Logger.Info?.WriteLine("-----> GraphicsPipeline creation: OK <-----");
    }

    public PipelineLayout PipelineLayout { get; }
    public Pipeline Pipeline { get; }

    public void Dispose()
    {
        _deviceContext.Api.DestroyPipelineLayout(
            _deviceContext.LogicalDevice,
            PipelineLayout,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
        _deviceContext.Api.DestroyPipeline(
            _deviceContext.LogicalDevice,
            Pipeline,
            ReadOnlySpan<AllocationCallbacks>.Empty
        );
    }

    private static ManagedPointer<PipelineColorBlendAttachmentState> SetupAttachmentStates(
        ImmutableArray<ColorTargetDescription> targets
    )
    {
        int ptr = 0;
        ManagedPointer<PipelineColorBlendAttachmentState> result = new(targets.Length);
        foreach (var blendState in targets.Select(desc => desc.BlendState))
            result[ptr++] = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = blendState.ColorWriteMask,
                BlendEnable = blendState.EnableBlend,
                ColorBlendOp = blendState.ColorBlendOp,
                SrcColorBlendFactor = blendState.SrcColorBlendFactor,
                DstColorBlendFactor = blendState.DstColorBlendFactor,
                AlphaBlendOp = blendState.AlphaBlendOp,
                SrcAlphaBlendFactor = blendState.SrcAlphaBlendFactor,
                DstAlphaBlendFactor = blendState.DstAlphaBlendFactor,
            };

        return result;
    }

    private static ManagedPointer<Format> GetFormats(ImmutableArray<ColorTargetDescription> targets)
    {
        ManagedPointer<Format> result = new(targets.Length);
        for (int i = 0; i < targets.Length; i++)
            result[i] = targets[i].Format;

        return result;
    }

    private static unsafe PipelineLayout CreatePipelineLayout(
        DeviceContext deviceContext,
        ReadOnlySpan<DescriptorSetLayout> descriptorSetLayoutSpan
    )
    {
        using ManagedPointer<DescriptorSetLayout> descriptorSetLayouts = new(
            descriptorSetLayoutSpan.Length
        );
        descriptorSetLayoutSpan.CopyTo(descriptorSetLayouts.Span);

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = (uint)descriptorSetLayouts.Length,
            PSetLayouts = descriptorSetLayouts.Pointer,
            PushConstantRangeCount = 0,
        };

        PipelineLayout pipelineLayout = default;
        if (
            deviceContext.Api.CreatePipelineLayout(
                deviceContext.LogicalDevice,
                new ReadOnlySpan<PipelineLayoutCreateInfo>(ref pipelineLayoutInfo),
                ReadOnlySpan<AllocationCallbacks>.Empty,
                new Span<PipelineLayout>(ref pipelineLayout)
            ) != Result.Success
        )
        {
            Logger.Error?.WriteLine("Failed to create pipeline layout.");
            throw new Exception("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }
}
