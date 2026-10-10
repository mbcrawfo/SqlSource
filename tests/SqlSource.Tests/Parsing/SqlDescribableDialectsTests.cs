using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public class SqlDescribableDialectsTests
{
    // SQLSRC209 names these two in its message.
    [Fact]
    public void All_Dialects_ArePostgresAndSqlServer() =>
        SqlDescribableDialects.All.Select(SqlDialectName.Canonical).ShouldBe(["postgres", "mssql"]);

    [Fact]
    public void Contains_EveryDialect_IsTrueForTheTwoAlone()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            SqlDescribableDialects
                .Contains(dialect)
                .ShouldBe(dialect is SqlDialect.PostgreSql or SqlDialect.SqlServer, dialect.ToString());
        }
    }
}
