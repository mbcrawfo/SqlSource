using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// An internal enum cannot be the parameter of a public test method, so a row gives the dialect's name.
public class SqlDialectNameTests
{
    [Theory]
    [InlineData("ansi", nameof(SqlDialect.Ansi))]
    [InlineData("mssql", nameof(SqlDialect.SqlServer))]
    [InlineData("sqlserver", nameof(SqlDialect.SqlServer))]
    [InlineData("tsql", nameof(SqlDialect.SqlServer))]
    [InlineData("postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("postgresql", nameof(SqlDialect.PostgreSql))]
    [InlineData("mysql", nameof(SqlDialect.MySql))]
    [InlineData("mariadb", nameof(SqlDialect.MariaDb))]
    [InlineData("sqlite", nameof(SqlDialect.Sqlite))]
    [InlineData("oracle", nameof(SqlDialect.Oracle))]
    // Any case, and surrounding whitespace as a value written on a line of its own has it.
    [InlineData("MSSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MySql", nameof(SqlDialect.MySql))]
    [InlineData("  oracle\t", nameof(SqlDialect.Oracle))]
    [InlineData("\n    sqlite\n", nameof(SqlDialect.Sqlite))]
    public void TryParse_NameOrAlias_GivesItsDialect(string name, string expected)
    {
        SqlDialectName.TryParse(name, out var dialect).ShouldBeTrue();

        dialect.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData("my sql")]
    [InlineData("postgres;mysql")]
    [InlineData("oracle,")]
    [InlineData("SqlDialect.Oracle")]
    [InlineData("3")]
    // Turkish dotless i: the comparison is ordinal, so no culture turns this into "sqlite".
    [InlineData("sqlıte")]
    public void TryParse_AnythingElse_IsRejected(string? name)
    {
        SqlDialectName.TryParse(name, out var dialect).ShouldBeFalse();

        dialect.ShouldBe(SqlDialect.Ansi);
    }

    [Fact]
    public void TryParse_Span_ReadsTheSameNames()
    {
        SqlDialectName.TryParse("dialect=MariaDB".AsSpan(8), out var dialect).ShouldBeTrue();

        dialect.ShouldBe(SqlDialect.MariaDb);
    }

    [Fact]
    public void TryParse_EveryDialect_HasAName()
    {
        string[] names = ["ansi", "mssql", "postgres", "mysql", "mariadb", "sqlite", "oracle"];

        var parsed = names.Select(name =>
        {
            SqlDialectName.TryParse(name, out var dialect).ShouldBeTrue();
            return dialect;
        });

        parsed.ShouldBe(Enum.GetValues<SqlDialect>());
    }

    [Fact]
    public void Accepted_ListsTheNameOfEveryDialect() =>
        SqlDialectName.Accepted.ShouldBe("ansi, mssql, postgres, mysql, mariadb, sqlite and oracle");

    [Fact]
    public void Ansi_IsTheDefaultValue() => default(SqlDialect).ShouldBe(SqlDialect.Ansi);
}
