using System.Runtime.CompilerServices;

namespace EmberVox.Core.Extensions;

public static unsafe class UnsafeExtensions
{
    extension(Unsafe)
    {
        /// <summary>
        ///    Checks the offset of a field of a structure.
        /// </summary>
        /// <param name="basePtr">A pointer to the struct.</param>
        /// <param name="value">A pointer to a field within the struct.</param>
        /// <typeparam name="T">Any type.</typeparam>
        /// <typeparam name="TU">Any type.</typeparam>
        /// <returns>The offset of <see cref="value" /> if within <see cref="basePtr" />, otherwise -1.</returns>
        public static IntPtr OffsetOf<T, TU>(ref T basePtr, ref TU value)
            where T : unmanaged
            where TU : unmanaged
        {
            ref byte basePtrB = ref Unsafe.As<T, byte>(ref basePtr);
            ref byte valuePtrB = ref Unsafe.As<TU, byte>(ref value);
            nint offset = Unsafe.ByteOffset(ref basePtrB, ref valuePtrB);
            // Ensure the pointer is within range.
            return offset >= 0x0 && offset < sizeof(T) ? offset : -1;
        }
    }

    extension<T>(ref T data)
        where T : unmanaged
    {
        public ReadOnlySpan<byte> AsBytes()
        {
            return new ReadOnlySpan<T>(ref data).AsBytes();
        }
    }
}
