namespace NexConvo.Identity.Application.Common;

/// <summary>A single page of results plus the total count (for bounded list rendering).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
