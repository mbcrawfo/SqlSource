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
    [InlineData("cockroachdb", nameof(SqlDialect.CockroachDb))]
    [InlineData("cockroach", nameof(SqlDialect.CockroachDb))]
    [InlineData("CockroachDB", nameof(SqlDialect.CockroachDb))]
    // Any case, and surrounding whitespace as a value written on a line of its own has it.
    [InlineData("MSSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("Postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MySql", nameof(SqlDialect.MySql))]
    [InlineData("  oracle\t", nameof(SqlDialect.Oracle))]
    [InlineData("\n    sqlite\n", nameof(SqlDialect.Sqlite))]
    public void TryParse_NameOrAlias_GivesItsDialect(string name, string expected)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeTrue();

        choice.Dialect.ToString().ShouldBe(expected);
        choice.Options.ShouldBe(SqlDialectOptions.None);
    }

    // The options of a flags enum print in the order of their values, separated by a comma and a space.
    [Theory]
    [InlineData("mysql,ansi-quotes", "MySql", "AnsiQuotes")]
    [InlineData("mysql,no-backslash-escapes", "MySql", "NoBackslashEscapes")]
    [InlineData("mysql,ansi-quotes,no-backslash-escapes", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("mariadb,ansi-quotes", "MariaDb", "AnsiQuotes")]
    // In any order, and one that is repeated counts once.
    [InlineData("mariadb,no-backslash-escapes,ansi-quotes", "MariaDb", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("mysql,ansi-quotes,ansi_quotes,ANSI-QUOTES", "MySql", "AnsiQuotes")]
    // As the server prints sql_mode.
    [InlineData("mysql,ANSI_QUOTES,NO_BACKSLASH_ESCAPES", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("MariaDB,Ansi-Quotes", "MariaDb", "AnsiQuotes")]
    // Whitespace around a part, as a value written over several lines has it.
    [InlineData("  mysql , ansi-quotes ,\tno_backslash_escapes\n", "MySql", "AnsiQuotes, NoBackslashEscapes")]
    [InlineData("\n    mysql,\n    ansi-quotes\n", "MySql", "AnsiQuotes")]
    public void TryParse_NameWithOptions_GivesTheDialectAndItsOptions(string value, string dialect, string options)
    {
        SqlDialectName.TryParse(value, out var choice).ShouldBeTrue();

        choice.Dialect.ToString().ShouldBe(dialect);
        choice.Options.ToString().ShouldBe(options);
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
    // An empty part.
    [InlineData("mysql,")]
    [InlineData("mysql,,ansi-quotes")]
    [InlineData("mysql,ansi-quotes,")]
    [InlineData("mysql, ")]
    [InlineData(",mysql")]
    // An option that does not exist.
    [InlineData("mysql,ansi")]
    [InlineData("mysql,ansi quotes")]
    [InlineData("mysql,ansi-quotes=on")]
    [InlineData("mysql,mariadb")]
    // An option of another dialect, and an option with no dialect.
    [InlineData("postgres,ansi-quotes")]
    [InlineData("ansi,no-backslash-escapes")]
    [InlineData("oracle,ansi_quotes")]
    [InlineData("cockroachdb,ansi-quotes")]
    [InlineData("crdb")]
    [InlineData("ansi-quotes")]
    [InlineData("ansi-quotes,mysql")]
    // Another separator.
    [InlineData("mysql;ansi-quotes")]
    [InlineData("mysql ansi-quotes")]
    [InlineData("mysql+ansi-quotes")]
    // Turkish dotless i in an option.
    [InlineData("mysql,ansı-quotes")]
    // Turkish dotless i: the comparison is ordinal, so no culture turns this into "sqlite".
    [InlineData("sqlıte")]
    public void TryParse_AnythingElse_IsRejected(string? name)
    {
        SqlDialectName.TryParse(name, out var choice).ShouldBeFalse();

        choice.ShouldBe(default);
    }

    [Fact]
    public void TryParse_Span_ReadsTheSameNamesAndOptions()
    {
        SqlDialectName.TryParse("dialect=MariaDB".AsSpan(8), out var plain).ShouldBeTrue();
        SqlDialectName.TryParse("dialect=MariaDB,ansi-quotes".AsSpan(8), out var withOption).ShouldBeTrue();

        plain.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.None));
        withOption.ShouldBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.AnsiQuotes));
    }

    [Fact]
    public void TryParse_EveryDialect_HasAName()
    {
        string[] names = ["ansi", "mssql", "postgres", "mysql", "mariadb", "sqlite", "oracle", "cockroachdb"];

        var parsed = names.Select(name =>
        {
            SqlDialectName.TryParse(name, out var choice).ShouldBeTrue();
            return choice.Dialect;
        });

        parsed.ShouldBe(Enum.GetValues<SqlDialect>());
    }

    [Fact]
    public void Accepted_ListsTheNameOfEveryDialect() =>
        SqlDialectName.Accepted.ShouldBe("ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle");

    [Fact]
    public void Ansi_IsTheDefaultValue() => default(SqlDialect).ShouldBe(SqlDialect.Ansi);
}
