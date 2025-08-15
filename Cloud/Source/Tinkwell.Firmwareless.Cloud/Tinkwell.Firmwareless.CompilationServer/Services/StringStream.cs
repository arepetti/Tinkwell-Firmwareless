using System.Text;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

sealed class StringStream : Stream
{
    public StringStream() : this(encoding: Encoding.UTF8) { }

    public StringStream(Encoding encoding)
    {
        _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        _inner = new MemoryStream();
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        _inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        _inner.Seek(offset, origin);

    public override void SetLength(long value) =>
        _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        _inner.Write(buffer, offset, count);

    public string ConvertToString()
    {
        long originalPos = _inner.CanSeek ? _inner.Position : 0;
        try
        {
            if (_inner.CanSeek)
                _inner.Position = 0;

            using (var reader = new StreamReader(
                _inner,
                _encoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
                return reader.ReadToEnd();
            }
        }
        finally
        {
            if (_inner.CanSeek)
                _inner.Position = originalPos;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }

    private readonly MemoryStream _inner;
    private readonly Encoding _encoding;
}