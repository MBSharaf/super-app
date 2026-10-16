namespace SuperApp.Application.Common.Responses;

/// <summary>A page of items plus the total count — services return this, controllers turn it into data + meta.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public PageMeta Meta => new(Page, PageSize, Total);
}

/// <summary>Paging input shared by every list endpoint (<c>?page=1&amp;pageSize=20</c>, max 100).</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > MaxPageSize ? 20 : PageSize;
    public int Skip => (SafePage - 1) * SafePageSize;
}
