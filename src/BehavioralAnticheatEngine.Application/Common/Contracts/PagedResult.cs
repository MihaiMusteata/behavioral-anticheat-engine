namespace BehavioralAnticheatEngine.Application.Common.Contracts;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount);
