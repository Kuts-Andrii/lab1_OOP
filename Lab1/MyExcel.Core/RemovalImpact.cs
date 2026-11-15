namespace MyExcel.Core;

public sealed record RemovalImpact(
    IReadOnlyList<CellAddress> NonEmptyCells,
    IReadOnlyList<CellAddress> DependentCells)
{
    public bool HasConsequences => NonEmptyCells.Count > 0 || DependentCells.Count > 0;
}
