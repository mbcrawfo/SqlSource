using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.SqlServerScript;
using static SqlSource.Tool.Tests.SqlServerSessionCases;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The session over a capture the test writes: the calls a describe asks for its columns, "columns" and the
// declarations it sends with them, in order, and what it makes of their answers.  No driver runs: the connection is a
// word that SqlClient could not open.
public sealed class SqlServerSessionColumnTests
{
    // Each column as the server gave it, the hidden row dropped; the table asked for the one origin; and the match.
    [Fact]
    public async Task DescribeAsync_Rows_RecordsColumnsAndMatch()
    {
        const string Sql = "SELECT id, name FROM dbo.users WHERE id = @id";
        var columns = new SqlServerColumnsRequest(Sql, "@id int");
        var table = new SqlServerTableRequest("dbo", "users");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Sql, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null)
            )
            .Add("columns", columns, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null))
            .Add("table", table, new SqlServerColumnsAnswer([IdColumn, NameColumn, Rowstat], null, null));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns", "table"]);
        asked[1].ShouldBe(Call("columns", columns));
        asked[2].ShouldBe(Call("table", table));
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldBe([
            new SidecarColumn(
                0,
                "id",
                Int,
                false,
                SidecarValues.NullableSource.Server,
                new SidecarOrigin("dbo", "users", "id"),
                true,
                false
            ),
            new SidecarColumn(
                1,
                "name",
                Name100,
                false,
                SidecarValues.NullableSource.Server,
                new SidecarOrigin("dbo", "users", "name"),
                false,
                false
            ),
        ]);
        description.MatchesTable.ShouldBe(new SidecarTable("dbo", "users"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }

    // An UPDATE with no OUTPUT: "columns" answers no row, and nothing of a result is recorded.
    [Fact]
    public async Task DescribeAsync_NoRows_IsNone()
    {
        const string Sql = "UPDATE dbo.users SET nickname = N'x' WHERE id = @id";
        var columns = new SqlServerColumnsRequest(Sql, "@id int");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Sql, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
    }

    // The SQL as written, with every parameter of the list declared in its order: a declared one as its declaration
    // reads, a reused one once under its own name, and an inferred one at the type the server gave it, an alias type by
    // its schema and name, each in brackets, and a CLR type of sys by its name.
    [Fact]
    public async Task DescribeAsync_ColumnsParams_DeclareEveryParameter()
    {
        const string Sql =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND email = @email AND node = @node "
            + "AND (name = @name OR nickname = @name)";
        const string Renamed =
            "SELECT id FROM dbo.users WHERE tenant = @tenant AND email = @email AND node = @node "
            + "AND (name = @name_1 OR nickname = @name_2)";
        var columns = new SqlServerColumnsRequest(
            Sql,
            "@tenant int, @email [dbo].[Email], @node hierarchyid, @name nvarchar(100)"
        );
        var script = new SqlServerScript()
            .Add(
                "declared",
                new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int"),
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add(
                "parameters",
                new SqlServerParametersRequest(Renamed, "@tenant int"),
                new SqlServerParametersAnswer(
                    [
                        new SqlServerParameterRow("@email", "nvarchar(320)", 640, 0, 0, "dbo", "Email", null),
                        new SqlServerParameterRow(
                            "@node",
                            "hierarchyid",
                            892,
                            0,
                            0,
                            "sys",
                            "hierarchyid",
                            "Microsoft.SqlServer.Types.SqlHierarchyId, Microsoft.SqlServer.Types"
                        ),
                        Name100Row with
                        {
                            Name = "@name_1",
                        },
                        Name100Row with
                        {
                            Name = "@name_2",
                        },
                    ],
                    null
                )
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", "int")));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        asked.Select(call => call.Method).ShouldBe(["declared", "parameters", "columns"]);
        asked[2].ShouldBe(Call("columns", columns));
    }

    // "columns" that fails is the query's failure at describe columns: a lost connection, and the error rows by their
    // type.
    [Fact]
    public async Task DescribeAsync_ColumnsFails_IsTheFailureAtDescribeColumns()
    {
        const string Sql = "SELECT id FROM dbo.users";
        var lost = await ColumnsAnsweringAsync(Sql, new SqlServerColumnsAnswer(null, null, Lost));
        var dynamic = await ColumnsAnsweringAsync(
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

        lost.Descriptor.Id.ShouldBe("SQLSRC225");
        lost.Step.ShouldBe(DescribeStep.DescribeColumns);
        lost.ServerLines.ShouldBe([ConnectionErrors.LostConnection]);
        dynamic.Descriptor.Id.ShouldBe("SQLSRC264");
        dynamic.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // sp_describe_undeclared_parameters reports a temporary table of a batch of several statements as 208 and 11501;
    // "columns", asked with the declared parameters alone, gives the cause.
    [Fact]
    public async Task DescribeAsync_Refused11501_AsksColumnsWithDeclarationsAlone()
    {
        const string Sql = "SELECT id INTO #t FROM dbo.users WHERE tenant = @tenant AND id = @id; SELECT id FROM #t";
        var parameters = new SqlServerParametersRequest(Sql, "@tenant int");
        var columns = new SqlServerColumnsRequest(Sql, "@tenant int");
        var script = new SqlServerScript()
            .Add(
                "declared",
                new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int"),
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer(
                    null,
                    new SqlServerServerError([
                        new SqlServerErrorRow(208, 16, 1, "Invalid object name '#t'."),
                        new SqlServerErrorRow(11501, 16, 1, "The batch could not be analyzed."),
                    ])
                )
            )
            .Add(
                "columns",
                columns,
                new SqlServerColumnsAnswer(
                    null,
                    [
                        new SqlServerDescribeError(11525, 10, "The metadata could not be determined: '#t'."),
                        new SqlServerDescribeError(11501, 10, "The batch could not be analyzed."),
                    ],
                    null
                )
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", "int")));

        asked.Select(call => call.Method).ShouldBe(["declared", "parameters", "columns"]);
        asked[2].ShouldBe(Call("columns", columns));
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC265");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // With no declared parameter, the declarations of "columns" are none, as those of "parameters" are.
    [Fact]
    public async Task DescribeAsync_Refused11501WithNoDeclaration_AsksColumnsWithNoParams()
    {
        const string Sql = "SELECT id INTO #t FROM dbo.users WHERE id = @id; SELECT id FROM #t";
        var refused = new SqlServerServerError([
            new SqlServerErrorRow(208, 16, 1, "Invalid object name '#t'."),
            new SqlServerErrorRow(11501, 16, 1, "The batch could not be analyzed."),
        ]);
        var columns = new SqlServerColumnsRequest(Sql, null);
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer(null, refused))
            .Add("columns", columns, new SqlServerColumnsAnswer(null, null, Lost));

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        asked[1].ShouldBe(Call("columns", columns));
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe([
            "Msg 208, Level 16, State 1: Invalid object name '#t'.",
            "Msg 11501, Level 16, State 1: The batch could not be analyzed.",
        ]);
    }

    [Fact]
    public async Task DescribeAsync_OtherParametersError_AsksNoColumns()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @p AND name = @p";
        var script = new SqlServerScript().Add(
            "parameters",
            new SqlServerParametersRequest("SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2", null),
            new SqlServerParametersAnswer(
                null,
                new SqlServerServerError([new SqlServerErrorRow(102, 15, 1, "Incorrect syntax near 'name'.")])
            )
        );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters"]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
    }

    // SELECT 1: the server gives the column no name, and nothing is asked after "columns".
    [Fact]
    public async Task DescribeAsync_NullName_Is266()
    {
        const string Sql = "SELECT id, 1 FROM dbo.users";
        var unnamed = IdColumn with
        {
            ColumnOrdinal = 2,
            Name = null,
            SourceDatabase = null,
            SourceSchema = null,
            SourceTable = null,
            SourceColumn = null,
            SourceType = null,
        };
        var script = new SqlServerScript()
            .Add("parameters", new SqlServerParametersRequest(Sql, null), new SqlServerParametersAnswer([], null))
            .Add(
                "columns",
                new SqlServerColumnsRequest(Sql, null),
                new SqlServerColumnsAnswer([IdColumn, unnamed, Rowstat], null, null)
            );

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.Select(call => call.Method).ShouldBe(["parameters", "columns"]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC266");
        failure.Arguments.ShouldBe(["2", Query]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
    }
}
