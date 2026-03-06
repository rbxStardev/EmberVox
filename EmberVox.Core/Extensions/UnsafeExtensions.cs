using System.Runtime.CompilerServices;

namespace EmberVox.Core.Extensions;

public static unsafe class UnsafeExtensions
{
    extension(Unsafe)
    {
        /// <summary>
        /// Checks the offset of a field of a structure.
        /// </summary>
        /// <param name="basePtr">A pointer to the struct.</param>
        /// <param name="value">A pointer to a field within the struct.</param>
        /// <typeparam name="T">Any type.</typeparam>
        /// <typeparam name="U">Any type.</typeparam>
        /// <returns>The offset of <see cref="value"/> if within <see cref="basePtr"/>, otherwise -1.</returns>
        public static IntPtr OffsetOf<T, U>(ref T basePtr, ref U value)
            where T : unmanaged
            where U : unmanaged
        {
            ref byte basePtrB = ref Unsafe.As<T, byte>(ref basePtr);
            ref byte valuePtrB = ref Unsafe.As<U, byte>(ref value);
            nint offset = Unsafe.ByteOffset(ref basePtrB, ref valuePtrB);
            // Ensure the pointer is within range.
            return offset >= 0x0 && offset < sizeof(T) ? offset : -1;
        }
    }
}
