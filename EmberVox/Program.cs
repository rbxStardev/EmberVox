namespace EmberVox;

public static class Program
{
    public static void Main(string[] args)
    {
        try
        {
            HelloTriangleApplication app = new();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
