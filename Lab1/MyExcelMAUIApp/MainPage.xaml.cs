using MyExcel.Core;
using SpreadsheetCell = MyExcel.Core.Cell;
using MyExcelMAUIApp.Services;

namespace MyExcelMAUIApp;

public partial class MainPage : ContentPage
{
    private const double RowHeaderWidth = 56;
    private const double CellWidth = 120;
    private const double CellHeight = 34;
    private const string DefaultDocumentName = "Нова таблиця";
    private const string SaveChoice = "Зберегти";
    private const string DiscardChoice = "Не зберігати";
    private const string CancelChoice = "Скасувати";

    private static readonly Color ErrorTextColor = GetResource<Color>("ErrorText");
    private static readonly Color NormalTextColor = GetResource<Color>("TextPrimary");
    private static readonly Color HeaderColor = GetResource<Color>("HeaderBackground");
    private static readonly Color SelectedHeaderColor = GetResource<Color>("SelectedHeaderBackground");

    private readonly SpreadsheetFileService _fileService;
    private readonly Dictionary<CellAddress, Entry> _cellEntries = [];
    private readonly List<Label> _columnHeaders = [];
    private readonly List<Label> _rowHeaders = [];

    private Spreadsheet _spreadsheet = new();
    private CellDisplayMode _displayMode = CellDisplayMode.Values;
    private CellAddress? _selectedCell;
    private string? _filePath;
    private string _documentName = DefaultDocumentName;
    private bool _isBusy;
    private bool _isInitialized;

    public MainPage()
        : this(new SpreadsheetFileService())
    {
    }

    public MainPage(SpreadsheetFileService fileService)
    {
        _fileService = fileService;

        InitializeComponent();
        _isInitialized = true;

        BuildSheetView();
        UpdateTitle();
        SetStatus("Готово. Введіть вираз у клітинку або в рядок формул. Довідка — F1.");
    }

