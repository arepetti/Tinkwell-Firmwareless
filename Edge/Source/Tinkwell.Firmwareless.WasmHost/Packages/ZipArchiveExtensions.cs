using System.IO.Compression;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

static class ZipArchiveExtensions
{
    public static Stream OpenEntry(this ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
            throw new FileNotFoundException($"Entry '{entryName}' not found in ZIP archive.");

        return entry.Open();
    }

    public static byte[] ReadAllBytes(this ZipArchive archive, string entryName)
    {
        using var ms = new MemoryStream();
        using var entryStream = archive.OpenEntry(entryName);
        entryStream.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadAllText(this ZipArchive archive, string entryName)
    {
        using var entryStream = archive.OpenEntry(entryName);
        using var reader = new StreamReader(entryStream, Encoders.UTF8);
        return reader.ReadToEnd();
    }
}
