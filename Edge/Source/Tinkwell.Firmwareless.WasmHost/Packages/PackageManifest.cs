namespace Tinkwell.Firmwareless.WasmHost.Packages;

sealed class PackageManifest
{
    public string FirmwarelessVersion { get; set; } = "^1.0";
    public string VendorId { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string Product { get; set; } = "";
    public string Model { get; set; } = "";
    public string FirmwareId { get; set; } = "";
    public FirmwareType FirmwareType { get; set; } = FirmwareType.Firmlet;
    public string FirmwareVersion { get; set; } = "";
    public string HostArchitecture { get; set; } = "";
    public string Permissions { get; set; } = "";
}
