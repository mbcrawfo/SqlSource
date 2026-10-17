using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A type's name as the sidecar records it: as PostgreSQL spells a type of its catalog, with its facets, and the
// schema and the name as identifiers for any other.
public sealed class PostgresTypeNamesTests
{
    // format_type(oid, -1) of char is "bpchar", and of a char with a length "character(n)".
    [Theory]
    [InlineData("int4", "integer", null, null, null, "integer")]
    [InlineData("varchar", "character varying", null, null, null, "character varying")]
    [InlineData("varchar", "character varying", 100, null, null, "character varying(100)")]
    [InlineData("bpchar", "bpchar", null, null, null, "bpchar")]
    [InlineData("bpchar", "bpchar", 2, null, null, "character(2)")]
    [InlineData("bit", "bit", 8, null, null, "bit(8)")]
    // A server quotes bit without a length, since plain bit means bit(1): format_type(1560, -1) is "bit" with the
    // quotes, and it is kept.
    [InlineData("bit", "\"bit\"", 8, null, null, "bit(8)")]
    [InlineData("bit", "\"bit\"", null, null, null, "\"bit\"")]
    [InlineData("varbit", "bit varying", 5, null, null, "bit varying(5)")]
    [InlineData("numeric", "numeric", null, null, null, "numeric")]
    [InlineData("numeric", "numeric", null, 18, 2, "numeric(18,2)")]
    [InlineData("numeric", "numeric", null, 2, -3, "numeric(2,-3)")]
    [InlineData("numeric", "numeric", null, 10, null, "numeric(10)")]
    [InlineData("timestamp", "timestamp without time zone", null, 3, null, "timestamp(3) without time zone")]
    [InlineData("timestamptz", "timestamp with time zone", null, 3, null, "timestamp(3) with time zone")]
    [InlineData("time", "time without time zone", null, 0, null, "time(0) without time zone")]
    [InlineData("timetz", "time with time zone", null, 6, null, "time(6) with time zone")]
    [InlineData("interval", "interval", null, 6, null, "interval(6)")]
    [InlineData("text", "text", 5, 5, 5, "text")]
    [InlineData("numrange", "numrange", null, null, null, "numrange")]
    public void Spell_TypeOfTheCatalog_IsItsSqlSpellingWithTheFacetsItCarries(
        string typname,
        string formatType,
        int? length,
        int? precision,
        int? scale,
        string expected
    ) =>
        PostgresTypeNames
            .Spell(PostgresRows.BaseOf(1, typname, formatType, "X"), new PostgresFacets(length, precision, scale))
            .ShouldBe(expected);

    // Whatever the session's search_path: the name does not depend on the session.  A facet is not written, since
    // the tool knows how to spell only the modifier of a type of the catalog.
    [Theory]
    [InlineData("public", "user_status", "public.user_status")]
    [InlineData("billing", "money_2", "billing.money_2")]
    [InlineData("My Schema", "Status", "\"My Schema\".\"Status\"")]
    [InlineData("we\"ird", "t\"\"", "\"we\"\"ird\".\"t\"\"\"\"\"")]
    [InlineData("public", "1st", "public.\"1st\"")]
    [InlineData("public", "café", "public.\"café\"")]
    public void Spell_TypeOfAnotherSchema_IsItsSchemaAndNameAsIdentifiers(
        string schema,
        string typname,
        string expected
    ) =>
        PostgresTypeNames
            .Spell(PostgresRows.BaseOf(1, typname, typname, "U", schema), new PostgresFacets(9, 9, 9))
            .ShouldBe(expected);

    [Theory]
    [InlineData("users", "users")]
    [InlineData("_t1", "_t1")]
    [InlineData("Users", "\"Users\"")]
    [InlineData("user name", "\"user name\"")]
    [InlineData("", "\"\"")]
    public void Identifier_Name_IsBareOnlyWhenItIsLowerCaseLettersDigitsAndUnderscores(string name, string expected) =>
        PostgresTypeNames.Identifier(name).ShouldBe(expected);

    [Fact]
    public void ArrayOf_ElementName_IsTheNameAndTheMark() =>
        PostgresTypeNames.ArrayOf("public.email_t").ShouldBe("public.email_t[]");

    // format_type(oid, typmod) of a type of the catalog is the name already.
    [Fact]
    public void WithModifier_TypeOfTheCatalog_IsTheServersTextAsItIs() =>
        PostgresTypeNames.WithModifier(PostgresRows.Varchar, "character varying(10)").ShouldBe("character varying(10)");

    // The tail is what format_type holds after format_type(oid, -1), behind the tool's own schema and name.
    [Fact]
    public void WithModifier_TypeOfAnotherSchema_IsItsSchemaAndNameAndTheTail() =>
        PostgresTypeNames
            .WithModifier(PostgresRows.BaseOf(1, "geometry", "geometry", "U", "public"), "geometry(Point,4326)")
            .ShouldBe("public.geometry(Point,4326)");

    [Fact]
    public void WithModifier_SchemaThatNeedsQuotes_IsQuoted() =>
        PostgresTypeNames
            .WithModifier(PostgresRows.BaseOf(1, "tagged", "tagged", "U", "My Schema"), "tagged(7)")
            .ShouldBe("\"My Schema\".tagged(7)");

    [Fact]
    public void WithModifier_FormattedTextWithoutATail_IsTheSchemaAndName() =>
        PostgresTypeNames
            .WithModifier(PostgresRows.BaseOf(1, "tagged", "tagged", "U", "public"), "tagged")
            .ShouldBe("public.tagged");

    [Fact]
    public void WithModifier_FormattedTextWithAnotherPrefix_Throws() =>
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresTypeNames.WithModifier(PostgresRows.BaseOf(1, "tagged", "tagged", "U", "public"), "other(7)")
            )
            .Message.ShouldSatisfyAllConditions(
                message => message.ShouldContain("tagged"),
                message => message.ShouldContain("other(7)")
            );
}
