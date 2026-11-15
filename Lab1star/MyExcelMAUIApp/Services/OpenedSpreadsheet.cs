using MyExcel.Core;

namespace MyExcelMAUIApp.Services;

public sealed record OpenedSpreadsheet(Spreadsheet Spreadsheet, string FilePath);
