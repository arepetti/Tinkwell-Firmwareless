using System.Runtime.InteropServices;

namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed record FirmwarelessHostInformation(string Name, string HardwareArchitecture, string HardwareVersion, string[] SupportedFirmwarelessVersions)
{
    public static readonly FirmwarelessHostInformation Default;

    static FirmwarelessHostInformation()
    {
        Default = new FirmwarelessHostInformation(
            Name: "Tinkwell Firmwareless Host",
            HardwareArchitecture: GetHardwareArchitecture(),
            HardwareVersion: "1.0.0",
            SupportedFirmwarelessVersions: ["^1.0"]
        );
    }

    private static string GetHardwareArchitecture()
    {
        // The container is always Linux and we assume it's
        // running on this machine (then with the same CPU architecture!)
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x86_64-pc-linux-gnu",
            Architecture.X86 => "i686-pc-linux-gnu",
            Architecture.Arm => "armv7-unknown-linux-gnueabihf",
            Architecture.Arm64 => "aarch64-unknown-linux-gnu",
            _ => "wasm", // Interpreted (if supported)
        };
    }
}
