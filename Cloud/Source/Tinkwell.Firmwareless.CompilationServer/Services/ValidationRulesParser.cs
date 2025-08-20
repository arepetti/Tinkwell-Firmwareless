using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class ValidationRulesParser
{
    public static ValidationRulesConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new ValueOrValueSetConverter())
            .Build();

        return deserializer.Deserialize<ValidationRulesConfig>(yaml);
    }

    public sealed class ValidationRulesConfig
    {
        public int Schema { get; set; }
        public ValidationBlock Validate { get; set; } = new();
    }

    public sealed class ValidationBlock
    {
        public Dictionary<string, ValueSet> Components { get; set; } = new();
        public List<ValidationRule> Rules { get; set; } = new();
    }

    public sealed class ValueSet
    {
        public List<string>? Any { get; set; }
        public List<string>? AnyRegex { get; set; }
    }

    public sealed class ValueOrValueSet
    {
        public string? Value { get; set; }
        public ValueSet? Set { get; set; }

        [MemberNotNullWhen(true, nameof(Value))]
        public bool IsString => Value != null;

        [MemberNotNullWhen(true, nameof(Set))]
        public bool IsSet => Set != null;
    }

    public sealed class MatchProfile
    {
        public ValueOrValueSet? Os { get; set; }
        public ValueOrValueSet? Architecture { get; set; }
        public ValueOrValueSet? Vendor { get; set; }
        public ValueOrValueSet? Abi { get; set; }
    }

    public sealed class ValidationRule
    {
        public MatchProfile When { get; set; } = new();
        public MatchProfile Require { get; set; } = new();
    }

    private sealed class ValueOrValueSetConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(ValueOrValueSet);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            ArgumentNullException.ThrowIfNull(parser);
            ArgumentNullException.ThrowIfNull(rootDeserializer);

            var current = parser.Current;

            try
            {
                switch (current)
                {
                    case Scalar scalar:
                        // We read the scalar ourselves, so we must consume it.
                        parser.MoveNext();
                        return new ValueOrValueSet { Value = scalar.Value };

                    case MappingStart:
                        // Do NOT MoveNext here. Let the root deserializer consume the mapping.
                        var set = (ValueSet?)rootDeserializer(typeof(ValueSet));
                        return new ValueOrValueSet { Set = set };

                    case null:
                        // End of document or unexpected end
                        throw new InvalidOperationException("Unexpected end of YAML while reading ValueOrValueSet.");

                    default:
                        // Unexpected node type
                        throw new YamlException(
                            current?.Start ?? Mark.Empty,
                            current?.End ?? Mark.Empty,
                            $"Unexpected node type '{current?.GetType().Name}' while parsing ValueOrValueSet. " +
                            "Expected a scalar (string) or a mapping (object)."
                        );
                }
            }
            catch (Exception e) when (e is not YamlException)
            {
                throw new YamlException(current?.Start ?? Mark.Empty, current?.End ?? Mark.Empty, "Failed to deserialize ValueOrValueSet.", e);
            }
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            => throw new NotSupportedException();
    }
}
