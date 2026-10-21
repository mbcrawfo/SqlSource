using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The describer of SQL Server where no server is needed: its dialect, and the parts of a connection's value that it
// gives the run to remove.
public sealed class SqlServerDescriberTests
{
    [Fact]
    public void Dialect_Is_SqlServer() => new SqlServerDescriber().Dialect.ShouldBe(SqlDialect.SqlServer);

    [Fact]
    public void SecretsOf_ValueWithAPassword_GivesThePassword() =>
        new SqlServerDescriber().SecretsOf("Data Source=x;Password=Sesame123").ShouldBe(["Sesame123"]);

    [Fact]
    public void SecretsOf_ValueWithoutAPassword_GivesNone() =>
        new SqlServerDescriber().SecretsOf("Data Source=x;Integrated Security=true").ShouldBeEmpty();

    // A value SqlClient cannot read has no parts it knows, and opening it fails in its own place.
    [Theory]
    [InlineData("Data Source=x;Nonsense=1")]
    [InlineData("=;")]
    public void SecretsOf_ValueSqlClientCannotRead_GivesNoneAndDoesNotThrow(string connection) =>
        new SqlServerDescriber().SecretsOf(connection).ShouldBeEmpty();
}
