using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Application.Features.Money;

/// <summary>One Cairo day of platform revenue (EGP). Costs (refunds paid by Super App, vouchers) are positive amounts.</summary>
public sealed record RevenueDayDto(DateOnly Date, decimal Bookings, decimal Fees, decimal Passes, decimal Refunds, decimal Promos, decimal Gmv);

/// <summary>A pitch owner's bookings in the period: how many, what customers paid and our commission.</summary>
public sealed record RevenueOwnerDto(string Name, int Bookings, decimal Gross, decimal Commission);

/// <summary>A Fantasy Pass paid in the period; plan = a league key or "both".</summary>
public sealed record RevenuePassDto(string Plan, decimal Amount, DateTime At);

public sealed record RevenueDto(DateOnly From, DateOnly To, IReadOnlyList<RevenueDayDto> Daily, IReadOnlyList<RevenueOwnerDto> Owners, IReadOnlyList<RevenuePassDto> Passes);

/// <summary>Reads the platform revenue wallet, paid payments and passes between two UTC instants.</summary>
public interface IRevenueReader
{
    /// <summary>Daily rows keyed by Cairo date (only days with movement), top owners by commission, passes.</summary>
    Task<(IReadOnlyList<RevenueDayDto> Days, IReadOnlyList<RevenueOwnerDto> Owners, IReadOnlyList<RevenuePassDto> Passes)> ReadAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}

/// <summary>
/// GET /admin/revenue (permission revenue.read) — what Super App earns, from its revenue wallet: booking and tournament
/// commission, service fees, Fantasy Pass; refunds it paid and Super Points vouchers as costs. Cash view: by the day the
/// money moved (Cairo time). GMV = paid payments, wallet top-ups excluded. Default: the last 30 days; at most a year.
/// </summary>
public sealed class RevenueService(IRevenueReader reader, IClock clock)
{
    public const int MaxDays = 366;

    public async Task<RevenueDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var end = to ?? DateOnly.FromDateTime(CairoTime.Now(clock));
        var start = from ?? end.AddDays(-29);
        if (start > end || end.DayNumber - start.DayNumber >= MaxDays)
        {
            throw new BadRequestException($"Choose a range of 1 to {MaxDays} days (from before to).", "range");
        }
        var (days, owners, passes) = await reader.ReadAsync(CairoTime.ToUtc(start.ToDateTime(TimeOnly.MinValue)), CairoTime.ToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue)), ct);
        var byDate = days.ToDictionary(d => d.Date);
        var daily = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(i => start.AddDays(i))
            .Select(d => byDate.GetValueOrDefault(d) ?? new RevenueDayDto(d, 0, 0, 0, 0, 0, 0)).ToList();
        return new RevenueDto(start, end, daily, owners, passes);
    }
}
