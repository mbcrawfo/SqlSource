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

// The session over a capture the test writes: the calls a describe asks for its parameters, "parameters", the probe of
// each copy and "declared", in order, and what it makes of their answers.  No driver runs: the connection is a word
// that SqlClient could not open.
public sealed class SqlServerSessionParameterTests
{
    [Fact]
    public async Task DescribeAsync_TwoInferredParameters_AsksParametersOnceWithNoDeclarations()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE id = @id AND name = @name";
        var parameters = new SqlServerParametersRequest(Sql, null);
        var columns = new SqlServerColumnsRequest(Sql, "@id int, @name nvarchar(100)");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@id" }, Name100Row with { Name = "@name" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([Call("parameters", parameters), Call("columns", columns)]);
        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Parameters.ShouldBe([
            new DescribedParameter("id", Int, SidecarValues.TypeSource.Inferred),
            new DescribedParameter("name", Name100, SidecarValues.TypeSource.Inferred),
        ]);
    }

    // The optional filter: the copy in "IS NULL" binds as xml and as int, so it types nothing and is ignored; the copy
    // compared with the column fails as xml, and is not probed as int.
    [Fact]
    public async Task DescribeAsync_OptionalFilter_ProbesEachCopyAndTakesTheTypeOfTheOneThatConstrains()
    {
        const string Sql = "SELECT id FROM dbo.users WHERE (@name IS NULL OR name = @name)";
        const string Renamed = "SELECT id FROM dbo.users WHERE (@name_1 IS NULL OR name = @name_2)";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var xmlFirst = new SqlServerProbeRequest(Renamed, "@name_1 xml, @name_2 nvarchar(100)");
        var intFirst = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 nvarchar(100)");
        var xmlSecond = new SqlServerProbeRequest(Renamed, "@name_1 int, @name_2 xml");
        var columns = new SqlServerColumnsRequest(Sql, "@name nvarchar(100)");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer(
                    [IntRow with { Name = "@name_1" }, Name100Row with { Name = "@name_2" }],
                    null
                )
            )
            .Add("probe", xmlFirst, new SqlServerProbeAnswer(true, null))
            .Add("probe", intFirst, new SqlServerProbeAnswer(true, null))
            .Add("probe", xmlSecond, new SqlServerProbeAnswer(false, null))
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([
            Call("parameters", parameters),
            Call("probe", xmlFirst),
            Call("probe", intFirst),
            Call("probe", xmlSecond),
            Call("columns", columns),
        ]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([
                new DescribedParameter("name", Name100, SidecarValues.TypeSource.InferredFromCopies),
            ]);
    }

    [Fact]
    public async Task DescribeAsync_CopiesAgree_AsksNoProbe()
    {
        const string Sql = "SELECT id FROM dbo.orders WHERE id = @p OR user_id = @p";
        const string Renamed = "SELECT id FROM dbo.orders WHERE id = @p_1 OR user_id = @p_2";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var columns = new SqlServerColumnsRequest(Sql, "@p int");
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, IntRow with { Name = "@p_2" }], null)
            )
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql));

        asked.ShouldBe([Call("parameters", parameters), Call("columns", columns)]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([new DescribedParameter("p", Int, SidecarValues.TypeSource.InferredFromCopies)]);
    }

    // The declared type is the server's reading of the declaration, asked before "parameters", which then leaves the
    // declared parameter out of its answer.
    [Fact]
    public async Task DescribeAsync_DeclaredParameter_AsksDeclaredFirstThenParametersUnderItsDeclaration()
    {
        const string Sql = "SELECT id FROM dbo.orders WHERE user_id = @tenant AND id = @id";
        var declared = new SqlServerDeclaredRequest("SELECT @tenant AS [1]", "@tenant int");
        var parameters = new SqlServerParametersRequest(Sql, "@tenant int");
        var columns = new SqlServerColumnsRequest(Sql, "@tenant int, @id int");
        var script = new SqlServerScript()
            .Add(
                "declared",
                declared,
                new SqlServerDeclaredAnswer(
                    [new SqlServerDeclaredRow(1, "int", 4, 10, 0, null, null, null)],
                    null,
                    null
                )
            )
            .Add("parameters", parameters, new SqlServerParametersAnswer([IntRow with { Name = "@id" }], null))
            .Add("columns", columns, NoRows);

        var (described, asked) = await script.DescribeAsync(Of(SqlDialectRules.SqlServer, Sql, ("tenant", " int ")));

        asked.ShouldBe([Call("declared", declared), Call("parameters", parameters), Call("columns", columns)]);
        described
            .Value.ShouldNotBeNull()
            .Parameters.ShouldBe([
                new DescribedParameter("tenant", Int, SidecarValues.TypeSource.Declared),
                new DescribedParameter("id", Int, SidecarValues.TypeSource.Inferred),
            ]);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredTypeTheGrammarRefuses_IsSqlsrc228AndAsksNothing()
    {
        var (described, asked) = await new SqlServerScript().DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p", ("p", "int; DROP TABLE dbo.victim"))
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[0].ShouldBe("int; DROP TABLE dbo.victim");
        failure.Arguments[3].ShouldBe(DescribeFailures.NotATypeName + ": " + DeclaredTypeText.Semicolon);
        asked.ShouldBeEmpty();
    }

    // A list that lacks a parameter the server reports, as SqlSource's reading of a DECLARE can make one: the list is
    // the test's.
    [Fact]
    public async Task DescribeAsync_ServerReportsAParameterTheListLacks_IsSqlsrc231()
    {
        const string Sql = "SELECT TOP (@n) id, @p + 1 AS q FROM dbo.users";
        var parameters = new SqlServerParametersRequest(Sql, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer(
                [new SqlServerParameterRow("@n", "bigint", 8, 19, 0, null, null, null), IntRow with { Name = "@p" }],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(Listed(Sql, Inferred("n")));

        asked.ShouldBe([Call("parameters", parameters)]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            "SQL Server",
            Query,
            "SQL Server reads '@p' as a parameter and SqlSource does not",
        ]);
    }

    // The lists are compared before any probe: a text the two read differently is not probed.
    [Fact]
    public async Task DescribeAsync_ListsDifferAndCopiesDisagree_IsSqlsrc231AndAsksNoProbe()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2 AND id = @q";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var script = new SqlServerScript().Add(
            "parameters",
            parameters,
            new SqlServerParametersAnswer(
                [IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }, IntRow with { Name = "@q" }],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(
            Listed("SELECT id FROM dbo.users WHERE id = @p AND name = @p AND id = @q", Inferred("p"))
        );

        asked.ShouldBe([Call("parameters", parameters)]);
        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
    }

    [Fact]
    public async Task DescribeAsync_CopiesThatConstrainDisagree_IsSqlsrc262()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2";
        var parameters = new SqlServerParametersRequest(Renamed, null);
        var script = new SqlServerScript()
            .Add(
                "parameters",
                parameters,
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }], null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 xml, @p_2 nvarchar(100)"),
                new SqlServerProbeAnswer(false, null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 int, @p_2 xml"),
                new SqlServerProbeAnswer(false, null)
            );

        var (described, _) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p")
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC262");
        failure.Arguments.ShouldBe(["@p", Query, "int", "nvarchar(100)"]);
    }

    // A probe whose connection was lost says nothing of the copy: the describe ends with the error.
    [Fact]
    public async Task DescribeAsync_ProbeAnswersAnError_IsSqlsrc225AtDescribeParameters()
    {
        const string Renamed = "SELECT id FROM dbo.users WHERE id = @p_1 AND name = @p_2";
        var script = new SqlServerScript()
            .Add(
                "parameters",
                new SqlServerParametersRequest(Renamed, null),
                new SqlServerParametersAnswer([IntRow with { Name = "@p_1" }, Name100Row with { Name = "@p_2" }], null)
            )
            .Add(
                "probe",
                new SqlServerProbeRequest(Renamed, "@p_1 xml, @p_2 nvarchar(100)"),
                new SqlServerProbeAnswer(null, Lost)
            );

        var (described, _) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE id = @p AND name = @p")
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        failure.ServerLines.ShouldBe(["the connection to the server was lost"]);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredAnswersErrorRows_IsSqlsrc228AndAsksNoParameters()
    {
        var declared = new SqlServerDeclaredRequest("SELECT @p AS [1]", "@p dbo.Nope");
        var script = new SqlServerScript().Add(
            "declared",
            declared,
            new SqlServerDeclaredAnswer(
                null,
                [
                    new SqlServerDescribeError(
                        2715,
                        1,
                        "Column, parameter, or variable #1: Cannot find data type dbo.Nope."
                    ),
                ],
                null
            )
        );

        var (described, asked) = await script.DescribeAsync(
            Of(SqlDialectRules.SqlServer, "SELECT id FROM dbo.users WHERE email = @p", ("p", "dbo.Nope"))
        );

        asked.ShouldBe([Call("declared", declared)]);
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments[3].ShouldBe(DescribeFailures.NoSuchType);
    }
}
