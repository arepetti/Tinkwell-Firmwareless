namespace Tinkwell.Firmwareless.Config;

public class ConfigFileReaderOptions
{
    public bool NoIncludes {  get; set; }

    public bool Unfiltered { get; set; }

    public object Parameters { get; set; } = new object();
}
