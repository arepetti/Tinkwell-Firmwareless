namespace Tinkwell.Firmwareless.PublicRepository.Database;

public sealed class Vendor : EntityBase
{
    public string Name { get; set; } = "";
    public string Notes { get; set; } = "";
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
