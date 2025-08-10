namespace Tinkwell.Firmwareless.PublicRepository.Database;

public sealed class ApiKey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "User";
    public string Hash { get; set; } = "";
    public string Salt { get; set; } = "";
    public string Scopes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? VendorId { get; set; }

    public Vendor? Vendor { get; set; }
}
