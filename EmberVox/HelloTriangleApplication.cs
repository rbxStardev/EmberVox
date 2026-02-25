using Silk.NET.Core;
using Silk.NET.Core.Native;

namespace EmberVox;

public unsafe class HelloTriangleApplication : IDisposable
{
    private const int WindowWidth = 800;
    private const int WindowHeight = 600;

    private IWindow _window;

    private Vk _vk = null!;
    private Instance _instance;

    public void Run()
    {
        InitWindow();
        InitVulkan();
        MainLoop();
        Dispose();
    }

    private void InitWindow()
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox: Vulkan",
            WindowBorder = WindowBorder.Fixed, // Basically makes it unresizable
        };

        _window = Window.Create(options);
        _window.Initialize();
    }

    private void InitVulkan()
    {
        _vk = Vk.GetApi();
        CreateInstance();
    }

    private void CreateInstance()
    {
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("EmberVox"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("No Engine"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13,
        };

        byte** extensions = _window.VkSurface!.GetRequiredExtensions(out uint extensionCount);

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
            EnabledExtensionCount = extensionCount,
            PpEnabledExtensionNames = extensions,
        };

        if (_vk.CreateInstance(&createInfo, null, out _instance) != Result.Success)
        {
            throw new Exception("Failed to create Vulkan instance");
        }

        SilkMarshal.Free((nint)appInfo.PApplicationName);
        SilkMarshal.Free((nint)appInfo.PEngineName);
    }

    private void MainLoop()
    {
        _window.Run();
    }

    public void Dispose()
    {
        _window.Dispose();

        GC.SuppressFinalize(this);
    }
}
