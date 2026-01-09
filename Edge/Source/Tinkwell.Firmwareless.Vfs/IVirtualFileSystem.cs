namespace Tinkwell.Firmwareless.Vfs;

public interface IVirtualFileSystem
{
    IVirtualFileSystemFile Open(Context context, string path, OpenMode mode, OpenFlags flags);
}