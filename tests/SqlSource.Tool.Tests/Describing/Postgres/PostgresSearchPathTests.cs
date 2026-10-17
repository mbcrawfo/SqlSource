using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Whether the session finds pg_catalog's functions first, from the search_path that "open" read: PostgreSQL searches
// pg_catalog first unless the path names it later.
public sealed class PostgresSearchPathTests
{
    [Theory]
    [InlineData("\"$user\", public")]
    [InlineData("public")]
    [InlineData("")]
    [InlineData("pg_catalog, public")]
    [InlineData("\"pg_catalog\", public")]
    [InlineData("PG_CATALOG, public")]
    [InlineData("\"Pg_Catalog\", public")]
    [InlineData("  pg_catalog ,public")]
    public void PutsCatalogFirst_PathThatNamesTheCatalogFirstOrNotAtAll_IsTrue(string searchPath) =>
        PostgresSearchPath.PutsCatalogFirst(searchPath).ShouldBeTrue();

    [Theory]
    [InlineData("public, pg_catalog")]
    [InlineData("\"$user\", public, pg_catalog")]
    [InlineData("shadow, pg_catalog, public")]
    [InlineData("pg_temp, pg_catalog")]
    [InlineData("\"a,b\", \"pg_catalog\"")]
    [InlineData("\"unclosed, pg_catalog")]
    // A quoted and a bare piece of one element are one schema: pg_catalog, named second.
    [InlineData("public, \"pg_\"catalog")]
    [InlineData(null)]
    public void PutsCatalogFirst_PathThatNamesAnotherSchemaFirstOrCannotBeRead_IsFalse(string? searchPath) =>
        PostgresSearchPath.PutsCatalogFirst(searchPath).ShouldBeFalse();
}
