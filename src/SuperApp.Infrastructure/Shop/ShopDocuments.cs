using System.Globalization;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SuperApp.Application.Features.Shop.Orders;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Shop PDFs made with QuestPDF (community licence): the order receipt; factory tickets and courier labels are added by their steps.</summary>
public sealed partial class ShopDocuments : IShopDocuments
{
    private const string Green = "#0B6E4F";
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    static ShopDocuments() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] OrderReceipt(OrderDetailsDto order, string customerName) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A5);
        page.Margin(28);
        page.DefaultTextStyle(t => t.FontSize(9));
        page.Header().Element(h => Header(h, "Receipt", order.Code, order.PlacedAt));
        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Spacing(10);
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c => { c.Item().Text("Billed to").SemiBold(); c.Item().Text(customerName); c.Item().Text(order.Phone); });
                row.RelativeItem().Column(c => { c.Item().Text("Deliver to").SemiBold(); foreach (var line in AddressLines(order.AddressJson)) { c.Item().Text(line); } });
            });
            col.Item().Element(e => Items(e, order));
            col.Item().AlignRight().Width(200).Element(e => Totals(e, order));
            col.Item().Text($"Payment: {PaymentLabel(order.PaymentMethod)}").SemiBold();
        });
        page.Footer().AlignCenter().Text("Thank you for shopping with Super App. Prices include VAT.").FontSize(8).FontColor(Colors.Grey.Darken1);
    })).GeneratePdf();

    private static void Header(IContainer container, string title, string code, DateTime at) => container.Row(row =>
    {
        row.RelativeItem().Column(c =>
        {
            c.Item().Text("Super App").FontSize(16).Bold().FontColor(Green);
            c.Item().Text(title).FontSize(11).SemiBold();
        });
        row.RelativeItem().AlignRight().Column(c =>
        {
            c.Item().AlignRight().Text(code).SemiBold();
            c.Item().AlignRight().Text(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), Cairo).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture));
        });
    });

    private static void Items(IContainer container, OrderDetailsDto order) => container.Table(table =>
    {
        table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2); });
        table.Header(h =>
        {
            foreach (var label in new[] { "Item", "Qty", "Price", "Total" })
            {
                h.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(3).Text(label).SemiBold();
            }
        });
        foreach (var item in order.Groups.SelectMany(g => g.Items))
        {
            table.Cell().PaddingVertical(3).Text($"{item.Title} · {item.Size} · {item.Color}{(item.Status == "Cancelled" ? " (cancelled)" : "")}");
            table.Cell().PaddingVertical(3).Text(item.Qty.ToString(CultureInfo.InvariantCulture));
            table.Cell().PaddingVertical(3).Text(Money(item.UnitPrice));
            table.Cell().PaddingVertical(3).Text(Money(item.LineTotal));
        }
    });

    private static void Totals(IContainer container, OrderDetailsDto order) => container.Column(c =>
    {
        void Line(string label, string value, bool bold = false) => c.Item().Row(r =>
        {
            var l = r.RelativeItem().Text(label);
            var v = r.ConstantItem(90).AlignRight().Text(value);
            if (bold) { l.Bold(); v.Bold(); }
        });
        Line("Subtotal", Money(order.Subtotal));
        if (order.Discount > 0) { Line($"Discount{(order.PromoCode is null ? "" : $" ({order.PromoCode})")}", "−" + Money(order.Discount)); }
        Line("Delivery", order.DeliveryFee == 0 ? "Free" : Money(order.DeliveryFee));
        Line("Total", Money(order.Total), bold: true);
        Line("VAT included (14%)", Money(order.VatAmount));
    });

    private static IEnumerable<string> AddressLines(string json)
    {
        using var doc = JsonDocument.Parse(json);
        foreach (var name in new[] { "label", "name", "line1", "line", "line2", "city" })
        {
            if (doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
            {
                yield return v.GetString()!;
            }
        }
    }

    private static string PaymentLabel(string method) => method switch
    {
        "CashOnDelivery" => "Cash on delivery",
        "Wallet" => "Super App wallet (simulated until payments go live)",
        _ => "Card / wallet app (simulated until payments go live)",
    };

    private static string Money(decimal amount) => amount.ToString("#,0.##", CultureInfo.InvariantCulture) + " EGP";
}
