using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Tinkwell.Firmwareless.CompilationServer.Services;

public sealed class CompiledFirmwareArchive : IDisposable
{
    public CompiledFirmwareArchive(IKeyVaultSignatureService signatureService)
    {
        _sha = SHA512.Create();
        Stream = new MemoryStream();
        _archive = new ZipArchive(Stream, ZipArchiveMode.Create, true);
        _signatureService = signatureService;
    }

    public Stream Stream { get; }

    public async Task AddFileAsync(string filePath, string entryName, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _frozen, true, true))
            throw new InvalidOperationException("The archive has been frozen and cannot be modified.");

        _archive.CreateEntryFromFile(filePath, entryName);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        var hash = await _sha.ComputeHashAsync(stream, cancellationToken);

        _integrityManifest.Add($"\"{entryName}\" SHA512 {Convert.ToHexStringLower(hash)}");
    }

    public async Task FreezeAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _frozen, true, true))
            throw new InvalidOperationException("The archive has been already frozen.");

        var integrityManifest = string.Join("\n", _integrityManifest);
        await AddBytesAsync(Encoding.UTF8.GetBytes(integrityManifest), "integrity/manifest.txt", cancellationToken);

        var signature = await _signatureService.SignDataAsync(integrityManifest, cancellationToken);
        await AddBytesAsync(signature, "integrity/manifest.sig", cancellationToken);

        _archive.Dispose();
        Stream.Position = 0;

        Interlocked.Exchange(ref _frozen, true);
    }

    public void Dispose()
    {
        _archive.Dispose();
        _sha.Dispose();
    }

    private readonly SHA512 _sha;
    private readonly ConcurrentBag<string> _integrityManifest = new();
    private readonly ZipArchive _archive;
    private readonly IKeyVaultSignatureService _signatureService;
    private bool _frozen;

    private async Task AddBytesAsync(byte[] buffer, string entryName, CancellationToken cancellationToken)
    {
        var entry = _archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(buffer, cancellationToken);
    }
}