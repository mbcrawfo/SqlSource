using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.SqlServerSessionCases;
using Calls = SqlSource.Tool.Describing.SqlServer.SqlServerCallNames;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// "batch", asked after "columns" refuses with 11501 a query that has no parameter and that "parameters" took: the
// function analyses its text as the body of a parameterized batch, where CREATE SCHEMA is a syntax error, and the
// procedure as its own batch, as the query runs.
public sealed class SqlServerSessionBatchTests
{
    private const string CreateSchema = "CREATE SCHEMA raw AUTHORIZATION dbo;";

    // The error rows the function answers for CREATE SCHEMA.
    private static readonly SqlServerColumnsAnswer Refused = new(
        null,
        [
            new SqlServerDescribeError(156, 2, "Incorrect syntax near the keyword 'schema'."),
            new SqlServerDescribeError(11501, 2, "The batch could not be analyzed because of compile errors."),
        ],
        null
    );

    // A describe of CREATE SCHEMA, whose "batch" answers as given, and the calls it asked after "open".
    private static Task<(
        Outcome<QueryDescription, DescribeFailure> Described,
        IReadOnlyList<(string Method, string Request)> Asked
    )> BatchAnsweringAsync(SqlServerBatchAnswer answer) =>
        new SqlServerScript()
            .NoParametersThenColumns(CreateSchema, Refused)
            .Batch(new SqlServerBatchRequest(CreateSchema), answer)
            .DescribeAsync(Of(SqlDialectRules.SqlServer, CreateSchema));

    [Fact]
    public async Task DescribeAsync_BatchAnswersNoColumn_IsNone()
    {
        var (described, asked) = await BatchAnsweringAsync(new SqlServerBatchAnswer(0, null));

        asked.Select(call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns, Calls.Batch]);
        asked[2].ShouldBe(Call(Calls.Batch, new SqlServerBatchRequest(CreateSchema)));
        var description = described.Value.ShouldNotBeNull(described.Failure?.Descriptor.Id);
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Parameters.ShouldBeEmpty();
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
    }

    // The procedure analyses the text as the query runs, so what it raises is the failure, with its lines.
    [Fact]
    public async Task DescribeAsync_BatchFails_IsItsFailureAtDescribeColumns()
    {
        var (described, _) = await BatchAnsweringAsync(
            new SqlServerBatchAnswer(
                null,
                new SqlServerServerError([
                    new SqlServerErrorRow(15151, 16, 1, "Cannot find the user 'nobody'."),
                    new SqlServerErrorRow(11501, 16, 1, "The batch could not be analyzed because of compile errors."),
                ])
            )
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.Arguments.ShouldBe([Query, "Cannot find the user 'nobody'."]);
        failure.ServerLines.ShouldBe([
            "Msg 15151, Level 16, State 1: Cannot find the user 'nobody'.",
            "Msg 11501, Level 16, State 1: The batch could not be analyzed because of compile errors.",
        ]);
    }

    // Columns that only the procedure describes have none of the tool's own facts, so the function's refusal stands.
    [Fact]
    public async Task DescribeAsync_BatchAnswersColumns_IsTheRefusalOfColumns()
    {
        var (described, _) = await BatchAnsweringAsync(new SqlServerBatchAnswer(1, null));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe([
            "Msg 156: Incorrect syntax near the keyword 'schema'.",
            "Msg 11501: The batch could not be analyzed because of compile errors.",
        ]);
    }

    // A query with a parameter runs through sp_executesql, which refuses what the function refuses: no "batch".
    [Fact]
    public async Task DescribeAsync_RefusedWithParameters_AsksNoBatch()
    {
        const string Sql = "CREATE SCHEMA raw AUTHORIZATION dbo; SELECT @id AS id;";
        var script = new SqlServerScript()
            .Parameters(
                new SqlServerParametersRequest(Sql, null, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null)
            )
            .Columns(new SqlServerColumnsRequest(Sql, "@id int"), Refused);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns]);
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC225");
    }

    // Error rows whose type gives a cause the user mends otherwise, dynamic SQL here, are no syntax error: no "batch".
    [Fact]
    public async Task DescribeAsync_RefusedWithACause_AsksNoBatch()
    {
        const string Sql = "EXEC (N'SELECT 1 AS a')";
        var script = new SqlServerScript().NoParametersThenColumns(
            Sql,
            new SqlServerColumnsAnswer(
                null,
                [
                    new SqlServerDescribeError(
                        11514,
                        4,
                        "The metadata could not be determined because of dynamic SQL."
                    ),
                    new SqlServerDescribeError(11501, 4, "The batch could not be analyzed."),
                ],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns]);
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC264");
    }

    // Error rows without 11501 are no batch the function could not compile: no "batch".
    [Fact]
    public async Task DescribeAsync_RefusedWithout11501_AsksNoBatch()
    {
        const string Sql = "EXEC";
        var script = new SqlServerScript().NoParametersThenColumns(
            Sql,
            new SqlServerColumnsAnswer(
                null,
                [
                    new SqlServerDescribeError(2812, 1, "Could not find stored procedure ''."),
                    new SqlServerDescribeError(11529, 1, "The metadata could not be determined."),
                ],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe([Calls.Parameters, Calls.Columns]);
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC225");
    }
}
