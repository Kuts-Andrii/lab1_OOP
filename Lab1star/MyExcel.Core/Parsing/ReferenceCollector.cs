using MyExcel.Core.Grammar;

namespace MyExcel.Core.Parsing;

internal sealed class ReferenceCollector : LabCalculatorBaseVisitor<bool>
{
    private readonly List<string> _references = [];

    public static IReadOnlyList<string> Collect(LabCalculatorParser.CompileUnitContext tree)
    {
        var collector = new ReferenceCollector();
        collector.Visit(tree);
        return collector._references;
    }

    public override bool VisitCellReferenceExpr(LabCalculatorParser.CellReferenceExprContext context)
    {
        _references.Add(context.CELL_REFERENCE().GetText());
        return true;
    }
}
