namespace Tinkwell.Firmwareless.Vfs.Providers;

public sealed class ClockDeviceVfsProvider : IVirtualFileSystemEntryProvider
{
    public const string Name = "clock";

    public IVirtualFileSystemEntryReference? Find(string path)
    {
        if (path == VirtualFileSystem.GetDevicePath(Name))
            return new ClockStream();

        return null;
    }

    private sealed class ClockStream : IVirtualFileSystemEntryReference, IVirtualFileSystemEntry
    {
        public string Path { get; } = VirtualFileSystem.GetDevicePath(Name);
        public bool CanRead => true;
        public bool CanWrite => false;

        public IVirtualFileSystemEntry GetEntry() => this;

        public void Dispose()
            => _stream.Dispose();

        public int Read(Context context, Span<byte> buffer, ReadFlags flags)
            => _stream.Read(buffer, flags);

        public int Write(Context context, Span<byte> buffer, WriteFlags flags)
            => throw new NotSupportedException();

        private readonly VfsStream _stream = new(GetUtcTime, autoReset: true);

        private static byte[] GetUtcTime()
            => BitConverter.GetBytes(DateTime.UtcNow.Ticks);
    }
}
