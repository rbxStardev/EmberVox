using EmberVox.Logging;
using EmberVox.Platform;
using EmberVox.Rendering;

namespace EmberVox.Sandbox;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            using WindowContext window = new();
            using VulkanRenderer vulkanRenderer = new(window.Handle);

            vulkanRenderer.MainLoop();
        }
        catch (Exception e)
        {
            Logger.Error?.WriteLine(e.Message);
            Console.WriteLine(e);
            throw;
        }
    }
}
