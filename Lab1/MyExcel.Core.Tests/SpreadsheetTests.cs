using System.Numerics;
using MyExcel.Core;
using MyExcel.Core.Serialization;

namespace MyExcel.Core.Tests;

public class SpreadsheetTests
{
    [Fact]
    public void ReferencesAreEvaluated_AndEmptyCellIsZero()
    {
        var sheet = new Spreadsheet(5, 5);
        sheet.SetExpression("A1", "10");
        sheet.SetExpression("A2", "A1 * 2 + C5");

        Assert.Equal(new BigInteger(20), sheet["A2"].Value.Number);
    }

    [Fact]
    public void ChangingCell_RecalculatesDependents()
    {
        var sheet = new Spreadsheet(5, 5);
        sheet.SetExpression("A1", "1");
        sheet.SetExpression("B1", "inc(A1)");
        sheet.SetExpression("C1", "B1 * 10");

        sheet.SetExpression("A1", "4");

        Assert.Equal(new BigInteger(50), sheet["C1"].Value.Number);
    }

    [Fact]
    public void CircularReference_IsDetected_ForAllCellsInCycle()
    {
        var sheet = new Spreadsheet(5, 5);
        sheet.SetExpression("A1", "B1 + 1");
        sheet.SetExpression("B1", "C1 + 1");
        sheet.SetExpression("C1", "A1 + 1");
        sheet.SetExpression("D1", "A1");

        Assert.Equal(CellErrorKind.CircularReference, sheet["A1"].Value.Error?.Kind);
        Assert.Equal(CellErrorKind.CircularReference, sheet["B1"].Value.Error?.Kind);
        Assert.Equal(CellErrorKind.CircularReference, sheet["C1"].Value.Error?.Kind);
        Assert.Equal(CellErrorKind.DependencyError, sheet["D1"].Value.Error?.Kind);
    }

    [Fact]
    public void SelfReference_IsCircular()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression("A1", "A1 + 1");

        Assert.Equal(CellErrorKind.CircularReference, sheet["A1"].Value.Error?.Kind);
    }

    [Fact]
    public void BreakingCycle_RestoresValues()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression("A1", "B1");
        sheet.SetExpression("B1", "A1");

        sheet.SetExpression("B1", "7");

        Assert.Equal(new BigInteger(7), sheet["A1"].Value.Number);
    }

    [Fact]
    public void ReferenceOutsideTable_IsInvalidReference()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression("A1", "Z100 + 1");

        Assert.Equal(CellErrorKind.InvalidReference, sheet["A1"].Value.Error?.Kind);
    }

    [Fact]
    public void SyntaxError_InReferencedCell_PropagatesAsDependencyError()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression("A1", "2 +");
        sheet.SetExpression("A2", "A1 * 2");

        Assert.Equal(CellErrorKind.Syntax, sheet["A1"].Value.Error?.Kind);
        Assert.Equal(CellErrorKind.DependencyError, sheet["A2"].Value.Error?.Kind);
    }

    [Fact]
    public void RemovingReferencedRow_ReportsImpact_AndProducesInvalidReference()
    {
        var sheet = new Spreadsheet(3, 3);
        sheet.SetExpression("C3", "5");
        sheet.SetExpression("A1", "C3 + 1");

        var impact = sheet.AnalyzeLastRowRemoval();
        sheet.RemoveLastRow();

        Assert.Equal(new[] { CellAddress.Parse("C3") }, impact.NonEmptyCells);
        Assert.Equal(new[] { CellAddress.Parse("A1") }, impact.DependentCells);
        Assert.Equal(2, sheet.RowCount);
        Assert.Equal(CellErrorKind.InvalidReference, sheet["A1"].Value.Error?.Kind);
    }

    [Fact]
    public void AddingColumn_MakesPreviouslyInvalidReferenceValid()
    {
        var sheet = new Spreadsheet(2, 2);
        sheet.SetExpression("A1", "C1 + 1");
        Assert.True(sheet["A1"].Value.IsError);

        sheet.AddColumn();

        Assert.Equal(BigInteger.One, sheet["A1"].Value.Number);
    }

    [Fact]
    public void CannotRemoveLastRemainingRow()
    {
        var sheet = new Spreadsheet(1, 1);

        Assert.Throws<InvalidOperationException>(sheet.RemoveLastRow);
    }

    [Fact]
    public void IsModified_TracksChanges()
    {
        var sheet = new Spreadsheet(2, 2);
        Assert.False(sheet.IsModified);

        sheet.SetExpression("A1", "1");
        Assert.True(sheet.IsModified);

        sheet.MarkAsSaved();
        Assert.False(sheet.IsModified);
    }

    [Fact]
    public void Serialization_RoundTrip_PreservesExpressionsAndSize()
    {
        var sheet = new Spreadsheet(4, 3);
        sheet.SetExpression("A1", "123456789012345678901234567890");
        sheet.SetExpression("C4", "A1 mod 7");

        var restored = SpreadsheetSerializer.Deserialize(SpreadsheetSerializer.Serialize(sheet));

        Assert.Equal(4, restored.RowCount);
        Assert.Equal(3, restored.ColumnCount);
        Assert.Equal("A1 mod 7", restored["C4"].Expression);
        Assert.Equal(sheet["C4"].Value.Number, restored["C4"].Value.Number);
        Assert.False(restored.IsModified);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"format\":\"Other\",\"version\":1,\"rows\":1,\"columns\":1}")]
    [InlineData("{\"format\":\"MyExcel.Spreadsheet\",\"version\":1,\"rows\":0,\"columns\":1}")]
    [InlineData("{\"format\":\"MyExcel.Spreadsheet\",\"version\":1,\"rows\":1,\"columns\":1,\"cells\":[{\"address\":\"B5\",\"expression\":\"1\"}]}")]
    public void Deserialize_InvalidFile_Throws(string json)
    {
        Assert.Throws<InvalidDataException>(() => SpreadsheetSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    public void ColumnNames_AreExcelLike(int index, string name)
    {
        Assert.Equal(name, CellAddress.ColumnIndexToName(index));
        Assert.True(CellAddress.TryColumnNameToIndex(name, out var parsed));
        Assert.Equal(index, parsed);
    }
}
