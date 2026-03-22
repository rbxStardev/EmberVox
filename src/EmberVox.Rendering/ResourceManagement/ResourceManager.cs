namespace EmberVox.Rendering.ResourceManagement;

public sealed class ResourceManager : IDisposable
{
    private readonly Stack<WeakReference<IResource>> _resources = [];

    public void SubmitResource(IResource resource)
    {
        WeakReference<IResource> resourceReference = new(resource);
        _resources.Push(resourceReference);
    }

    public void Dispose()
    {
        while (_resources.TryPop(out var weakResourceReference))
        {
            if (weakResourceReference.TryGetTarget(out var resource))
            {
                resource.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }
}
