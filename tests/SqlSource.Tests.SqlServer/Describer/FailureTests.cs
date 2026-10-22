using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tests.SqlServer.Describer;

// A query whose columns the describer cannot record, against a real server, each described in a session of its own:
// the cause is read from the error_type of the rows of sys.dm_exec_describe_first_result_set, whose rows are the
// server: lines, unless "parameters" failed first and "columns" gave no cause.  The tests hold the ids and the
// numbers, not the server's texts.
[Collection(UsesSqlServer.Name)]
public sealed class FailureTests(SqlServerContainer sqlServer)
{
    // "parameters" refuses the batch with 208 and 11501, and "columns", asked with the declared parameters alone, gives
    // its cause by its type, TEMPORARY_TABLE: the lines are the rows of "columns", not the errors of "parameters".
    [Fact]
    public async Task DescribeAsync_TemporaryTableInTwoStatements_IsSqlsrc265WithTheProceduresRows()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "CREATE TABLE #y (a int NOT NULL); SELECT a FROM #y"),
            "SQLSRC265"
        );

        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.Help.ShouldBe(SqlServerErrors.TemporaryTableHelp);
        failure.ServerLines[0].ShouldStartWith("Msg 11525: ");
        failure.ServerLines.ShouldNotContain(line => line.StartsWith("Msg 208", StringComparison.Ordinal));
    }

    // SqlSource reads @s as a variable, as the server does, so the list is empty.
    [Fact]
    public async Task DescribeAsync_ExecOfAVariable_IsSqlsrc264()
    {
        var failure = await ShouldFailAsync(
            Listed("DECLARE @s nvarchar(max) = N'SELECT 1 AS a'; EXEC(@s)"),
            "SQLSRC264"
        );

        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines[0].ShouldStartWith("Msg 11513: ");
    }

    [Fact]
    public async Task DescribeAsync_IfOfTwoShapes_IsSqlsrc263()
    {
        var failure = await ShouldFailAsync(
            Of(SqlDialectRules.SqlServer, "IF @a = 1 SELECT id FROM dbo.users ELSE SELECT name FROM dbo.users"),
            "SQLSRC263"
        );

        failure.Arguments.ShouldBe([Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines[0].ShouldStartWith("Msg 11509: ");
    }

    // The server does not refuse two paths whose columns differ in their names alone: it gives the column no name,
    // which the tool reports as a column with no name.
    [Fact]
    public async Task DescribeAsync_IfWhosePathsDifferInAColumnsNameAlone_IsSqlsrc266()
    {
        var failure = await ShouldFailAsync(
            Of(
                SqlDialectRules.SqlServer,
                "IF @a = 1 SELECT id, name AS x FROM dbo.users ELSE SELECT id, name AS y FROM dbo.users"
            ),
            "SQLSRC266"
        );

        failure.Arguments.ShouldBe(["2", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBeEmpty();
    }

    // An OUTPUT with no INTO on a table with a trigger is no shape the user mends by a diagnostic of its own.
    // "parameters" refuses it with 334 and 11501, and "columns", asked after, answers 334 and 11501 as rows, neither of
    // a type that gives a cause, so the failure is that of "parameters", with its lines.
    [Fact]
    public async Task DescribeAsync_OutputOnATableWithATrigger_IsSqlsrc225Naming334()
    {
        var (described, capture) = await Sessions.DescribeCapturedAsync(
            sqlServer.ConnectionString,
            Of(SqlDialectRules.SqlServer, "INSERT INTO dbo.trig (id, v) OUTPUT inserted.id VALUES (@id, @v)")
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225", string.Join("; ", failure.ServerLines));
        failure.Arguments[0].ShouldBe(Query);
        failure.ServerLines[0].ShouldStartWith("Msg 334, ");
        failure.ServerLines[^1].ShouldStartWith("Msg 11501, ");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        capture.Calls.Select(static call => call.Method).ShouldBe(["open", "parameters", "columns"]);
        var columns = capture
            .Calls[^1]
            .Answer.Deserialize<SqlServerColumnsAnswer>(CaptureJson.Options)
            .ShouldNotBeNull();
        columns.Errors.ShouldNotBeNull().Select(static error => error.Number).ShouldBe([334, 11501]);
    }

    private async Task<DescribeFailure> ShouldFailAsync(DescribeRequest request, string id)
    {
        var described = await Sessions.DescribeAsync(sqlServer.ConnectionString, request);

        var failure = described.Failure.ShouldNotBeNull(
            string.Join(", ", described.Value?.Columns?.Select(column => column.Name) ?? [])
        );
        failure.Descriptor.Id.ShouldBe(id, string.Join("; ", failure.ServerLines));
        return failure;
    }
}
