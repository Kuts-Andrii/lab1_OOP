using System.Numerics;
using MyExcel.Core.Parsing;

namespace MyExcel.Core;

public static class Calculator
{
    public static CellError? CheckSyntax(string expression) =>
        ExpressionParser.Parse(expression).SyntaxError;

    public static CellValue Evaluate(string expression, Func<CellAddress, BigInteger>? resolveCell = null)
    {
        var parsed = ExpressionParser.Parse(expression);
        if (!parsed.IsValid)
        {
            return CellValue.FromError(parsed.SyntaxError!);
        }

        var visitor = new EvaluationVisitor(reference =>
        {
            if (resolveCell is null || !CellAddress.TryParse(reference, out var address))
            {
                throw new EvaluationException(
                    CellErrorKind.InvalidReference, $"Невідома клітинка {reference.ToUpperInvariant()}.");
            }

            return resolveCell(address);
        });

        try
        {
            return CellValue.FromNumber(visitor.Visit(parsed.Tree!));
        }
        catch (EvaluationException exception)
        {
            return CellValue.FromError(exception.ToCellError());
        }
    }
}
