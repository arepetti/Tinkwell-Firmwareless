using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class PackageValidator(ILogger<PackageValidator> logger, IPublicRepository repository) : IPackageValidator
{
    public FirmwarelessHostInformation HostInfo { get; set; } = FirmwarelessHostInformation.Default;

    public async Task ValidateAsync(string path, CancellationToken cancellationToken)
    {
        if (_publicRepositoryInfo is null)
        {
            var publicKeyPem = await _repository.GetPublicKeyAsync(cancellationToken);
            _publicRepositoryInfo = new FirmwarelessRepositoryInformation(publicKeyPem);
        }

        using var archive = ZipFile.OpenRead(path);
        CheckForCorruptedFiles(path, archive);
        CheckCompatibility(archive);
    }

    private readonly ILogger<PackageValidator> _logger = logger;
    private readonly IPublicRepository _repository = repository;
    private FirmwarelessRepositoryInformation? _publicRepositoryInfo;

    private void CheckForCorruptedFiles(string path, ZipArchive archive)
    {
        foreach (var manifestEntry in ReadIntegrityManifest(path, archive))
        {
            // We do this "just to be sure", down in the pipeline we process only entries verified using the
            // compilation server signature (which is trusted!) and the package itself is created by the server. Even if
            // a malicious actor tampers with the archive at rest (in the Hub) or while in transit adding a malicious
            // payload then it won't ever be unzipped anywhere.
            if (!IsSafeEntryName(manifestEntry.ArchiveEntryName))
                throw new FirmwareValidationException($"Entry {manifestEntry.ArchiveEntryName} has an invalid name.");

            using var entryStream = archive.OpenEntry(manifestEntry.ArchiveEntryName);
            var computedHash = ComputeSha512(entryStream);

            if (manifestEntry.Hash.Equals(computedHash, StringComparison.OrdinalIgnoreCase) == false)
                throw new FirmwareValidationException($"Entry {manifestEntry.ArchiveEntryName} seems to be corrupted.");
        }
    }

    private void CheckCompatibility(ZipArchive archive)
    {
        var manifest = PackageManifestReader.Read(archive);

        if (manifest.HostArchitecture != HostInfo.HardwareArchitecture)
            throw new FirmwareCompatibilityException($"Incompatible host architecture. Required: {manifest.HostArchitecture}, current: {HostInfo.HardwareArchitecture}.");

        if (!IsCompatible(manifest.FirmwarelessVersion))
            throw new FirmwareCompatibilityException($"Incompatible host firmwareless implementation. Required: {manifest.FirmwarelessVersion}, supported: {string.Join(',', HostInfo.SupportedFirmwarelessVersions)}.");

        if (manifest.FirmwareType == FirmwareType.DeviceRuntime)
            throw new FirmwareCompatibilityException($"The package contains a device runtime, not a firmlet or service.");
    }

    private IEnumerable<(string ArchiveEntryName, string Hash)> ReadIntegrityManifest(string archivePath, ZipArchive archive)
    {
        var signature = archive.ReadAllBytes(Names.IntegrityManifestSignatureEntryName);
        var manifest = archive.ReadAllBytes(Names.IntegrityManifestEntryName);

        if (VerifyManifestSignature(manifest, signature) == false)
            throw new FirmwareValidationException($"Integrity manifest in {archivePath} seems to be corrupted.");

        var manifestLines = Encoders.UTF8.GetString(manifest)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in manifestLines)
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 3 && parts[1] == "SHA512")
                yield return (parts[0].Trim('"'), parts[2]);
            else
                throw new FirmwareValidationException($"Invalid integrity manifest in {archivePath}.");
        }
    }

    private static string ComputeSha512(Stream stream)
    {
        using var sha = SHA512.Create();
        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            sha.TransformBlock(buffer, 0, bytesRead, null, 0);
        sha.TransformFinalBlock([], 0, 0);

        return Convert.ToHexStringLower(sha.Hash!);
    }

    private bool VerifyManifestSignature(byte[] manifest, byte[] signature)
    {
        if (_publicRepositoryInfo?.PublicKeyPem is null)
            throw new FirmwareValidationException("Public repository information is not available.");

        try
        {
            var publicKey = RSA.Create();
            publicKey.ImportFromPem(_publicRepositoryInfo.PublicKeyPem);

            // Note that we sign the hash of the manifest, not the manifest itself
            var hash = SHA512.HashData(manifest);
            return publicKey.VerifyData(hash, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Cannot verify manifest signature: {Message}", e.Message);
            return false;
        }
    }

    private bool IsCompatible(string packageVersionRange)
        => HostInfo.SupportedFirmwarelessVersions.Any(x => IsCompatible(packageVersionRange, x));

    private static bool IsCompatible(string range, string version)
    {
        if (string.IsNullOrWhiteSpace(range) || string.IsNullOrWhiteSpace(version))
            return false;

        var targetVersion = Version.Parse(version);

        if (!range.StartsWith("^"))
            return Version.Parse(range) == targetVersion;

        var baseVersion = Version.Parse(range.Substring(1));

        Version upperBound;
        if (baseVersion.Major > 0)
            upperBound = new Version(baseVersion.Major + 1, 0, 0);
        else if (baseVersion.Minor > 0)
            upperBound = new Version(0, baseVersion.Minor + 1, 0);
        else
            upperBound = new Version(0, 0, baseVersion.Build + 1);

        return targetVersion >= baseVersion && targetVersion < upperBound;
    }

    private static bool IsSafeEntryName(string entryName)
        => !string.IsNullOrWhiteSpace(entryName) && !entryName.StartsWith('/') && !entryName.Contains("../");
}
