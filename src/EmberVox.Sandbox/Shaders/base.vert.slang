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
    
    output.pos = mul(worldPos, viewProj);
    output.fragColor = input.inColor;
    output.fragTexCoord = input.inTextCoord;
    
    return output;
}