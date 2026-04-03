import pbrShared;

[[vk::binding(0, 0)]] public ConstantBuffer<UniformBufferObject> ubo;

[[shader("vertex")]]
VSOutput vertMain(VSInput input) {
    VSOutput output;
    
    float4 worldPos = mul(ubo.model, float4(input.Position, 1.0));
    output.Position = mul(ubo.proj, mul(ubo.view, worldPos));
    output.WorldPos = worldPos.xyz;
    
    output.Normal = normalize(mul((float3x3)ubo.model, input.Normal));
    output.UV = input.UV;
    output.Tangent = input.Tangent;
    
    return output;
}