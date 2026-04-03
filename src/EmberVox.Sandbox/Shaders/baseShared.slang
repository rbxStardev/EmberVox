public struct VSInput
{
    public float4 inColor;
    public float3 inPosition;
    public float2 inTextCoord;
}

public struct UniformBuffer
{
    public float4x4 model;
    public float4x4 view;
    public float4x4 proj;
}

public struct VSOutput
{
    public float4 pos : SV_Position;
    public float4 fragColor;
    public float2 fragTexCoord;
}