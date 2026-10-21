using System;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer;

// The split of a script into batches, which needs no server.
public sealed class SqlServerScriptsTests
{
    [Fact]
    public void Batches_LineOfGoAlone_SplitsThere() =>
        SqlServerScripts
            .Batches("CREATE SCHEMA b;\nGO\nCREATE TABLE b.t (id int);\n")
            .ShouldBe(["CREATE SCHEMA b;\n", "CREATE TABLE b.t (id int);\n"]);

    [Fact]
    public void Batches_GoInAnyCaseWithSpaces_SplitsThere() =>
        SqlServerScripts
            .Batches("SELECT 1;\n  go  \nSELECT 2;\nGo\nSELECT 3;\n")
            .ShouldBe(["SELECT 1;\n", "SELECT 2;\n", "SELECT 3;\n"]);

    [Fact]
    public void Batches_GoAtTheEnd_KeepsNoEmptyBatch() =>
        SqlServerScripts.Batches("SELECT 1;\nGO\n\nGO\n").ShouldBe(["SELECT 1;\n"]);

    [Fact]
    public void Batches_LinesEndingInCarriageReturns_SplitsThere() =>
        SqlServerScripts.Batches("SELECT 1;\r\nGO\r\nSELECT 2;\r\n").ShouldBe(["SELECT 1;\r\n", "SELECT 2;\r\n"]);

    [Fact]
    public void Batches_GoInsideALine_DoesNotSplit() =>
        SqlServerScripts.Batches("SELECT 1 AS go\n").ShouldBe(["SELECT 1 AS go\n"]);

    [Fact]
    public void Batches_GoWithACount_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => SqlServerScripts.Batches("SELECT 1\nGO 5\n"))
            .Message.ShouldContain("a count is not supported");
}
