namespace Tinkwell.Firmwareless.Vfs;

public interface IVirtualFileSystemEntry : IDisposable
{
    int Read(Context context, Span<byte> buffer, ReadFlags flags);

    int Write(Context context, Span<byte> buffer, WriteFlags flags);
}
