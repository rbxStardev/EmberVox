namespace EmberVox;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            HelloTriangleApplication app = new();
            app.Run();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
