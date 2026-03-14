using System.Numerics;

namespace EmberVox.Engine.Components;

public record struct TransformComponent
{
    public Vector3 Position;
    public Vector3 Scale;
    public Quaternion Rotation;
}
