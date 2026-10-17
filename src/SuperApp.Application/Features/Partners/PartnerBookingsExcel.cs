using SuperApp.Application.Abstractions.Documents;

namespace SuperApp.Application.Features.Partners;

/// <summary>The bookings export: a dashboard (counts and money) and the bookings table.</summary>
public static class PartnerBookingsExcel
{
    private static readonly string[] Headers =
    [
        "Code", "Pitch", "Date", "Start", "Hours", "Customer", "Phone", "Source", "Pitch amount", "Service fee", "Total",
        "Commission %", "Pay at venue", "Status", "Checked in", "Cancelled", "Refund", "Booked at",
    ];

    public static ExcelDashboard Dashboard(string business, PartnerBookingsQuery q, IReadOnlyList<PartnerBookingDto> rows)
    {
        var active = rows.Where(r => !r.Status.StartsWith("Cancelled", StringComparison.Ordinal)).ToList();
        var range = q.From is null && q.To is null ? "all dates" : $"{q.From?.ToString("yyyy-MM-dd") ?? "…"} to {q.To?.ToString("yyyy-MM-dd") ?? "…"}";
        return new ExcelDashboard($"{business} — bookings", $"{q.Status ?? "upcoming"} · {range}",
        [
            new("Bookings", rows.Count),
            new("Active (not cancelled)", active.Count),
            new("Cancelled", rows.Count - active.Count),
            new("No-shows", rows.Count(r => r.Status == "NoShow")),
            new("Walk-ins", rows.Count(r => r.Source == "WalkIn")),
            new("Hours booked", active.Sum(r => r.Duration)),
            new("Pitch revenue", active.Sum(r => r.PitchAmount), IsMoney: true),
            new("Super App commission", active.Sum(r => Math.Round(r.PitchAmount * r.CommissionRate / 100, 2)), IsMoney: true),
            new("Refunded", rows.Sum(r => r.RefundAmount), IsMoney: true),
        ]);
    }

    public static ExcelSheet Sheet(IReadOnlyList<PartnerBookingDto> rows) => new("Bookings", Headers, rows.Select(r => (IReadOnlyList<object?>)
    [
        r.Code, r.PitchName, r.Date, $"{r.StartHour:00}:00", r.Duration, r.CustomerName, r.CustomerPhone, r.Source, r.PitchAmount, r.ServiceFee, r.Total,
        r.CommissionRate, r.PayAtVenue, r.Status, r.CheckedInAt, r.CancelledAt, r.RefundAmount, r.CreatedAt,
    ]).ToList());
}
