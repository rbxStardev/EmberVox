using EmberVox.Core.Types;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering;

public static class Initializers
{
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
        ManagedPointer<VertexInputBindingDescription> bindingDescriptions,
        ManagedPointer<VertexInputAttributeDescription> attributeDescriptions
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

    public static PipelineMultisampleStateCreateInfo CreateMultisampleStateInfo()
    {
        return new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            SampleShadingEnable = Vk.False,
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

    #endregion
}
