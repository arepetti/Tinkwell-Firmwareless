namespace Tinkwell.Firmwareless.Vfs;

public sealed record FileDescriptor(Identity Owner, ResourceAccessPermissions Permissions, IVirtualFileSystemEntryReference Entry);
