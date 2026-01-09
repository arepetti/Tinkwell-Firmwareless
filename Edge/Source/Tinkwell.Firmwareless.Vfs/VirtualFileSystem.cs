namespace Tinkwell.Firmwareless.Vfs;

public sealed class VirtualFileSystem(IVirtualFileSystemEntryProvider[] providers) : IVirtualFileSystem
{
    public IVirtualFileSystemFile Open(Context context, string path, OpenMode mode, OpenFlags flags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        if (!Enum.IsDefined(mode))
            throw new ArgumentException($"Mode {mode} is not valid");

        if (!Enum.IsDefined(flags))
            throw new ArgumentException($"Flags {flags} are not valid");

        var entry = GetEntryFromPath(path);
        var permissions = GetAccessPermissions(context, entry, mode, flags);
        return new VfsFile(new FileDescriptor(context.Caller, permissions, entry));
    }

    internal static string GetDevicePath(string deviceName)
        => $"/dev/{deviceName}";

    sealed class VfsFile(FileDescriptor descriptor) : IVirtualFileSystemFile
    {
        public void Close(Context context)
        {
            try
            {
                if (_entry is not null)
                {
                    if (context.Caller != _descriptor.Owner && context.Caller != Identity.System)
                        throw new VfsAccessException(context.Caller, _descriptor.Entry.Path);

                    _entry.Dispose();
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        public int Read(Context context, Span<byte> buffer, ReadFlags flags)
        {
            if (!Enum.IsDefined(flags))
                throw new ArgumentException($"Flags {flags} are not valid");

            if (!_descriptor.Permissions.HasFlag(ResourceAccessPermissions.Read))
                throw new VfsAccessException(context.Caller, _descriptor.Entry.Path, OpenMode.Read);

            var entry = GetEntry(context);
            return entry.Read(context, buffer, flags);
        }

        public int Write(Context context, Span<byte> buffer, WriteFlags flags)
        {
            if (!Enum.IsDefined(flags))
                throw new ArgumentException($"Flags {flags} are not valid");

            if (!_descriptor.Permissions.HasFlag(ResourceAccessPermissions.Write))
                throw new VfsAccessException(context.Caller, _descriptor.Entry.Path, OpenMode.Write);

            var entry = GetEntry(context);
            return entry.Write(context, buffer, flags);
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            Close(Context.Null);
        }

        private readonly FileDescriptor _descriptor = descriptor;
        private IVirtualFileSystemEntry? _entry;
        private bool _disposed;

        private IVirtualFileSystemEntry GetEntry(Context context)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _entry ??= _descriptor.Entry.GetEntry();

            if (context.Caller != _descriptor.Owner)
                throw new VfsAccessException(context.Caller, _descriptor.Entry.Path);

            return _entry;
        }
    }

    private IVirtualFileSystemEntryReference GetEntryFromPath(string path)
    {
        var entry = providers.Select(x => x.Find(path)).Where(x => x is not null);
        int count = entry.Count();

        if (count == 0)
            throw new VfsNotFoundException(path);

        if (count == 1)
            return entry.First()!;

        throw new VfsAmbiguousPathException(path);
    }

    private static ResourceAccessPermissions GetAccessPermissions(Context context, IVirtualFileSystemEntryReference entry, OpenMode mode, OpenFlags flags)
    {
        if (mode == OpenMode.Read && !entry.CanRead)
            throw new VfsAccessException(context.Caller, entry.Path, mode);

        if (mode == OpenMode.Write && !entry.CanWrite)
            throw new VfsAccessException(context.Caller, entry.Path, mode);

        // No caller-based access permissions for now

        if (flags.HasFlag(OpenFlags.Probe))
            return ResourceAccessPermissions.None;

        return mode == OpenMode.Read ? ResourceAccessPermissions.Read : ResourceAccessPermissions.Write;
    }
}
