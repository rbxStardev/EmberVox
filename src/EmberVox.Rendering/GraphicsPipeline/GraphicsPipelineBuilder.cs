using EmberVox.Rendering.Contexts;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.GraphicsPipeline;

public class GraphicsPipelineBuilder
{
    private DeviceContext _deviceContext = null!;
    private byte[] _vertexShaderCode = null!;
    private byte[] _fragmentShaderCode = null!;
    private PrimitiveTopology _primitiveTopology;
    private TargetInfo _targetInfo;
    private VertexInputRate _inputRate;
    private RasterizerState _rasterizerState;
    private MultisampleState _multisampleState;
    private DepthStencilState _depthStencilState;

    private GraphicsPipelineBuilder() { }

    public static GraphicsPipelineBuilder Empty => new();

    public GraphicsPipelineBuilder ProvideDependencies(DeviceContext deviceContext)
    {
        _deviceContext = deviceContext;

        return this;
    }

    public GraphicsPipelineBuilder WithVertexShaderCode(ReadOnlySpan<byte> vertexShaderCode)
    {
        _vertexShaderCode = vertexShaderCode.ToArray();

        return this;
    }

    public GraphicsPipelineBuilder WithFragmentShaderCode(ReadOnlySpan<byte> fragmentShaderCode)
    {
        _fragmentShaderCode = fragmentShaderCode.ToArray();

        return this;
    }

    public GraphicsPipelineBuilder WithPrimitiveTopology(PrimitiveTopology primitiveTopology)
    {
        _primitiveTopology = primitiveTopology;

        return this;
    }

    public GraphicsPipelineBuilder WithTargetInfo(TargetInfo targetInfo)
    {
        _targetInfo = targetInfo;

        return this;
    }

    public GraphicsPipelineBuilder WithInputRate(VertexInputRate inputRate)
    {
        _inputRate = inputRate;

        return this;
    }

    public GraphicsPipelineBuilder WithRasterizerState(RasterizerState rasterizerState)
    {
        _rasterizerState = rasterizerState;

        return this;
    }

    public GraphicsPipelineBuilder WithMultisampleState(MultisampleState multisampleState)
    {
        _multisampleState = multisampleState;

        return this;
    }

    public GraphicsPipelineBuilder WithDepthStencilState(DepthStencilState depthStencilState)
    {
        _depthStencilState = depthStencilState;

        return this;
    }

    public NewGraphicsPipeline Build()
    {
        return new NewGraphicsPipeline(
            _deviceContext,
            _vertexShaderCode,
            _fragmentShaderCode,
            _primitiveTopology,
            _targetInfo,
            _inputRate,
            _rasterizerState,
            _multisampleState,
            _depthStencilState
        );
    }
}
