namespace MyExcel.Core;

public sealed record CellError(CellErrorKind Kind, string Message, int? Position = null)
{
    public string Code => Kind switch
    {
        CellErrorKind.Syntax => "#СИНТАКС!",
        CellErrorKind.DivisionByZero => "#ДІЛ/0!",
        CellErrorKind.InvalidReference => "#ПОСИЛ!",
        CellErrorKind.CircularReference => "#ЦИКЛ!",
        CellErrorKind.DependencyError => "#ЗНАЧ!",
        _ => "#ПОМИЛКА!",
    };
}
