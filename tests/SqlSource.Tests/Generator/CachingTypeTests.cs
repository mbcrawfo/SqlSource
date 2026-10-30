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
    private const string Instant = "global::NodaTime.Instant";

    private readonly CachingScene _scene = new();

    // Npgsql, NodaTime and its plugin, which turn NodaTime on by default.
    private static MetadataReference[] WithNodaTimesPlugin =>
        [NpgsqlReferences.Npgsql10, LibraryReferences.NodaTime("3.3.7"), LibraryReferences.NpgsqlNodaTime("10.0.3")];

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

        AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBeCached();
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBeCached();
        OutputReasons(result).ShouldAllBeCached();
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

    // An edit of the project's settings file's type override: each file's map of overrides comes from its level, which
    // comes from the project's settings file, which every file's chain reaches, so every file is typed again, and each
    // column of int4 now has the file's type.
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

        AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBe(IncrementalStepRunReason.Modified);
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
        AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBeCached();
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBeCached();
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

    // An edit of the settings file of Users' folder that swaps the two column types it names: the mapped types are
    // equal, and Orders, which the file does not reach, gathers an equal input and is not typed again.
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
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(IncrementalStepRunReason.Modified);
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::System.DateOnly");
    }

    // The -- library-types: marker is no part of the hash, so its edit leaves Users' entry current, and types Users
    // again without the library.  Orders gathers an equal input and is not typed again, and the probe of the libraries
    // runs on the run's compilation and gives an equal value.
    [Fact]
    public void LibraryTypesMarker_Edit_RetypesOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: WithNodaTimesPlugin);
        var users = UsersWithLibraryTypes("on");
        var driver = CreateDriver(
                ProjectFileWithTypeOverrides(null),
                [
                    users,
                    _scene.Orders,
                    TypedSidecar(users, "GetUser", "timestamptz"),
                    TypedSidecar(_scene.Orders, "GetOrder", "timestamptz"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        ColumnTextOf(driver.GetRunResult(), "app/Users/Users.sql").ShouldBe(Instant);

        var result = Run(driver.ReplaceAdditionalText(users, UsersWithLibraryTypes("off")), compilation);

        AllReasons(result, TrackingNames.LibraryProbe).ShouldBe([IncrementalStepRunReason.Unchanged]);
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<TypedFile>(result, TrackingNames.TypedFile, file => file.Types.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        ColumnTextOf(result, "app/Users/Users.sql").ShouldBe("global::System.DateTime");
        ColumnTextOf(result, "app/Orders/Orders.sql").ShouldBe(Instant);
    }

    // The plugin added turns the library on by default: the probe changes, every file is typed again with NodaTime's
    // type, and the models and the methods that read it are planned again.
    [Fact]
    public void LibraryReference_Added_RetypesAndReplansMethods()
    {
        var compilation = MethodsCompilation(TypeSource("App", "UserQueries"));
        var driver = _scene.FirstRunOfMethods(
            compilation,
            TypedSidecar(_scene.Users, "GetUser", "timestamptz"),
            TypedSidecar(_scene.Orders, "GetOrder", "timestamptz")
        );
        MethodsOf(driver.GetRunResult(), UserMethods).ShouldContain("GetFieldValue<global::System.DateTime>(");

        var result = Run(
            driver,
            compilation.AddReferences(LibraryReferences.NodaTime("3.3.7"), LibraryReferences.NpgsqlNodaTime("10.0.3"))
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.LibraryProbe).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypedFile).ShouldAllBe(IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.FileModels).ShouldAllBe(IncrementalStepRunReason.Modified);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Modified),
                ignoreOrder: true
            );
        MethodsOf(result, UserMethods).ShouldContain("GetFieldValue<global::NodaTime.Instant>(");
    }

    // An edit of the library types of the settings file of Users' folder: Orders, which the file does not reach,
    // gathers an equal input and is not typed again.
    [Fact]
    public void SettingsFileLibraryTypes_Edit_RetypesOnlyItsFolder()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: WithNodaTimesPlugin);
        var folder = LibraryTypesFile("on");
        var driver = _scene.FirstRunWithTypes(
            compilation,
            ProjectFileWithTypeOverrides(null),
            TypedSidecar(_scene.Users, "GetUser", "timestamptz"),
            TypedSidecar(_scene.Orders, "GetOrder", "timestamptz"),
            folder
        );

        var result = Run(driver.ReplaceAdditionalText(folder, LibraryTypesFile("off")), compilation);

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
        ColumnTextOf(result, "app/Users/Users.sql").ShouldBe("global::System.DateTime");
        ColumnTextOf(result, "app/Orders/Orders.sql").ShouldBe(Instant);

        // The settings file of Users' folder, with this state of NodaTime.
        static InMemoryAdditionalText LibraryTypesFile(string state) =>
            new("/app/Users/sqlsource.json", "{\"libraryTypes\": {\"nodaTime\": \"" + state + "\"}}");
    }

    // An edit of the project's settings file's model suffix, which moves its library types written after it: a level
    // carries no position of a settings file's member, so each file gathers an equal input and none is typed again.
    [Fact]
    public void Run_ModelSuffixAboveTheProjectsLibraryTypesChanged_TypesNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: WithNodaTimesPlugin);
        var project = ProjectFileWithSuffix("Row");
        var driver = _scene.FirstRunWithTypes(compilation, project);

        var result = Run(driver.ReplaceAdditionalText(project, ProjectFileWithSuffix("Record")), compilation);

        AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBe(IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBeCached();

        // The project's settings file with this model suffix, and NodaTime on after it.
        static InMemoryAdditionalText ProjectFileWithSuffix(string suffix) =>
            ProjectFile(
                OutputKind.Models,
                "postgres",
                "{\"outputModelSuffix\": \"" + suffix + "\", \"libraryTypes\": {\"nodaTime\": \"on\"}}"
            );
    }

    // Users, whose query's -- library-types: marker gives NodaTime this state.
    private InMemoryAdditionalText UsersWithLibraryTypes(string state) =>
        new(_scene.Users.Path, "-- name: GetUser\n-- library-types: nodatime=" + state + "\nSELECT 1 AS value;\n");

    // NodaTime's version is for its floor, which typing does not read: a reference that changes the version alone
    // changes the probe, and the probe that typing reads is equal, so no file gathers its input or is typed again.
    [Fact]
    public void NodaTimeVersion_Changed_TypesNoFileAgain()
    {
        var older = LibraryReferences.Synthetic("3.2.0.0", instant: true, pluginExtensions: true);
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10, older]);
        var driver = _scene.FirstRunWithTypes(
            compilation,
            ProjectFileWithTypeOverrides(null),
            TypedSidecar(_scene.Users, "GetUser", "timestamptz"),
            TypedSidecar(_scene.Orders, "GetOrder", "timestamptz")
        );
        ColumnTextOf(driver.GetRunResult(), "app/Users/Users.sql").ShouldBe(Instant);

        var result = Run(
            driver,
            compilation.ReplaceReference(
                older,
                LibraryReferences.Synthetic("3.3.0.0", instant: true, pluginExtensions: true)
            )
        );

        AllReasons(result, TrackingNames.LibraryProbe).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileTypesInput).ShouldAllBeCached();
        AllReasons(result, TrackingNames.TypedFile).ShouldAllBeCached();
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
