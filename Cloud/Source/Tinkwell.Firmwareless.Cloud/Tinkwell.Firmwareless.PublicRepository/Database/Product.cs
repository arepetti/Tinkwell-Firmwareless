namespace Tinkwell.Firmwareless.PublicRepository.Database;

public enum ProductStatus
{
    Development,
    Production,
    Retired,
}

public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public ProductStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Guid VendorId { get; set; }
    public required Vendor Vendor { get; set; }

    public ICollection<Firmware> Firmwares { get; set; } = new List<Firmware>();
}
