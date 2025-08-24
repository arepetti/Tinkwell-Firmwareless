using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class CompiledFirmwareArchive : IDisposable
{
    public CompiledFirmwareArchive(ILogger<CompiledFirmwareArchive> logger, IKeyVaultSignatureService signatureService)
    {
        _sha = SHA512.Create();
        _stream = new MemoryStream();
        _archive = new ZipArchive(_stream, ZipArchiveMode.Create, true);
        _signatureService = signatureService;
        _logger = logger;
    }

    public async Task<Stream> PackageOutputAsync(CompilationJob job, Func<string, string> getOutputFileName, CancellationToken cancellationToken)
    {
        Debug.Assert(job.Manifest is not null);

        _logger.LogInformation("Packaging compilation output as zip archive");

        foreach (var unit in job.Manifest.CompilationUnits)
        {
            _logger.LogDebug("Adding file {FileName} to archive", unit);
            await AddFileAsync(Path.Combine(job.WorkingDirectoryPath, unit), $"src/{unit}", cancellationToken);

            string compiledUnitPath = Path.Combine(job.WorkingDirectoryPath, getOutputFileName(unit));
            string compiledUnitFileName = Path.GetFileName(compiledUnitPath);

            _logger.LogDebug("Adding file {FileName} to archive", compiledUnitFileName);
            await AddFileAsync(compiledUnitPath, compiledUnitFileName, cancellationToken);
        }

        foreach (var sourceRelativePath in job.Manifest.Assets)
        {
            _logger.LogDebug("Adding file {FileName} to archive", sourceRelativePath);
            var assetFilePath = Path.Combine(job.WorkingDirectoryPath, sourceRelativePath);
            var assetFileName = $"{Names.AssetsDirectoryName}/{sourceRelativePath}";
            await AddFileAsync(assetFilePath, assetFileName, cancellationToken);
        }

        var firmwareJsonPath = Path.Combine(job.WorkingDirectoryPath, Names.CompiledFirmwareManifestEntryName);
        if (File.Exists(firmwareJsonPath))
            await AddFileAsync(firmwareJsonPath, Names.CompiledFirmwareManifestEntryName, cancellationToken);

        var stdoutPath = Path.Combine(job.WorkingDirectoryPath, Names.CompilerStdoutFileName);
        if (File.Exists(stdoutPath))
            await AddFileAsync(stdoutPath, Names.CompiledFirmwareStdoutEntryName, cancellationToken);

        var stderrPath = Path.Combine(job.WorkingDirectoryPath, Names.CompilerStderrFileName);
        if (File.Exists(stderrPath))
            await AddFileAsync(stderrPath, Names.CompiledFirmwareStderrEntryName, cancellationToken);

        await FreezeAsync(cancellationToken);

        return _stream;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _sha.Dispose();
    }

    private readonly SHA512 _sha;
    private readonly ConcurrentBag<string> _integrityManifest = new();
    private readonly Stream _stream;
    private readonly ZipArchive _archive;
    private readonly IKeyVaultSignatureService _signatureService;
    private readonly ILogger<CompiledFirmwareArchive> _logger;
    private bool _frozen;

    private async Task AddFileAsync(string filePath, string entryName, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _frozen, true, true))
            throw new InvalidOperationException("The archive has been frozen and cannot be modified.");

        _archive.CreateEntryFromFile(filePath, entryName);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var hash = await _sha.ComputeHashAsync(stream, cancellationToken);

        _integrityManifest.Add($"\"{entryName}\" SHA512 {Convert.ToHexStringLower(hash)}");
    }

    private async Task AddBytesAsync(byte[] buffer, string entryName, CancellationToken cancellationToken)
    {
        var entry = _archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(buffer, cancellationToken);
    }

    private async Task FreezeAsync(CancellationToken cancellationToken)
    {
        var integrityManifest = string.Join("\n", _integrityManifest);
        await AddBytesAsync(Encoding.UTF8.GetBytes(integrityManifest), Names.CompiledFirmwareIntegrityManifestEntryName, cancellationToken);

        var signature = await _signatureService.SignDataAsync(integrityManifest, cancellationToken);
        await AddBytesAsync(signature, Names.CompiledFirmwareIntegrityManifestSignatureEntryName, cancellationToken);

        _archive.Dispose();
        _stream.Position = 0;

        Interlocked.Exchange(ref _frozen, true);
    }
}