    private void BuildSheetView()
    {
        _cellEntries.Clear();
        _columnHeaders.Clear();
        _rowHeaders.Clear();
        SheetGrid.Children.Clear();
        SheetGrid.RowDefinitions.Clear();
        SheetGrid.ColumnDefinitions.Clear();

        SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(RowHeaderWidth)));
        for (var column = 0; column < _spreadsheet.ColumnCount; column++)
        {
            SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        }

        for (var row = 0; row <= _spreadsheet.RowCount; row++)
        {
            SheetGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));
        }

        SheetGrid.Add(CreateHeaderLabel(string.Empty), 0, 0);

        for (var column = 0; column < _spreadsheet.ColumnCount; column++)
        {
            var header = CreateHeaderLabel(CellAddress.ColumnIndexToName(column));
            _columnHeaders.Add(header);
            SheetGrid.Add(header, column + 1, 0);
        }

        for (var row = 0; row < _spreadsheet.RowCount; row++)
        {
            var header = CreateHeaderLabel((row + 1).ToString());
            _rowHeaders.Add(header);
            SheetGrid.Add(header, 0, row + 1);

            for (var column = 0; column < _spreadsheet.ColumnCount; column++)
            {
                SheetGrid.Add(CreateCellEntry(new CellAddress(row, column)), column + 1, row + 1);
            }
        }

        if (_selectedCell is { } selected && !_spreadsheet.Contains(selected))
        {
            _selectedCell = null;
        }

        RefreshAllCells();
        UpdateSelectionView();
        SizeLabel.Text = $"Рядків: {_spreadsheet.RowCount}   Стовпчиків: {_spreadsheet.ColumnCount}";
    }

    private static Label CreateHeaderLabel(string text) => new()
    {
        Text = text,
        Style = GetResource<Style>("HeaderLabel"),
    };

    private Entry CreateCellEntry(CellAddress address)
    {
        var entry = new Entry
        {
            Style = GetResource<Style>("CellEntry"),
            ClearButtonVisibility = ClearButtonVisibility.Never,
        };

        entry.Focused += (_, _) => OnCellFocused(address, entry);
        entry.Unfocused += (_, _) => OnCellUnfocused(address, entry);
        entry.Completed += (_, _) => OnCellCompleted(address, entry);

        _cellEntries[address] = entry;
        return entry;
    }

    private void RefreshAllCells()
    {
        foreach (var address in _cellEntries.Keys)
        {
            RefreshCell(address);
        }
    }

    private void RefreshCell(CellAddress address)
    {
        var entry = _cellEntries[address];
        var cell = _spreadsheet.GetCell(address);
        var showExpression = _displayMode == CellDisplayMode.Expressions || entry.IsFocused;

        entry.Text = showExpression ? cell.Expression : cell.Value.DisplayText;
        entry.TextColor = cell.Value.IsError ? ErrorTextColor : NormalTextColor;
        entry.HorizontalTextAlignment = !showExpression && cell.Value.Number is not null
            ? TextAlignment.End
            : TextAlignment.Start;

        ToolTipProperties.SetText(entry, BuildToolTip(cell, showExpression)!);
    }

    private static string? BuildToolTip(SpreadsheetCell cell, bool showExpression) => cell switch
    {
        { Value.Error: { } error } => error.Message,
        { IsEmpty: true } => null,
        _ => showExpression ? $"= {cell.Value.DisplayText}" : cell.Expression,
    };

    private void UpdateSelectionView()
    {
        foreach (var header in _columnHeaders.Concat(_rowHeaders))
        {
            header.BackgroundColor = HeaderColor;
        }

        if (_selectedCell is not { } address)
        {
            SelectedCellLabel.Text = "—";
            FormulaEntry.Text = string.Empty;
            return;
        }

        _columnHeaders[address.Column].BackgroundColor = SelectedHeaderColor;
        _rowHeaders[address.Row].BackgroundColor = SelectedHeaderColor;

        var cell = _spreadsheet.GetCell(address);
        SelectedCellLabel.Text = address.ToString();
        FormulaEntry.Text = cell.Expression;
        SetStatus(DescribeCell(cell));
    }

    private static string DescribeCell(SpreadsheetCell cell)
    {
        if (cell.Value.Error is { } error)
        {
            return $"{cell.Address}: {error.Code} {error.Message}";
        }

        return cell.IsEmpty
            ? $"{cell.Address}: порожня клітинка (у виразах має значення 0)"
            : $"{cell.Address} = {cell.Value.DisplayText}";
    }

    private void UpdateTitle()
    {
        var modifiedMark = _spreadsheet.IsModified ? " *" : string.Empty;
        Title = $"{_documentName}{modifiedMark} — MyExcel";
    }

    private void SetStatus(string text) => StatusLabel.Text = text;

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        BusyIndicator.IsRunning = isBusy;
        BusyIndicator.IsVisible = isBusy;
        ToolbarPanel.IsEnabled = !isBusy;
        SheetGrid.IsEnabled = !isBusy;
    }

    private static T GetResource<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value) && value is T typed
            ? typed
            : throw new InvalidOperationException($"Ресурс '{key}' не знайдено.");

    private void OnCellFocused(CellAddress address, Entry entry)
    {
        if (!IsCurrentEntry(address, entry))
        {
            return;
        }

        _selectedCell = address;
        entry.Text = _spreadsheet.GetCell(address).Expression;
        UpdateSelectionView();
    }

    private void OnCellUnfocused(CellAddress address, Entry entry)
    {
        if (IsCurrentEntry(address, entry))
        {
            CommitCellInput(address, entry.Text);
        }
    }

    private void OnCellCompleted(CellAddress address, Entry entry)
    {
        if (!IsCurrentEntry(address, entry))
        {
            return;
        }

        var below = new CellAddress(address.Row + 1, address.Column);
        if (_cellEntries.TryGetValue(below, out var nextEntry))
        {
            nextEntry.Focus();
        }
        else
        {
            CommitCellInput(address, entry.Text);
            entry.Unfocus();
        }
    }

    private void CommitCellInput(CellAddress address, string? text)
    {
        if (!_spreadsheet.Contains(address))
        {
            return;
        }

        var expression = text?.Trim() ?? string.Empty;
        if (expression != _spreadsheet.GetCell(address).Expression)
        {
            _spreadsheet.SetExpression(address, expression);
            RefreshAllCells();
            UpdateTitle();
        }
        else
        {
            RefreshCell(address);
        }

        if (_selectedCell == address)
        {
            UpdateSelectionView();
        }
    }

    private void CommitPendingEdit()
    {
        if (_selectedCell is { } address
            && _cellEntries.TryGetValue(address, out var entry)
            && entry.IsFocused)
        {
            CommitCellInput(address, entry.Text);
        }
    }

    private bool IsCurrentEntry(CellAddress address, Entry entry) =>
        _cellEntries.TryGetValue(address, out var current) && ReferenceEquals(current, entry);

    private void FormulaEntry_Completed(object sender, EventArgs e) => ApplyFormula();

    private void ApplyFormulaButton_Clicked(object sender, EventArgs e) => ApplyFormula();

    private void ApplyFormula()
    {
        if (_selectedCell is not { } address)
        {
            SetStatus("Спочатку виберіть клітинку таблиці.");
            return;
        }

        CommitCellInput(address, FormulaEntry.Text);
    }

    private void DisplayModeRadio_CheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!_isInitialized || !e.Value)
        {
            return;
        }

        SetDisplayMode(ReferenceEquals(sender, ExpressionModeRadio)
            ? CellDisplayMode.Expressions
            : CellDisplayMode.Values);
    }

    private void ExpressionModeMenu_Clicked(object sender, EventArgs e) =>
        SetDisplayMode(CellDisplayMode.Expressions);

    private void ValueModeMenu_Clicked(object sender, EventArgs e) =>
        SetDisplayMode(CellDisplayMode.Values);

    private void SetDisplayMode(CellDisplayMode mode)
    {
        CommitPendingEdit();
        _displayMode = mode;
        ExpressionModeRadio.IsChecked = mode == CellDisplayMode.Expressions;
        ValueModeRadio.IsChecked = mode == CellDisplayMode.Values;
        RefreshAllCells();
        SetStatus(mode == CellDisplayMode.Expressions
            ? "Режим ВИРАЗ: у клітинках показано введені вирази."
            : "Режим ЗНАЧЕННЯ: у клітинках показано обчислені значення.");
    }

    private void CalculateButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        _spreadsheet.Recalculate();
        RefreshAllCells();

        var errorCount = _spreadsheet.ErrorCount;
        SetStatus(errorCount == 0
            ? "Перерахунок виконано. Помилок немає."
            : $"Перерахунок виконано. Клітинок з помилками: {errorCount} (наведіть курсор на клітинку, щоб побачити опис).");
    }

    private async void AddRowButton_Clicked(object sender, EventArgs e) =>
        await ChangeStructureAsync(_spreadsheet.AddRow, $"Додано рядок {_spreadsheet.RowCount + 1}.");

    private async void AddColumnButton_Clicked(object sender, EventArgs e) =>
        await ChangeStructureAsync(
            _spreadsheet.AddColumn,
            $"Додано стовпчик {CellAddress.ColumnIndexToName(_spreadsheet.ColumnCount)}.");

    private async void DeleteRowButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        var rowName = $"рядок {_spreadsheet.RowCount}";
        if (_spreadsheet.RowCount > 1
            && !await ConfirmRemovalAsync(rowName, _spreadsheet.AnalyzeLastRowRemoval()))
        {
            return;
        }

        await ChangeStructureAsync(_spreadsheet.RemoveLastRow, $"Видалено {rowName}.");
    }

    private async void DeleteColumnButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        var columnName = $"стовпчик {CellAddress.ColumnIndexToName(_spreadsheet.ColumnCount - 1)}";
        if (_spreadsheet.ColumnCount > 1
            && !await ConfirmRemovalAsync(columnName, _spreadsheet.AnalyzeLastColumnRemoval()))
        {
            return;
        }

        await ChangeStructureAsync(_spreadsheet.RemoveLastColumn, $"Видалено {columnName}.");
    }

    private async Task ChangeStructureAsync(Action change, string successMessage)
    {
        CommitPendingEdit();
        try
        {
            change();
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlertAsync("Неможливо виконати дію", exception.Message, "OK");
            return;
        }

        BuildSheetView();
        UpdateTitle();
        SetStatus(successMessage);
    }

    private async Task<bool> ConfirmRemovalAsync(string target, RemovalImpact impact)
    {
        if (!impact.HasConsequences)
        {
            return true;
        }

        var message = new List<string>();
        if (impact.NonEmptyCells.Count > 0)
        {
            message.Add($"Буде видалено дані клітинок: {string.Join(", ", impact.NonEmptyCells)}.");
        }

        if (impact.DependentCells.Count > 0)
        {
            message.Add(
                $"На {target} посилаються клітинки {string.Join(", ", impact.DependentCells)} — " +
                "після видалення в них з'явиться помилка #ПОСИЛ!.");
        }

        message.Add($"Видалити {target}?");
        return await DisplayAlertAsync("Підтвердження видалення", string.Join("\n\n", message), "Видалити", CancelChoice);
    }

    private async void NewButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        if (await ConfirmDiscardChangesAsync())
        {
            LoadSpreadsheet(new Spreadsheet(), filePath: null, DefaultDocumentName);
            SetStatus("Створено нову таблицю.");
        }
    }

    private async void ReadButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }

        await RunBusyAsync("Відкриття файлу...", async () =>
        {
            var opened = await _fileService.OpenAsync();
            if (opened is null)
            {
                SetStatus("Відкриття скасовано.");
                return;
            }

            LoadSpreadsheet(opened.Spreadsheet, opened.FilePath, Path.GetFileNameWithoutExtension(opened.FilePath));
            SetStatus($"Відкрито файл: {opened.FilePath}");
        });
    }

    private async void SaveButton_Clicked(object sender, EventArgs e) => await SaveDocumentAsync();

    private async void SaveAsButton_Clicked(object sender, EventArgs e) => await SaveDocumentAsAsync();

    private async Task<bool> SaveDocumentAsync()
    {
        CommitPendingEdit();
        if (_filePath is null)
        {
            return await SaveDocumentAsAsync();
        }

        var path = _filePath;
        return await RunBusyAsync("Збереження...", async () =>
        {
            await _fileService.SaveAsync(_spreadsheet, path);
            UpdateTitle();
            SetStatus($"Збережено: {path}");
        }) && !_spreadsheet.IsModified;
    }

    private async Task<bool> SaveDocumentAsAsync()
    {
        CommitPendingEdit();
        return await RunBusyAsync("Збереження...", async () =>
        {
            var path = await _fileService.SaveAsAsync(_spreadsheet, _documentName);
            if (path is null)
            {
                SetStatus("Збереження скасовано.");
                return;
            }

            _filePath = path;
            _documentName = Path.GetFileNameWithoutExtension(path);
            UpdateTitle();
            SetStatus($"Збережено: {path}");
        }) && !_spreadsheet.IsModified;
    }

    private void LoadSpreadsheet(Spreadsheet spreadsheet, string? filePath, string documentName)
    {
        _spreadsheet = spreadsheet;
        _filePath = filePath;
        _documentName = documentName;
        _selectedCell = null;
        BuildSheetView();
        UpdateTitle();
    }

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!_spreadsheet.IsModified)
        {
            return true;
        }

        var choice = await DisplayActionSheetAsync(
            "У таблиці є незбережені зміни. Зберегти їх?", CancelChoice, null, SaveChoice, DiscardChoice);

        return choice switch
        {
            SaveChoice => await SaveDocumentAsync(),
            DiscardChoice => true,
            _ => false,
        };
    }

    private async void HelpButton_Clicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(HelpPage));

    private async void AboutButton_Clicked(object sender, EventArgs e) =>
        await DisplayAlertAsync(
            "Про програму",
            "MyExcel — програма для роботи з електронними таблицями.\n\n" +
            "Лабораторна робота №1 з курсу «Об'єктно-орієнтоване програмування».\n" +
            "Варіант 2: операції +, -, *, / (бінарні); mod, div; +, - (унарні); inc, dec.\n" +
            "Цілі числа довільної довжини, збереження та зчитування файлів.\n\n" +
            "Виконав: студент групи К-28 Куць Андрій Станіславович\n" +
            "КНУ імені Тараса Шевченка, ФКНК, 2026",
            "OK");

    private async void ExitButton_Clicked(object sender, EventArgs e)
    {
        CommitPendingEdit();
        var canExit = _spreadsheet.IsModified
            ? await ConfirmDiscardChangesAsync()
            : await DisplayAlertAsync("Підтвердження", "Ви дійсно хочете вийти?", "Так", "Ні");

        if (canExit)
        {
            Application.Current?.Quit();
        }
    }

    private async Task<bool> RunBusyAsync(string statusText, Func<Task> operation)
    {
        if (_isBusy)
        {
            return false;
        }

        SetBusy(true);
        SetStatus(statusText);
        try
        {
            await operation();
            return true;
        }
        catch (InvalidDataException exception)
        {
            await ShowErrorAsync("Помилка формату файлу", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            await ShowErrorAsync("Немає доступу до файлу", exception.Message);
        }
        catch (IOException exception)
        {
            await ShowErrorAsync("Помилка роботи з файлом", exception.Message);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Неочікувана помилка", exception.Message);
        }
        finally
        {
            SetBusy(false);
        }

        return false;
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        SetStatus($"{title}: {message}");
        await DisplayAlertAsync(title, message, "OK");
    }
}
