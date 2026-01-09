using System.Diagnostics;

namespace Tinkwell.Firmwareless.Vfs.Providers;

// autoReset and init() are used only for reading. This class does not support
// read/write (it's one or the other).
sealed class VfsStream(Func<byte[]>? init = null, bool autoReset = true) : IDisposable
{
    public int Read(Span<byte> buffer, ReadFlags flags)
    {
        Debug.Assert(init is not null);

        _stream ??= new MemoryStream(init());

        // After each COMPLETE read we re-initialize the buffer with new data (if requested).
        // We support a single exact read (when count is big enough to hold the entire buffer)
        // or multiple smaller reads (if bigger then we return the number of bytes effectively read).
        if (buffer.Length != _stream.Length)
        {
            int read = _stream.Read(buffer);
            if (_stream.Position == _stream.Length - 1 && autoReset)
                Reset();

            return read;
        }

        _stream.ReadExactly(buffer);

        if (autoReset)
            Reset();

        return buffer.Length;
    }

    public int Write(Span<byte> buffer, WriteFlags flags)
    {
        _stream ??= new MemoryStream();
        _stream.Write(buffer);
        return buffer.Length;
    }

    public byte[] GetBytes()
        => _stream?.GetBuffer() ?? [];

    public void Dispose()
        => (_stream as IDisposable)?.Dispose();

    private MemoryStream? _stream;

    private void Reset()
    {
        (_stream as IDisposable)?.Dispose();
        _stream = null;
    }
}