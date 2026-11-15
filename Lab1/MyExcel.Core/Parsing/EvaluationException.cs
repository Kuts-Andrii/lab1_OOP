namespace MyExcel.Core.Parsing;

internal sealed class EvaluationException(CellErrorKind kind, string message) : Exception(message)
{
    public CellErrorKind Kind { get; } = kind;

    public CellError ToCellError() => new(Kind, Message);
}
