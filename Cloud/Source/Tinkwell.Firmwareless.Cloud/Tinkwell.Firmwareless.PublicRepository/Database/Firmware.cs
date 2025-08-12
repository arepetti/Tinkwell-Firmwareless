namespace Tinkwell.Firmwareless.PublicRepository.Database;

public enum FirmwareType
{
    Firmlet,
    DeviceRuntime,
}

public enum FirmwareStatus
{
    PreRelease,
    Release,
    Deprecated,
}

public sealed class Firmware
{
    public Guid Id { get; set; }
    public string Version { get; set; } = "";
    public FirmwareType Type { get; set; }
    public FirmwareStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Guid ProductId { get; set; }
    public required Product Product { get; set; }
}