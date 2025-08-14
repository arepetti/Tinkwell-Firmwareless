namespace Tinkwell.Firmwareless.PublicRepository.Configuration;

public sealed class FileUploadOptions
{
    public long MaxFirmwareSizeBytes { get; set; } = 16 * 1024 * 1024; // Default to 16MB
    public string[] AllowedContentTypes { get; set; } = [];
}
