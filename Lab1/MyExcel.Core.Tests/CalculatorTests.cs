using System.Numerics;
using MyExcel.Core;

namespace MyExcel.Core.Tests;

public class CalculatorTests
{
    [Theory]
    [InlineData("2 + 3", "5")]
    [InlineData("2 + 3 * 4", "14")]
    [InlineData("(2 + 3) * 4", "20")]
    [InlineData("10 - 4 - 3", "3")]
    [InlineData("100 / 10 / 5", "2")]
    [InlineData("7 / 2", "3")]
    [InlineData("-7 / 2", "-3")]
    [InlineData("7 div 2", "3")]
    [InlineData("-7 div 2", "-4")]
    [InlineData("7 div -2", "-4")]
    [InlineData("7 mod 3", "1")]
    [InlineData("-7 mod 3", "2")]
    [InlineData("7 mod -3", "-2")]
    [InlineData("17 MOD 5 + 1", "3")]
    [InlineData("-5", "-5")]
    [InlineData("+5", "5")]
    [InlineData("--5", "5")]
    [InlineData("2 * -3", "-6")]
    [InlineData("2 - -3", "5")]
    [InlineData("inc(5)", "6")]
    [InlineData("dec(5)", "4")]
    [InlineData("INC(dec(inc(0)))", "1")]
    [InlineData("inc(2) * dec(4)", "9")]
    [InlineData("= 1 + 1", "2")]
    [InlineData("  ( ( 42 ) )  ", "42")]
    public void Evaluate_ReturnsExpectedValue(string expression, string expected)
    {
        var result = Calculator.Evaluate(expression);

        Assert.False(result.IsError, result.Error?.Message);
        Assert.Equal(BigInteger.Parse(expected), result.Number);
    }

    [Fact]
    public void Evaluate_SupportsArbitraryLengthIntegers()
    {
        var result = Calculator.Evaluate("99999999999999999999 * 99999999999999999999");

        Assert.Equal(BigInteger.Parse("9999999999999999999800000000000000000001"), result.Number);
    }

    [Theory]
    [InlineData("2 +")]
    [InlineData("* 3")]
    [InlineData("(1 + 2")]
    [InlineData("1 + 2)")]
    [InlineData("1 2")]
    [InlineData("inc 5")]
    [InlineData("2 ^ 3")]
    [InlineData("A0")]
    [InlineData("")]
    public void CheckSyntax_DetectsErrors(string expression)
    {
        var error = Calculator.CheckSyntax(expression);

        Assert.NotNull(error);
        Assert.Equal(CellErrorKind.Syntax, error.Kind);
    }

    [Fact]
    public void CheckSyntax_ReportsErrorPosition()
    {
        var error = Calculator.CheckSyntax("12 + * 3");

        Assert.NotNull(error);
        Assert.Equal(6, error.Position);
    }

    [Theory]
    [InlineData("5 / 0")]
    [InlineData("5 div (2 - 2)")]
    [InlineData("5 mod 0")]
    public void Evaluate_DivisionByZero_ReturnsError(string expression)
    {
        var result = Calculator.Evaluate(expression);

        Assert.Equal(CellErrorKind.DivisionByZero, result.Error?.Kind);
    }

    [Fact]
    public void Evaluate_ResolvesCellReferences()
    {
        var result = Calculator.Evaluate("a1 * 2 + B10", address => address.ToString() switch
        {
            "A1" => 5,
            "B10" => 1,
            _ => 0,
        });

        Assert.Equal(new BigInteger(11), result.Number);
    }
}
