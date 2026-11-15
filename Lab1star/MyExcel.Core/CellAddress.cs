namespace MyExcel.Core;

public readonly record struct CellAddress : IComparable<CellAddress>
{
    private const int AlphabetSize = 26;
    private const int MaxColumnNameLength = 6;

    public CellAddress(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        Row = row;
        Column = column;
    }

    public int Row { get; }

    public int Column { get; }

    public static string ColumnIndexToName(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

        var name = string.Empty;
        var number = columnIndex + 1;
        while (number > 0)
        {
            var remainder = (number - 1) % AlphabetSize;
            name = (char)('A' + remainder) + name;
            number = (number - 1) / AlphabetSize;
        }

        return name;
    }

    public static bool TryColumnNameToIndex(string? name, out int columnIndex)
    {
        columnIndex = -1;
        if (string.IsNullOrEmpty(name) || name.Length > MaxColumnNameLength)
        {
            return false;
        }

        var number = 0;
        foreach (var symbol in name)
        {
            var upper = char.ToUpperInvariant(symbol);
            if (upper is < 'A' or > 'Z')
            {
                return false;
            }

            number = number * AlphabetSize + (upper - 'A' + 1);
        }

        columnIndex = number - 1;
        return true;
    }

    public static bool TryParse(string? text, out CellAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        var lettersCount = 0;
        while (lettersCount < text.Length && char.IsAsciiLetter(text[lettersCount]))
        {
            lettersCount++;
        }

        var digits = text[lettersCount..];
        if (lettersCount == 0
            || digits.Length == 0
            || digits[0] == '0'
            || !digits.All(char.IsAsciiDigit)
            || !int.TryParse(digits, out var rowNumber)
            || !TryColumnNameToIndex(text[..lettersCount], out var column))
        {
            return false;
        }

        address = new CellAddress(rowNumber - 1, column);
        return true;
    }

    public static CellAddress Parse(string text) =>
        TryParse(text, out var address)
            ? address
            : throw new FormatException($"Некоректна адреса клітинки: '{text}'.");

    public string ColumnName => ColumnIndexToName(Column);

    public int CompareTo(CellAddress other) =>
        Row != other.Row ? Row.CompareTo(other.Row) : Column.CompareTo(other.Column);

    public override string ToString() => $"{ColumnName}{Row + 1}";
}
