using System.Runtime.InteropServices;

namespace EmberVox.Core;

public static class ReadonlySpanExtensions
{
    /*
    public static ReadOnlySpan<byte> AsBytes<T>(this ReadOnlySpan<T> readOnlySpan)
        where T : unmanaged
    {
        return MemoryMarshal.AsBytes(readOnlySpan);
    }
    */

    public static Span<byte> AsBytes<T>(this Span<T> source)
        where T : unmanaged => MemoryMarshal.Cast<T, byte>(source);
}
