using System.Globalization;
using System.Numerics;

namespace MyExcel.Core;

public sealed class CellValue
{
    private CellValue(BigInteger? number, CellError? error)
    {
        Number = number;
        Error = error;
    }

    public static CellValue Empty { get; } = new(null, null);

    public BigInteger? Number { get; }

    public CellError? Error { get; }

    public bool IsError => Error is not null;

    public string DisplayText =>
        Error?.Code ?? Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static CellValue FromNumber(BigInteger number) => new(number, null);

    public static CellValue FromError(CellError error) => new(null, error);

    public override string ToString() => DisplayText;
}
