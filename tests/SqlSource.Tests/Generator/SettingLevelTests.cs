// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
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
    private const string Sql = "-- name: Q\nSELECT 1;\n";

    // Sql with the query at sql by its marker, so that the type's file has its constant, whose text shows the marker's
    // line stripped, and the run reports nothing.
    private const string SqlAtSql = "-- name: Q\n-- output: sql\nSELECT 1;\n";

    // The constant of SqlAtSql, which a comparison of two type files checks is there.
    private const string Constant = "public const string Q = \"SELECT 1;\";\n";

    // The query of the method runs, with its entry written for the file as each level's markers leave it.
    private static readonly SqlFile Users = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n"
    );

    // The single query of Sql under the defaults, Q at its first line.
    private static readonly string[] Default = [NotDescribable("codegen", 1)];

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

    // Each level with each output.  The property is the least specific level, and codegen is both the default and the
    // output MethodRuns gives the project, so a property of codegen would decide nothing: that row is left out.
    public static TheoryData<SettingLevel, string> OutputLevels =>
        new()
        {
            { SettingLevel.Property, "Sql" },
            { SettingLevel.Property, "Models" },
            { SettingLevel.Metadata, "Sql" },
            { SettingLevel.Metadata, "Models" },
            { SettingLevel.Metadata, "CodeGen" },
            { SettingLevel.Attribute, "Sql" },
            { SettingLevel.Attribute, "Models" },
            { SettingLevel.Attribute, "CodeGen" },
            { SettingLevel.Header, "Sql" },
            { SettingLevel.Header, "Models" },
            { SettingLevel.Header, "CodeGen" },
            { SettingLevel.Query, "Sql" },
            { SettingLevel.Query, "Models" },
            { SettingLevel.Query, "CodeGen" },
        };

    // Every level of a setting that the attribute has too.
    public static TheoryData<SettingLevel> Levels =>
        [SettingLevel.Property, SettingLevel.Metadata, SettingLevel.Attribute, SettingLevel.Header, SettingLevel.Query];

    // The output's row reads the default's query, whose diagnostic names the output read.  Every other row's query is
    // at sql by its marker, so that the two runs' type files have a constant to compare.
    [Theory]
    [InlineData("SqlSourceOutput", " Code-Gen ", "codegen")]
    [InlineData("SqlSourceDatabase", "not read by the generator, so never wrong", null)]
    [InlineData("SqlSourceInputModelSuffix", "Args", null)]
    [InlineData("SqlSourceOutputModelSuffix", "Row", null)]
    [InlineData("SqlSourceModelNamespace", "App.Models", null)]
    [InlineData("SqlSourceInputModelType", "sealed record", null)]
    [InlineData("SqlSourceOutputModelType", "Class", null)]
    [InlineData("SqlSourceTypeOverrides", "timestamptz=System.DateTimeOffset  int4[]=int?[]", null)]
    public void Run_ValidSettingAsPropertyAndAsMetadata_ChangesNoGeneratedCode(
        string name,
        string value,
        string? output
    )
    {
        var text = output is null ? SqlAtSql : Sql;
        var plain = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", text)
        );

        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", text, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        plain.Diagnostics.ShouldBe(output is null ? [] : Default);
        run.Diagnostics.ShouldBe(output is null ? [] : [NotDescribable(output, 1)]);
        if (output is null)
        {
            plain.Sources["App.Sample.g.cs"].ShouldContain(Constant);
        }

        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("SqlSourceOutput", "model")]
    [InlineData("SqlSourceOutput", "sql models")]
    [InlineData("SqlSourceInputModelSuffix", "A B")]
    [InlineData("SqlSourceOutputModelSuffix", "A.B")]
    [InlineData("SqlSourceModelNamespace", "App.")]
    [InlineData("SqlSourceInputModelType", "struct")]
    [InlineData("SqlSourceOutputModelType", "sealed")]
    [InlineData("SqlSourceCollectionType", "HashSet")]
    [InlineData("SqlSourceTypeOverrides", "timestampz=System.DateTimeOffset")]
    [InlineData("SqlSourceTypeOverrides", "int4=int?")]
    public void Run_InvalidSettingAsPropertyAndAsMetadata_IsReportedOnceWithoutAPosition(string name, string value)
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        // A value that is not valid is not set, and the output is the default.
        run.Diagnostics.ShouldBe(
            ["SQLSRC014 (1,1)-(1,1): '" + value + "' is not a valid value of " + name, .. Default],
            ignoreOrder: true
        );
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    // An entry of the type override that is not valid is reported alone.  That the other entries apply is
    // SettingsLevelReaderTests.Read_TypeOverrideThatIsNotValid_IsRecordedAndTheOthersApply.
    [Fact]
    public void Run_TypeOverridesWithAnEntryThatIsNotValid_ReportThatEntryOnce()
    {
        const string Value = "int4=long\n  timestampz=System.DateTimeOffset date=System.DateOnly";

        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [
                new SqlFile(
                    "/app/Repo/Q.sql",
                    Sql,
                    Metadata: new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = Value }
                ),
            ],
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = Value }
        );

        run.Diagnostics.ShouldBe(
            [
                "SQLSRC014 (1,1)-(1,1): 'timestampz=System.DateTimeOffset' is not a valid value of "
                    + "SqlSourceTypeOverrides",
                .. Default,
            ],
            ignoreOrder: true
        );
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A key in brackets, as T-SQL writes a name, is not read, and the error shows the forms that are.
    [Fact]
    public void Run_TypeOverrideWithAKeyInBrackets_IsSqlsrc014ShowingTheFormsThatAreRead()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql)],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceTypeOverrides"] = "[dbo].[Email]=MyApp.Email nvarchar=string",
            }
        );

        run.Diagnostics.ShouldBe(
            [
                "SQLSRC014 (1,1)-(1,1): '[dbo].[Email]=MyApp.Email' is not a valid value of SqlSourceTypeOverrides.  "
                    + "A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case "
                    + "exactly",
                .. Default,
            ],
            ignoreOrder: true
        );
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("InputModelSuffix = \"Args\", OutputModelSuffix = \"Row\", ModelNamespace = \"App.Models\"")]
    [InlineData("InputModelType = GeneratorModelType.Class, OutputModelType = GeneratorModelType.SealedRecord")]
    [InlineData("InputModelSuffix = \"\", ModelNamespace = \"  \"")]
    public void Run_ValidAttributeProperty_ChangesNoGeneratedCode(string argument)
    {
        var plain = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/Q.sql", SqlAtSql)
        );

        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(argument),
            new SqlFile("/app/Repo/Q.sql", SqlAtSql)
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        plain.Sources["App.Sample.g.cs"].ShouldContain(Constant);
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Theory]
    [InlineData("Output = (GeneratorOutput)7", "7", "Output")]
    [InlineData("Output = (GeneratorOutput)(-1)", "-1", "Output")]
    [InlineData("InputModelType = (GeneratorModelType)4", "4", "InputModelType")]
    [InlineData("OutputModelType = (GeneratorModelType)9", "9", "OutputModelType")]
    [InlineData("CollectionType = (GeneratorCollectionType)10", "10", "CollectionType")]
    [InlineData("MethodLocation = (MethodLocation)4", "4", "MethodLocation")]
    [InlineData("InputModelSuffix = \"A B\"", "A B", "InputModelSuffix")]
    [InlineData("OutputModelSuffix = \"A.B\"", "A.B", "OutputModelSuffix")]
    [InlineData("ModelNamespace = \"App.\"", "App.", "ModelNamespace")]
    public void Run_AttributePropertyThatIsNotValid_IsAnErrorAtTheAttribute(
        string argument,
        string value,
        string property
    )
    {
        var run = GeneratorHarness.Run(
            null,
            GeneratorHarness.SampleSource(argument),
            new SqlFile("/app/Repo/Q.sql", Sql)
        );

        // The type's files are still checked, under the default output.
        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(Default[0]);
        var diagnostic = run
            .Diagnostics.Where(item => item.StartsWith("SQLSRC006", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
        diagnostic.ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(5,2)-");
        diagnostic.ShouldEndWith(": '" + value + "' is not a valid value of " + property);
        run.Sources.Keys.ShouldNotContain("App.Sample.g.cs");
    }

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
        plain.Sources["App.Sample.g.cs"].ShouldMatch(@"public (const string Q = |static string Q\()");
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    // At sql, so that the query has a constant, which shows the marker's line stripped as a comment.
    [Fact]
    public void Run_MethodLocationAsAPropertyOrAMarker_IsNotASetting()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", "-- name: Q\n-- method-location: public\nSELECT 1; -- c\n")],
            properties: new Dictionary<string, string?> { ["SqlSourceMethodLocation"] = "nope" }
        );

        // The property is not read, and the line is an ordinary comment.
        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("\"SELECT 1;\"");
    }

    // Below the level the project's property says another output, sql for codegen and codegen otherwise, so that the
    // level is what decides.
    [Theory]
    [MemberData(nameof(OutputLevels))]
    public void Run_OutputAtEachLevel_DecidesBetweenConstantAndMethod(SettingLevel level, string output)
    {
        var settings = new LevelSettings();
        if (level != SettingLevel.Property)
        {
            _ = settings.At(SettingLevel.Property, Setting.Output, output == "CodeGen" ? "Sql" : "CodeGen");
        }

        _ = settings.At(level, Setting.Output, output);
        var file = settings.File(Users);

        // A property among the settings' replaces the output that MethodRuns gives the project, codegen.  At sql the
        // query needs no entry, and one would be SQLSRC245.
        var run = MethodRuns.Run(
            file,
            output == "Sql"
                ? []
                : [ModelRuns.Entry(file, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])],
            settings.Source(MethodRuns.Source),
            properties: settings.Properties
        );

        run.ShouldBeClean();
        var type = run.Sources["App.Sample.g.cs"];
        if (output == "CodeGen")
        {
            type.ShouldNotContain("GetUser");
            run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUser(\n");
            ModelRuns.ModelFiles(run).ShouldBe(["App.GetUserDto-Model.g.cs", "App.GetUserParams-Model.g.cs"]);
        }
        else
        {
            type.ShouldContain("public const string GetUser = \"SELECT id FROM users WHERE id = @id;\";\n");
            run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
            ModelRuns
                .ModelFiles(run)
                .ShouldBe(output == "Models" ? ["App.GetUserDto-Model.g.cs", "App.GetUserParams-Model.g.cs"] : []);
        }
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void Run_CollectionTypeAtEachLevel_IsTheMethodsReturnType(SettingLevel level)
    {
        var settings = new LevelSettings().At(level, Setting.CollectionType, "List");
        var file = settings.File(Users);

        var run = MethodRuns.Run(
            file,
            [ModelRuns.Entry(file, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])],
            settings.Source(MethodRuns.Source),
            properties: settings.Properties
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile]
            .ShouldContain(
                " global::System.Threading.Tasks.Task<global::System.Collections.Generic.List<global::App.GetUserDto>> "
                    + "GetUser(\n"
            );
    }

    // The attribute is the setting's one place.  The default is the extension class.
    [Theory]
    [InlineData(null, "    public static partial class SampleExtensions\n", "        public static async ")]
    [InlineData(
        "MethodLocation = MethodLocation.ExtensionClass",
        "    public static partial class SampleExtensions\n",
        "        public static async "
    )]
    [InlineData("MethodLocation = MethodLocation.Public", "    partial class Sample\n", "        public static async ")]
    [InlineData(
        "MethodLocation = MethodLocation.Internal",
        "    partial class Sample\n",
        "        internal static async "
    )]
    [InlineData(
        "MethodLocation = MethodLocation.Private",
        "    partial class Sample\n",
        "        private static async "
    )]
    public void Run_MethodLocationOnTheAttribute_PlacesTheMethod(string? argument, string holder, string modifiers)
    {
        var source = argument is null ? MethodRuns.Source : MethodRuns.SampleSource(argument);

        var run = MethodRuns.Run(
            Users,
            [ModelRuns.Entry(Users, "GetUser", [ModelRuns.Parameter("id")], [ModelRuns.Column("id")])],
            source
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(holder);
        methods.ShouldContain(modifiers + "global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n");
        run.Sources["App.Sample.g.cs"].ShouldNotContain("GetUser");
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
}
