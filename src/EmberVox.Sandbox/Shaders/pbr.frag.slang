import pbrShared;

[[vk::push_constant]] public PushConstants material;
[[vk::binding(0, 0)]] public ConstantBuffer<UniformBufferObject> ubo;

[[vk::binding(1, 0)]] public Sampler2D baseColorTexture;
[[vk::binding(2, 0)]] public Sampler2D metallicRoughnessTexture;
[[vk::binding(3, 0)]] public Sampler2D normalTexture;
[[vk::binding(4, 0)]] public Sampler2D occlusionTexture;
[[vk::binding(5, 0)]] public Sampler2D emissiveTexture;

[[shader("fragment")]]
float4 fragMain(VSOutput input) : SV_TARGET {
    float4 baseColor = baseColorTexture.Sample(input.UV) * material.baseColorFactor;
    
    float2 metallicRoughness = metallicRoughnessTexture.Sample(input.UV).bg;
    float metallic = metallicRoughness.x * material.metallicFactor;
    float roughness = metallicRoughness.y * material.roughnessFactor;
    
    float ao = occlusionTexture.Sample(input.UV).r;
    
    float3 emissive = emissiveTexture.Sample(input.UV).rgb;

    float3 N = normalize(input.Normal);
    if (material.normalTextureSet >= 0) {
        float3 tangentNormal = normalTexture.Sample(input.UV).xyz * 2.0 - 1.0;
        float3 T = normalize(input.Tangent.xyz);
        float3 B = normalize(cross(N, T)) * input.Tangent.w;
        float3x3 TBN = float3x3(T, B, N);
        N = normalize(mul(tangentNormal, TBN));
    }

    float3 V = normalize(ubo.camPos.xyz - input.WorldPos);
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), baseColor.rgb, metallic);
    float3 Lo = float3(0.0, 0.0, 0.0);

    for (int i = 0; i < 4; i++) {
        float3 L = normalize(ubo.lightPositions[i].xyz - input.WorldPos);
        float3 H = normalize(V + L);
        float distance = length(ubo.lightPositions[i].xyz - input.WorldPos);
        float attenuation = 1.0 / (distance * distance);
        float3 radiance = ubo.lightColors[i].rgb * attenuation;

        float NdotL = max(dot(N, L), 0.0);
        float NdotV = max(dot(N, V), 0.0);
        float NdotH = max(dot(N, H), 0.0);
        float HdotV = max(dot(H, V), 0.0);

        float D = DistributionGGX(NdotH, roughness);
        float G = GeometrySmith(NdotV, NdotL, roughness);
        float3 F = FresnelSchlick(HdotV, F0);

        float3 kS = F;
        float3 kD = (1.0 - kS) * (1.0 - metallic);

        float3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.0001);
        Lo += (kD * baseColor.rgb / PI + specular) * radiance * NdotL;
    }

    float3 ambient = float3(0.03, 0.03, 0.03) * baseColor.rgb * ao;
    float3 color = ambient + Lo + emissive;
    color = color / (color + float3(1.0, 1.0, 1.0));
    color = pow(color, float3(1.0 / ubo.gamma, 1.0 / ubo.gamma, 1.0 / ubo.gamma));

    return float4(color, baseColor.a);
}