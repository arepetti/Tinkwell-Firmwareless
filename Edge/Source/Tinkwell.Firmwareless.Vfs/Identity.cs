namespace Tinkwell.Firmwareless.Vfs;

public readonly record struct Identity(string Id)
{
    public static readonly Identity System = new();
}
