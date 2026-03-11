using System.Numerics;
using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Engine.Components;
using EmberVox.Engine.VoxelUtils;
using EmberVox.Platform;
using EmberVox.Rendering;
using Silk.NET.Assimp;
using Silk.NET.Vulkan;
using Camera = EmberVox.Engine.Camera;
using File = System.IO.File;

namespace EmberVox.Sandbox;

public static class Program
{
    /*
    private static readonly VertexData[] Vertices =
    [
        new() // Front Top Left
        {
            Position = new Vector3(-0.5f, 0.5f, 0.5f),
            Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            TexCoord = Vector2.Zero,
        },
        new() // Front Top Right
        {
            Position = new Vector3(0.5f, 0.5f, 0.5f), // Greater Z position
            Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
            TexCoord = Vector2.UnitX,
        },
        new() // Front Bottom Right
        {
            Position = new Vector3(0.5f, -0.5f, 0.5f),
            Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            TexCoord = Vector2.One,
        },
        new() // Front Bottom Left
        {
            Position = new Vector3(-0.5f, -0.5f, 0.5f),
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            TexCoord = Vector2.UnitY,
        },
        new() // Back Top Left
        {
            Position = new Vector3(-0.5f, 0.5f, -0.5f),
            Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            TexCoord = Vector2.UnitX,
        },
        new() // Back Top Right
        {
            Position = new Vector3(0.5f, 0.5f, -0.5f), // Greater Z position
            Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
            TexCoord = Vector2.Zero,
        },
        new() // Back Bottom Right
        {
            Position = new Vector3(0.5f, -0.5f, -0.5f),
            Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            TexCoord = Vector2.UnitY,
        },
        new() // Back Bottom Left
        {
            Position = new Vector3(-0.5f, -0.5f, -0.5f),
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
            TexCoord = Vector2.One,
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
    */

    private static readonly List<VertexData> Vertices = [];
    private static readonly List<uint> Indices = [];

    private static readonly Camera MainCamera = new();

    public static unsafe void Main(string[] args)
    {
        try
        {
            Transform mainCameraTransform = MainCamera.Transform;
            //mainCameraTransform.Position = Vector3.UnitZ * 16;
            var xQuat = Quaternion.CreateFromAxisAngle(
                Vector3.UnitZ,
                float.DegreesToRadians(90.0f)
            );
            var yQuat = Quaternion.CreateFromAxisAngle(
                Vector3.UnitX,
                float.DegreesToRadians(45.0f)
            );
            mainCameraTransform.Rotation = yQuat * mainCameraTransform.Rotation;
            mainCameraTransform.Rotation *= xQuat;

            MainCamera.Transform = mainCameraTransform;

            using WindowContext window = new();
            using VulkanRenderer vulkanRenderer = new(window.Handle, MainCamera);

            window.Handle.Update += HandleOnUpdate;

            Assimp assimp = Assimp.GetApi();
            Scene* scene =
                assimp.ImportFile(
                    Path.Combine(AppContext.BaseDirectory, "Models", "viking_room.obj"), (uint)(PostProcessSteps.Triangulate | PostProcessSteps.FlipWindingOrder | PostProcessSteps.JoinIdenticalVertices));

            if (scene == null)
                throw new Exception($"Assimp failed: {assimp.GetErrorStringS()}");
            
            for (int m = 0; m < scene->MNumMeshes; m++)
            {
                Mesh* mesh = scene->MMeshes[m];

                for (int v = 0; v < mesh->MNumVertices; v++)
                {
                    Vector3 vertexPosition = mesh->MVertices[v];
                    Vector3 vertexUv = mesh->MTextureCoords[0][v];
                    Vertices.Add(new VertexData(vertexPosition, vertexUv.AsVector2(), Vector4.One));
                }

                for (int f = 0; f < mesh->MNumFaces; f++)
                {
                    Face face = mesh->MFaces[f];
                    for (int i = 0; i < face.MNumIndices; i++)
                    {
                        uint index = face.MIndices[i];
                        Indices.Add(index);
                    }
                }
            }
            
            MeshComponent meshComponent = new()
            {
                Vertices = Vertices.ToArray(),
                Indices = Indices.ToArray(),
            };
            
            assimp.ReleaseImport(scene);

            vulkanRenderer.RegisterMesh(meshComponent);

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
        
    }
}
