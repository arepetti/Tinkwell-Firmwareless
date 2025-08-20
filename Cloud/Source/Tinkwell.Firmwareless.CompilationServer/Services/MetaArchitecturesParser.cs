using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class MetaArchitecturesParser
{
    public static MetaArchitecturesConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<MetaArchitecturesConfig>(yaml);
    }

    public sealed class MetaArchitecturesConfig
    {
        public int Schema { get; set; }
        public Normalization Normalize { get; set; } = new();
        public Dictionary<string, MetaDefinition> Meta { get; set; } = new();
    }

    public sealed class Normalization
    {
        public Dictionary<string, string> Architecture { get; set; } = new();
        public Dictionary<string, string> Vendor { get; set; } = new();
        public Dictionary<string, string> Os { get; set; } = new();
        public Dictionary<string, string> Abi { get; set; } = new();
    }

    public sealed class MetaDefinition
    {
        public Dictionary<string, string> Set { get; set; } = new();

        public List<string> Features { get; set; } = new();
    }
}
