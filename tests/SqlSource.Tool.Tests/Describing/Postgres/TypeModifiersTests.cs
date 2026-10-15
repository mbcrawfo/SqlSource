using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A type modifier as pg_attribute.atttypmod holds it, decoded by type.  Every number below was read from a PostgreSQL
// 18 server with `SELECT atttypmod FROM pg_attribute`.
public class TypeModifiersTests
{
    [Theory]
    [InlineData("varchar", 14, 10, null, null)]
    [InlineData("bpchar", 6, 2, null, null)]
    [InlineData("bit", 5, 5, null, null)]
    [InlineData("varbit", 8, 8, null, null)]
    [InlineData("timestamptz", 3, null, 3, null)]
    [InlineData("timestamp", 6, null, 6, null)]
    [InlineData("timetz", 2, null, 2, null)]
    [InlineData("time", 0, null, 0, null)]
    [InlineData("numeric", 393222, null, 6, 2)]
    // numeric(5,-2): the scale is 11 bits, read as signed.
    [InlineData("numeric", 329730, null, 5, -2)]
    // interval(3): all fields, and the precision in the low 16 bits.
    [InlineData("interval", 2147418115, null, 3, null)]
    // interval year to month: a precision of 0xFFFF is none.
    [InlineData("interval", 458751, null, null, null)]
    // interval day to second(3): the fields in the high bits are left to the server's spelling.
    [InlineData("interval", 470286339, null, 3, null)]
    public void Decode_ModifierAsTheCatalogHoldsIt_IsItsFacets(
        string typname,
        int modifier,
        int? length,
        int? precision,
        int? scale
    ) => TypeModifiers.Decode(typname, modifier).ShouldBe(new PostgresFacets(length, precision, scale));

    [Theory]
    [InlineData("varchar", -1)]
    [InlineData("numeric", -1)]
    [InlineData("interval", -1)]
    [InlineData("int4", 7)]
    [InlineData("text", 14)]
    public void Decode_NoModifierOrATypeWithoutFacets_IsNone(string typname, int modifier) =>
        TypeModifiers.Decode(typname, modifier).ShouldBe(PostgresFacets.None);
}
