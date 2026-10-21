using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests;

// The request a test of a describer builds: its parameters as the plan of a run lists them, or as the test gives them.
public sealed class DescribeRequestsTests
{
    [Fact]
    public void Of_SqlWithAParameterTwice_ListsItOnceInTheOrderItFirstAppears()
    {
        var request = Of("SELECT @b, @a, @B");

        request.Name.ShouldBe("Q");
        request.Parameters.Select(parameter => parameter.Name).ShouldBe(["b", "a"]);
        request.Parameters.ShouldAllBe(parameter => !parameter.IsDeclared);
    }

    // A bracketed name is quoted under the rules of SQL Server, and plain text under those of PostgreSQL.
    [Fact]
    public void Of_SqlServerRules_ReadNoParameterInABracketedName()
    {
        const string Sql = "SELECT [@a], @b";

        Of(SqlDialectRules.SqlServer, Sql).Parameters.ShouldBe([Inferred("b")]);
        Of(SqlDialectRules.PostgreSql, Sql).Parameters.ShouldBe([Inferred("a"), Inferred("b")]);
    }

    [Fact]
    public void Of_DeclaredParameterTheSqlLacks_IsListedAfterTheSqlsOwn()
    {
        var request = Of("SELECT @a", ("z", "int4"));

        request.Parameters.ShouldBe([Inferred("a"), Declared("z", "int4")]);
    }

    [Fact]
    public void Of_DeclaredParameterTheSqlHolds_IsDeclaredInItsPlace()
    {
        var request = Of("SELECT @a", ("A", "text"));

        request.Parameters.ShouldHaveSingleItem().ShouldBe(Declared("a", "text"));
    }

    [Fact]
    public void Listed_Parameters_AreKeptAsGiven()
    {
        var request = Listed("SELECT 1", Declared("x", "int4"), Inferred("y"));

        request.Name.ShouldBe("Q");
        request.Sql.ShouldBe("SELECT 1");
        request.Parameters.ShouldBe([Declared("x", "int4"), Inferred("y")]);
    }
}
