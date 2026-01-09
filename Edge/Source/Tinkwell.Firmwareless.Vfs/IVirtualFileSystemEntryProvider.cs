namespace Tinkwell.Firmwareless.Vfs;

public interface IVirtualFileSystemEntryProvider
{
    IVirtualFileSystemEntryReference? Find(string path);
}
