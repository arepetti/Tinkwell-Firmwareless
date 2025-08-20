namespace Tinkwell.Firmwareless.PublicRepository.Services.Queries;

public sealed record FindRequest(int? PageIndex, int? PageLength, string? Filter, string? Sort)
{
    public const int DefaultPageLength = 20;
    public const int MaximumPageLength = 200;
}

