using System.Numerics;
using EmberVox.Core.Logging;
using EmberVox.Core.Types;
using EmberVox.Engine;
using EmberVox.Engine.Components;
using EmberVox.Engine.Utils;
using EmberVox.Engine.Voxels;
using EmberVox.Engine.Voxels.Utils;
using EmberVox.Platform;
using EmberVox.Rendering;
using EmberVox.Rendering.GraphicsPipeline;
using EmberVox.Rendering.RenderingManagement;
using EmberVox.Rendering.Types;
using Silk.NET.Assimp;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Camera = EmberVox.Engine.Camera;
using File = System.IO.File;
using Mesh = EmberVox.Rendering.Types.Mesh;

namespace EmberVox.Sandbox;

public class DemoEngine : IDisposable
{
    private readonly WindowContext _windowContext;
    private readonly VulkanRenderer _renderer;
    private readonly Assimp _assimp;
    private readonly Camera _mainCamera;

    public DemoEngine()
    {
        _windowContext = new WindowContext();
        _renderer = new VulkanRenderer(_windowContext);
        _assimp = Assimp.GetApi();

        InputManager.Initialize(_windowContext.Handle.CreateInput());

        /*
        #region Viking Room

        //-> Loading Model File
        _assimp = Assimp.GetApi();
        Scene* scene = _assimp.ImportFile(
            Path.Combine(AppContext.BaseDirectory, "Models", "viking_room.obj"),
            (uint)(
                PostProcessSteps.Triangulate
                | PostProcessSteps.FlipWindingOrder
                | PostProcessSteps.JoinIdenticalVertices
            )
        );

        if (scene == null)
            throw new Exception($"Assimp failed: {_assimp.GetErrorStringS()}");

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

        _assimp.ReleaseImport(scene);

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
        */

        /*
        #region Viking Room

        //-> Loading Model File
        List<(Mesh, Material)> modelData = LoadModel(
            Path.Combine(AppContext.BaseDirectory, "Models", "viking_room.obj"),
            Path.Combine(AppContext.BaseDirectory, "Textures", "viking_room.png")
        );

        foreach (var (mesh, material) in modelData)
        {
            _renderer.RegisterMaterial(material);
            _renderer.RegisterMesh(mesh, material);
        }

        #endregion
        */

        /*
        #region Rubber Duck

        //-> Loading Model File
        List<(Mesh, Material)> duckData = LoadModel(
            Path.Combine(AppContext.BaseDirectory, "Models", "rubber_duck.glb")
        );

        foreach (var (mesh, material) in duckData)
        {
            _renderer.RegisterMaterial(material);
            _renderer.RegisterMesh(mesh, material);
        }

        #endregion
        */

        /*
        #region Voxel

        //-> Gathering Model Vertices & Indices
           List<Vertex> voxelVertices = [];
           List<uint> voxelIndices = [];
           int totalFaces = 0;

           foreach (VoxelFace voxelFace in Enum.GetValues<VoxelFace>())
           {
               voxelVertices.AddRange(
                   VoxelDataUtils.GetVoxelFaceVertices(voxelFace, new Vector3(0 + 2, 0, 0))
               );
               voxelIndices.AddRange(VoxelDataUtils.GetVoxelFaceIndices(totalFaces));
               totalFaces++;
           }

           //-> Gathering Model Texture Data
           FastNoiseLite noise = new FastNoiseLite();
           noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
           noise.SetFrequency(0.05f);

           //-> Creating Model Resources
           TextureData voxelTextureData = TextureUtils.GetDataFromNoise(512, 512, noise);

           Material voxelMaterial = new Material(
               _renderer,
               new Texture2D(_renderer.DeviceContext, _renderer.CommandContext, voxelTextureData)
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
        */

        /*
        #region Textured Voxel

        //-> Gathering Model Vertices & Indices
        List<Vertex> texturedVoxelVertices = [];
        List<uint> texturedVoxelIndices = [];
        int texturedTotalFaces = 0;

        foreach (VoxelFace voxelFace in Enum.GetValues<VoxelFace>())
        {
            texturedVoxelVertices.AddRange(
                VoxelDataUtils.GetVoxelFaceVertices(
                    voxelFace,
                    new Vector3(0, 0, 2),
                    VoxelType.Grass,
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
                TextureUtils.GenDataFromImage(
                    Path.Combine(AppContext.BaseDirectory, "Textures", "atlas.png")
                )
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
        */

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

        byte[] vertCode = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Shaders", "base.vert.spv")
        );
        byte[] fragCode = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Shaders", "base.frag.spv")
        );

        Logger.Warning?.WriteLine("Initializing test graphics pipeline");
        var shaderMaterial = ShaderMaterialBuilder
            .Empty.ProvideDependencies(_renderer.DeviceContext, _renderer.SwapChainContext)
            .WithVertexShaderCode(vertCode)
            .WithFragmentShaderCode(fragCode)
            .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
            .WithTargetInfo(
                new TargetInfo
                {
                    ColorTargetDescriptions =
                    [
                        new ColorTargetDescription
                        {
                            BlendState = new BlendState
                            {
                                ColorWriteMask =
                                    ColorComponentFlags.RBit
                                    | ColorComponentFlags.GBit
                                    | ColorComponentFlags.BBit
                                    | ColorComponentFlags.ABit,
                                EnableBlend = true,
                                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                                ColorBlendOp = BlendOp.Add,
                                SrcAlphaBlendFactor = BlendFactor.One,
                                DstAlphaBlendFactor = BlendFactor.Zero,
                                AlphaBlendOp = BlendOp.Add,
                            },
                            Format = _renderer.SwapChainContext.SwapChainImageFormat,
                        },
                    ],
                    DepthAttachmentFormat = _renderer.DepthContext.DepthImageFormat,
                }
            )
            .WithInputRate(VertexInputRate.Vertex)
            .WithRasterizerState(
                new RasterizerState
                {
                    PolygonMode = PolygonMode.Fill,
                    FrontFace = FrontFace.Clockwise,
                    CullMode = CullModeFlags.BackBit,
                    LineWidth = 1.0f,
                    DepthClampEnable = false,
                    DepthBiasClamp = 0.0f,
                    DepthBiasEnable = false,
                    DepthBiasConstantFactor = 0.0f,
                    DepthBiasSlopeFactor = 1.0f,
                    RasterizerDiscardEnable = false,
                }
            )
            .WithMultisampleState(
                new MultisampleState
                {
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                    SampleShadingEnable = false,
                }
            )
            .WithDepthStencilState(
                new DepthStencilState
                {
                    DepthTestEnable = false,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.Less,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false,
                }
            )
            .Build();

        //-> Gathering Model Vertices & Indices
        List<Vertex> voxelVertices = [];
        List<uint> voxelIndices = [];
        int totalFaces = 0;

        foreach (var voxelFace in Enum.GetValues<VoxelFace>())
        {
            voxelVertices.AddRange(
                VoxelDataUtils.GetVoxelFaceVertices(voxelFace, new Vector3(0 + 2, 0, 0))
            );
            voxelIndices.AddRange(VoxelDataUtils.GetVoxelFaceIndices(totalFaces));
            totalFaces++;
        }

        //-> Gathering Model Texture Data
        var noise = new FastNoiseLite();
        noise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        noise.SetFrequency(0.05f);

        //-> Creating Model Resources
        var voxelTextureData = TextureUtils.GetDataFromNoise(512, 512, noise);

        var texture2D = new Texture2D(
            _renderer.DeviceContext,
            _renderer.CommandContext,
            voxelTextureData
        );
        _renderer.ResourceManager.SubmitResource(texture2D);
        shaderMaterial.SetTexture(
            texture2D.Sampler,
            texture2D.ImageView,
            ImageLayout.ShaderReadOnlyOptimal
        );

        MeshComponent voxelMesh = new()
        {
            ShaderMaterial = shaderMaterial,
            Mesh = new Mesh(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                voxelVertices.ToArray(),
                voxelIndices.ToArray()
            ),
        };

        _renderer.RegisterShaderMaterial(shaderMaterial);
        _renderer.RegisterMesh(voxelMesh.Mesh, voxelMesh.ShaderMaterial);

        //-> Gathering Model Vertices & Indices
        List<Vertex> texturedVoxelVertices = [];
        List<uint> texturedVoxelIndices = [];
        int texturedTotalFaces = 0;

        foreach (var voxelFace in Enum.GetValues<VoxelFace>())
        {
            texturedVoxelVertices.AddRange(
                VoxelDataUtils.GetVoxelFaceVertices(
                    voxelFace,
                    new Vector3(0, 0, 2),
                    VoxelType.Grass,
                    true
                )
            );
            texturedVoxelIndices.AddRange(VoxelDataUtils.GetVoxelFaceIndices(texturedTotalFaces));
            texturedTotalFaces++;
        }

        //-> Creating Model Resources
        var texturedVoxelMaterial = ShaderMaterialBuilder
            .Empty.ProvideDependencies(_renderer.DeviceContext, _renderer.SwapChainContext)
            .WithVertexShaderCode(vertCode)
            .WithFragmentShaderCode(fragCode)
            .WithPrimitiveTopology(PrimitiveTopology.TriangleList)
            .WithTargetInfo(
                new TargetInfo
                {
                    ColorTargetDescriptions =
                    [
                        new ColorTargetDescription
                        {
                            BlendState = new BlendState
                            {
                                ColorWriteMask =
                                    ColorComponentFlags.RBit
                                    | ColorComponentFlags.GBit
                                    | ColorComponentFlags.BBit
                                    | ColorComponentFlags.ABit,
                                EnableBlend = true,
                                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                                ColorBlendOp = BlendOp.Add,
                                SrcAlphaBlendFactor = BlendFactor.One,
                                DstAlphaBlendFactor = BlendFactor.Zero,
                                AlphaBlendOp = BlendOp.Add,
                            },
                            Format = _renderer.SwapChainContext.SwapChainImageFormat,
                        },
                    ],
                    DepthAttachmentFormat = _renderer.DepthContext.DepthImageFormat,
                }
            )
            .WithInputRate(VertexInputRate.Vertex)
            .WithRasterizerState(
                new RasterizerState
                {
                    PolygonMode = PolygonMode.Fill,
                    FrontFace = FrontFace.Clockwise,
                    CullMode = CullModeFlags.BackBit,
                    LineWidth = 1.0f,
                    DepthClampEnable = false,
                    DepthBiasClamp = 0.0f,
                    DepthBiasEnable = false,
                    DepthBiasConstantFactor = 0.0f,
                    DepthBiasSlopeFactor = 1.0f,
                    RasterizerDiscardEnable = false,
                }
            )
            .WithMultisampleState(
                new MultisampleState
                {
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                    SampleShadingEnable = false,
                }
            )
            .WithDepthStencilState(
                new DepthStencilState
                {
                    DepthTestEnable = false,
                    DepthWriteEnable = true,
                    DepthCompareOp = CompareOp.Less,
                    DepthBoundsTestEnable = false,
                    StencilTestEnable = false,
                }
            )
            .Build();

        var texture2D2 = new Texture2D(
            _renderer.DeviceContext,
            _renderer.CommandContext,
            TextureUtils.GenDataFromImage(
                Path.Combine(AppContext.BaseDirectory, "Textures", "atlas.png")
            )
        );
        _renderer.ResourceManager.SubmitResource(texture2D2);
        texturedVoxelMaterial.SetTexture(
            texture2D2.Sampler,
            texture2D2.ImageView,
            ImageLayout.ShaderReadOnlyOptimal
        );

        MeshComponent texturedVoxelMesh = new()
        {
            ShaderMaterial = texturedVoxelMaterial,
            Mesh = new Mesh(
                _renderer.DeviceContext,
                _renderer.CommandContext,
                texturedVoxelVertices.ToArray(),
                texturedVoxelIndices.ToArray()
            ),
        };

        _renderer.RegisterShaderMaterial(texturedVoxelMaterial);
        _renderer.RegisterMesh(texturedVoxelMesh.Mesh, texturedVoxelMesh.ShaderMaterial);

        _windowContext.Handle.Update += HandleOnUpdate;
        _windowContext.Handle.FramebufferResize += HandleOnFramebufferResize;
        _windowContext.Handle.Render += HandleOnRender;

        InputManager.KeyPressed += InputManagerOnKeyPressed;

        _windowContext.Handle.Run();

        _renderer.MainLoop();
    }

    /*
    public unsafe List<(Mesh, Material)> LoadModel(string modelPath, string? texturePath = null)
    {
        Scene* scene = _assimp.ImportFile(
            modelPath,
            (uint)(
                PostProcessSteps.JoinIdenticalVertices
                | PostProcessSteps.Triangulate
                | PostProcessSteps.FlipWindingOrder
                | PostProcessSteps.ImproveCacheLocality
                | PostProcessSteps.RemoveRedundantMaterials
                | PostProcessSteps.GenerateUVCoords
            )
        );

        if (scene == null)
            throw new Exception("Could not load model: " + modelPath);

        List<(Mesh, Material)> parts = [];

        for (int meshIndex = 0; meshIndex < scene->MNumMeshes; meshIndex++)
        {
            Silk.NET.Assimp.Mesh* mesh = scene->MMeshes[meshIndex];
            Mesh loadedMesh = LoadMeshFromScene(scene, meshIndex);

            Material loadedMaterial;
            try
            {
                loadedMaterial = LoadMaterialFromScene(scene, (int)mesh->MMaterialIndex);
            }
            catch
            {
                if (texturePath == null)
                    throw new Exception(
                        $"Mesh [{meshIndex}] has no embedded texture and no separate texture or fallback was provided"
                    );

                loadedMaterial = new Material(
                    _renderer,
                    new Texture2D(
                        _renderer.DeviceContext,
                        _renderer.CommandContext,
                        TextureUtils.GenDataFromImage(texturePath)
                    )
                );
            }

            parts.Add((loadedMesh, loadedMaterial));
        }

        _assimp.ReleaseImport(scene);

        return parts;
    }
    */

    private unsafe Mesh LoadMeshFromScene(Scene* scene, int meshIndex)
    {
        var mesh = scene->MMeshes[meshIndex];

        List<Vertex> vertices = [];
        List<uint> indices = [];

        // Populate Vertices
        for (int vertexIndex = 0; vertexIndex < mesh->MNumVertices; vertexIndex++)
        {
            var vertexPos = mesh->MVertices[vertexIndex];
            var vertexUv = mesh->MTextureCoords[0][vertexIndex];
            var vertexColor =
                mesh->MColors[0] != null ? mesh->MColors[0][vertexIndex] : Vector4.One;

            vertices.Add(
                new Vertex
                {
                    Position = vertexPos,
                    TexCoord = vertexUv.AsVector2(),
                    Color = vertexColor,
                }
            );
        }

        // Populate Indices
        for (int faceIndex = 0; faceIndex < mesh->MNumFaces; faceIndex++)
        {
            var face = mesh->MFaces[faceIndex];
            for (int indexIndex = 0; indexIndex < face.MNumIndices; indexIndex++)
            {
                indices.Add(face.MIndices[indexIndex]);
            }
        }

        return new Mesh(
            _renderer.DeviceContext,
            _renderer.CommandContext,
            vertices.ToArray(),
            indices.ToArray()
        );
    }

    /*
    private unsafe Material LoadMaterialFromScene(Scene* scene, int materialIndex)
    {
        Silk.NET.Assimp.Material* material = scene->MMaterials[materialIndex];

        AssimpString texturePath;
        _assimp.GetMaterialTexture(
            material,
            TextureType.BaseColor,
            0,
            &texturePath,
            null,
            null,
            null,
            null,
            null,
            null
        );

        Texture* modelTexture = _assimp.GetEmbeddedTexture(scene, texturePath.AsString);

        if (modelTexture == null)
            throw new Exception("Could not load model texture");

        byte[] pixelData;
        int width,
            height;

        // Checks if the texture is compressed (put simply, everything in a line)
        if (modelTexture->MHeight == 0)
        {
            ReadOnlySpan<byte> compressedBytes = new(
                modelTexture->PcData,
                (int)modelTexture->MWidth
            );
            var result = ImageResult.FromMemory(
                compressedBytes.ToArray(),
                ColorComponents.RedGreenBlueAlpha
            );

            width = result.Width;
            height = result.Height;

            pixelData = new byte[width * height * 4];
            result.Data.CopyTo(pixelData);
        }
        else
        {
            width = (int)modelTexture->MWidth;
            height = (int)modelTexture->MHeight;

            pixelData = new byte[width * height * 4];
            ReadOnlySpan<Texel> texels = new(modelTexture->PcData, width * height);

            for (int texelIndex = 0; texelIndex < texels.Length; texelIndex++)
            {
                pixelData[texelIndex * 4] = texels[texelIndex].R;
                pixelData[texelIndex * 4 + 1] = texels[texelIndex].G;
                pixelData[texelIndex * 4 + 2] = texels[texelIndex].B;
                pixelData[texelIndex * 4 + 3] = texels[texelIndex].A;
            }
        }

        TextureData textureData = new TextureData(width, height, pixelData);

        Texture2D materialTexture = new(
            _renderer.DeviceContext,
            _renderer.CommandContext,
            textureData
        );
        return new Material(_renderer, materialTexture);
    }
    */

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
        _renderer.WindowOnFramebufferResize();
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
