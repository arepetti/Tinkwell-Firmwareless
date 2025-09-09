namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static class WasmChecker
{
    public static bool IsWasm(string path)
    {
        // WebAssembly files start with: 0x00 0x61 0x73 0x6D (i.e., "\0asm")
        var buffer = ReadMagicNumbers(path);
        return buffer[0] == 0x00 && buffer[1] == 0x61 && buffer[2] == 0x73 && buffer[3] == 0x6D;
    }

    public static bool IsAot(string path)
    {
        // WAMRC .aot files typically start with: 0x00 0x61 0x6f 0x74 (i.e., "\0aot")
        var buffer = ReadMagicNumbers(path);
        return buffer[0] == 0x00 && buffer[1] == 0x61 && buffer[2] == 0x6f && buffer[3] == 0x74;
    }

    private static byte[] ReadMagicNumbers(string path)
    {
        byte[] buffer = new byte[4];

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        fs.ReadExactly(buffer, 0, buffer.Length);
        return buffer;
    }
}
