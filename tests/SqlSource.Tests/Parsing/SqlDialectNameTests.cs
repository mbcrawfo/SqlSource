using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// An internal enum cannot be the parameter of a public test method, so a row gives the dialect's name.
public sealed class SqlDialectNameTests
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
    [InlineData("mysql,ansi-quotes,ansi_quotes", "MySql", "AnsiQuotes")]
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

    // The version of a MySQL or MariaDB server, as a versioned comment writes it.
    [Theory]
    [InlineData("mysql,version=8.0.36", "MySql", "None", 80036)]
    [InlineData("mariadb, version = 10.11.6 ", "MariaDb", "None", 101106)]
    [InlineData("MySQL,VERSION=8.4.0,ansi-quotes", "MySql", "AnsiQuotes", 80400)]
    [InlineData("mysql,ansi_quotes,version=5.7.44,Version=5.7.44", "MySql", "AnsiQuotes", 50744)]
    [InlineData("mysql,version=08.04.00", "MySql", "None", 80400)]
    [InlineData("mariadb,version=11.4.10", "MariaDb", "None", 110410)]
    [InlineData("mysql", "MySql", "None", 0)]
    public void TryParse_NameWithAVersion_GivesTheVersionOfItsServer(
        string value,
        string dialect,
        string options,
        int version
    )
    {
        SqlDialectName.TryParse(value, out var choice).ShouldBeTrue();

        choice.Dialect.ToString().ShouldBe(dialect);
        choice.Options.ToString().ShouldBe(options);
        choice.ServerVersion.ShouldBe(version);
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
    // A version that is not one: of another dialect, without three numbers of one or two digits, of major 0, two
    // versions, or a word that is not version.
    [InlineData("postgres,version=16.1.0")]
    [InlineData("ansi,version=8.0.36")]
    [InlineData("mysql,version=8.0")]
    [InlineData("mysql,version=8")]
    [InlineData("mysql,version=8.0.36.1")]
    [InlineData("mysql,version=8.0.100")]
    [InlineData("mysql,version=0.1.2")]
    [InlineData("mysql,version=")]
    [InlineData("mysql,version")]
    [InlineData("mysql,version=8.0.x")]
    [InlineData("mysql,version=8 .0.36")]
    [InlineData("mysql,version=8.0.36 x")]
    [InlineData("mysql,version 8.0.36")]
    [InlineData("mysql,versions=8.0.36")]
    [InlineData("mysql,version=٨.0.36")]
    [InlineData("mysql,version=8.0.36,version=8.4.0")]
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
        SqlDialectName.TryParse("dialect: MariaDB".AsSpan(9), out var plain).ShouldBeTrue();
        SqlDialectName.TryParse("dialect: MariaDB,ansi-quotes".AsSpan(9), out var withOption).ShouldBeTrue();

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
    public void Accepted_OfEveryDialect_ListsItsName() =>
        SqlDialectName.Accepted.ShouldBe("ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle");

    [Theory]
    [InlineData("Ansi", "ansi")]
    [InlineData("SqlServer", "mssql")]
    [InlineData("PostgreSql", "postgres")]
    [InlineData("CockroachDb", "cockroachdb")]
    [InlineData("MySql", "mysql")]
    [InlineData("MariaDb", "mariadb")]
    [InlineData("Sqlite", "sqlite")]
    [InlineData("Oracle", "oracle")]
    public void Canonical_Dialect_IsItsFirstName(string dialect, string expected)
    {
        var name = SqlDialectName.Canonical(Enum.Parse<SqlDialect>(dialect));

        name.ShouldBe(expected);
        SqlDialectName.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.Dialect.ShouldBe(Enum.Parse<SqlDialect>(dialect));
    }

    [Fact]
    public void Ansi_AsADialect_IsTheDefaultValue() => default(SqlDialect).ShouldBe(SqlDialect.Ansi);
}
