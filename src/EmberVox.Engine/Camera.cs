using EmberVox.Engine.Components;

namespace EmberVox.Engine;

public class Camera
{
    public TransformComponent TransformComponent { get; set; }
    public float FieldOfView { get; set; }

    public Camera()
    {
        TransformComponent = new TransformComponent();
        FieldOfView = 70.0f;
    }
}
