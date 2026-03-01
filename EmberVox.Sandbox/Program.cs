using EmberVox.Platform;
using EmberVox.Rendering;

namespace EmberVox;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            using WindowContext window = new();
            using VulkanRenderer vulkanRenderer = new(window.Handle);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
