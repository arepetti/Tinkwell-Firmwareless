namespace Tinkwell.Firmwareless.PublicRepository.Database;

public enum FirmwareType
{
    Service,
    Firmlet,
    DeviceRuntime,
}

public enum FirmwareStatus
{
    PreRelease,
    Release,
    Deprecated,
}

public enum FirmwareVerification
{
    Unverified,
    Verified,
    Signed,
    SignedAndVerified,
}

public sealed class Firmware : EntityBase
{
    public string Version { get; set; } = "";
    public string Compatibility { get; set; } = "";
    public string Author { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string ReleaseNotesUrl { get; set; } = "";
    public FirmwareType Type { get; set; }
    public FirmwareStatus Status { get; set; }
    public FirmwareVerification Verification { get; set; } = FirmwareVerification.Unverified;

    public Guid ProductId { get; set; }
    public required Product Product { get; set; }
}