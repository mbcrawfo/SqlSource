using System;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer;

// The split of a script of SQL Server into batches, which needs no server.
public sealed class EndToEndFilesTests
{
    [Fact]
    public void SqlServerBatches_LineOfGoAlone_SplitsThere() =>
        EndToEndFiles
            .SqlServerBatches("CREATE SCHEMA b;\nGO\nCREATE TABLE b.t (id int);\n")
            .ShouldBe(["CREATE SCHEMA b;\n", "CREATE TABLE b.t (id int);\n"]);

    [Fact]
    public void SqlServerBatches_GoInAnyCaseWithSpaces_SplitsThere() =>
        EndToEndFiles
            .SqlServerBatches("SELECT 1;\n  go  \nSELECT 2;\nGo\nSELECT 3;\n")
            .ShouldBe(["SELECT 1;\n", "SELECT 2;\n", "SELECT 3;\n"]);

    [Fact]
    public void SqlServerBatches_GoAtTheEnd_KeepsNoEmptyBatch() =>
        EndToEndFiles.SqlServerBatches("SELECT 1;\nGO\n\nGO\n").ShouldBe(["SELECT 1;\n"]);

    [Fact]
    public void SqlServerBatches_LinesEndingInCarriageReturns_SplitsThere() =>
        EndToEndFiles.SqlServerBatches("SELECT 1;\r\nGO\r\nSELECT 2;\r\n").ShouldBe(["SELECT 1;\r\n", "SELECT 2;\r\n"]);

    [Fact]
    public void SqlServerBatches_GoInsideALine_DoesNotSplit() =>
        EndToEndFiles.SqlServerBatches("SELECT 1 AS go\n").ShouldBe(["SELECT 1 AS go\n"]);

    [Fact]
    public void SqlServerBatches_GoFollowedByTextOnItsLine_DoesNotSplit() =>
        EndToEndFiles.SqlServerBatches("SELECT 'a\nGO here';\n").ShouldBe(["SELECT 'a\nGO here';\n"]);

    [Fact]
    public void SqlServerBatches_GoInALineComment_DoesNotSplit() =>
        EndToEndFiles.SqlServerBatches("SELECT 1; -- GO\nSELECT 2;\n").ShouldBe(["SELECT 1; -- GO\nSELECT 2;\n"]);

    // A known limit, which sqlcmd shares: the split reads lines and not SQL, so a line of GO alone inside a block
    // comment ends the batch there.  The files of this repository hold no such line.
    [Fact]
    public void SqlServerBatches_LineOfGoAloneInsideABlockComment_Splits() =>
        EndToEndFiles.SqlServerBatches("/*\nGO\n*/").ShouldBe(["/*\n", "*/"]);

    [Fact]
    public void SqlServerBatches_GoWithACount_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => EndToEndFiles.SqlServerBatches("SELECT 1\nGO 5\n"))
            .Message.ShouldContain("a count is not supported");
}
