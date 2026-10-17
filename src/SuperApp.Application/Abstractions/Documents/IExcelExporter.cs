namespace SuperApp.Application.Abstractions.Documents;

/// <summary>A figure on the dashboard sheet ("Bookings: 42"). Money figures are shown in EGP.</summary>
public sealed record ExcelFigure(string Label, decimal Value, bool IsMoney = false);

/// <summary>The first sheet of every export: title, what was exported, and the key figures.</summary>
public sealed record ExcelDashboard(string Title, string Subtitle, IReadOnlyList<ExcelFigure> Figures);

/// <summary>A data sheet: one header row and the rows (numbers, dates and text keep their type).</summary>
public sealed record ExcelSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>Every export is an Excel file with a dashboard sheet first (rule 8).</summary>
public interface IExcelExporter
{
    byte[] Build(ExcelDashboard dashboard, params ExcelSheet[] sheets);
}
