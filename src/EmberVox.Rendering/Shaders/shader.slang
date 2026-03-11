struct VSInput
{
    float3 inPosition;
    float4 inColor;
    float2 inTextCoord;
}

struct UniformBuffer
{
    float4x4 model;
    float4x4 view;
    float4x4 proj;
}
[[vk::binding(0,0)]]
ConstantBuffer<UniformBuffer> ubo;

struct VSOutput
{
    float4 pos : SV_Position;
    float4 fragColor;
    float2 fragTexCoord;
}

[shader("vertex")]
VSOutput vertMain(VSInput input)
{
    VSOutput output;
    
    float4 tempPos = float4(input.inPosition, 1.0);
    float4 worldPos = mul(tempPos, ubo.model);
    float4x4 viewProj = mul(ubo.view, ubo.proj);
    float4 projPos = mul(worldPos, viewProj);
    
    //output.pos = mul(ubo.proj, mul(ubo.view, mul(ubo.model, float4(input.inPosition, 0.0, 1.0))));
    output.pos = projPos;
    output.fragColor = input.inColor;
    output.fragTexCoord = input.inTextCoord;
    
    return output;
}

Sampler2D texture;

[shader("fragment")]
float4 fragMain(VSOutput vertIn) : SV_TARGET
{
    return float4(vertIn.fragColor * texture.Sample(vertIn.fragTexCoord).rgba);
}