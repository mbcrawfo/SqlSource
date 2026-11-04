using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlDescribableDialectsTests
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

    // A dialect that becomes describable needs an engine of its own, or its type override is read by another's rules.
    [Fact]
    public void EngineOf_DescribableDialects_EachHasAnEngineOfItsOwn() =>
        SqlDescribableDialects
            .All.Select(SqlDescribableDialects.EngineOf)
            .Distinct()
            .Count()
            .ShouldBe(SqlDescribableDialects.All.Length);

    [Fact]
    public void EngineOf_EveryDialect_IsSqlServerForMssqlAndPostgresOtherwise()
    {
        foreach (var dialect in Enum.GetValues<SqlDialect>())
        {
            SqlDescribableDialects
                .EngineOf(dialect)
                .ShouldBe(
                    dialect == SqlDialect.SqlServer ? DatabaseEngine.SqlServer : DatabaseEngine.PostgreSql,
                    dialect.ToString()
                );
        }
    }

    [Fact]
    public void DialectOf_EachEngine_IsTheDescribableDialectOfThatEngine()
    {
        foreach (var engine in DatabaseEngines.All)
        {
            var dialect = SqlDescribableDialects.DialectOf(engine);

            SqlDescribableDialects.Contains(dialect).ShouldBeTrue(engine.ToString());
            SqlDescribableDialects.EngineOf(dialect).ShouldBe(engine);
        }
    }
}
