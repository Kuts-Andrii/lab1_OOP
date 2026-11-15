using System.Text;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using MyExcel.Core;
using MyExcel.Core.Serialization;
#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace MyExcelMAUIApp.Services;

public sealed class SpreadsheetFileService
{
    private static readonly FilePickerFileType SpreadsheetFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] = [SpreadsheetSerializer.FileExtension, ".json"],
        });

    public async Task<OpenedSpreadsheet?> OpenAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Відкрити таблицю",
            FileTypes = SpreadsheetFileType,
        });

        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        return new OpenedSpreadsheet(SpreadsheetSerializer.Deserialize(json), file.FullPath);
    }

    public async Task SaveAsync(Spreadsheet spreadsheet, string filePath)
    {
        await File.WriteAllTextAsync(filePath, SpreadsheetSerializer.Serialize(spreadsheet), Encoding.UTF8);
        spreadsheet.MarkAsSaved();
    }

    public async Task<string?> SaveAsAsync(Spreadsheet spreadsheet, string suggestedName)
    {
        var filePath = await PickSaveFilePathAsync(EnsureExtension(suggestedName));
        if (filePath is null)
        {
            return null;
        }

        await SaveAsync(spreadsheet, filePath);
        return filePath;
    }

    public static string EnsureExtension(string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Таблиця" : name.Trim();
        return trimmed.EndsWith(SpreadsheetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + SpreadsheetSerializer.FileExtension;
    }

    private static async Task<string?> PickSaveFilePathAsync(string fileName)
    {
#if WINDOWS
        var picker = new FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(fileName),
        };
        picker.FileTypeChoices.Add("Таблиця MyExcel", [SpreadsheetSerializer.FileExtension]);

        var window = (Microsoft.UI.Xaml.Window)Microsoft.Maui.Controls.Application.Current!.Windows[0].Handler!.PlatformView!;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Збереження підтримується лише у Windows.");
#endif
    }
}
