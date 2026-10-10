using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// An entry of a sidecar, from the planned query, the session and the description.
public sealed class EntryBuilderTests : IDisposable
{
    private static readonly ServerInfo Server = new("16.4", "Npgsql", "8.0.0", EquatableArray<ServerSetting>.Empty);

    private static readonly PostgresType Integer = new(
        "integer",
        "base",
        "pg_catalog",
        "int4",
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static QueryDescription With(params DescribedParameter[] parameters) =>
        FakeDescriber.NoRows with
        {
            Parameters = new EquatableArray<DescribedParameter>([.. parameters]),
        };

    private static SidecarEntry Build(PlannedQuery query, QueryDescription description) =>
        EntryBuilder.Build(query, SqlDialect.PostgreSql, "postgres", Server, description);

    [Fact]
    public void Build_Description_TakesEachMemberFromItsSource()
    {
        var query = Plans.Query(_folder, "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n");
        var column = new SidecarColumn(0, "id", Integer, false, "catalog", null, true, false);
        var description = new QueryDescription(
            SidecarResultKind.Rows,
            new EquatableArray<DescribedParameter>([new DescribedParameter("id", Integer, "inferred")]),
            new EquatableArray<SidecarColumn>([column]),
            new SidecarTable("public", "users"),
            "not-needed",
            "matched"
        );

        var entry = EntryBuilder.Build(query, SqlDialect.PostgreSql, "Billing", Server, description);

        entry.ShouldBe(
            new SidecarEntry(
                "GetUser",
                default,
                query.Hash.ShouldNotBeNull(),
                "postgres",
                "Billing",
                "16.4",
                SidecarResultKind.Rows,
                new SidecarTable("public", "users"),
                "not-needed",
                "matched",
                new EquatableArray<SidecarParameter>([new SidecarParameter("id", 0, Integer, null, "inferred")]),
                new EquatableArray<SidecarColumn>([column])
            )
        );
    }

    [Fact]
    public void Build_ParameterTheDescriptionLacks_HasNoTypeAndNoSource()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @a, @b;\n");

        var entry = Build(query, With(new DescribedParameter("b", Integer, "inferred")));

        entry
            .Parameters.ToArray()
            .ShouldBe([
                new SidecarParameter("a", 0, null, null, null),
                new SidecarParameter("b", 1, Integer, null, "inferred"),
            ]);
    }

    [Fact]
    public void Build_ParameterTheDescriptionGivesInAnotherCase_IsFoundAndKeepsTheNameOfTheList()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @userId;\n");

        var entry = Build(query, With(new DescribedParameter("USERID", Integer, "inferred")));

        entry.Parameters.ToArray().ShouldBe([new SidecarParameter("userId", 0, Integer, null, "inferred")]);
    }

    // A parameter that only a marker declares comes last in the query's list, and so in the entry.
    [Fact]
    public void Build_ParameterThatOnlyAMarkerDeclares_ComesAfterTheOnesOfTheSql()
    {
        var query = Plans.Query(_folder, "-- name: A\n-- param: @extra int\nSELECT @a;\n");

        var entry = Build(query, With(new DescribedParameter("extra", Integer, "declared")));

        entry
            .Parameters.ToArray()
            .ShouldBe([
                new SidecarParameter("a", 0, null, null, null),
                new SidecarParameter("extra", 1, Integer, null, "declared"),
            ]);
    }

    [Theory]
    [InlineData("-- param: @a null\n", true)]
    [InlineData("-- param: @a not null\n", false)]
    [InlineData("", null)]
    public void Build_Nullable_IsWhatTheMarkerSays(string marker, bool? nullable)
    {
        var query = Plans.Query(_folder, $"-- name: A\n{marker}SELECT @a;\n");

        Build(query, FakeDescriber.NoRows).Parameters.Single().Nullable.ShouldBe(nullable);
    }

    // A bug of the describer, and SQLSRC200.
    [Fact]
    public void Build_ParameterTheQueryDoesNotHave_Throws()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @a;\n");

        Should
            .Throw<InvalidOperationException>(() => Build(query, With(new DescribedParameter("b", Integer, null))))
            .Message.ShouldBe("The describer gave the parameter 'b', which the query 'A' does not have.");
    }
}
