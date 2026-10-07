using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDialectChoiceTests
{
    [Fact]
    public void Default_IsAnsiWithNoOptions() =>
        default(SqlDialectChoice).ShouldBe(new SqlDialectChoice(SqlDialect.Ansi, SqlDialectOptions.None));

    [Fact]
    public void Conversion_FromADialect_HasNoOptions()
    {
        SqlDialectChoice choice = SqlDialect.MySql;

        choice.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
    }

    [Fact]
    public void Equality_ComparesTheDialectAndTheOptions()
    {
        var ansiQuotes = new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes);

        ansiQuotes.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
        ansiQuotes.ShouldNotBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
        ansiQuotes.ShouldNotBe(new SqlDialectChoice(SqlDialect.MariaDb, SqlDialectOptions.AnsiQuotes));
    }
}
