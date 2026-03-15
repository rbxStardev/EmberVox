using System.Numerics;

namespace EmberVox.Engine.Components;

public record struct TransformComponent
{
    public Vector3 Position;
    public Vector3 Scale;
    public Quaternion Rotation;

    public TransformComponent()
    {
        Position = Vector3.Zero;
        Scale = Vector3.One;
        Rotation = Quaternion.Identity;
    }
}
