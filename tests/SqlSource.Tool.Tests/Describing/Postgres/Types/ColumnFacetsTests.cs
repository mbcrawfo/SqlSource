using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Types;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Types;

// A column's facets from the three numbers Npgsql gives, which are the type's modifier decoded.  A type takes the
// numbers it carries and no other, whatever the answer holds.
public sealed class ColumnFacetsTests
{
    private static PostgresColumnRow Column(int? length = null, int? precision = null, int? scale = null) =>
        PostgresTables.ExpressionOf(0, "c", 0, length, precision, scale);

    [Theory]
    [InlineData("bpchar")]
    [InlineData("varchar")]
    [InlineData("bit")]
    [InlineData("varbit")]
    public void Of_TypeWithALength_TakesTheLengthAlone(string typname) =>
        ColumnFacets
            .Of(typname, Column(length: 10, precision: 5, scale: 2))
            .ShouldBe(new PostgresFacets(10, null, null));

    [Theory]
    [InlineData("time")]
    [InlineData("timetz")]
    [InlineData("timestamp")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    public void Of_TypeWithAPrecision_TakesThePrecisionAlone(string typname) =>
        ColumnFacets
            .Of(typname, Column(length: 10, precision: 3, scale: 2))
            .ShouldBe(new PostgresFacets(null, 3, null));

    [Fact]
    public void Of_Numeric_TakesThePrecisionAndTheScale() =>
        ColumnFacets
            .Of("numeric", Column(length: 10, precision: 18, scale: 2))
            .ShouldBe(new PostgresFacets(null, 18, 2));

    // Npgsql gives no scale for a scale of zero, and format_type spells numeric(18,0).
    [Fact]
    public void Of_NumericWithAPrecisionAndNoScale_HasTheScaleZero() =>
        ColumnFacets.Of("numeric", Column(precision: 18)).ShouldBe(new PostgresFacets(null, 18, 0));

    [Fact]
    public void Of_NumericWithoutAModifier_HasNone() =>
        ColumnFacets.Of("numeric", Column()).ShouldBe(PostgresFacets.None);

    // PostgreSQL 15 stores a scale in eleven bits, and Npgsql reads sixteen: -3 arrives as 2045.
    [Theory]
    [InlineData(2045, -3)]
    [InlineData(2047, -1)]
    [InlineData(1024, -1024)]
    [InlineData(1023, 1023)]
    [InlineData(0, 0)]
    public void Of_NumericScale_IsNegativeFrom1024(int given, int expected) =>
        ColumnFacets.Of("numeric", Column(precision: 2, scale: given)).ShouldBe(new PostgresFacets(null, 2, expected));

    // An interval with fields and no precision, "interval year to month", has this modifier.
    [Fact]
    public void Of_IntervalWithThePrecision65535_HasNone() =>
        ColumnFacets.Of("interval", Column(precision: 65535)).ShouldBe(PostgresFacets.None);

    // A type that carries no facet drops what it was given, an array included, to which Npgsql gives the numbers of
    // its element.
    [Theory]
    [InlineData("text")]
    [InlineData("int4")]
    [InlineData("date")]
    [InlineData("uuid")]
    [InlineData("_varchar")]
    [InlineData("_numeric")]
    [InlineData("email_t")]
    public void Of_TypeThatCarriesNoFacet_DropsWhatItWasGiven(string typname) =>
        ColumnFacets.Of(typname, Column(length: 10, precision: 6, scale: 2)).ShouldBe(PostgresFacets.None);

    [Theory]
    [InlineData("varchar")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    public void Of_TypeThatCarriesAFacetWithoutAModifier_HasNone(string typname) =>
        ColumnFacets.Of(typname, Column()).ShouldBe(PostgresFacets.None);
}
