using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace MyExcel.Core.Serialization;

public static class SpreadsheetSerializer
{
    public const string FileExtension = ".myexcel";
    public const string FormatName = "MyExcel.Spreadsheet";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static string Serialize(Spreadsheet spreadsheet)
    {
        ArgumentNullException.ThrowIfNull(spreadsheet);

        var document = new SpreadsheetDocument(
            FormatName,
            FormatVersion,
            spreadsheet.RowCount,
            spreadsheet.ColumnCount,
            spreadsheet.NonEmptyCells
                .Select(cell => new CellRecord(cell.Address.ToString(), cell.Expression))
                .ToList());

        return JsonSerializer.Serialize(document, Options);
    }

    public static Spreadsheet Deserialize(string json)
    {
        SpreadsheetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SpreadsheetDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Файл пошкоджений: некоректний JSON.", exception);
        }

        if (document is null || document.Format != FormatName)
        {
            throw new InvalidDataException("Файл не є таблицею MyExcel.");
        }

        if (document.Version > FormatVersion)
        {
            throw new InvalidDataException(
                $"Файл створено новішою версією програми (версія формату {document.Version}).");
        }

        Spreadsheet spreadsheet;
        try
        {
            spreadsheet = new Spreadsheet(document.Rows, document.Columns);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException($"Некоректний розмір таблиці: {exception.Message}", exception);
        }

        foreach (var record in document.Cells ?? [])
        {
            if (!CellAddress.TryParse(record.Address, out var address) || !spreadsheet.Contains(address))
            {
                throw new InvalidDataException($"Некоректна адреса клітинки у файлі: '{record.Address}'.");
            }

            spreadsheet.SetExpressionCore(address, record.Expression?.Trim() ?? string.Empty);
        }

        spreadsheet.Recalculate();
        spreadsheet.MarkAsSaved();
        return spreadsheet;
    }

    internal sealed record SpreadsheetDocument(
        string? Format, int Version, int Rows, int Columns, List<CellRecord>? Cells);

    internal sealed record CellRecord(string? Address, string? Expression);
}
