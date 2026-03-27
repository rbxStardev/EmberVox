using EmberVox.Rendering.Types;

namespace EmberVox.Engine.Components;

public record struct MeshComponent
{
    public Mesh Mesh;
    public ShaderMaterial ShaderMaterial;
}
