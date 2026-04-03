import baseShared;

[[vk::binding(0, 0)]]
ConstantBuffer<UniformBuffer> ubo;

[shader("vertex")]
VSOutput vertMain(VSInput input)
{
    VSOutput output;
    
    float4 tempPos = float4(input.inPosition, 1.0);
    float4 worldPos = mul(tempPos, ubo.model);
    float4x4 viewProj = mul(ubo.view, ubo.proj);
    
    output.pos = mul(worldPos, viewProj);
    output.fragColor = input.inColor;
    output.fragTexCoord = input.inTextCoord;
    
    return output;
}