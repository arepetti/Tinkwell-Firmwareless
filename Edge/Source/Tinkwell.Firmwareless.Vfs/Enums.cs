namespace Tinkwell.Firmwareless.Vfs;

[Flags]
public enum ResourceAccessPermissions
{
    None = 0,
    Read = 1,
    Write = 2,
}

public enum OpenMode
{
    Read = 0,
    Write = 1,
}

[Flags]
public enum OpenFlags
{
    None = 0,
    Probe = 1,
}

[Flags]
public enum ReadFlags
{
    None = 0
}

[Flags]
public enum WriteFlags
{
    None = 0
}