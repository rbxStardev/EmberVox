using EmberVox.Core.Logging;

namespace EmberVox.Sandbox;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            using DemoEngine engine = new DemoEngine();
        }
        catch (Exception e)
        {
            Logger.Error?.WriteLine(e.Message);
            Console.WriteLine(e);
            throw;
        }
    }
}
