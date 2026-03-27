namespace EmberVox.Engine.ResourceManagement;

public sealed class ResourceManager : IDisposable
{
    private readonly Stack<WeakReference<IDisposable>> _resources = [];

    public void Dispose()
    {
        while (_resources.TryPop(out var weakResourceReference))
            if (weakResourceReference.TryGetTarget(out var resource))
                resource.Dispose();

        GC.SuppressFinalize(this);
    }

    public void SubmitResource(IDisposable resource)
    {
        WeakReference<IDisposable> resourceReference = new(resource);
        _resources.Push(resourceReference);
    }
}
