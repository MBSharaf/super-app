using FluentValidation;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>
/// One pitch's money in the range (bookings by match date, cancelled ones left out).
/// <c>PaidInApp</c> reached Super Malaab; <c>CashAtPitch</c> is what the venue takes itself (the rest of a deposit
/// booking, or a pay-at-the-pitch booking); <c>StillAtPitch</c> is the part of it not collected yet.
/// <c>OurIncome</c> is commission + service fee kept by Super Malaab (after refunds); <c>VenueShareInApp</c> is what
/// Super Malaab owes the venue from the money it collected (after refunds).
/// </summary>
public sealed record PitchSettlementRow(
    long PitchId,
    string PitchName,
    string VenueName,
    long PartnerId,
    string PartnerName,
    string PaymentMode,
    int Bookings,
    decimal Gross,
    decimal PaidInApp,
    decimal CashAtPitch,
    decimal StillAtPitch,
    decimal OurIncome,
    decimal VenueShareInApp);

/// <summary>
/// One partner's account today: <c>Held</c> is waiting for the match to finish, <c>Available</c> can be withdrawn,
/// <c>InPayout</c> is asked for and not sent yet, <c>PaidOut</c> was sent. <c>Owed</c> = Held + Available.
/// </summary>
public sealed record PartnerSettlementRow(
    long PartnerId,
    string PartnerName,
    int Pitches,
    string PaymentModes,
    decimal VenueShareInApp,
    decimal Held,
    decimal Available,
    decimal InPayout,
    decimal PaidOut,
    decimal Owed);

public sealed record SettlementTotals(int Bookings, decimal Gross, decimal PaidInApp, decimal CashAtPitch, decimal StillAtPitch, decimal OurIncome, decimal VenueShareInApp, decimal Owed);

public sealed record SettlementsDto(DateOnly From, DateOnly To, SettlementTotals Totals, IReadOnlyList<PitchSettlementRow> Pitches, IReadOnlyList<PartnerSettlementRow> Partners);

/// <summary>GET /admin/settlements — ?from&amp;to match dates (default: the last 30 days, at most a year); ?q partner or pitch name.</summary>
public sealed record SettlementsQuery(DateOnly? From = null, DateOnly? To = null, string? Q = null);

public interface ISettlementsReader
{
    Task<IReadOnlyList<PitchSettlementRow>> PitchesAsync(DateOnly from, DateOnly to, string? q, CancellationToken ct);

    Task<IReadOnlyList<PartnerSettlementRow>> PartnersAsync(IReadOnlyList<PitchSettlementRow> pitches, CancellationToken ct);
}

/// <summary>"Pitch accounts" in the admin console: how much each pitch made, who holds the money and what Super Malaab still owes it.</summary>
public interface ISettlementsService
{
    Task<SettlementsDto> GetAsync(SettlementsQuery query, CancellationToken ct);

    Task<ExcelFile> ExportAsync(SettlementsQuery query, CancellationToken ct);
}

public sealed class SettlementsService(ISettlementsReader reader, IExcelExporter excel, IAuditLogger audit, IClock clock, IValidator<SettlementsQuery> rules) : ISettlementsService
{
    public async Task<SettlementsDto> GetAsync(SettlementsQuery query, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(query, ct);
        var to = query.To ?? DateOnly.FromDateTime(clock.UtcNow);
        var from = query.From ?? to.AddDays(-29);
        if (from > to || to.DayNumber - from.DayNumber > 366)
        {
            throw new BadRequestException("Choose a range of up to one year.", "from");
        }
        var pitches = await reader.PitchesAsync(from, to, query.Q?.Trim(), ct);
        var partners = await reader.PartnersAsync(pitches, ct);
        var totals = new SettlementTotals(pitches.Sum(p => p.Bookings), pitches.Sum(p => p.Gross), pitches.Sum(p => p.PaidInApp), pitches.Sum(p => p.CashAtPitch),
            pitches.Sum(p => p.StillAtPitch), pitches.Sum(p => p.OurIncome), pitches.Sum(p => p.VenueShareInApp), partners.Sum(p => p.Owed));
        return new SettlementsDto(from, to, totals, pitches, partners);
    }

    public async Task<ExcelFile> ExportAsync(SettlementsQuery query, CancellationToken ct)
    {
        var data = await GetAsync(query, ct);
        await audit.LogAsync(AuditSystem.Admin, "settlements.exported", $"Pitch accounts {data.From:yyyy-MM-dd} – {data.To:yyyy-MM-dd}", ct: ct);
        var t = data.Totals;
        var dashboard = new ExcelDashboard("Pitch accounts", $"{data.From:yyyy-MM-dd} – {data.To:yyyy-MM-dd}",
        [
            new("Bookings", t.Bookings), new("Booked value", t.Gross, true), new("Paid in the app", t.PaidInApp, true),
            new("Cash at the pitch", t.CashAtPitch, true), new("Still to collect at the pitch", t.StillAtPitch, true),
            new("Super Malaab income", t.OurIncome, true), new("Venues' share of app money", t.VenueShareInApp, true), new("Owed to venues now", t.Owed, true),
        ]);
        var pitches = new ExcelSheet("Pitches",
            ["Pitch", "Venue", "Partner", "Payment", "Bookings", "Booked value", "Paid in app", "Cash at pitch", "Still at pitch", "Super Malaab income", "Venue share (app)"],
            [.. data.Pitches.Select(p => (IReadOnlyList<object?>)[p.PitchName, p.VenueName, p.PartnerName, ModeLabel(p.PaymentMode), p.Bookings, p.Gross, p.PaidInApp,
                p.CashAtPitch, p.StillAtPitch, p.OurIncome, p.VenueShareInApp])]);
        var partners = new ExcelSheet("Partners",
            ["Partner", "Pitches", "Payment", "Venue share (app)", "Held until the match", "Available", "In payout", "Paid out", "Owed now"],
            [.. data.Partners.Select(p => (IReadOnlyList<object?>)[p.PartnerName, p.Pitches, p.PaymentModes, p.VenueShareInApp, p.Held, p.Available, p.InPayout, p.PaidOut, p.Owed])]);
        return new ExcelFile($"supermalaab-pitch-accounts-{clock.UtcNow:yyyyMMdd}.xlsx", excel.Build(dashboard, pitches, partners));
    }

    public static string ModeLabel(string mode) => mode == nameof(PitchPaymentMode.FullOnline) ? "Full payment in the app" : "Deposit in the app, rest at the pitch";
}

public sealed class SettlementsQueryValidator : AbstractValidator<SettlementsQuery>
{
    public SettlementsQueryValidator() => RuleFor(x => x.Q).MaximumLength(100);
}
