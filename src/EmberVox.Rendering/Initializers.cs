using EmberVox.Core.Types;
using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.ShaderReflection;
using Silk.NET.SPIRV.Reflect;
using Silk.NET.Vulkan;
using Format = Silk.NET.Vulkan.Format;

namespace EmberVox.Rendering;

public static class Initializers
{
    public static VertexInputAttributeDescription CreateVertexInputDescription(
        uint binding,
        Format format,
        uint location,
        uint offset
    )
    {
        return new VertexInputAttributeDescription
        {
            Binding = binding,
            Format = format,
            Location = location,
            Offset = offset,
        };
    }

    public static VertexInputBindingDescription CreateVertexInputBindingDescription(
        uint binding,
        VertexInputRate vertexInputRate,
        uint stride
    )
    {
        return new VertexInputBindingDescription
        {
            Binding = binding,
            InputRate = vertexInputRate,
            Stride = stride,
        };
    }

    public static unsafe PipelineShaderStageCreateInfo CreatePipelineShaderStageCreateInfo(
        ShaderModule module,
        byte* entryPoint,
        ShaderStageFlags shaderStage
    )
    {
        return new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Module = module,
            PName = entryPoint,
            Stage = shaderStage,
        };
    }

    public static unsafe PipelineRenderingCreateInfo CreatePipelineRenderingInfo(
        ManagedPointer<Format> formats,
        Format depthAttachmentFormat
    )
    {
        return new PipelineRenderingCreateInfo
        {
            SType = StructureType.PipelineRenderingCreateInfo,
            ColorAttachmentCount = (uint)formats.Length,
            PColorAttachmentFormats = formats.Pointer,
            DepthAttachmentFormat = depthAttachmentFormat,
        };
    }

    public static ManagedPointer<DescriptorSetLayoutBinding> CreateDescriptorSetLayoutBindings(
        ReadOnlySpan<byte> vertexShaderCode,
        ReadOnlySpan<byte> fragmentShaderCode
    )
    {
        using Reflect reflect = Reflect.GetApi();
        using ShaderReflector vertReflector = new ShaderReflector(reflect, vertexShaderCode);
        using ShaderReflector fragReflector = new ShaderReflector(reflect, fragmentShaderCode);

        List<DescriptorSetLayoutBinding> descriptorSetLayoutBindingList = [];
        descriptorSetLayoutBindingList.AddRange(
            vertReflector.DescriptorBindingsByName.Values.Select(
                shaderBinding => new DescriptorSetLayoutBinding
                {
                    Binding = shaderBinding.BindingIndex,
                    DescriptorType = shaderBinding.BindingType,
                    DescriptorCount = 1,
                    StageFlags = vertReflector.StageFlags,
                }
            )
        );
        descriptorSetLayoutBindingList.AddRange(
            fragReflector.DescriptorBindingsByName.Values.Select(
                shaderBinding => new DescriptorSetLayoutBinding
                {
                    Binding = shaderBinding.BindingIndex,
                    DescriptorType = shaderBinding.BindingType,
                    DescriptorCount = 1,
                    StageFlags = fragReflector.StageFlags,
                }
            )
        );

        ManagedPointer<DescriptorSetLayoutBinding> result = new(
            descriptorSetLayoutBindingList.Count
        );
        descriptorSetLayoutBindingList.CopyTo(result.Span);

        return result;
    }

    #region Graphics Pipeline

    public static unsafe PipelineDynamicStateCreateInfo CreateDynamicStateInfo(
        ManagedPointer<DynamicState> dynamicStates
    )
    {
        return new PipelineDynamicStateCreateInfo
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = (uint)dynamicStates.Length,
            PDynamicStates = dynamicStates.Pointer,
        };
    }

    public static unsafe PipelineVertexInputStateCreateInfo CreateVertexInputStateInfo(
        ManagedPointer<VertexInputAttributeDescription> attributeDescriptions,
        ManagedPointer<VertexInputBindingDescription> bindingDescriptions
    )
    {
        return new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = (uint)bindingDescriptions.Length,
            PVertexBindingDescriptions = bindingDescriptions.Pointer,
            VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
            PVertexAttributeDescriptions = attributeDescriptions.Pointer,
        };
    }

    public static PipelineInputAssemblyStateCreateInfo CreateInputAssemblyStateInfo(
        PrimitiveTopology topology
    )
    {
        return new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = topology,
        };
    }

    public static PipelineViewportStateCreateInfo CreateViewportStateInfo()
    {
        return new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1,
        };
    }

    public static PipelineRasterizationStateCreateInfo CreateRasterizationStateInfo()
    {
        return new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = PolygonMode.Fill,
            FrontFace = FrontFace.Clockwise,
            CullMode = CullModeFlags.BackBit,
            LineWidth = 1.0f,
            DepthClampEnable = Vk.False,
            DepthBiasClamp = 0.0f,
            DepthBiasEnable = Vk.False,
            DepthBiasConstantFactor = 0.0f,
            DepthBiasSlopeFactor = 1.0f,
            RasterizerDiscardEnable = Vk.False,
        };
    }

    public static PipelineRasterizationStateCreateInfo CreateRasterizationStateInfo(
        RasterizerState rasterizerState
    )
    {
        return new PipelineRasterizationStateCreateInfo
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = rasterizerState.PolygonMode,
            FrontFace = rasterizerState.FrontFace,
            CullMode = rasterizerState.CullMode,
            LineWidth = rasterizerState.LineWidth,
            DepthClampEnable = rasterizerState.DepthClampEnable,
            DepthBiasClamp = rasterizerState.DepthBiasClamp,
            DepthBiasEnable = rasterizerState.DepthBiasEnable,
            DepthBiasConstantFactor = rasterizerState.DepthBiasConstantFactor,
            DepthBiasSlopeFactor = rasterizerState.DepthBiasSlopeFactor,
            RasterizerDiscardEnable = rasterizerState.RasterizerDiscardEnable,
        };
    }

    public static PipelineMultisampleStateCreateInfo CreateMultisampleStateInfo()
    {
        return new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            SampleShadingEnable = Vk.False,
        };
    }

    public static PipelineMultisampleStateCreateInfo CreateMultisampleStateInfo(
        MultisampleState multisampleState
    )
    {
        return new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = multisampleState.RasterizationSamples,
            SampleShadingEnable = multisampleState.SampleShadingEnable,
        };
    }

    public static unsafe PipelineColorBlendStateCreateInfo CreateColorBlendStateInfo(
        ManagedPointer<PipelineColorBlendAttachmentState> colorBlendAttachmentStates
    )
    {
        return new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = Vk.False,
            LogicOp = LogicOp.Copy,
            AttachmentCount = (uint)colorBlendAttachmentStates.Length,
            PAttachments = colorBlendAttachmentStates.Pointer,
        };
    }

    public static PipelineDepthStencilStateCreateInfo CreateDepthStencilStateInfo()
    {
        return new PipelineDepthStencilStateCreateInfo
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = Vk.True,
            DepthWriteEnable = Vk.True,
            DepthCompareOp = CompareOp.Less,
            DepthBoundsTestEnable = Vk.False,
            StencilTestEnable = Vk.False,
        };
    }

    public static PipelineDepthStencilStateCreateInfo CreateDepthStencilStateInfo(
        DepthStencilState depthStencilState
    )
    {
        return new PipelineDepthStencilStateCreateInfo
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = depthStencilState.DepthTestEnable,
            DepthWriteEnable = depthStencilState.DepthWriteEnable,
            DepthCompareOp = depthStencilState.DepthCompareOp,
            DepthBoundsTestEnable = depthStencilState.DepthBoundsTestEnable,
            StencilTestEnable = depthStencilState.StencilTestEnable,
        };
    }

    #endregion
}
