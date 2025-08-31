using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

static class PackageManifestReader
{
    public static PackageManifest Read(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return Read(archive);
    }

    public static PackageManifest Read(ZipArchive archive)
    {
        var content = archive.ReadAllText(Names.PackageManifestEntryName);
        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.Converters.Add(new JsonStringEnumConverter());
            var manifest = JsonSerializer.Deserialize<PackageManifest>(content, options);
            if (manifest == null)
                throw new InvalidFirmwarePackageException($"Package manifest '{Names.PackageManifestEntryName}' is invalid.");

            return manifest;
        }
        catch (JsonException e)
        {
            throw new InvalidFirmwarePackageException($"Package manifest '{Names.PackageManifestEntryName}' is invalid.", e);
        }
    }
}
