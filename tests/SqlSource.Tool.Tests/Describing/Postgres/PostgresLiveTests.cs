using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The texts the live functions make themselves.  What they do with a server is in tests/SqlSource.Tests.Postgres.
public sealed class PostgresLiveTests
{
    [Fact]
    public void ExecuteText_StatementWithoutParameters_HasNoList() =>
        PostgresLive.ExecuteText(new PostgresColumnsRequest("sqlsource_1", 0)).ShouldBe("EXECUTE sqlsource_1");

    [Fact]
    public void ExecuteText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExecuteText(new PostgresColumnsRequest("sqlsource_7", 3))
            .ShouldBe("EXECUTE sqlsource_7(NULL, NULL, NULL)");

    [Fact]
    public void ExplainText_StatementWithoutParameters_ExplainsTheExecuteAlone() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_1", 0, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");

    [Fact]
    public void ExplainText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExplainText(new PostgresExplainRequest("sqlsource_7", 2, []))
            .ShouldBe("EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_7(NULL, NULL)");

    [Fact]
    public void SetTexts_RequestWithSettings_ForcesTheGenericPlanWithoutPruningThenSetsEach() =>
        PostgresLive
            .SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["enable_nestloop=off", "enable_hashjoin=off"]))
            .ShouldBe([
                "SET LOCAL plan_cache_mode = force_generic_plan",
                "SET LOCAL enable_partition_pruning = off",
                "SET LOCAL enable_nestloop = off",
                "SET LOCAL enable_hashjoin = off",
            ]);

    [Fact]
    public void SetTexts_SettingOutsideTheList_Throws() =>
        Should.Throw<ArgumentException>(() =>
            PostgresLive.SetTexts(new PostgresExplainRequest("sqlsource_1", 0, ["work_mem=1"]))
        );
}
