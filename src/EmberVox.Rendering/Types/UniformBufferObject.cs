using System.Numerics;

namespace EmberVox.Rendering.Types;

public struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Proj;
}
