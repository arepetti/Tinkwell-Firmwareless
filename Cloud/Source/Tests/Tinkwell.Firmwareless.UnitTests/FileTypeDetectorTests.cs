
using FluentAssertions;
using System.IO;
using Tinkkwell.Firmwareless;
using Xunit;

namespace Tinkwell.Firmwareless.UnitTests;

public class FileTypeDetectorTests
{
    [Fact]
    public void Detect_WasmFile_ReturnsWasmType()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        File.WriteAllBytes(tempFilePath, new byte[] { 0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00 }); // WASM magic number and version

        // Act
        var fileType = FileTypeDetector.Detect(tempFilePath);

        // Assert
        fileType.Should().Be(FileTypeDetector.FileType.Wasm);

        // Cleanup
        File.Delete(tempFilePath);
    }

    [Fact]
    public void Detect_ZipFile_ReturnsZipType()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        using (var zipStream = new FileStream(tempFilePath, FileMode.Create))
        {
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                archive.CreateEntry("test.txt");
            }
        }

        // Act
        var fileType = FileTypeDetector.Detect(tempFilePath);

        // Assert
        fileType.Should().Be(FileTypeDetector.FileType.Zip);

        // Cleanup
        File.Delete(tempFilePath);
    }

    [Fact]
    public void Detect_UnknownFile_ReturnsUnknownType()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        File.WriteAllBytes(tempFilePath, new byte[] { 0x01, 0x02, 0x03, 0x04 }); // Random bytes

        // Act
        var fileType = FileTypeDetector.Detect(tempFilePath);

        // Assert
        fileType.Should().Be(FileTypeDetector.FileType.Unknown);

        // Cleanup
        File.Delete(tempFilePath);
    }

    [Fact]
    public void Detect_EmptyFile_ReturnsUnknownType()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        File.WriteAllBytes(tempFilePath, new byte[] { }); // Empty file

        // Act
        var fileType = FileTypeDetector.Detect(tempFilePath);

        // Assert
        fileType.Should().Be(FileTypeDetector.FileType.Unknown);

        // Cleanup
        File.Delete(tempFilePath);
    }

    [Fact]
    public void Detect_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFilePath = "non_existent_file.bin";

        // Act
        Action act = () => FileTypeDetector.Detect(nonExistentFilePath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }
}
