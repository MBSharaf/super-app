using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SuperApp.Application.Features.Money;

namespace SuperApp.Infrastructure.Payments;

/// <summary>Payment and refund receipts (QuestPDF, community licence), made when downloaded.</summary>
public sealed class MoneyDocuments : IMoneyDocuments
{
    private const string Green = "#0B6E4F";
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    static MoneyDocuments() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] Receipt(ReceiptDto receipt, string customerName) => Document.Create(doc => doc.Page(page =>
    {
        var refund = receipt.Kind == "Refund";
        var at = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(receipt.CreatedAt, DateTimeKind.Utc), Cairo);
        page.Size(PageSizes.A5);
        page.Margin(28);
        page.DefaultTextStyle(t => t.FontSize(10));
        page.Header().Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Super App").FontSize(16).Bold().FontColor(Green);
                c.Item().Text(receipt.Kind switch { "Refund" => "Refund receipt", "PayoutAdvice" => "Payout advice", _ => "Payment receipt" }).FontSize(11).SemiBold();
            });
            row.RelativeItem().AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text(receipt.Number).SemiBold();
                c.Item().AlignRight().Text(at.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture));
            });
        });
        page.Content().PaddingVertical(16).Column(col =>
        {
            col.Spacing(8);
            col.Item().Text($"{(receipt.Kind == "PayoutAdvice" ? "Paid to" : refund ? "Refunded to" : "Paid by")}: {customerName}");
            col.Item().Text($"For: {(receipt.RefType switch { "Booking" => "pitch booking", "Payout" => "withdrawal", _ => receipt.RefType })} {receipt.RefCode}");
            col.Item().Text($"Amount: {receipt.Amount.ToString("N2", CultureInfo.InvariantCulture)} EGP").FontSize(13).Bold();
            if (refund)
            {
                col.Item().Text("The money was added to your Super App wallet.");
            }
        });
        page.Footer().AlignCenter().Text("Super App · Prices include VAT.").FontSize(8).FontColor(Colors.Grey.Darken1);
    })).GeneratePdf();
}
