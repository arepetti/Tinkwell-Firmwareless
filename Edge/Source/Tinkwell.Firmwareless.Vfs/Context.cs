namespace Tinkwell.Firmwareless.Vfs;

public readonly record struct Context(Identity Caller)
{
    public static readonly Context Null = new(Identity.System);

    public static Context FromIdentity(string id)
        => new(new Identity(id));
}
