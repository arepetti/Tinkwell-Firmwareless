namespace Tinkwell.Firmwareless.Vfs;

public interface IVirtualFileSystemEntryReference
{
    string Path { get; }

    bool CanRead { get; }

    bool CanWrite { get; }

    IVirtualFileSystemEntry GetEntry();
}
