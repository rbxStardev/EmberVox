using EmberVox.Core.Logging;

namespace EmberVox.Sandbox;

public static class Program
{
    public static void Main()
    {
        try
        {
            using var engine = new DemoEngine();
        }
        catch (Exception e)
        {
            Logger.Error?.WriteLine(e.Message);
            Console.WriteLine(e);
            throw;
        }
    }
}
