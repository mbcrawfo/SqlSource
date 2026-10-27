// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the typing of the queries: which files an edit makes the generator type again.
public sealed class CachingTypeTests
{
    private readonly CachingScene _scene = new();

    // The probe and the resolution run on the new compilation and give equal values, so no file is typed again.
    [Fact]
    public void Run_CSharpEdit_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, ProjectFileWithTypeOverrides(null));
        Outputs(driver.GetRunResult(), TrackingNames.FileTypes)
            .Select(output => (FileTypes)output.Value)
            .ShouldAllBe(types => types.Queries.Count == 1);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The edit makes the entry of Users stale, so Users has no types now, and Orders is not typed again.
    [Fact]
    public void Run_SqlFileEdited_TypesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(compilation, ProjectFileWithTypeOverrides(null));

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        // Every file gathered its input again, since the parsed files changed, and Orders' is equal.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql").Queries.ShouldBeEmpty();
    }

    // The same edit under mssql: Users has no entry now, and Orders is not typed again.
    [Fact]
    public void Run_SqlFileEditedUnderMssql_TypesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [SqlClientReferences.SqlClient7]);
        var driver = _scene.FirstRunWithSqlServerSidecars(compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql").Queries.ShouldBeEmpty();
    }

    // The new entry of Users has a column of text, and Orders is not typed again.
    [Fact]
    public void Run_SidecarEdited_TypesOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunWithTypes(compilation, ProjectFileWithTypeOverrides(null), usersSidecar);

        var edited = TypedSidecar(_scene.Users, "GetUser", "text");
        var result = Run(driver.ReplaceAdditionalText(usersSidecar, edited), compilation);

        // Orders' check was taken from the previous run, and so was every other input of its types.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("string");
    }

    // Each file's map of overrides comes from the property, so every file is typed again, and each column of int4
    // now has the property's type.  A test of the MSBuild path: the output, the dialect and the type override are the
    // project's properties.
    [Fact]
    public void Run_TypeOverridesPropertyChanged_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunOnMSBuild(
            compilation,
            OptionsWithTypeOverrides(null),
            _scene.Users,
            _scene.Orders,
            TypedSidecar(_scene.Users, "GetUser", "int4"),
            TypedSidecar(_scene.Orders, "GetOrder", "int4")
        );

        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(OptionsWithTypeOverrides("int4=long")), compilation);

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The counterpart of Run_TypeOverridesPropertyChanged_TypesEveryFileAgain, by an edit of the project's settings
    // file, with the same reasons: each file's map of overrides comes from its level, which comes from the project's
    // settings file, which every file's chain reaches, so every file is typed again, and each column of int4 now has
    // the file's type.
    [Fact]
    public void Run_TypeOverridesOfTheProjectsFileChanged_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var project = ProjectFileWithTypeOverrides(null);
        var driver = _scene.FirstRunWithTypes(compilation, project);

        var edited = ProjectFileWithTypeOverrides( /*lang=json,strict*/
            """{"int4": "long"}"""
        );
        var result = Run(driver.ReplaceAdditionalText(project, edited), compilation);

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The probe and the resolution of the settings file's one type run on the new compilation and give equal values,
    // so a project with a type override types no file again either.
    [Fact]
    public void Run_MethodBodyEditedWithATypeOverride_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithTypes(
            compilation,
            ProjectFileWithTypeOverrides( /*lang=json,strict*/
                """{"int4": "long"}"""
            )
        );

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.ResolvedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The project's settings file maps int4 to a type the project does not have, so Users, whose column is of int4,
    // is SQLSRC250.  A new file declares the type: its resolution changes, its converters, none, do not, and Users is
    // typed again without the error.  Every file with a usable entry carries the resolved types, so Orders, whose
    // column is of text, is typed again too, and gives an equal value, which nothing after it reads again.
    [Fact]
    public void Run_CSharpEditAddsTheMappedType_TypesTheFileThatUsesItAnew()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = CreateDriver(
                ProjectFileWithTypeOverrides( /*lang=json,strict*/
                    """{"int4": "MyApp.Missing"}"""
                ),
                [
                    _scene.Users,
                    _scene.Orders,
                    TypedSidecar(_scene.Users, "GetUser", "int4"),
                    TypedSidecar(_scene.Orders, "GetOrder", "text"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC250"]);

        var result = Run(
            driver,
            compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "namespace MyApp;\n\npublic sealed class Missing;\n",
                    GeneratorHarness.ParseOptions,
                    "/app/MyApp/Missing.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        );

        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MappedTypeConverters).ShouldBe([IncrementalStepRunReason.Unchanged]);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::MyApp.Missing");
    }

    // The C# type of a -- param: marker is no part of the hash, so an edit of it checks the sidecar again to an equal
    // value, and types Users again with the new type.  Orders' check and types are taken from the previous run.
    [Fact]
    public void Run_ParamMarkersCSharpTypeEdited_ChecksTheSameEntryAndTypesTheFileAgain()
    {
        var compilation = GeneratorHarness
            .CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10])
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "namespace MyApp;\n\npublic readonly record struct UserId(int Value);\n\n"
                        + "public readonly record struct OtherId(int Value);\n",
                    GeneratorHarness.ParseOptions,
                    "/app/MyApp/Ids.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
        var users = UsersWithMarker("UserId");
        var parameter = TestSidecars.Parameter(0, "id", TestSidecars.Postgres("int4", internalName: "int4"), false);
        var driver = CreateDriver(
                ProjectFileWithTypeOverrides(null),
                [
                    users,
                    _scene.Orders,
                    TypedSidecar(users, "GetUser", "int4", [parameter]),
                    TypedSidecar(_scene.Orders, "GetOrder", "int4"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();

        var result = Run(driver.ReplaceAdditionalText(users, UsersWithMarker("OtherId")), compilation);

        Reasons<SidecarCheckResult>(result, TrackingNames.SidecarCheck, check => check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Unchanged,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Parameters.ShouldHaveSingleItem()
            .Text.ShouldBe("global::MyApp.OtherId");
    }

    // The column types of Users' metadata change the type of its column read from users.id.  The edit swaps the two
    // types it names, so the mapped types are equal, and Orders, without metadata, gathers an equal input and is not
    // typed again.  A test of the MSBuild path: the output and the dialect are the project's properties, and the
    // column types Users' metadata.
    [Fact]
    public void Run_ColumnTypeEdited_RetypesOnlyThatFile()
    {
        var compilation = WithIds(GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]));
        var driver = FirstRunOnMSBuild(
            compilation,
            OptionsWithColumnTypes("users.id=MyApp.UserId posts.id=MyApp.OtherId"),
            _scene.Users,
            _scene.Orders,
            OriginSidecar(_scene.Users, "GetUser"),
            OriginSidecar(_scene.Orders, "GetOrder")
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                OptionsWithColumnTypes("users.id=MyApp.OtherId posts.id=MyApp.UserId")
            ),
            compilation
        );

        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        ColumnTextOf(result, "app/Users/Users.sql").ShouldBe("global::MyApp.OtherId");
        ColumnTextOf(result, "app/Orders/Orders.sql").ShouldBe("long");
    }

    // The counterpart of Run_ColumnTypeEdited_RetypesOnlyThatFile, by an edit of the settings file of Users' folder,
    // with the same reasons: the edit swaps the two types it names, so the mapped types are equal, and Orders, which
    // the file does not reach, gathers an equal input and is not typed again.
    [Fact]
    public void Run_ColumnTypesOfAFolderFileEdited_RetypesOnlyThatFoldersFile()
    {
        var compilation = WithIds(GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]));
        var folder = ColumnTypesFile("MyApp.UserId", "MyApp.OtherId");
        var driver = _scene.FirstRunWithTypes(
            compilation,
            ProjectFileWithTypeOverrides(null),
            OriginSidecar(_scene.Users, "GetUser"),
            OriginSidecar(_scene.Orders, "GetOrder"),
            folder
        );

        var result = Run(
            driver.ReplaceAdditionalText(folder, ColumnTypesFile("MyApp.OtherId", "MyApp.UserId")),
            compilation
        );

        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        ColumnTextOf(result, "app/Users/Users.sql").ShouldBe("global::MyApp.OtherId");
        ColumnTextOf(result, "app/Orders/Orders.sql").ShouldBe("long");

        // The settings file of Users' folder, with these types for users.id and posts.id.
        static InMemoryAdditionalText ColumnTypesFile(string usersId, string postsId) =>
            new(
                "/app/Users/sqlsource.json",
                "{\"columnTypes\": {\"users.id\": \"" + usersId + "\", \"posts.id\": \"" + postsId + "\"}}"
            );
    }

    // The C# type of a -- column: marker is no part of the hash, so an edit of it checks the sidecar again to an equal
    // value, and types Users again with the new type.  Orders' check is taken from the previous run.
    [Fact]
    public void Run_ArrowEdited_SidecarCheckUnchanged()
    {
        var compilation = WithIds(GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]));
        var users = UsersWithColumnMarker("UserId");
        var driver = CreateDriver(
                ProjectFileWithTypeOverrides(null),
                [
                    users,
                    _scene.Orders,
                    TypedSidecar(users, "GetUser", "int4"),
                    TypedSidecar(_scene.Orders, "GetOrder", "int4"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();

        var result = Run(driver.ReplaceAdditionalText(users, UsersWithColumnMarker("OtherId")), compilation);

        Reasons<SidecarCheckResult>(result, TrackingNames.SidecarCheck, check => check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Unchanged,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        ColumnTextOf(result, "app/Users/Users.sql").ShouldBe("global::MyApp.OtherId");
    }

    // Without Npgsql, date has no C# type, SQLSRC249.  The reference changes the probe, and every file is typed
    // again, with the type of Npgsql 10.
    [Fact]
    public void Run_NpgsqlReferenceAdded_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = CreateDriver(
                ProjectFileWithTypeOverrides(null),
                [
                    _scene.Users,
                    _scene.Orders,
                    TypedSidecar(_scene.Users, "GetUser", "date"),
                    TypedSidecar(_scene.Orders, "GetOrder", "date"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC249", "SQLSRC249"]);

        var result = Run(driver, compilation.AddReferences(NpgsqlReferences.Npgsql10));

        AllReasons(result, TrackingNames.NpgsqlProbe).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::System.DateOnly");
    }

    // Users, whose one parameter a -- param: marker gives the C# type of MyApp.
    private InMemoryAdditionalText UsersWithMarker(string type) =>
        new(_scene.Users.Path, "-- name: GetUser\n-- param: @id -> MyApp." + type + "\nSELECT 1 WHERE @id = 1;\n");

    // Users, whose column value a -- column: marker gives the C# type of MyApp.
    private InMemoryAdditionalText UsersWithColumnMarker(string type) =>
        new(_scene.Users.Path, "-- name: GetUser\n-- column: value -> MyApp." + type + "\nSELECT 1 AS value;\n");

    // The compilation with MyApp.UserId and MyApp.OtherId.
    private static Compilation WithIds(Compilation compilation) =>
        compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(
                "namespace MyApp;\n\npublic readonly record struct UserId(long Value);\n\n"
                    + "public readonly record struct OtherId(long Value);\n",
                GeneratorHarness.ParseOptions,
                "/app/MyApp/Ids.cs",
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    // The options at Models under postgres, with these column types in the metadata of Users, for a test of the
    // MSBuild path.
    private TestOptionsProvider OptionsWithColumnTypes(string columnTypes) =>
        new(
            OutputKind.Models,
            null,
            "postgres",
            fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [_scene.Users.Path] = new Dictionary<string, string> { ["SqlSourceColumnTypes"] = columnTypes },
            }
        );

    // The options at Models under postgres, with this type override as the project's property, for a test of the
    // MSBuild path.
    private static TestOptionsProvider OptionsWithTypeOverrides(string? overrides) =>
        new(
            OutputKind.Models,
            null,
            "postgres",
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = overrides }
        );

    // A current sidecar whose entry has one column, value, an int8 read from public.users.id.
    private static InMemoryAdditionalText OriginSidecar(InMemoryAdditionalText sqlFile, string queryName)
    {
        var file = ToSqlFile(sqlFile);
        var sidecar = Sidecars.Of(
            file,
            TestSidecars.Entry(
                queryName,
                Sidecars.HashOf(file, queryName, SqlDialect.PostgreSql),
                "postgres",
                "main",
                "18.0",
                columns:
                [
                    TestSidecars.Column(
                        0,
                        "value",
                        TestSidecars.Postgres("int8", internalName: "int8"),
                        origin: new SidecarOrigin("public", "users", "id")
                    ),
                ]
            )
        );
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    // The C# type of the one column of the one query of the file.
    private static string ColumnTextOf(GeneratorDriverRunResult result, string path) =>
        TypesOf(result, path).Queries.ShouldHaveSingleItem().Columns.ShouldNotBeNull().ShouldHaveSingleItem().Text;
}
