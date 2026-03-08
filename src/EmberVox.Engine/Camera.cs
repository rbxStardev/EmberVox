using EmberVox.Engine.Components;

namespace EmberVox.Engine;

public class Camera
{
    public Transform Transform { get; set; }
    public float FieldOfView { get; set; }

    public Camera()
    {
        Transform = new Transform();
        FieldOfView = 70.0f;
    }
}
