using BusinessAnalytics.Application.Common.Models;

namespace BusinessAnalytics.Application.DTOs;

public record SystemLogQuery : PagedQuery
{
    public string? Level { get; init; }
    public string? Entity { get; init; }
    public long? EntityId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}
