namespace Tinkwell.Firmwareless.PublicRepository.Database;

public sealed class Vendor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
