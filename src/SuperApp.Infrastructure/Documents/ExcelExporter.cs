using ClosedXML.Excel;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Services;

namespace SuperApp.Infrastructure.Documents;

/// <summary>Builds .xlsx files: a "Dashboard" sheet with the key figures, then one filterable table per data sheet.</summary>
public sealed class ExcelExporter(IClock clock) : IExcelExporter
{
    private static readonly XLColor Brand = XLColor.FromHtml("#0B6E4F");

    public byte[] Build(ExcelDashboard dashboard, params ExcelSheet[] sheets)
    {
        using var book = new XLWorkbook();
        AddDashboard(book, dashboard);
        foreach (var sheet in sheets)
        {
            AddTable(book, sheet);
        }
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddDashboard(XLWorkbook book, ExcelDashboard d)
    {
        var ws = book.Worksheets.Add("Dashboard");
        ws.Cell(1, 1).Value = d.Title;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(Brand);
        ws.Cell(2, 1).Value = d.Subtitle;
        ws.Cell(3, 1).Value = $"Super App · made {clock.UtcNow:yyyy-MM-dd HH:mm} UTC";
        ws.Cell(3, 1).Style.Font.SetFontColor(XLColor.Gray);
        for (var i = 0; i < d.Figures.Count; i++)
        {
            var f = d.Figures[i];
            var row = 5 + i;
            ws.Cell(row, 1).Value = f.Label;
            ws.Cell(row, 2).Value = f.Value;
            ws.Cell(row, 2).Style.NumberFormat.Format = f.IsMoney ? "#,##0 \"EGP\"" : "#,##0";
            ws.Cell(row, 2).Style.Font.SetBold().Font.SetFontSize(13);
            ws.Range(row, 1, row, 2).Style.Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(XLColor.LightGray);
        }
        ws.Column(1).Width = 34;
        ws.Column(2).Width = 20;
    }

    private static void AddTable(XLWorkbook book, ExcelSheet sheet)
    {
        var ws = book.Worksheets.Add(sheet.Name.Length > 31 ? sheet.Name[..31] : sheet.Name);
        for (var c = 0; c < sheet.Headers.Count; c++)
        {
            ws.Cell(1, c + 1).Value = sheet.Headers[c];
        }
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            for (var c = 0; c < sheet.Rows[r].Count; c++)
            {
                ws.Cell(r + 2, c + 1).Value = ToCell(sheet.Rows[r][c]);
            }
        }
        var header = ws.Range(1, 1, 1, Math.Max(1, sheet.Headers.Count));
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Brand);
        ws.Range(1, 1, Math.Max(1, sheet.Rows.Count + 1), Math.Max(1, sheet.Headers.Count)).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(sheet.Rows.Count + 1, 300));
    }

    private static XLCellValue ToCell(object? value) => value switch
    {
        null => Blank.Value,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        DateTime t => t,
        bool b => b ? "Yes" : "No",
        decimal m => m,
        int i => i,
        long l => l,
        double x => x,
        _ => value.ToString(),
    };
}
