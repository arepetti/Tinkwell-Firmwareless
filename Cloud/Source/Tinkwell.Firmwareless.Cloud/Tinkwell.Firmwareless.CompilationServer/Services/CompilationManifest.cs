namespace Tinkwell.Firmwareless.CompilationServer.Services;

public class CompilationManifest
{
    public bool EnableMultiThread { get; set; }
    public bool EnableTailCall { get; set; }
    public bool EnableGarbageCollection { get; set; }
    public List<string> CompilationUnits { get; set; } = new();
}
