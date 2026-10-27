// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The settings files, sqlsource.json, that apply to a .sql file's folder and every folder below it.  The dialect comes
// from the header's marker, then the chain of the file's folder, and the parse's keep-comments from the chain's list.
// Every other setting comes from the markers, then the attribute, then the chain, nearest file first; the type
// override and the column types merge per key across them, the nearer file's entry ranking above the farther's.  A
// type's nested class comes from the attribute, then the chain of the type's C# file.  A settings file's problems are
// reported at its line and column when the chain of the folder of some .sql file or C# file reaches it.  Nothing that
// MSBuild tells the generator is a setting.
public sealed class SettingsFileTests
{
    // ANSI and MySQL read the backticks as the quotes of an identifier, and the comment starts after it.  PostgreSQL
    // has no such quote, so the two dashes inside them start the comment.
    private const string Query = "SELECT `--x` AS a -- c\n";

    private const string Postgres = "SELECT `";

    private const string NotPostgres = "SELECT `--x` AS a";

    private const string QueryPath = "/app/Repo/Q.sql";

    private const string Kept = "-- name: Q\nSELECT 1 -- kept\n";

    private const string KeptConstant = "public const string Q = \"SELECT 1 -- kept\";";

    private const string StrippedConstant = "public const string Q = \"SELECT 1\";";

    private const string SettingsPath = "/app/Repo/sqlsource.json";

    private const string FartherSettingsPath = "/app/sqlsource.json";

    private const string GetUser = "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n";

    // The attribute of the assembly that the SDK writes under obj, here under the intermediate folder of the artifacts
    // layout, outside the project's folder /repo/App.
    private const string ArtifactsAssemblyInfo = "/repo/artifacts/obj/App/debug/App.AssemblyInfo.cs";

    // What the targets give a project in /repo/App whose IntermediateOutputPath is /repo/build/obj/Debug/, whose OutDir
    // is /repo/out/Debug/, and which writes the compiler's generated files to /repo/gen/: the base folders, the
    // intermediate folder, the output folder and the folder of the generated files, each after a |.
    private const string AllFoldersOfTheBuild =
        "|/repo/App/obj/|/repo/build/obj/Debug/|/repo/App/bin/|/repo/out/Debug/|/repo/gen/";

