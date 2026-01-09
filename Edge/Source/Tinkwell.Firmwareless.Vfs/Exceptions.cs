namespace Tinkwell.Firmwareless.Vfs;

public abstract class VfsException(string message) : TinkwellException(message) { }

public sealed class VfsOutOfResourcesException(string path)
    : VfsException($"Path {path} is ambiguous.")
{ }

public sealed class VfsNotFoundException(string resourcePath)
    : VfsException($"Resource {resourcePath} does not exist.")
{ }

public sealed class VfsAmbiguousPathException(string path)
    : VfsException($"Path {path} is ambiguous.")
{ }

public sealed class VfsAccessException : VfsException
{
    public VfsAccessException(Identity identity, string resourcePath, OpenMode mode)
        : base($"{identity.Id} has not {mode} access to {resourcePath}.") { }

    public VfsAccessException(Identity identity, string resourcePath)
        : base($"{identity.Id} has not access to {resourcePath}.") { }
}