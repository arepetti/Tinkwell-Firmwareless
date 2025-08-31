namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed record FirmwarelessHostInformation(string Name, string HardwareArchitecture, string[] SupportedFirmwarelessVersions)
{
    public static readonly FirmwarelessHostInformation Default = new(
        Name: "Tinkwell Firmwareless Host",
        HardwareArchitecture: "linux",
        SupportedFirmwarelessVersions: ["1.0"]
    );
}
