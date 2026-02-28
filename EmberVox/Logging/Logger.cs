using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace EmberVox.Logging;

public static class Logger
{
    public static Log? Debug { get; private set; }
    public static Log? Error { get; private set; }
    public static Log? Info { get; private set; }
    public static Log? Metric { get; private set; }
    public static Log? Warning { get; private set; }

    static Logger()
    {
        Debug = new Log(LogLevel.Debug);
        Error = new Log(LogLevel.Error);
        Info = new Log(LogLevel.Info);
        Metric = new Log(LogLevel.Metric);
        Warning = new Log(LogLevel.Warning);
    }

    static ConsoleColor GetLogColor(LogLevel level) =>
        level switch
        {
            LogLevel.Debug => ConsoleColor.Magenta,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Metric => ConsoleColor.Blue,
            LogLevel.Warning => ConsoleColor.Yellow,
            _ => ConsoleColor.Gray,
        };

    public readonly struct Log
    {
        internal readonly LogLevel Level;

        internal Log(LogLevel level)
        {
            Level = level;
        }

        public void Write(ReadOnlySpan<char> message)
        {
#if !__MOBILE__
            Console.ForegroundColor = GetLogColor(Level);
#endif
            Console.Write(message);
#if !__MOBILE__
            Console.ResetColor();
#endif
        }

        [OverloadResolutionPriority(int.MaxValue)]
        public void WriteLine(ReadOnlySpan<char> message)
        {
#if !__MOBILE__
            Console.ForegroundColor = GetLogColor(Level);
#endif
            Console.WriteLine(message);
#if !__MOBILE__
            Console.ResetColor();
#endif
        }

        public void NewLine()
        {
            Console.WriteLine();
        }

        public void Write(ReadOnlySpan<byte> utf8)
        {
            var length = Encoding.UTF8.GetMaxCharCount(utf8.Length);
            char[]? array = null;
            if (length >= 2048)
            {
                array = ArrayPool<char>.Shared.Rent(length);
            }
            var dst = array ?? stackalloc char[length];
            try
            {
                Encoding.UTF8.TryGetChars(utf8, dst, out var written);
                Write(dst[..written]);
            }
            finally
            {
                if (array != null)
                    ArrayPool<char>.Shared.Return(array);
            }
        }

        public void Write<T>(T value)
            where T : ISpanFormattable
        {
            var bufferLength = 16;
            while (!TryWrite(value, bufferLength))
                bufferLength <<= 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryWrite<T>(T value, int bufferLength)
            where T : ISpanFormattable
        {
            char[]? array = null;
            if (bufferLength * 2 >= 2048)
            {
                array = ArrayPool<char>.Shared.Rent(bufferLength);
            }
            var dst = array ?? stackalloc char[bufferLength];
            try
            {
                if (!value.TryFormat(dst, out var written, [], null))
                    return false;
                Write(dst[..written]);
                return true;
            }
            finally
            {
                if (array != null)
                    ArrayPool<char>.Shared.Return(array);
            }
        }

        public void WriteLine<T>(T value)
            where T : ISpanFormattable
        {
            var bufferLength = 16;
            while (!TryWriteLine(value, bufferLength))
                bufferLength <<= 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public bool TryWriteLine<T>(T value, int bufferLength)
            where T : ISpanFormattable
        {
            char[]? array = null;
            if (bufferLength * 2 >= 2048)
            {
                array = ArrayPool<char>.Shared.Rent(bufferLength);
            }
            var dst = array ?? stackalloc char[bufferLength];
            try
            {
                if (!value.TryFormat(dst, out var written, [], null))
                    return false;
                WriteLine(dst[..written]);
                return true;
            }
            finally
            {
                if (array != null)
                    ArrayPool<char>.Shared.Return(array);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteLine(ReadOnlySpan<byte> utf8)
        {
            var length = Encoding.UTF8.GetMaxCharCount(utf8.Length);
            char[]? array = null;
            if (length >= 2048)
                array = ArrayPool<char>.Shared.Rent(length);
            var dst = array ?? stackalloc char[length];
            try
            {
                Encoding.UTF8.TryGetChars(utf8, dst, out var written);
                WriteLine(dst[..written]);
            }
            finally
            {
                if (array != null)
                    ArrayPool<char>.Shared.Return(array);
            }
        }
    }
}
