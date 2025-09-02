using System.IO.Compression;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

public static class PackageUnpacker
{
    public static void ExtractToDirectory(string archivePath, string targetDirectory)
    {
        // We do not use the list of entries from package.json (there isn't such a list) and surely we do not
        // extract the entire archive into a local directory: we use the integrity manifest (which has been signed by
        // the compilation server) to extract only the entries we verified. Even if a malicious actor tampers with the
        // archive at rest, their payoad won't ever be unzipped.
        using var archive = ZipFile.OpenRead(archivePath);
        var entries = archive
            .ReadAllText(Names.IntegrityManifestEntryName)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Split(' ', StringSplitOptions.TrimEntries)[0].Trim('"'));

        foreach (var entry in entries)
            archive.GetEntry(entry)!.ExtractToFile(Path.Combine(targetDirectory, entry));
    }
}