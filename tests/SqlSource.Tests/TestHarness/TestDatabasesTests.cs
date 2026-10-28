using System;
using System.Data.Common;
using System.Linq;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;

namespace SqlSource.Tests.TestHarness;

public sealed class TestDatabasesTests
{
    [Theory]
    [InlineData("Server=127.0.0.1,1433;Database=master;User Id=sa;Password=p;TrustServerCertificate=True", "fixtures")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=p", "app")]
    public void ConnectionStringOf_TemplateWithADatabase_ReplacesItAndKeepsTheRest(string template, string database)
    {
        var built = new DbConnectionStringBuilder
        {
            ConnectionString = TestDatabases.ConnectionStringOf(template, database),
        };
        var expected = new DbConnectionStringBuilder { ConnectionString = template };

        built["Database"].ShouldBe(database);
        built.Count.ShouldBe(expected.Count);
        built.Keys.Cast<string>().Count(key => key.Equals("Database", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
    }

    [Fact]
    public void DefaultOf_Databases_IsTheFirstName() =>
        TestDatabases.DefaultOf([("fixtures", []), ("app", [])], typeof(TestDatabasesTests)).ShouldBe("fixtures");

    [Fact]
    public void DefaultOf_NoDatabase_ThrowsNamingTheContainer() =>
        Should
            .Throw<InvalidOperationException>(() => TestDatabases.DefaultOf([], typeof(TestDatabasesTests)))
            .Message.ShouldContain(nameof(TestDatabasesTests));
}
