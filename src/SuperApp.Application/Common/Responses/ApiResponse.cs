namespace SuperApp.Application.Common.Responses;

/// <summary>
/// The one response shape of every endpoint (API document §1.3). The frontend reads <see cref="Success"/>,
/// shows <see cref="Message"/> and maps <see cref="Errors"/> to form fields.
/// </summary>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    public PageMeta? Meta { get; init; }
    public string? TraceId { get; init; }
}

/// <summary>Builders for <see cref="ApiResponse{T}"/>.</summary>
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T? data, string? message = null, PageMeta? meta = null)
        => new() { Success = true, Data = data, Message = message, Meta = meta };

    public static ApiResponse<object> Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null, string? traceId = null)
        => new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}

/// <summary>Paging information returned with every list.</summary>
public sealed record PageMeta(int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
