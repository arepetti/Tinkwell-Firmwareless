namespace Tinkwell.Firmwareless.PublicRepository.Database;

public enum ProductStatus
{
    Development,
    Production,
    Retired,
}

public sealed class Product : EntityBase
{
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public ProductStatus Status { get; set; }

    public Guid VendorId { get; set; }
    public required Vendor Vendor { get; set; }

    public ICollection<Firmware> Firmwares { get; set; } = new List<Firmware>();
}
