namespace Omnijoy.Core.DTOs;

/// <summary>
/// Generic paginated result wrapper. Eliminates per-entity pagination record boilerplate.
/// JSON property names are lowercase-camel by ASP.NET Core's default serialiser settings
/// (consistent with the types it replaces).
/// </summary>
public record PagedResult<T>(
    T[] Items,
    int Page,
    int PageSize,
    bool HasMore
);
