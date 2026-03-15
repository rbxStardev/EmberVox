using System.Numerics;
using EmberVox.Engine.Components;
using Silk.NET.Input;

namespace EmberVox.Engine;

public class Camera
{
    public TransformComponent TransformComponent { get; set; }
    public float FieldOfView { get; set; } = 70.0f;

    public float MovementSpeed { get; set; } = 10.0f;
    public float MouseSensitivity { get; set; } = 1.0f;
    public float ZoomSensitivity { get; set; } = 1.0f;
    public float Zoom { get; set; } = 1.0f;

    private Vector3 Front => Vector3.Transform(-Vector3.UnitZ, TransformComponent.Rotation);
    private Vector3 Right => Vector3.Transform(Vector3.UnitX, TransformComponent.Rotation);
    private Vector3 Up => Vector3.Transform(Vector3.UnitY, TransformComponent.Rotation);

    private float _yaw = -90.0f;
    private float _pitch = 0.0f;
    private bool _firstMouseMove = true;
    private Vector2 _lastMousePosition = Vector2.Zero;

    public Camera()
    {
        TransformComponent = new TransformComponent();

        InputManager.MouseMoved += InputManagerOnMouseMoved;
        InputManager.MouseScrolled += InputManagerOnMouseScrolled;
    }

    public void Update(double deltaTime)
    {
        float flyDirection = InputManager.GetInputKeysAxis(Key.ControlLeft, Key.Space);
        Vector2 movementDirection = InputManager.GetInputKeysVector(Key.A, Key.D, Key.S, Key.W);

        var flatFront = Vector3.Normalize(Front with { Y = 0.0f });

        Vector3 velocity = Vector3.Zero;
        velocity += flatFront * movementDirection.Y;
        velocity += Right * movementDirection.X;
        velocity += Vector3.UnitY * flyDirection;

        if (velocity != Vector3.Zero)
        {
            velocity = Vector3.Normalize(velocity) * MovementSpeed;
        }

        TransformComponent = TransformComponent with
        {
            Position = TransformComponent.Position + velocity * (float)deltaTime,
        };
    }

    private void ProcessMouseMove(Vector2 offset, bool constrainPitch = true)
    {
        offset *= MouseSensitivity;

        _yaw += offset.X;
        _pitch += offset.Y;

        if (constrainPitch)
            _pitch = Math.Clamp(_pitch, -89.0f, 89.0f);

        var rotation = Quaternion.CreateFromYawPitchRoll(
            float.DegreesToRadians(_yaw),
            float.DegreesToRadians(_pitch),
            0
        );

        TransformComponent = TransformComponent with { Rotation = rotation };
    }

    private void ProcessMouseScrolled(float offset)
    {
        Zoom = Math.Clamp(Zoom + offset * ZoomSensitivity, 1.0f, FieldOfView - 5.0f);
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(
            TransformComponent.Position,
            TransformComponent.Position + Front,
            Up
        );
    }

    public Matrix4x4 GetProjectionMatrix(
        float aspectRatio,
        float nearPlane = 0.1f,
        float farPlane = 500.0f
    )
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            float.DegreesToRadians(FieldOfView - Zoom),
            aspectRatio,
            nearPlane,
            farPlane
        );
    }

    private void InputManagerOnMouseMoved(object? _, Vector2 position)
    {
        if (_firstMouseMove)
        {
            _lastMousePosition.X = position.X;
            _lastMousePosition.Y = position.Y;
            _firstMouseMove = false;
        }

        Vector2 offset = _lastMousePosition - position;
        _lastMousePosition = position;

        ProcessMouseMove(offset);
    }

    private void InputManagerOnMouseScrolled(object? _, ScrollWheel scrollWheel)
    {
        float offset = scrollWheel.Y;

        ProcessMouseScrolled(offset);
    }
}
