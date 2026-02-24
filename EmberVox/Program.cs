using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace EmberVox;

public static class Program
{
    public static int WindowWidth = 1280;
    public static int WindowHeight = 720;

    private static IWindow _window = null!;

    public static void Main(string[] args)
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(WindowWidth, WindowHeight),
            Title = "EmberVox",
        };

        _window = Window.Create(options);

        // TODO -> Check if platform supports vulkan

        _window.Run();
        _window.Close();
    }
}
