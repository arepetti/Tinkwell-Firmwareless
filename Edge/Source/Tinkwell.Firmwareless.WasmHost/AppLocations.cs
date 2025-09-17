namespace Tinkwell.Firmwareless.WasmHost;

static class AppLocations
{
    public static string ConfigurationPath { get; }

    public static string FirmletsPackagesPath { get; }

    public static string FirmletsPath { get; }

    static AppLocations()
    {
        ConfigurationPath = MakeDirectory("configuration");
        FirmletsPackagesPath = MakeDirectory("firmlets", "packages");
        FirmletsPath = MakeDirectory("firmlets", "cache");
    }

    private static string MakeDirectory(params string[] paths)
    {
        var fullPath = Path.Combine(
            [Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tinkwell", "Firmwareless", ..paths]);

        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);

        return fullPath;
    }
}
