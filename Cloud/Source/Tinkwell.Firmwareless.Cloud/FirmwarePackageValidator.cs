using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Tinkwell.Firmwareless;

public static class FirmwarePackageValidator
{
    public static void Validate(string zipPath, string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath, nameof(zipPath));
        ArgumentNullException.ThrowIfNull(publicKeyPem, nameof(publicKeyPem));

        if (FileTypeDetector.Detect(zipPath) != FileTypeDetector.FileType.Zip)
            throw new InvalidOperationException("Invalid file type.");

        using var archive = ZipFile.OpenRead(zipPath);
        var manifest = ReadIntegrityManifestLines(archive, publicKeyPem);
        var verifiedEntries = VerifyFilesIntegrity(archive, manifest);
        VerifyRequiredFilesHaveBeenChecked(archive, verifiedEntries);
    }

    private static string[] ReadIntegrityManifestLines(ZipArchive archive, string publicKeyPem)
    {
        var manifestBytes = ReadIntegrityManifestBytes(archive, publicKeyPem);
        var manifestText = Encoding.UTF8.GetString(manifestBytes);
        return manifestText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
    }

    private static byte[] ReadIntegrityManifestBytes(ZipArchive archive, string publicKeyPem)
    {
        // This is the integrity manifest with the hash calculated for the archive content,
        // we can use it as a simple measure to check if the content has been tampered but
        // alone it doesn't do much (it's like a CRC on steroyds). To  be sure the content
        // is what the vendor intended we need a public key and a signature to verify
        // that the manifest itself has not been tampered with.
        var manifestBytes = ReadZipEntry(archive, "integrity/manifest.txt");

        // Verify manifest's signature (we have a certificate).
        if (string.IsNullOrWhiteSpace(publicKeyPem))
            return manifestBytes!;

        var signatureBytes = ReadZipEntry(archive, "integrity/manifest.sig");

        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem.ToCharArray());

        bool isSignatureValid = rsa.VerifyData(
            manifestBytes,
            signatureBytes,
            HashAlgorithmName.SHA512,
            RSASignaturePadding.Pkcs1);

        if (!isSignatureValid)
            throw new InvalidOperationException("Manifest signature verification failed");

        return manifestBytes;
    }

    private static List<string> VerifyFilesIntegrity(ZipArchive archive, string[] manifest)
    {
        // Parse manifest lines, we expect this format:
        // "filename" SHA512 hexhash
        // Note that we first honor the list in manifest.txt but we have to check that the "required" files
        // are there (firmware manifest and WASM files). In practice manifest.txt can list everything (including assets)
        // and we're going to check their integrity but we enforce only the presence of the required files.
        var verifiedEntries = new List<string>();
        foreach (var line in manifest)
        {
            var match = Regex.Match(line, "^\"(.+)\"\\s+(\\S+)\\s+([0-9a-fA-F]{128})$");
            if (!match.Success)
                throw new InvalidOperationException($"Invalid integrity manifest line format: {line}");

            string filename = match.Groups[1].Value;
            string algorithm = match.Groups[2].Value;
            string expectedHash = match.Groups[3].Value.ToLowerInvariant();

            if (!string.Equals(algorithm, "SHA512", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Unsupported hashing algorithm: {algorithm}");

            var entry = archive.GetEntry(filename)
                ?? throw new InvalidOperationException($"File listed in integrity manifest not found in zip: {filename}");

            using var fileStream = entry.Open();
            string actualHash = ComputeSha512Hex(fileStream);

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Hash mismatch for {filename}");

            verifiedEntries.Add(filename);
        }

        return verifiedEntries;
    }

    private static void VerifyRequiredFilesHaveBeenChecked(ZipArchive archive, List<string> verifiedEntries)
    {
        foreach (var entry in archive.Entries)
        {
            if (Path.GetExtension(entry.Name).Equals(".wasm", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("firmware.json", StringComparison.OrdinalIgnoreCase))
            {
                if (!verifiedEntries.Contains(entry.FullName))
                    throw new InvalidOperationException($"Required file {entry.FullName} is missing from integrity manifest.");
            }
        }
    }

    private static byte[] ReadZipEntry(ZipArchive archive, string entryName)
    {
        var manifestEntry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"{entryName} not found in ZIP");

        using var stream = manifestEntry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string ComputeSha512Hex(Stream stream)
    {
        using var sha = SHA512.Create();
        byte[] hash = sha.ComputeHash(stream);
        return Convert.ToHexStringLower(hash);
    }
}