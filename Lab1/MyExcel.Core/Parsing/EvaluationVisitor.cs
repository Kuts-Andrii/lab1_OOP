using System.Globalization;
using System.Numerics;
using MyExcel.Core.Grammar;

namespace MyExcel.Core.Parsing;

internal sealed class EvaluationVisitor(Func<string, BigInteger> resolveReference)
    : LabCalculatorBaseVisitor<BigInteger>
{
    public override BigInteger VisitCompileUnit(LabCalculatorParser.CompileUnitContext context) =>
        Visit(context.expression());

    public override BigInteger VisitParenthesizedExpr(LabCalculatorParser.ParenthesizedExprContext context) =>
        Visit(context.expression());

    public override BigInteger VisitNumberExpr(LabCalculatorParser.NumberExprContext context) =>
        BigInteger.Parse(context.NUMBER().GetText(), NumberStyles.None, CultureInfo.InvariantCulture);

    public override BigInteger VisitCellReferenceExpr(LabCalculatorParser.CellReferenceExprContext context) =>
        resolveReference(context.CELL_REFERENCE().GetText());

    public override BigInteger VisitIncDecExpr(LabCalculatorParser.IncDecExprContext context)
    {
        var operand = Visit(context.expression());
        return context.operatorToken.Type == LabCalculatorParser.INC ? operand + 1 : operand - 1;
    }

    public override BigInteger VisitUnaryExpr(LabCalculatorParser.UnaryExprContext context)
    {
        var operand = Visit(context.expression());
        return context.operatorToken.Type == LabCalculatorParser.SUBTRACT ? -operand : operand;
    }

    public override BigInteger VisitAdditiveExpr(LabCalculatorParser.AdditiveExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));
        return context.operatorToken.Type == LabCalculatorParser.ADD ? left + right : left - right;
    }

    public override BigInteger VisitMultiplicativeExpr(LabCalculatorParser.MultiplicativeExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));
        var operation = context.operatorToken.Type;

        if (operation == LabCalculatorParser.MULTIPLY)
        {
            return left * right;
        }

        if (right.IsZero)
        {
            throw new EvaluationException(
                CellErrorKind.DivisionByZero,
                $"Ділення на нуль в операції '{context.operatorToken.Text}'.");
        }

        if (operation == LabCalculatorParser.DIVIDE)
        {
            return IntegerArithmetic.TruncatedDivide(left, right);
        }

        return operation == LabCalculatorParser.DIV
            ? IntegerArithmetic.FloorDivide(left, right)
            : IntegerArithmetic.FloorModulo(left, right);
    }
}
