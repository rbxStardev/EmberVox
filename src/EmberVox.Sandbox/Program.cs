using System.Diagnostics;
using System.Numerics;
using EmberVox.Core.Logging;
using EmberVox.Engine;
using EmberVox.Engine.Components;
using EmberVox.Platform;
using EmberVox.Rendering;

namespace EmberVox.Sandbox;

public static class Program
{
    private static readonly Camera MainCamera = new();

    public static void Main(string[] args)
    {
        try
        {
            using WindowContext window = new();
            using VulkanRenderer vulkanRenderer = new(window.Handle, MainCamera);

            window.Handle.Update += HandleOnUpdate;

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
