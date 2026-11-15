using Antlr4.Runtime;

namespace MyExcel.Core.Parsing;

internal sealed class ParserErrorListener : BaseErrorListener
{
    private const int EndOfFileTokenType = -1;

    public override void SyntaxError(
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        var position = charPositionInLine + 1;
        var isEndOfInput = offendingSymbol is null || offendingSymbol.Type == EndOfFileTokenType;
        var message = isEndOfInput
            ? $"Позиція {position}: неочікуваний кінець виразу (пропущено операнд або закриваючу дужку)."
            : $"Позиція {position}: неочікувана лексема '{offendingSymbol!.Text}'.";

        throw new ExpressionSyntaxException(message, position);
    }
}
