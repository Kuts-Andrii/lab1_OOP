namespace MyExcel.Core.Parsing;

internal sealed class ExpressionSyntaxException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}
