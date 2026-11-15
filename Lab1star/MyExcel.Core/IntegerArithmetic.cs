using System.Numerics;

namespace MyExcel.Core;

public static class IntegerArithmetic
{
    public static BigInteger TruncatedDivide(BigInteger dividend, BigInteger divisor) =>
        BigInteger.Divide(dividend, divisor);

    public static BigInteger FloorDivide(BigInteger dividend, BigInteger divisor)
    {
        var quotient = BigInteger.DivRem(dividend, divisor, out var remainder);
        var hasOppositeSigns = remainder.Sign * divisor.Sign < 0;
        return hasOppositeSigns ? quotient - BigInteger.One : quotient;
    }

    public static BigInteger FloorModulo(BigInteger dividend, BigInteger divisor) =>
        dividend - divisor * FloorDivide(dividend, divisor);
}
