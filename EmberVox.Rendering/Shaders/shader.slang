struct VSInput
{
    float2 inPosition;
    float4 inColor;
}

struct VSOutput
{
    float4 pos : SV_Position;
    float4 color;
}

[shader("vertex")]
VSOutput vertMain(VSInput input)
{
    VSOutput output;
    output.pos = float4(input.inPosition, 0.0, 1.0);
    output.color = input.inColor;
    
    return output;
}

[shader("fragment")]
float4 fragMain(VSOutput vertIn) : SV_TARGET
{
    return vertIn.color;
}