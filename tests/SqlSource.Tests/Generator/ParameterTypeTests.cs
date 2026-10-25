// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// A parameter given a C# type of its own by "-> Type" on its -- param: marker, over the type override at any level.
// Each run is at Models under postgres on Npgsql 10, with an entry written for each query's current hash, unless it
// says otherwise.
public sealed class ParameterTypeTests
{
    private const string Place = "the -- param: marker of the parameter";

    private const string ParamsModel = "App.ListPostsParams-Model.g.cs";

    // App.UserId, over long, with its converters.
    private const string UserIdSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(long value) => new(value);

                [SqlSourceConverter]
                public static long ToDatabase(UserId id) => id.Value;
            }
        }
        """;

    private static readonly PostgresType Int8 = Catalog("int8") with { Name = "bigint" };

    [Fact]
    public void Run_ParamMarker_GivesTheParameterItsType()
    {
        var run = RunPosts("-- param: @author -> UserId\n");

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("global::App.UserId Author");
    }

    [Fact]
    public void Run_ParamMarkerWithADatabaseType_GivesTheParameterItsType()
    {
        var run = RunPosts("-- param: @author int8 not null -> App.UserId\n");

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("global::App.UserId Author");
    }

    // The marker is over the type override of every level, and leaves the other parameters to it.
    [Fact]
    public void Run_ParamMarker_WinsOverTheTypeOverride()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListPosts\n-- type-override: int8=long\n-- param: @author -> UserId\n"
                + "SELECT id FROM posts WHERE author_id = @author AND editor_id = @editor;\n"
        );

        var run = Run(
            UserIdSource,
            [
                file,
                Sidecars.Of(
                    file,
                    Entry(
                        file,
                        "ListPosts",
                        [Parameter("author", Int8, false), Parameter("editor", Int8, false)],
                        [Column("id")]
                    )
                ),
            ],
            new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "int8=long" }
        );

        run.ShouldBeClean();
        var model = run.Sources[ParamsModel];
        model.ShouldContain("global::App.UserId Author");
        model.ShouldContain("long Editor");
    }

    [Fact]
    public void Run_ParamMarker_Null_IsNullable()
    {
        var run = RunPosts("-- param: @author null -> UserId\n", nullable: true);

        run.ShouldBeClean();
        run.Sources[ParamsModel].ShouldContain("global::App.UserId? Author");
    }

    [Fact]
    public void Run_ParamMarker_UsesTheConverter()
    {
        var file = PostsFile("-- param: @author -> UserId\n");

        var run = MethodRuns.Run(file, [PostsEntry(file, nullable: false)], UserIdSource);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain("parameter.TypedValue = global::App.UserId.ToDatabase(parameters.Author);\n");
    }

    [Fact]
    public void Run_ParamMarkerUnderSqlServer_UsesTheConverter()
    {
        var file = PostsFile("-- param: @author -> UserId\n");
        var entry = MethodRuns.SqlServerEntry(
            file,
            "ListPosts",
            [TestSidecars.Parameter(0, "author", TestSidecars.SqlServer("bigint", 8, 19), false)],
            [TestSidecars.Column(0, "id", TestSidecars.SqlServer("int", 4, 10))]
        );

        var run = MethodRuns.RunSqlServer(file, [entry], UserIdSource);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain("parameter.Value = global::App.UserId.ToDatabase(parameters.Author);\n");
    }

    [Fact]
    public void Run_ParamMarker_MissingType_IsSQLSRC250AtItsPlace()
    {
        var run = RunPosts("-- param: @author -> NoSuchType\n");

        run.Diagnostics.ShouldBe([
            $"SQLSRC250 {GeneratorHarness.UsersPath}(1,10)-(1,19): '@author' is mapped to 'NoSuchType' for parameter "
                + $"'author', in {Place}, and no type of that name is found, or more than one that this project can "
                + "see.  Correct the name, with its namespace and type arguments, or for a name that two references "
                + "have, map to a type of this project.",
        ]);
    }

    [Theory]
    [InlineData(
        "App.Hidden",
        "SQLSRC251",
        "and this project cannot see that type.  Make it, and each type it is nested in, public, or internal when it "
            + "is this project's own, or give this project its assembly's internals with InternalsVisibleTo."
    )]
    [InlineData(
        "System.Math",
        "SQLSRC252",
        "and no property can have that type.  Choose another type, and for System.Nullable<T> name T, since a column "
            + "that can be null makes its property nullable."
    )]
    public void Run_ParamMarker_TypeThatCannotBeUsed_IsReportedAtItsPlace(string type, string id, string reason)
    {
        var library = GeneratorHarness.CreateReference(
            OutputKind.Sql,
            "Lib",
            "namespace App { internal readonly record struct Hidden(long Value); }"
        );
        var file = PostsFile("-- param: @author -> " + type + "\n");

        var run = Run(
            Source,
            [file, Sidecars.Of(file, PostsEntry(file, nullable: false))],
            references: [NpgsqlReferences.Npgsql10, library]
        );

        run.Diagnostics.ShouldBe([
            $"{id} {GeneratorHarness.UsersPath}(1,10)-(1,19): '@author' is mapped to '{type}' for parameter "
                + $"'author', in {Place}, {reason}",
        ]);
    }

    private static SqlFile PostsFile(string marker) =>
        new(
            GeneratorHarness.UsersPath,
            "-- name: ListPosts\n" + marker + "SELECT id FROM posts WHERE author_id = @author;\n"
        );

    private static SidecarEntry PostsEntry(SqlFile file, bool nullable) =>
        Entry(file, "ListPosts", [Parameter("author", Int8, nullable)], [Column("id")]);

    private static GeneratorRun RunPosts(string marker, bool nullable = false)
    {
        var file = PostsFile(marker);
        return Run(UserIdSource, [file, Sidecars.Of(file, PostsEntry(file, nullable))]);
    }
}
