using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generation;

public sealed class UsableQueriesTests
{
    [Fact]
    public void Of_TwoFiles_GivesEachQueryWithItsIndicesAndSettings()
    {
        var first = TestModels.Query("GetUser", "SELECT 1;");
        var second = TestModels.Query("ListUsers", "SELECT 2;", markers: new SettingsLevel { Output = OutputKind.Sql });
        var third = TestModels.Query("GetOrder", "SELECT 3;", nameLocation: TestModels.NameLocation("Orders.sql"));
        var fourth = TestModels.Query("ListOrders", "SELECT 4;", nameLocation: TestModels.NameLocation("Orders.sql"));
        var type = Queries(
            TestModels.Type(settings: new SettingsLevel { Output = OutputKind.Models }),
            [SettingsLevel.None, new SettingsLevel { OutputModelSuffix = "Row" }],
            File("Users.sql", first, second),
            File("Orders.sql", third, fourth)
        );

        var queries = Walk(UsableQueries.Of(type, null));

        queries.Select(static each => (each.FileIndex, each.QueryIndex)).ShouldBe([(0, 0), (0, 1), (1, 0), (1, 1)]);
        queries.Select(static each => each.Query).ShouldBe([first, second, third, fourth]);
        queries
            .Select(static each => each.Settings.Output)
            .ShouldBe([OutputKind.Models, OutputKind.Sql, OutputKind.Models, OutputKind.Models]);
        queries.Select(static each => each.Settings.OutputModelSuffix).ShouldBe(["Dto", "Dto", "Row", "Row"]);
    }

    [Fact]
    public void Of_FileWithADuplicateName_LeavesItOutAndReportsSqlsrc008()
    {
        var first = TestModels.Query("GetUser", "SELECT 1;");
        var other = TestModels.Query("Other", "SELECT 2;", nameLocation: TestModels.NameLocation("Orders.sql"));
        var duplicate = TestModels.Query("GetUser", "SELECT 3;", nameLocation: TestModels.NameLocation("Orders.sql"));
        var type = Queries(
            TestModels.Type(),
            [SettingsLevel.None, SettingsLevel.None],
            File("Users.sql", first),
            File("Orders.sql", other, duplicate)
        );
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        var queries = Walk(UsableQueries.Of(type, diagnostics));

        queries.Select(static each => each.Query).ShouldBe([first]);
        diagnostics
            .ToImmutable()
            .ShouldBe([
                DiagnosticInfo.Create(
                    SqlDiagnostics.DuplicateQueryName,
                    TestModels.NameLocation("Orders.sql"),
                    "GetUser",
                    "Users.sql",
                    "UserRepository"
                ),
            ]);
        Walk(UsableQueries.Of(type, null)).Select(static each => each.Query).ShouldBe([first]);
    }

    private static List<UsableQuery> Walk(UsableQuerySequence sequence)
    {
        var queries = new List<UsableQuery>();
        foreach (var query in sequence)
        {
            queries.Add(query);
        }

        return queries;
    }

    private static TypeQueries Queries(TargetType type, SettingsLevel[] fileSettings, params ParsedSqlFile[] files) =>
        new(
            new TypeFiles(type, TestSidecars.Of<string>(), TestSidecars.Of<DiagnosticInfo>()),
            TestSidecars.Of(files),
            TestSidecars.Of(fileSettings),
            HintName.Create(type)
        );

    private static ParsedSqlFile File(string fileName, params SqlQuery[] queries) =>
        new(
            "app/Repo/" + fileName,
            fileName,
            TestSidecars.Of(queries),
            TestSidecars.Of<DiagnosticInfo>(),
            TestSidecars.Of<DiagnosticInfo>(),
            SqlDialect.Ansi
        );
}
