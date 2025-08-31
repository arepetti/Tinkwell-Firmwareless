namespace Tinkwell.Firmwareless.CompilationServer.Services;

public class CompilationManifest
{
    public string? FirmwarelessVersion { get; set; } = "1.0";
    public bool EnableMultiThread { get; set; }
    public bool EnableTailCall { get; set; }
    public bool EnableGarbageCollection { get; set; }
    public List<string> CompilationUnits { get; set; } = new();
    public List<string> Assets { get; set; } = new();
}
