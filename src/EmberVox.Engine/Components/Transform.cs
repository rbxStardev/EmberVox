using System.Numerics;

namespace EmberVox.Engine.Components;

public record struct Transform
{
    public Vector3 Position;
    public Vector3 Scale;
    public Quaternion Rotation;

    public Transform()
    {
        Position = Vector3.Zero;
        Scale = Vector3.One;
        Rotation = Quaternion.Identity;
    }

    public Transform(Vector3 position)
    {
        Position = position;
        Scale = Vector3.One;
        Rotation = Quaternion.Identity;
    }

    public Transform(Quaternion rotation)
    {
        Position = Vector3.Zero;
        Scale = Vector3.One;
        Rotation = rotation;
    }

    public Transform(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Scale = Vector3.One;
        Rotation = rotation;
    }
}
