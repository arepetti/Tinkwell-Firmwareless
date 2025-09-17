namespace Tinkwell.Firmwareless.WasmHost.Runtime;

enum ProductType
{
    Service,
    Firmlet,
}

sealed class ProductEntry
{
    public string Id { get; set; } = "";
    public ProductType Type { get; set; } = ProductType.Firmlet;
    public string VendorId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string? FirmwareVersion { get; set; }
    public string? Package { get; set; }

    internal bool Disabled { get; set; }
}

sealed class SystemConfiguration
{
    public List<ProductEntry> Products { get; set; } = [];
}