    // App.UserId and App.OtherId, over long, beside the type that claims the files.
    private const string IdsSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value);

            public readonly record struct OtherId(long Value);
        }
        """;

    private static readonly PostgresType Int8 = Catalog("int8") with { Name = "bigint" };

    [Fact]
    public void Run_DialectInTheFolder_ParsesTheFileWithIt()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectInAFolderAbove_ParsesTheFileWithIt()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Postgres + "\";");
    }

    [Fact]
    public void Run_DialectMarker_OverridesTheFolder()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"dialect":"postgres"}"""
            ),
            new SqlFile(QueryPath, "-- dialect: ansi\n" + Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + NotPostgres + "\";");
    }

    // The parse builds the form of the query with its comments, which the folder's list asks for, and the constant is
    // that form.
    [Fact]
    public void Run_KeepCommentsInTheFolder_ParsesAndEmitsTheFormWithComments()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/sqlsource.json", /*lang=json,strict*/
                """{"generatorParameters":["keep-comments"]}"""
            ),
            new SqlFile(QueryPath, Kept)
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Single<FileParseInput>(run, TrackingNames.FileParseInput).CommentsWanted.ShouldBeTrue();
        _ = StepOutputs
            .Single<ParsedSqlFile>(run, TrackingNames.ParsedFile)
            .Queries.ShouldHaveSingleItem()
            .KeptSegments.ShouldNotBeNull();
        run.Sources["App.Sample.g.cs"].ShouldContain(KeptConstant);
    }

    // The nearer file's list replaces the farther files' whole, the project's file's among them, so no level outside
    // the nearer file can ask for comments, the form with them is not built and the constant has none.
    [Fact]
    public void Run_EmptyListInANearerFile_ParsesAndEmitsNoFormWithComments()
    {
        var run = Run(
            null,
            [
                GeneratorHarness.SettingsFile(
                    "/app/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters":["keep-comments"]}"""
                ),
                GeneratorHarness.SettingsFile(
                    "/app/Repo/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters":[]}"""
                ),
                new SqlFile(QueryPath, Kept),
            ],
            generatorParameters: "keep-comments"
        );

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs.Single<FileParseInput>(run, TrackingNames.FileParseInput).CommentsWanted.ShouldBeFalse();
        StepOutputs
            .Single<ParsedSqlFile>(run, TrackingNames.ParsedFile)
            .Queries.ShouldHaveSingleItem()
            .KeptSegments.ShouldBeNull();
        run.Sources["App.Sample.g.cs"].ShouldContain(StrippedConstant);
    }

    // The project asks for models, which need an entry for each query, and the folder's file asks for the constant
    // alone, which needs none.
    [Fact]
    public void Run_OutputInTheFolder_Applies()
    {
        var run = ModelRuns.Run(
            Source,
            [
                GeneratorHarness.SettingsFile(
                    SettingsPath, /*lang=json,strict*/
                    """{"output":"sql"}"""
                ),
                new SqlFile(GeneratorHarness.UsersPath, GetUser),
            ]
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string GetUser = ");
    }

    // The attribute's suffix wins, and the folder's file still gives the other.
    [Fact]
    public void Run_AttributeOverridesTheFolder()
    {
        var run = RunUsers(
            Source.Replace(
                "[SqlSourceGenerate]",
                "[SqlSourceGenerate(InputModelSuffix = \"Args\")]",
                StringComparison.Ordinal
            ),
            null,
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"inputModelSuffix":"In","outputModelSuffix":"Row"}"""
            )
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserArgs-Model.g.cs", "App.GetUserRow-Model.g.cs"]);
    }

    // Each file says the setting, and the farther one says another that the nearer one leaves to it.
    [Fact]
    public void Run_NearerFileOverridesAFartherOne()
    {
        var run = RunUsers(
            Source,
            null,
            GeneratorHarness.SettingsFile(
                FartherSettingsPath, /*lang=json,strict*/
                """{"inputModelSuffix":"Far","outputModelSuffix":"Row"}"""
            ),
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"inputModelSuffix":"Near"}"""
            )
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserNear-Model.g.cs", "App.GetUserRow-Model.g.cs"]);
    }

    // A root file that sets nothing else still hides the file above: its output, sql, does not apply, and neither does
    // its dialect.  So the query has the default output, codegen, under ansi, which has no database to describe it.
    // The run writes no project's file, which the root would hide too.
    [Fact]
    public void Run_RootFileThatSaysNothingElse_HidesTheFilesAbove()
    {
        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            GeneratorHarness.SettingsFile(
                FartherSettingsPath, /*lang=json,strict*/
                """{"output":"Sql","dialect":"postgres"}"""
            ),
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"root":true}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC209 " + QueryPath + "(");
        diagnostic.ShouldContain("The output 'codegen' needs each query described by its database");
        diagnostic.ShouldContain("the dialect 'ansi' has no database to ask");
    }

    // The root file's own settings apply, and the file above it, the project's, is not read: the root file gives the
    // output and the dialect that the project's file gives the others.
    [Fact]
    public void Run_RootFileHidesTheFilesAbove()
    {
        var run = RunUsers(
            Source,
            /*lang=json,strict*/
            """{"inputModelSuffix":"Above"}""",
            GeneratorHarness.SettingsFile(
                FartherSettingsPath, /*lang=json,strict*/
                """{"root":true,"output":"Models","dialect":"postgres","outputModelSuffix":"Row"}"""
            )
        );

        run.ShouldBeClean();
        ModelFiles(run).ShouldBe(["App.GetUserParams-Model.g.cs", "App.GetUserRow-Model.g.cs"]);
    }

    // The nearer list has no keep-comments, so the farther file's does not reach the query.
    [Fact]
    public void Run_GeneratorParametersOfANearerFile_ReplaceTheFartherListWhole()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                FartherSettingsPath, /*lang=json,strict*/
                """{"generatorParameters":["keep-comments"]}"""
            ),
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"generatorParameters":["no-token-validation"]}"""
            ),
            new SqlFile(QueryPath, Kept + "\n-- name: ListFrom\nSELECT * FROM {{table}};\n")
        );

        run.ShouldBeClean();
        var type = run.Sources["App.Sample.g.cs"];
        type.ShouldContain(StrippedConstant);
        type.ShouldContain(" ListFrom(");
        type.ShouldNotContain("ThrowIfNullOrWhiteSpace");
    }

    // Within one place a key equal to the type's exactly beats a plain one that matches it ignoring case; the nearer
    // file is the more specific place.  The two are different keys, so both reach the lookup and only the places
    // decide.
    [Fact]
    public void Run_TypeOverridePlainKeyInANearerFile_BeatsAnExactKeyInAFartherOne()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        var status = TestSidecars.Postgres(
            name: "Status",
            kind: SidecarValues.Kind.Enum,
            schema: "public",
            internalName: "Status",
            labels: ["new", "old"]
        );
        var run = ModelRuns.Run(
            Source,
            [
                GeneratorHarness.SettingsFile(
                    FartherSettingsPath, /*lang=json,strict*/
                    """{"typeOverrides":{"public.\"Status\"":"System.Guid"}}"""
                ),
                GeneratorHarness.SettingsFile(
                    SettingsPath, /*lang=json,strict*/
                    """{"typeOverrides":{"public.status":"System.Uri"}}"""
                ),
                file,
                Sidecars.Of(file, Entry(file, "GetUser", [], [Column("c0", status)])),
            ]
        );

        run.ShouldBeClean();
        var column = ColumnOf(run);
        column.Text.ShouldBe("global::System.Uri");
        column.Place.ShouldBe("the typeOverrides of the settings file " + SettingsPath);
        column.Key.ShouldBe("public.status");
    }

    // Within one place a key with a schema beats one without; the nearer file is the more specific place.
    [Fact]
    public void Run_ColumnTypeWithoutASchemaInANearerFile_BeatsOneWithASchemaFarther()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListPosts\nSELECT id FROM users;\n");
        var run = ModelRuns.Run(
            IdsSource,
            [
                GeneratorHarness.SettingsFile(
                    FartherSettingsPath, /*lang=json,strict*/
                    """{"columnTypes":{"public.users.id":"OtherId"}}"""
                ),
                GeneratorHarness.SettingsFile(
                    SettingsPath, /*lang=json,strict*/
                    """{"columnTypes":{"users.id":"UserId"}}"""
                ),
                file,
                Sidecars.Of(
                    file,
                    Entry(
                        file,
                        "ListPosts",
                        [],
                        [Column("id", Int8, origin: new SidecarOrigin("public", "users", "id"))]
                    )
                ),
            ]
        );

        run.ShouldBeClean();
        var column = ColumnOf(run);
        column.Text.ShouldBe("global::App.UserId");
        column.Place.ShouldBe("the columnTypes of the settings file " + SettingsPath);
        column.Mapped.ShouldBe("users.id");
    }

    [Fact]
    public void Run_MappedTypeNotFound_NamesTheSettingsFile()
    {
        var run = RunInt4(
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"typeOverrides":{"int4":"MyApp.Missing"}}"""
            )
        );

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC250 ");
        diagnostic.ShouldContain("in the typeOverrides of the settings file /app/Repo/sqlsource.json");
    }

    // The class belongs to the type, so the folder of its C# file decides, and not that of the .sql file it claims.
    [Fact]
    public void Run_NestedClassInTheTypesFolder_NamesTheClass()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App;
                    [SqlSourceGenerate(Path = "../Queries/Users.sql", SqlLocation = SqlLocation.Nested)]
                    public partial class Sample
                    {
                        public static string Get() => Queries.GetUser;
                    }
                    """
                ),
            ],
            [
                GeneratorHarness.SettingsFile(
                    SettingsPath, /*lang=json,strict*/
                    """{"nestedClass":"Queries"}"""
                ),
                GeneratorHarness.SettingsFile(
                    "/app/Queries/sqlsource.json", /*lang=json,strict*/
                    """{"nestedClass":"Statements"}"""
                ),
                new SqlFile("/app/Queries/Users.sql", SqlTexts.GetUser),
            ]
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("private static class Queries\n");
    }

    // A file that a type links from outside its own folders takes the chain of the folder the file is in.
    [Fact]
    public void Run_LinkedFileOutsideTheProject_UsesItsOwnFoldersChain()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    GeneratorHarness.SampleSource("Path = \"../../shared/Users.sql\"")
                ),
            ],
            [
                GeneratorHarness.SettingsFile(
                    "/shared/sqlsource.json", /*lang=json,strict*/
                    """{"generatorParameters":["keep-comments"]}"""
                ),
                GeneratorHarness.SettingsFile(
                    SettingsPath, /*lang=json,strict*/
                    """{"generatorParameters":[]}"""
                ),
                new SqlFile("/shared/Users.sql", Kept),
            ]
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain(KeptConstant);
    }

    [Fact]
    public void Run_UnknownKey_IsReportedAtTheSettingsFile()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                "{\n  \"output\": \"sql\",\n  \"bogus\": 1\n}\n"
            ),
            new SqlFile(QueryPath, Query)
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC016 /app/Repo/sqlsource.json(3,3)-(3,10): 'bogus' is not a key of a settings file",
        ]);
    }

    [Fact]
    public void Run_InvalidTypeOverride_IsReported()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"typeOverrides":{" int4 ":" int? "}}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        var diagnostic = run.Diagnostics.ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC019 /app/Repo/sqlsource.json(1,");
        diagnostic.ShouldContain("'int4: int?' is not a valid value of typeOverrides");
    }

    // The tool reads the database and reports its value; the build does not.  The unknown key shows that the build
    // checked the file.
    [Fact]
    public void Run_InvalidDatabase_IsNotReported()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"database":" a b ","bogus":1}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC016 /app/Repo/sqlsource.json(1,");
    }

    // Every item is under /app, whose file ends each chain, so no chain reaches the file at the root.  The attribute's
    // file has a path that is not rooted where the compiler sets no folder for generated files, as the test driver
    // does, so it is no item; in a build it is under the intermediate folder, which reaches nothing either.  The root
    // file gives the output, so the run writes no project's file, and the broken one is at its path.
    [Fact]
    public void Run_FileAboveARoot_IsNotReported()
    {
        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            GeneratorHarness.SettingsFile(GeneratorHarness.ProjectSettingsPath, "{"),
            GeneratorHarness.SettingsFile(
                FartherSettingsPath, /*lang=json,strict*/
                """{"root": true, "output": "Sql"}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
    }

    // A tree whose path is not rooted, as another generator's file is where the compiler sets no folder for generated
    // files, is no C# file of the project, so its chain reaches nothing, not even the file at the root.  The root file
    // gives the output, so the run writes no project's file, and the broken one is at its path.
    [Fact]
    public void Run_TreeOfAnotherGenerator_ReachesNoSettingsFile()
    {
        var run = GeneratorHarness.Run(
            null,
            [
                new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "Other.Gen/Other.Gen.Generator/Extra.g.cs",
                    "namespace Other;\n\ninternal static class Extra;\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile(GeneratorHarness.ProjectSettingsPath, "{"),
                GeneratorHarness.SettingsFile(
                    FartherSettingsPath, /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile(QueryPath, Query),
            ]
        );

        run.ShouldBeClean();
    }

    // With obj outside the project, as the artifacts layout has it, the SDK's own Compile items are under
    // /repo/artifacts, whose chain climbs past the project's root file to the broken file of the repository.  The
    // build gives the generator the intermediate folder, and a compile path under it reaches nothing.
    [Fact]
    public void Run_CompileItemUnderTheIntermediateFolder_ReachesNoSettingsFile()
    {
        var run = RunOutsideTheProject(
            ArtifactsAssemblyInfo,
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/App/" }
        );

        run.ShouldBeClean();
    }

    [Fact]
    public void Run_CompileItemUnderTheOutputFolder_ReachesNoSettingsFile()
    {
        var run = RunOutsideTheProject(
            "/repo/artifacts/bin/App/debug/Extra.cs",
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/bin/App/" }
        );

        run.ShouldBeClean();
    }

    // Each folder the build writes C# trees or its outputs into, outside the project: IntermediateOutputPath moved out
    // of obj, where the SDK's items and the compiler's own output are, OutDir, and the folder the compiler writes its
    // generated files to under EmitCompilerGeneratedFiles, where it roots their trees.  The property is what the
    // targets give for such a project, the base folders being in the project.
    [Theory]
    [InlineData("/repo/build/obj/Debug/App.AssemblyInfo.cs")]
    [InlineData("/repo/build/obj/Debug/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs")]
    [InlineData("/repo/out/Debug/Extra.cs")]
    [InlineData("/repo/gen/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs")]
    public void Run_CompileItemUnderAFolderOfTheBuildOutsideTheProject_ReachesNoSettingsFile(string compilePath)
    {
        var run = RunOutsideTheProject(
            compilePath,
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = AllFoldersOfTheBuild }
        );

        run.ShouldBeClean();
    }

    // The same, with the folder that holds the tree left out of the property: the tree reaches the file above the root.
    [Theory]
    [InlineData("/repo/build/obj/Debug/App.AssemblyInfo.cs", "|/repo/build/obj/Debug/")]
    [InlineData("/repo/out/Debug/Extra.cs", "|/repo/out/Debug/")]
    [InlineData("/repo/gen/SqlSource/SqlSource.SqlSourceGenerator/SqlSourceGenerateAttribute.g.cs", "|/repo/gen/")]
    public void Run_CompileItemUnderAFolderTheBuildDoesNotGive_ReachesTheFileAboveTheRoot(
        string compilePath,
        string folder
    )
    {
        var run = RunOutsideTheProject(
            compilePath,
            new Dictionary<string, string?>
            {
                ["SqlSourceBuildFolders"] = AllFoldersOfTheBuild.Replace(folder, "", StringComparison.Ordinal),
            }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The same project without the build's folders: the SDK's item reaches the file above the project's root.
    [Fact]
    public void Run_CompileItemOutsideTheProjectWithoutTheBuildFolders_ReachesTheFileAboveTheRoot()
    {
        var run = RunOutsideTheProject(ArtifactsAssemblyInfo, null);

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // A folder is compared segment by segment, so objx is not under obj.
    [Fact]
    public void Run_CompileItemInAFolderThatOnlyStartsLikeTheIntermediateFolder_ReachesTheFileAboveTheRoot()
    {
        var run = RunOutsideTheProject(
            "/repo/artifacts/objx/App/debug/App.AssemblyInfo.cs",
            new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The targets escape a folder's # and ;, at which the compiler would cut the value, and the generator decodes them,
    // so a project under /repo/My#App keeps its build's files out.  The project's root file gives the output, since it
    // hides a project's file of the run.
    [Fact]
    public void Run_CompileItemUnderAnEscapedIntermediateFolder_ReachesNoSettingsFile()
    {
        var run = GeneratorHarness.Run(
            null,
            [
                new SourceFile("/repo/My#App/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "/repo/My#App/artifacts/obj/App/debug/App.AssemblyInfo.cs",
                    "[assembly: System.Reflection.AssemblyMetadata(\"Built\", \"yes\")]\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/My#App/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/My#App/App/Q.sql", Query),
            ],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceBuildFolders"] = "/repo/My%23App/artifacts/obj/App/",
            }
        );

        run.ShouldBeClean();
    }

    // A value the compiler cut at a # or a ;, as an unescaped /repo/App#1/obj/ would arrive, /repo/App.  A value that
    // does not end with a separator is no folder of the build, so the project's own C# file still reaches its folder's
    // file.
    [Fact]
    public void Run_IntermediateFolderCutShort_LeavesNoCompileItemOut()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "/repo/App/Code/Helpers.cs",
                    "[assembly: System.Reflection.AssemblyMetadata(\"Helpers\", \"none\")]\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile(
                    "/repo/App/Code/sqlsource.json", /*lang=json,strict*/
                    """{"bogus":1}"""
                ),
                new SqlFile("/repo/App/Q.sql", Query),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/App" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC016 /repo/App/Code/sqlsource.json(1,");
    }

    // A .sql file is an item of the project wherever it is, so one under the intermediate folder still reaches the
    // files above it.  The project's root file gives the output, since it hides a project's file of the run.
    [Fact]
    public void Run_SqlFileUnderTheIntermediateFolder_ReachesTheFileAboveTheRoot()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null))],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/App/Q.sql", Query),
                new SqlFile("/repo/artifacts/obj/App/Generated.sql", Query),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceBuildFolders"] = "/repo/artifacts/obj/App/" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC015 /repo/sqlsource.json(1,");
    }

    // The text of /app's file is not JSON, so its root is unknown and the chain goes on to the root's file.  Both are
    // reported, in the order of their folders.  The root's file gives the output, so the run writes no project's file,
    // and the test's own is at its path.
    [Fact]
    public void Run_FileAboveAFileThatIsNotJson_IsReported()
    {
        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            GeneratorHarness.SettingsFile(
                GeneratorHarness.ProjectSettingsPath, /*lang=json,strict*/
                """{"output":"Sql","bogus":1}"""
            ),
            GeneratorHarness.SettingsFile(FartherSettingsPath, "{"),
            new SqlFile(QueryPath, Query)
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics[0].ShouldStartWith("SQLSRC016 /sqlsource.json(1,");
        run.Diagnostics[1].ShouldStartWith("SQLSRC015 /app/sqlsource.json(1,");
    }

    // The C# file declares no type, only an attribute of the assembly, and no .sql file is in its folder.
    [Fact]
    public void Run_FileReachedOnlyByACSharpFile_IsReported()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null)),
                new SourceFile(
                    "/app/Code/Helpers.cs",
                    "[assembly: System.Reflection.AssemblyMetadata(\"Helpers\", \"none\")]\n"
                ),
            ],
            [
                GeneratorHarness.SettingsFile(
                    "/app/Code/sqlsource.json", /*lang=json,strict*/
                    """{"bogus":1}"""
                ),
                new SqlFile(QueryPath, Query),
            ]
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC016 /app/Code/sqlsource.json(1,");
    }

    // No type claims the .sql file of /app/Other, and its folder's file is checked all the same.
    [Fact]
    public void Run_FileReachedByAnUnclaimedSqlFile_IsReported()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Other/sqlsource.json", /*lang=json,strict*/
                """{"bogus":1}"""
            ),
            new SqlFile("/app/Other/Unclaimed.sql", Query),
            new SqlFile(QueryPath, Query)
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC016 /app/Other/sqlsource.json(1,");
        StepOutputs.Single<ParsedSqlFile>(run, TrackingNames.ParsedFile).FileName.ShouldBe("Q.sql");
    }

    [Fact]
    public void Run_FileNoItemReaches_IsNotReported()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/other/sqlsource.json", /*lang=json,strict*/
                """{"bogus":1}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
    }

    // The same file twice, and once more under another case of its folder: the first is kept, and reported once.
    [Fact]
    public void Run_FileListedTwice_IsReportedOnce()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"bogus":1}"""
            ),
            GeneratorHarness.SettingsFile(
                SettingsPath, /*lang=json,strict*/
                """{"bogus":1}"""
            ),
            GeneratorHarness.SettingsFile(
                "/APP/Repo/sqlsource.json", /*lang=json,strict*/
                """{"other":1}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        // The message whole: the last file's path differs from the first's only by case, which ShouldStartWith ignores,
        // so its key, other, tells the two apart.
        run.Diagnostics.ShouldBe([
            "SQLSRC016 /app/Repo/sqlsource.json(1,2)-(1,9): 'bogus' is not a key of a settings file",
        ]);
    }

    // Only a file named sqlsource.json exactly is a settings file, so this one, which the project lists itself as
    // SqlSource.json, neither sets the dialect nor is reported.  The build's own search never gives such a path: on a
    // file system that ignores case, as macOS's and Windows' do, it finds the file and gives it under the name
    // sqlsource.json, so it is read there, and on Linux it does not find it, as MSBuild's own search for a
    // Directory.Build.props does.
    [Fact]
    public void Run_ListedFileNamedInAnotherCase_IsNotRead()
    {
        var run = Run(
            null,
            GeneratorHarness.SettingsFile(
                "/app/Repo/SqlSource.json", /*lang=json,strict*/
                """{"dialect":"postgres","bogus":1}"""
            ),
            new SqlFile(QueryPath, Query)
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + NotPostgres + "\";");
    }

    private static GeneratorRun Run(string? dialect, params SqlFile[] files) => Run(dialect, files, null);

    private static GeneratorRun Run(string? dialect, SqlFile[] files, string? generatorParameters) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            files,
            generatorParameters: generatorParameters,
            dialect: dialect
        );

    // The type and its .sql file in /repo/App, whose settings file is a root that gives the output, since it hides a
    // project's file of the run, and a broken settings file at the repository's root, /repo, with one more C# file at
    // the path given, under the properties given.
    private static GeneratorRun RunOutsideTheProject(
        string compilePath,
        IReadOnlyDictionary<string, string?>? properties
    ) =>
        GeneratorHarness.Run(
            null,
            [
                new SourceFile("/repo/App/Sample.cs", GeneratorHarness.SampleSource(null)),
                new SourceFile(compilePath, "[assembly: System.Reflection.AssemblyMetadata(\"Built\", \"yes\")]\n"),
            ],
            [
                GeneratorHarness.SettingsFile("/repo/sqlsource.json", "{"),
                GeneratorHarness.SettingsFile(
                    "/repo/App/sqlsource.json", /*lang=json,strict*/
                    """{"root": true, "output": "Sql"}"""
                ),
                new SqlFile("/repo/App/Q.sql", Query),
            ],
            properties: properties
        );

    // GetUser of Users, at Models under postgres, with an entry of one parameter and one column, both id, the
    // project's settings and the settings files given.
    private static GeneratorRun RunUsers(string source, string? settings, params SqlFile[] settingsFiles)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, GetUser);
        return ModelRuns.Run(
            source,
            [.. settingsFiles, file, Sidecars.Of(file, Entry(file, "GetUser", [Parameter("id")], [Column("id")]))],
            settings: settings
        );
    }

    // GetUser of Users, at Models under postgres, with an entry of one column of int4, under the settings files given.
    private static GeneratorRun RunInt4(params SqlFile[] settingsFiles)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, SqlTexts.GetUser);
        return ModelRuns.Run(
            Source,
            [.. settingsFiles, file, Sidecars.Of(file, Entry(file, "GetUser", [], [Column("c0", Int4)]))]
        );
    }

    // The type of the one column of the run's one query.
    private static PropertyType ColumnOf(GeneratorRun run) =>
        StepOutputs
            .Of<FileTypes>(run, TrackingNames.FileTypes)
            .SelectMany(file => file.Queries)
            .ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem();
}
