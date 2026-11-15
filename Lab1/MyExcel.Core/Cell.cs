using MyExcel.Core.Parsing;

namespace MyExcel.Core;

public sealed class Cell
{
    internal Cell(CellAddress address, string expression, ParsedExpression? parsed)
    {
        Address = address;
        Expression = expression;
        Parsed = parsed;
    }

    public CellAddress Address { get; }

    public string Expression { get; }

    public CellValue Value { get; internal set; } = CellValue.Empty;

    public bool IsEmpty => Expression.Length == 0;

    internal ParsedExpression? Parsed { get; }

    internal static Cell CreateEmpty(CellAddress address) => new(address, string.Empty, null);
}
