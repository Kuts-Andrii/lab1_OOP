namespace MyExcel.Core;

public enum CellErrorKind
{
    Syntax,
    DivisionByZero,
    InvalidReference,
    CircularReference,
    DependencyError,
}
