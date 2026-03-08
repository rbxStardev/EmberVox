using System.Numerics;
using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Engine;
using EmberVox.Engine.Components;
using EmberVox.Platform;
using EmberVox.Rendering;

namespace EmberVox.Sandbox;

public static class Program
{
    private static readonly VertexData[] Vertices =
    [
        new() // Front Top Left
        {
            Position = new Vector3(-0.5f, 0.5f, 0.5f),
            Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
        },
        new() // Front Top Right
        {
            Position = new Vector3(0.5f, 0.5f, 0.5f), // Greater Z position
            Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
        },
        new() // Front Bottom Right
        {
            Position = new Vector3(0.5f, -0.5f, 0.5f),
            Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
        },
        new() // Front Bottom Left
        {
            Position = new Vector3(-0.5f, -0.5f, 0.5f),
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
        },
        new() // Back Top Left
        {
            Position = new Vector3(-0.5f, 0.5f, -0.5f),
            Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
        },
        new() // Back Top Right
        {
            Position = new Vector3(0.5f, 0.5f, -0.5f), // Greater Z position
            Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
        },
        new() // Back Bottom Right
        {
            Position = new Vector3(0.5f, -0.5f, -0.5f),
            Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
        },
        new() // Back Bottom Left
        {
            Position = new Vector3(-0.5f, -0.5f, -0.5f),
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
        },
    ];

    //private static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];
    private static readonly uint[] Indices =
    [
        // Top Face
        4,
        5,
        1,
        1,
        0,
        4,
        // Bottom Face
        3,
        2,
        6,
        6,
        7,
        3,
        // Front Face
        0,
        1,
        2,
        2,
        3,
        0,
        // Back Face
        5,
        4,
        7,
        7,
        6,
        5,
        // Right Face
        1,
        5,
        6,
        6,
        2,
        1,
        // Left Face
        4,
        0,
        3,
        3,
        7,
        4,
    ];

    private static readonly Camera MainCamera = new();

    public static void Main(string[] args)
    {
        try
        {
            using WindowContext window = new();
            using VulkanRenderer vulkanRenderer = new(window.Handle, MainCamera);

            window.Handle.Update += HandleOnUpdate;

            MeshComponent mesh = new() { Vertices = Vertices, Indices = Indices };

            vulkanRenderer.RegisterMesh(mesh);

            vulkanRenderer.MainLoop();
        }
        catch (Exception e)
        {
            Logger.Error?.WriteLine(e.Message);
            Console.WriteLine(e);
            throw;
        }
    }

    private static void HandleOnUpdate(double deltaTime)
    {
        Transform cameraTransform = MainCamera.Transform;
        var xQuat = Quaternion.CreateFromAxisAngle(
            Vector3.UnitY,
            float.DegreesToRadians((float)(36.0f * deltaTime))
        );
        var yQuat = Quaternion.CreateFromAxisAngle(
            Vector3.UnitX,
            float.DegreesToRadians((float)(36.0f * deltaTime))
        );
        cameraTransform.Rotation = yQuat * cameraTransform.Rotation;
        cameraTransform.Rotation *= xQuat;

        MainCamera.Transform = cameraTransform;
    }
}
