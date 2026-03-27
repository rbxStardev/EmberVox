using System.Runtime.InteropServices;

namespace EmberVox.Core.Extensions;

public static class SpanExtensions
{
    /*
    public static ReadOnlySpan<byte> AsBytes<T>(this ReadOnlySpan<T> readOnlySpan)
        where T : unmanaged
    {
        return MemoryMarshal.AsBytes(readOnlySpan);
    }
    */

    public static ReadOnlySpan<byte> AsBytes<T>(this ReadOnlySpan<T> source)
        where T : unmanaged
    {
        return MemoryMarshal.Cast<T, byte>(source);
    }
}
