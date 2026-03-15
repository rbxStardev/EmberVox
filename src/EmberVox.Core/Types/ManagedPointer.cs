using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;

namespace EmberVox.Core.Types;

// TODO - Move *ToIndexable* to interface
public sealed unsafe class ManagedPointer<T> : MemoryManager<T>, IEnumerable<T>
    where T : unmanaged
{
    public int Length { get; }
    public int Alignment { get; }
    public T* Pointer { get; }
    public Span<T> Span => Pointer != null ? new Span<T>(Pointer, Length) : [];
    public int LengthInBytes => Length * sizeof(T);

    // TODO - ToIndexable
    public ref T this[int index] => ref GetElementUnsafe(index);

    private bool _disposedValue;

    public ManagedPointer(int length, int alignment = 0)
    {
        Length = length;
        Alignment = alignment;
        if (length <= 0)
        {
            return;
        }

        if (alignment == 0)
        {
            Pointer = (T*)NativeMemory.AllocZeroed((nuint)length, (nuint)sizeof(T));
        }
        else
        {
            int byteCount = length * sizeof(T);
            Pointer = (T*)NativeMemory.AlignedAlloc((nuint)byteCount, (nuint)alignment);

            new Span<T>(Pointer, byteCount).Clear();
        }

        GC.AddMemoryPressure(LengthInBytes);
    }

    // TODO - ToIndexable
    ref T GetElementUnsafe(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Length);
        return ref Pointer[index];
    }

    // TODO - ToIndexable
    public ref readonly T TryGetReadOnlyRef(int index) => ref GetElementUnsafe(index);

    public bool TryGetSpanUnsafe(out Span<T> span)
    {
        span = Span;
        return span.Length > 0;
    }

    public override Span<T> GetSpan() => Span;

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        return new MemoryHandle(Pointer + elementIndex);
    }

    public override void Unpin() { }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Length; i++)
        {
            yield return GetElementUnsafe(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #region Disposal

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ManagedPointer()
    {
        Dispose(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Dispose managed values
            }

            if (Pointer != null)
            {
                if (Alignment != 0)
                {
                    NativeMemory.AlignedFree(Pointer);
                }
                else
                {
                    NativeMemory.Free(Pointer);
                }
                GC.RemoveMemoryPressure(LengthInBytes);
            }

            _disposedValue = true;
        }
    }

    #endregion
}
