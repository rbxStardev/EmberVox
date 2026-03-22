struct VSOutput
{
    float4 pos : SV_Position;
    float4 fragColor;
    float2 fragTexCoord;
}

Sampler2D texture;

[shader("fragment")]
float4 fragMain(VSOutput vertIn) : SV_TARGET
{
    return float4(vertIn.fragColor * texture.Sample(vertIn.fragTexCoord).rgba);
}