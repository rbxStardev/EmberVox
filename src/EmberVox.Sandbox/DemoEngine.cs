using System.Numerics;
using EmberVox.Engine;
using EmberVox.Engine.Components;
using EmberVox.Engine.VoxelUtils;
using EmberVox.Platform;
using EmberVox.Rendering;
using EmberVox.Rendering.RenderingManagement;
using EmberVox.Rendering.Types;
using Silk.NET.Assimp;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Camera = EmberVox.Engine.Camera;
using Material = EmberVox.Rendering.Types.Material;
using Mesh = EmberVox.Rendering.Types.Mesh;

namespace EmberVox.Sandbox;

public class DemoEngine : IDisposable
{
    private readonly WindowContext _windowContext;
    private readonly VulkanRenderer _renderer;
    private readonly Camera _mainCamera;

    public unsafe DemoEngine()
    {
        _windowContext = new WindowContext();
        _renderer = new VulkanRenderer(_windowContext);

        InputManager.Initialize(_windowContext.Handle.CreateInput());

        #region Viking Room

        //-> Loading Model File
        Assimp assimp = Assimp.GetApi();
        Scene* scene = assimp.ImportFile(
            Path.Combine(AppContext.BaseDirectory, "Models", "viking_room.obj"),
            (uint)(
                PostProcessSteps.Triangulate
                | PostProcessSteps.FlipWindingOrder
                | PostProcessSteps.JoinIdenticalVertices
            )
        );

        if (scene == null)
            throw new Exception($"Assimp failed: {assimp.GetErrorStringS()}");

        //-> Gathering Model Vertices & Indices
        List<Vertex> vikingRoomVertices = [];
        List<uint> vikingRoomIndices = [];

        for (int m = 0; m < scene->MNumMeshes; m++)
        {
            Silk.NET.Assimp.Mesh* sceneMesh = scene->MMeshes[m];

            for (int v = 0; v < sceneMesh->MNumVertices; v++)
            {
                Vector3 vertexPosition = sceneMesh->MVertices[v];
                Vector3 vertexUv = sceneMesh->MTextureCoords[0][v];
                vikingRoomVertices.Add(
                    new Vertex
                    {
                        Position = vertexPosition - new Vector3(1, 0, 0),
                        TexCoord = vertexUv.AsVector2(),
                        Color = Vector4.One,
                    }
                );
            }

            for (int f = 0; f < sceneMesh->MNumFaces; f++)
            {
                Face face = sceneMesh->MFaces[f];
                for (int i = 0; i < face.MNumIndices; i++)
                {
                    uint index = face.MIndices[i];
                    vikingRoomIndices.Add(index);
                }
            }
        }

        assimp.ReleaseImport(scene);

        //-> Creating Model Resources
        Material vikingRoomMaterial = new Material(
            _renderer,
            new Texture2D(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                Path.Combine(AppContext.BaseDirectory, "Textures", "viking_room.png")
            )
        );
        _renderer.RegisterMaterial(vikingRoomMaterial);

        MeshComponent vikingRoomMeshComponent = new()
        {
            Material = vikingRoomMaterial,
            Mesh = new Mesh(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                vikingRoomVertices.ToArray(),
                vikingRoomIndices.ToArray()
            ),
        };

        _renderer.RegisterMesh(vikingRoomMeshComponent.Mesh, vikingRoomMeshComponent.Material);

        #endregion

        #region Voxel

        //-> Gathering Model Vertices & Indices
        List<Vertex> voxelVertices = [];
        List<uint> voxelIndices = [];
        int totalFaces = 0;

        foreach (VoxelFace voxelFace in Enum.GetValues<VoxelFace>())
        {
            voxelVertices.AddRange(
                VoxelDataUtils.GetVoxelFaceVertices(
                    VoxelType.Grass,
                    voxelFace,
                    new Vector3(0 + 1, 0, 0)
                )
            );
            voxelIndices.AddRange(VoxelDataUtils.GetVoxelFaceIndices(totalFaces));
            totalFaces++;
        }

        //-> Creating Model Resources
        Material voxelMaterial = new Material(
            _renderer,
            new NoiseTexture(_renderer.DeviceContext, _renderer.CommandContext, 512, 512)
        );
        _renderer.RegisterMaterial(voxelMaterial);

        MeshComponent voxelMesh = new()
        {
            Material = voxelMaterial,
            Mesh = new Mesh(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                voxelVertices.ToArray(),
                voxelIndices.ToArray()
            ),
        };

        _renderer.RegisterMesh(voxelMesh.Mesh, voxelMesh.Material);

        #endregion

        #region Textured Voxel

        //-> Gathering Model Vertices & Indices
        List<Vertex> texturedVoxelVertices = [];
        List<uint> texturedVoxelIndices = [];
        int texturedTotalFaces = 0;

        foreach (VoxelFace voxelFace in Enum.GetValues<VoxelFace>())
        {
            texturedVoxelVertices.AddRange(
                VoxelDataUtils.GetVoxelFaceVertices(
                    VoxelType.Grass,
                    voxelFace,
                    new Vector3(0, 0, 2),
                    true
                )
            );
            texturedVoxelIndices.AddRange(VoxelDataUtils.GetVoxelFaceIndices(texturedTotalFaces));
            texturedTotalFaces++;
        }

        //-> Creating Model Resources
        Material texturedVoxelMaterial = new Material(
            _renderer,
            new Texture2D(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                Path.Combine(AppContext.BaseDirectory, "Textures", "atlas.png")
            )
        );
        _renderer.RegisterMaterial(texturedVoxelMaterial);

        MeshComponent texturedVoxelMesh = new()
        {
            Material = texturedVoxelMaterial,
            Mesh = new Mesh(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                texturedVoxelVertices.ToArray(),
                texturedVoxelIndices.ToArray()
            ),
        };

        _renderer.RegisterMesh(texturedVoxelMesh.Mesh, texturedVoxelMesh.Material);

        #endregion

        /*
        Mesh mesh2 = new()
        {
            Vertices = Vertices
                .Select(v => new VertexData(
                    v.Position + new Vector3(1, 0, 0),
                    v.TexCoord,
                    v.Color
                ))
                .ToArray(),
            Indices = Indices.ToArray(),
        };

        vulkanRenderer.RegisterMesh(mesh2);
        */

        _mainCamera = new Camera();

        _windowContext.Handle.Update += HandleOnUpdate;
        _windowContext.Handle.FramebufferResize += HandleOnFramebufferResize;
        _windowContext.Handle.Render += HandleOnRender;

        InputManager.KeyPressed += InputManagerOnKeyPressed;

        _windowContext.Handle.Run();

        _renderer.MainLoop();
    }

    private void InputManagerOnKeyPressed(object? sender, Key key)
    {
        if (key == Key.Escape)
        {
            _windowContext.Handle.Close();
        }
    }

    private void HandleOnRender(double deltaTime)
    {
        float aspectRatio =
            (float)_windowContext.Handle.FramebufferSize.X
            / _windowContext.Handle.FramebufferSize.Y;
        _renderer.WindowOnRender(
            deltaTime,
            _mainCamera.GetViewMatrix(),
            _mainCamera.GetProjectionMatrix(aspectRatio)
        );
    }

    private void HandleOnFramebufferResize(Vector2D<int> newSize)
    {
        _renderer.WindowOnFramebufferResize(newSize);
    }

    private void HandleOnUpdate(double deltaTime)
    {
        _mainCamera.Update(deltaTime);
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _windowContext.Dispose();
    }
}
