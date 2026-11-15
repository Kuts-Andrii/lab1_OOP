using Antlr4.Runtime;

namespace MyExcel.Core.Parsing;

internal sealed class LexerErrorListener(string source) : IAntlrErrorListener<int>
{
    public void SyntaxError(
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        var position = charPositionInLine + 1;
        var symbol = charPositionInLine < source.Length ? source[charPositionInLine] : '?';
        throw new ExpressionSyntaxException(
            $"Позиція {position}: недопустимий символ або некоректне ім'я клітинки, що починається з '{symbol}'.",
            position);
    }
}
