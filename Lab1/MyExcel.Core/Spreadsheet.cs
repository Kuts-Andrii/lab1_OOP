using System.Numerics;
using MyExcel.Core.Parsing;

namespace MyExcel.Core;

public sealed class Spreadsheet
{
    public const int DefaultRowCount = 20;
    public const int DefaultColumnCount = 10;
    public const int MaxRowCount = 1000;
    public const int MaxColumnCount = 702;

    private readonly Dictionary<CellAddress, Cell> _cells = [];

    public Spreadsheet(int rowCount = DefaultRowCount, int columnCount = DefaultColumnCount)
    {
        if (rowCount is < 1 or > MaxRowCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowCount), $"Кількість рядків має бути від 1 до {MaxRowCount}.");
        }

        if (columnCount is < 1 or > MaxColumnCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnCount), $"Кількість стовпчиків має бути від 1 до {MaxColumnCount}.");
        }

        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public int RowCount { get; private set; }

    public int ColumnCount { get; private set; }

    public bool IsModified { get; private set; }

    public IEnumerable<Cell> NonEmptyCells => _cells.Values.OrderBy(cell => cell.Address);

    public int ErrorCount => _cells.Values.Count(cell => cell.Value.IsError);

    public Cell this[string address] => GetCell(CellAddress.Parse(address));

    public bool Contains(CellAddress address) =>
        address.Row < RowCount && address.Column < ColumnCount;

    public Cell GetCell(CellAddress address)
    {
        EnsureInside(address);
        return _cells.TryGetValue(address, out var cell) ? cell : Cell.CreateEmpty(address);
    }

    public void SetExpression(CellAddress address, string? expression)
    {
        EnsureInside(address);
        var newExpression = expression?.Trim() ?? string.Empty;
        if (GetCell(address).Expression == newExpression)
        {
            return;
        }

        SetExpressionCore(address, newExpression);
        IsModified = true;
        Recalculate();
    }

    public void SetExpression(string address, string? expression) =>
        SetExpression(CellAddress.Parse(address), expression);

    public void AddRow()
    {
        if (RowCount >= MaxRowCount)
        {
            throw new InvalidOperationException($"Досягнуто максимальну кількість рядків ({MaxRowCount}).");
        }

        RowCount++;
        OnStructureChanged();
    }

    public void AddColumn()
    {
        if (ColumnCount >= MaxColumnCount)
        {
            throw new InvalidOperationException($"Досягнуто максимальну кількість стовпчиків ({MaxColumnCount}).");
        }

        ColumnCount++;
        OnStructureChanged();
    }

    public RemovalImpact AnalyzeLastRowRemoval() =>
        AnalyzeRemoval(address => address.Row == RowCount - 1);

    public RemovalImpact AnalyzeLastColumnRemoval() =>
        AnalyzeRemoval(address => address.Column == ColumnCount - 1);

    public void RemoveLastRow()
    {
        if (RowCount <= 1)
        {
            throw new InvalidOperationException("Таблиця повинна містити хоча б один рядок.");
        }

        RemoveCells(address => address.Row == RowCount - 1);
        RowCount--;
        OnStructureChanged();
    }

    public void RemoveLastColumn()
    {
        if (ColumnCount <= 1)
        {
            throw new InvalidOperationException("Таблиця повинна містити хоча б один стовпчик.");
        }

        RemoveCells(address => address.Column == ColumnCount - 1);
        ColumnCount--;
        OnStructureChanged();
    }

    public void MarkAsSaved() => IsModified = false;

    public void Recalculate()
    {
        var graph = _cells.Values.ToDictionary(
            cell => cell.Address,
            cell => GetValidReferences(cell).Where(_cells.ContainsKey).ToList());

        var session = new RecalculationSession(this, CycleDetector.FindCycles(graph));
        foreach (var cell in _cells.Values)
        {
            cell.Value = session.Evaluate(cell.Address);
        }
    }

    internal void SetExpressionCore(CellAddress address, string expression)
    {
        if (expression.Length == 0)
        {
            _cells.Remove(address);
        }
        else
        {
            _cells[address] = new Cell(address, expression, ExpressionParser.Parse(expression));
        }
    }

    private void OnStructureChanged()
    {
        IsModified = true;
        Recalculate();
    }

    private void EnsureInside(CellAddress address)
    {
        if (!Contains(address))
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"Клітинки {address} немає в таблиці.");
        }
    }

    private RemovalImpact AnalyzeRemoval(Func<CellAddress, bool> isRemoved)
    {
        var nonEmpty = _cells.Keys.Where(isRemoved).Order().ToList();
        var dependents = _cells.Values
            .Where(cell => !isRemoved(cell.Address) && GetValidReferences(cell).Any(isRemoved))
            .Select(cell => cell.Address)
            .Order()
            .ToList();
        return new RemovalImpact(nonEmpty, dependents);
    }

    private void RemoveCells(Func<CellAddress, bool> isRemoved)
    {
        foreach (var address in _cells.Keys.Where(isRemoved).ToList())
        {
            _cells.Remove(address);
        }
    }

    private IEnumerable<CellAddress> GetValidReferences(Cell cell)
    {
        if (cell.Parsed is not { IsValid: true } parsed)
        {
            return [];
        }

        return parsed.References
            .Select(reference => (IsValid: CellAddress.TryParse(reference, out var address), Address: address))
            .Where(reference => reference.IsValid && Contains(reference.Address))
            .Select(reference => reference.Address)
            .Distinct();
    }

    private sealed class RecalculationSession(
        Spreadsheet sheet,
        IReadOnlyDictionary<CellAddress, IReadOnlyList<CellAddress>> cycles)
    {
        private readonly Dictionary<CellAddress, CellValue> _computed = [];

        public CellValue Evaluate(CellAddress address)
        {
            if (_computed.TryGetValue(address, out var known))
            {
                return known;
            }

            var cell = sheet._cells[address];
            CellValue value;
            if (cell.Parsed is not { IsValid: true } parsed)
            {
                value = CellValue.FromError(cell.Parsed?.SyntaxError
                    ?? new CellError(CellErrorKind.Syntax, "Некоректний вираз."));
            }
            else if (cycles.TryGetValue(address, out var cycle))
            {
                value = CellValue.FromError(new CellError(
                    CellErrorKind.CircularReference,
                    $"Циклічне посилання між клітинками: {string.Join(", ", cycle)}."));
            }
            else
            {
                value = EvaluateExpression(parsed);
            }

            _computed[address] = value;
            return value;
        }

        private CellValue EvaluateExpression(ParsedExpression parsed)
        {
            try
            {
                return CellValue.FromNumber(new EvaluationVisitor(ResolveReference).Visit(parsed.Tree!));
            }
            catch (EvaluationException exception)
            {
                return CellValue.FromError(exception.ToCellError());
            }
        }

        private BigInteger ResolveReference(string reference)
        {
            if (!CellAddress.TryParse(reference, out var address) || !sheet.Contains(address))
            {
                throw new EvaluationException(
                    CellErrorKind.InvalidReference,
                    $"Клітинки {reference.ToUpperInvariant()} не існує в таблиці.");
            }

            if (!sheet._cells.ContainsKey(address))
            {
                return BigInteger.Zero;
            }

            var value = Evaluate(address);
            if (value.IsError)
            {
                throw new EvaluationException(
                    CellErrorKind.DependencyError,
                    $"Клітинка {address} містить помилку {value.Error!.Code}");
            }

            return value.Number!.Value;
        }
    }
}
