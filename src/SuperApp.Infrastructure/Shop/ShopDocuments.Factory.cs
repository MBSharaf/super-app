using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SuperApp.Application.Features.Shop.Factory;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The factory job ticket (QR = job code) and the courier label (10 × 15 cm).</summary>
public sealed partial class ShopDocuments : IFactoryDocuments
{
    public byte[] JobTicket(FactoryJobDto job, string factoryName) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A6);
        page.Margin(16);
        page.DefaultTextStyle(t => t.FontSize(9));
        page.Content().Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text(job.Card.Code).FontSize(16).Bold();
                    c.Item().Text($"Order {job.Card.OrderCode}{(job.Card.IsSample ? " · SAMPLE" : "")}");
                    c.Item().Text($"Due {job.Card.DueAt.ToString("d MMM HH:mm", CultureInfo.InvariantCulture)} UTC").FontColor(job.Card.Late ? Colors.Red.Medium : Colors.Black);
                });
                r.ConstantItem(80).Image(Qr(job.Card.Code));
            });
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            col.Item().Text(job.Card.Title).SemiBold();
            col.Item().Text($"{job.Card.ProductName} · size {job.Card.Size} · {job.Card.Color} · qty {job.Card.Qty}");
            col.Item().Text($"Print: {(job.Card.HasFront ? "front" : "")}{(job.Card.HasFront && job.Card.HasBack ? " + " : "")}{(job.Card.HasBack ? "back" : "")}");
            if (job.Card.ReprintCount > 0) { col.Item().Text($"Reprint #{job.Card.ReprintCount}").FontColor(Colors.Red.Medium); }
            col.Item().PaddingTop(8).Text(factoryName).FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    })).GeneratePdf();

    public byte[] ShippingLabel(ShipmentLabel label) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(new PageSize(283, 425));
        page.Margin(14);
        page.DefaultTextStyle(t => t.FontSize(9));
        page.Content().Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("Super App").FontSize(16).Bold().FontColor(Green);
                r.RelativeItem().AlignRight().Text(label.Provider == "courier.simulated" ? "Simulated courier" : label.Provider).FontSize(8);
            });
            col.Item().AlignCenter().Width(130).Image(Qr(label.TrackingNumber));
            col.Item().AlignCenter().Text(label.TrackingNumber).FontSize(13).Bold();
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            col.Item().Text("To").SemiBold();
            col.Item().Text(label.ReceiverName);
            foreach (var line in AddressLines(label.AddressJson)) { col.Item().Text(line); }
            col.Item().Text(label.Phone);
            col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            col.Item().Text($"Order {label.OrderCode} · {label.Pieces} piece(s)");
            col.Item().Text(label.CodAmount > 0 ? $"COLLECT {Money(label.CodAmount)}" : "Prepaid — collect nothing").FontSize(12).Bold();
            col.Item().Text($"From {label.SenderName}, {label.SenderAddress}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    })).GeneratePdf();

    private static byte[] Qr(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
