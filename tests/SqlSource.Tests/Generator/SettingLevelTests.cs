// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The settings of models and methods are accepted at every level they have, and a value none of them takes is
// reported where it was written.  Without a dialect no query can have models, so they change only the diagnostics.
//
// Every run without a dialect leaves the output unset, since the output's levels are among what these tests compare,
// unless it says otherwise.  So a query whose output is the default, codegen, is SQLSRC209 at its name, and gets
// neither a method nor a constant.  The settings that change a method are run at codegen under postgres, through
// MethodRuns, with an entry for the query.
public sealed class SettingLevelTests
{
    // The query of the method runs, with its entry written for the file as each level's markers leave it.
    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
    );

    // Users with its header's output at sql.
    private static readonly SqlFile UsersAtSql = new(GeneratorHarness.UsersPath, "-- output: sql\n\n" + Users.Text);

    public static TheoryData<string, string, string[], string[]> MarkersWithoutAnEffect =>
        new()
        {
            {
                "-- name: Q\n-- output: sql\nSELECT id FROM users WHERE id = @id;\n",
                "-- database: main\n-- output: models\n-- input-model-suffix: Args\n-- output-model-suffix: Row\n"
                    + "-- model-namespace: App.Models\n-- input-model-type: sealed record\n"
                    + "-- output-model-type: class\n-- collection-type: ImmutableArray\n"
                    + "-- type-override: numeric = System.Double\n\n"
                    + "-- name: Q -> one\n-- param: @id int not null\n-- database: other\n-- output: sql\n"
                    + "-- input-model: QueryArgs\n-- output-model: App.Models.QueryRow\n-- collection-type: List\n"
                    + "-- type-override: numeric = default\n-- type-override: int4 = long\n"
                    + "SELECT id FROM users WHERE id = @id;\n",
                [],
                []
            },
            {
                "-- output: models\n\n-- name: Q\nSELECT id FROM {{table}} WHERE {{filter}};\n",
                "-- output: models\n\n-- name: Q -> many\n-- param: @since timestamptz not null\n"
                    + "-- param: @page int\n-- token: {{filter:created_at > @since}}\n"
                    + "SELECT id FROM {{table:users}} WHERE {{filter}};\n",
                [NotDescribable("models", 3), NoDefault("table", "models", 3), NoDefault("filter", "models", 3)],
                [NotDescribable("models", 3)]
            },
        };

    public static TheoryData<SettingLevel, string, string, string> PlacementSettingsAtEachLevel
    {
        get
        {
            var rows = new TheoryData<SettingLevel, string, string, string>();
            foreach (
                var level in new[]
                {
                    SettingLevel.ProjectFile,
                    SettingLevel.FolderFile,
                    SettingLevel.Header,
                    SettingLevel.Query,
                }
            )
            {
                rows.Add(level, "member-location", "nested-class", "    private static partial class Sql\n");
                rows.Add(level, "visibility", "public", "public static partial class SampleExtensions\n");
                // Away from a nested class the nested class is ignored, and the methods stay in the extension class.
                rows.Add(level, "nested-class", "Users", "internal static partial class SampleExtensions\n");
            }

            return rows;
        }
    }

    // Every level below the project's file.  Codegen is both the default and the output MethodRuns gives the project,
    // so a project's file of codegen would decide nothing.
    public static TheoryData<SettingLevel> LevelsBelowTheProjectFile =>
        [SettingLevel.FolderFile, SettingLevel.Header, SettingLevel.Query];

    // Every level of a setting.
    public static TheoryData<SettingLevel> Levels =>
        [SettingLevel.ProjectFile, SettingLevel.FolderFile, SettingLevel.Header, SettingLevel.Query];

    // The markers that change no type's file without a dialect, in a preamble and in a query: the type's file is the
    // one of the same file without them.  A query's output marker of sql over the header's of models leaves the file
    // of the query at sql, and the tokens' defaults take SQLSRC210 away.
    [Theory]
    [MemberData(nameof(MarkersWithoutAnEffect))]
    public void Run_MarkersWithoutAnEffect_GiveTheFileOfTheSameQueryWithoutThem(
        string plainSql,
        string markedSql,
        string[] plainDiagnostics,
        string[] markedDiagnostics
    )
    {
        var plain = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", plainSql)
        );

        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", markedSql)
        );

        plain.Diagnostics.ShouldBe(plainDiagnostics);
        run.Diagnostics.ShouldBe(markedDiagnostics);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();

        // The query has its constant or token method, so the comparison of the two files compares a member.
        plain.Sources[GeneratorHarness.SampleFile].ShouldMatch(@"public (const string Q = |static string Q\()");
        run.Sources[GeneratorHarness.SampleFile].ShouldBe(plain.Sources[GeneratorHarness.SampleFile]);
    }

    // At sql, so that the query has a constant, which shows the marker's line stripped as a comment.
    [Fact]
    public void Run_MethodLocationAsAMarker_IsNotASetting()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", "-- name: Q\n-- method-location: public\nSELECT 1; -- c\n")
        );

        // The line is an ordinary comment.
        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("\"SELECT 1;\"");
    }

    // Sql at each level, below which the project's file says codegen, so that the level is what decides: the query is a
    // constant, with no method and no model.  At sql the query needs no entry, and one would be SQLSRC245.
    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_SqlOutputAtEachLevel_GivesAConstant(SettingLevel level)
    {
        var run = RunWithOutputAt(level, "Sql", belowIt: "CodeGen", withEntry: false);

        run.Sources[GeneratorHarness.SampleFile].ShouldContain(GetUserConstant);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        ModelRuns.ModelFiles(run).ShouldBeEmpty();
    }

    // Models at each level, below which the project's file says codegen: the query is a constant with its models.
    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_ModelsOutputAtEachLevel_GivesAConstantAndTheModels(SettingLevel level)
    {
        var run = RunWithOutputAt(level, "Models", belowIt: "CodeGen", withEntry: true);

        run.Sources[GeneratorHarness.SampleFile].ShouldContain(GetUserConstant);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        ModelRuns.ModelFiles(run).ShouldBe([ModelRuns.GetUserDtoFile, "App.GetUserParams-Model.g.cs"]);
    }

    // CodeGen at each level below the project's file, which says sql: the query is a method with its models.
    [Theory]
    [MemberData(nameof(LevelsBelowTheProjectFile))]
    public void Run_CodeGenOutputAtEachLevel_GivesAMethodAndTheModels(SettingLevel level)
    {
        var run = RunWithOutputAt(level, "CodeGen", belowIt: "Sql", withEntry: true);

        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUser(\n");
        ModelRuns.ModelFiles(run).ShouldBe([ModelRuns.GetUserDtoFile, "App.GetUserParams-Model.g.cs"]);
    }

    // The member location, the visibility and the nested class are read at every level, and each takes the query's
    // method where it says.  A row gives the setting's marker, as the type of a setting is internal, a value that is
    // valid for it, and what the file of methods then holds.  The query is a method, so the type's file stays as it is.
    [Theory]
    [MemberData(nameof(PlacementSettingsAtEachLevel))]
    public void Run_PlacementSettingAtEachLevel_PlacesTheMethod(
        SettingLevel level,
        string marker,
        string value,
        string expected
    )
    {
        var entry = new[] { Setting.MemberLocation, Setting.Visibility, Setting.NestedClass }.Single(setting =>
            setting.Marker == marker
        );
        var plain = RunPlacement(new LevelSettings());

        var run = RunPlacement(new LevelSettings().At(level, entry, LevelSettings.StringAt(level, value)));

        run.Sources[GeneratorHarness.SampleFile].ShouldBe(plain.Sources[GeneratorHarness.SampleFile]);
        run.Sources[MethodRuns.MethodsFile].ShouldContain(expected);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_CollectionTypeAtEachLevel_IsTheMethodsReturnType(SettingLevel level)
    {
        var settings = new LevelSettings().At(level, Setting.CollectionType, LevelSettings.StringAt(level, "List"));
        var files = settings.Files(Users);
        var file = files[0];

        var run = MethodRuns.Run(
            file,
            [ModelRuns.Entry(file, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])],
            MethodRuns.Source,
            settings: settings.ProjectSettings,
            otherFiles: files[1..]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                " global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<global::App.GetUserDto>> "
                    + "GetUser(\n"
            );
    }

    // Each member location in the file's header, with each visibility on the type.  The default is the extension
    // class, internal; a method in the nested class is public, in a class whose visibility is the file's.
    [Theory]
    [InlineData("", "    internal static partial class SampleExtensions\n", "        internal static async ")]
    [InlineData(
        "-- member-location: extension-class\n",
        "    internal static partial class SampleExtensions\n",
        "        internal static async "
    )]
    [InlineData(
        "-- member-location: nested-class\n",
        "        private static partial class Sql\n",
        "            public static async "
    )]
    [InlineData(
        "-- member-location: class\n-- visibility: public\n",
        "    partial class Sample\n",
        "        public static async "
    )]
    [InlineData(
        "-- member-location: class\n-- visibility: internal\n",
        "    partial class Sample\n",
        "        internal static async "
    )]
    [InlineData(
        "-- member-location: class\n-- visibility: private\n",
        "    partial class Sample\n",
        "        private static async "
    )]
    public void Run_MemberLocationInTheHeader_PlacesTheMethod(string header, string holder, string modifiers)
    {
        var users = Users with { Text = header + "\n" + Users.Text };

        var run = MethodRuns.Run(
            users,
            [ModelRuns.Entry(users, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(holder);
        methods.ShouldContain(modifiers + "global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n");
        run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser");
    }

    // The file owns its output: two types that claim it, under a project's file of models, both take its header's sql,
    // so each gets the constant, neither gets models, and the query needs no entry.
    [Fact]
    public void Run_TwoTypesClaimingOneFile_BothTakeItsHeaderOutput()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(Source = \".\")]\n"
                        + "public partial class First;\n\n"
                        + "[SqlSourceGenerate(Source = \".\")]\npublic partial class Second;\n"
                ),
            ],
            [UsersAtSql],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.First.g.cs"].ShouldContain(GetUserConstant);
        run.Sources["App.Second.g.cs"].ShouldContain(GetUserConstant);
        ModelRuns.ModelFiles(run).ShouldBeEmpty();
    }

    // The header is more specific than the settings file of the file's folder.
    [Fact]
    public void Run_HeaderOutputOverSettingsFileOutput_Wins()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [
                UsersAtSql,
                GeneratorHarness.SettingsFile(
                    "/app/Repo/sqlsource.json", /*lang=json,strict*/
                    "{\"output\": \"Models\"}"
                ),
            ],
            dialect: "postgres"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain(GetUserConstant);
        ModelRuns.ModelFiles(run).ShouldBeEmpty();
    }

    // SQLSRC209 for the query Q at this line, under the default dialect.
    private static string NotDescribable(string output, int line) =>
        $"SQLSRC209 /app/Repo/Q.sql({line},10)-({line},11): The output '{output}' needs each query described by its "
        + "database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the "
        + "output to 'sql'.";

    // SQLSRC210 for this token of the query Q at this line, under this output.
    private static string NoDefault(string token, string output, int line) =>
        $"SQLSRC210 /app/Repo/Q.sql({line},10)-({line},11): The token '{token}' has no default, which a query whose "
        + $"output is '{output}' needs.  Give it a default, or set the output to 'sql'.";

    // GetUser of Users as a constant of Sample.
    private const string GetUserConstant = "public const string GetUser = \"SELECT id FROM users WHERE id = @id;\";\n";

    // Users, the level of the settings given, and an entry of GetUser, run at the default output.
    private static GeneratorRun RunPlacement(LevelSettings settings)
    {
        var files = settings.Files(Users);
        var file = files[0];
        var run = MethodRuns.Run(
            file,
            [ModelRuns.Entry(file, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])],
            MethodRuns.Source,
            settings: settings.ProjectSettings,
            otherFiles: files[1..]
        );

        run.ShouldBeClean();
        return run;
    }

    // Users with the output at the level, and the other output at the project's file below it unless the level is the
    // project's file, with an entry of GetUser when one is given.  An output of the project's file among the settings
    // replaces the one MethodRuns gives the project, codegen.
    private static GeneratorRun RunWithOutputAt(SettingLevel level, string output, string belowIt, bool withEntry)
    {
        var settings = new LevelSettings();
        if (level != SettingLevel.ProjectFile)
        {
            _ = settings.At(
                SettingLevel.ProjectFile,
                Setting.Output,
                LevelSettings.StringAt(SettingLevel.ProjectFile, belowIt)
            );
        }

        _ = settings.At(level, Setting.Output, LevelSettings.StringAt(level, output));
        var files = settings.Files(Users);
        var file = files[0];
        var run = MethodRuns.Run(
            file,
            withEntry ? [ModelRuns.Entry(file, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])] : [],
            MethodRuns.Source,
            settings: settings.ProjectSettings,
            otherFiles: files[1..]
        );

        run.ShouldBeClean();
        return run;
    }
}
