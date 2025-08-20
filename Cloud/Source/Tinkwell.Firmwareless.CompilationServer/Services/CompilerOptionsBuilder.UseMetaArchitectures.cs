namespace Tinkwell.Firmwareless.CompilationServer.Services;

partial class CompilerOptionsBuilder
{
    public void UseMetaArchitectures(string path)
    {
        if (!string.IsNullOrWhiteSpace(_vendor))
            return;

        var config = MetaArchitecturesParser.Load(path);
        if (!config.Meta.TryGetValue(_architecture, out var definition))
            throw new ArgumentException($"Architecture {_architecture} is not a valid meta architecture name.");

        foreach (var entry in definition.Set)
        {
            switch (entry.Key)
            {
                case ArchitectureKey:
                    _architecture = entry.Value;
                    break;
                case VendorKey:
                    _vendor = entry.Value;
                    break;
                case OsKey:
                    _os = entry.Value;
                    break;
                case AbiKey:
                    _abi = entry.Value;
                    break;
                default:
                    _flags.Add(entry.Key, entry.Value);
                    break;
            }
        }

        _features.AddRange(definition.Features);
        Normalize(config.Normalize);
    }

    private const string ArchitectureKey = "architecture";
    private const string VendorKey = "vendor";
    private const string OsKey = "os";
    private const string AbiKey = "abi";

    private void Normalize(MetaArchitecturesParser.Normalization normalization)
    {
        string? value = null;

        if (normalization.Architecture.TryGetValue(_architecture, out value))
            _architecture = value;

        if (normalization.Vendor.TryGetValue(_vendor, out value))
            _vendor = value;

        if (normalization.Os.TryGetValue(_os, out value))
            _os = value;

        if (normalization.Abi.TryGetValue(_abi, out value))
            _abi = value;
    }
}