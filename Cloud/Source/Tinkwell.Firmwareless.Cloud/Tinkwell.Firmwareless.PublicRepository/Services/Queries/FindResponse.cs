namespace Tinkwell.Firmwareless.PublicRepository.Services.Queries;

public sealed record FindResponse<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageLength)
{
    public bool HasMore
        => (PageIndex + 1) < TotalCount / PageLength;
}