using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class CompilationConfigParser
{
    public static CompilationConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<CompilationConfig>(yaml);
    }

    public sealed class CompilationConfig
    {
        public int Schema { get; set; }
        public Dictionary<string, List<string>> Flags { get; set; } = new();
        public Dictionary<string, Dictionary<string, List<string>>> Features { get; set; } = new();
    }
}
