using MyExcel.Core.Grammar;

namespace MyExcel.Core.Parsing;

internal sealed class ParsedExpression
{
    private ParsedExpression(
        LabCalculatorParser.CompileUnitContext? tree,
        IReadOnlyList<string> references,
        CellError? syntaxError)
    {
        Tree = tree;
        References = references;
        SyntaxError = syntaxError;
    }

    public LabCalculatorParser.CompileUnitContext? Tree { get; }

    public IReadOnlyList<string> References { get; }

    public CellError? SyntaxError { get; }

    public bool IsValid => SyntaxError is null;

    public static ParsedExpression Success(
        LabCalculatorParser.CompileUnitContext tree, IReadOnlyList<string> references) =>
        new(tree, references, null);

    public static ParsedExpression Failure(CellError error) =>
        new(null, [], error);
}
