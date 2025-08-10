namespace Tinkwell.Firmwareless.PublicRepository.Authentication;

public sealed class ApiKeyOptions
{
    public string KeyPrefix { get; set; } = "ak_";
    public string HmacSecret { get; set; } = "";
    public int HmacBytes { get; set; } = 8;
}
