using Antlr4.Runtime;
using MyExcel.Core.Grammar;

namespace MyExcel.Core.Parsing;

internal static class ExpressionParser
{
    public static ParsedExpression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var lexer = new LabCalculatorLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new LexerErrorListener(expression));

        var parser = new LabCalculatorParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ParserErrorListener());

        try
        {
            var tree = parser.compileUnit();
            return ParsedExpression.Success(tree, ReferenceCollector.Collect(tree));
        }
        catch (ExpressionSyntaxException exception)
        {
            return ParsedExpression.Failure(
                new CellError(CellErrorKind.Syntax, exception.Message, exception.Position));
        }
    }
}
