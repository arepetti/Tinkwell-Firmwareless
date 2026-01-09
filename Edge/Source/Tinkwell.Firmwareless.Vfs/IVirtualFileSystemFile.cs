namespace Tinkwell.Firmwareless.Vfs;

public interface IVirtualFileSystemFile : IDisposable
{
    void Close(Context context);

    int Read(Context context, Span<byte> buffer, ReadFlags flags);

    int Write(Context context, Span<byte> buffer, WriteFlags flags);
}
