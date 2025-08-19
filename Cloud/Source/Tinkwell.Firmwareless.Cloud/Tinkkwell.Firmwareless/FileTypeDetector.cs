namespace Tinkwell.Firmwareless;

public static class FileTypeDetector
{
    public enum FileType
    {
        Unknown,
        Wasm,
        Zip
    }

    public static FileType Detect(string path)
    {
        byte[] buffer = new byte[4];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Length < 4)
                return FileType.Unknown;

            stream.ReadExactly(buffer, 0, 4);
        }

        // .wasm: 00 61 73 6D (\0asm)
        if (buffer[0] == 0x00 && buffer[1] == 0x61 && buffer[2] == 0x73 && buffer[3] == 0x6D)
            return FileType.Wasm;

        // .zip: 50 4B 03 04 (PK\x03\x04)
        if (buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04)
            return FileType.Zip;

        return FileType.Unknown;
    }
}