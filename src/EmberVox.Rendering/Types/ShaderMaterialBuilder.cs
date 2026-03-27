using EmberVox.Rendering.Contexts;
using EmberVox.Rendering.GraphicsPipeline;
using Silk.NET.Vulkan;

namespace EmberVox.Rendering.Types;

public class ShaderMaterialBuilder
{
    private DepthStencilState _depthStencilState;
    private DeviceContext _deviceContext = null!;
    private byte[] _fragmentShaderCode = null!;
    private VertexInputRate _inputRate;
    private MultisampleState _multisampleState;
    private PrimitiveTopology _primitiveTopology;
    private RasterizerState _rasterizerState;
    private SwapChainContext _swapChainContext = null!;
    private TargetInfo _targetInfo;
    private byte[] _vertexShaderCode = null!;

    private ShaderMaterialBuilder() { }

    public static ShaderMaterialBuilder Empty => new();

    public ShaderMaterialBuilder ProvideDependencies(
        DeviceContext deviceContext,
        SwapChainContext swapChainContext
    )
    {
        _deviceContext = deviceContext;
        _swapChainContext = swapChainContext;

        return this;
    }

    public ShaderMaterialBuilder WithVertexShaderCode(ReadOnlySpan<byte> vertexShaderCode)
    {
        _vertexShaderCode = vertexShaderCode.ToArray();

        return this;
    }

    public ShaderMaterialBuilder WithFragmentShaderCode(ReadOnlySpan<byte> fragmentShaderCode)
    {
        _fragmentShaderCode = fragmentShaderCode.ToArray();

        return this;
    }

    public ShaderMaterialBuilder WithPrimitiveTopology(PrimitiveTopology primitiveTopology)
    {
        _primitiveTopology = primitiveTopology;

        return this;
    }

    public ShaderMaterialBuilder WithTargetInfo(TargetInfo targetInfo)
    {
        _targetInfo = targetInfo;

        return this;
    }

    public ShaderMaterialBuilder WithInputRate(VertexInputRate inputRate)
    {
        _inputRate = inputRate;

        return this;
    }

    public ShaderMaterialBuilder WithRasterizerState(RasterizerState rasterizerState)
    {
        _rasterizerState = rasterizerState;

        return this;
    }

    public ShaderMaterialBuilder WithMultisampleState(MultisampleState multisampleState)
    {
        _multisampleState = multisampleState;

        return this;
    }

    public ShaderMaterialBuilder WithDepthStencilState(DepthStencilState depthStencilState)
    {
        _depthStencilState = depthStencilState;

        return this;
    }

    public ShaderMaterial Build()
    {
        return new ShaderMaterial(
            _deviceContext,
            _swapChainContext,
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
