namespace Tinkwell.Firmwareless.CompilationServer.Services;

static class Names
{
    public const string CompilerImageName = "wamrc-compiler:latest";

    public const string AssetsDirectoryName = "assets";

    public const string InputOptionsFileName = "firmware.json";

    public const string CompilerMetaArchitecturesFileName = "meta-architectures.yml";
    public const string CompilerTargetValidationFileName = "target-validation.yml";
    public const string CompilerTargetOptionsFileName = "target-options.yml";
    public const string CompilerStdoutFileName = "stdout.txt";
    public const string CompilerStderrFileName = "stderr.txt";

    public const string CompiledFirmwareManifestEntryName = "package.json";
    public const string CompiledFirmwareIntegrityManifestEntryName = "integrity/manifest.txt";
    public const string CompiledFirmwareIntegrityManifestSignatureEntryName = "integrity/manifest.sig";
    public const string CompiledFirmwareStdoutEntryName = "log/stdout.txt";
    public const string CompiledFirmwareStderrEntryName = "log/stderr.txt";
}
