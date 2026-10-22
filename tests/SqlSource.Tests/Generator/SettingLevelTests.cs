// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The settings of models and methods are accepted at every level they have, and a value none of them takes is
// reported where it was written.  Without a dialect no query can have models, so they change only the diagnostics.
//
// Every run leaves the output unset, since the output's levels are among what these tests compare.  The project
// sets no dialect either, so a query whose output is the default, codegen, is SQLSRC209 at its name.
public sealed class SettingLevelTests
{
    private const string Sql = "-- name: Q\nSELECT 1;\n";

    // The single query of Sql under the defaults, Q at its first line.
    private static readonly string[] Default = [NotDescribable("codegen", 1)];

    public static TheoryData<string, string, string[], string[]> MarkersWithoutAnEffect =>
        new()
        {
            {
                "-- name: Q\nSELECT id FROM users WHERE id = @id;\n",
                "-- database: main\n-- output: models\n-- input-model-suffix: Args\n-- output-model-suffix: Row\n"
                    + "-- model-namespace: App.Models\n-- input-model-type: sealed record\n"
                    + "-- output-model-type: class\n-- collection-type: ImmutableArray\n"
                    + "-- type-override: numeric = System.Double\n\n"
                    + "-- name: Q -> one\n-- param: @id int not null\n-- database: other\n-- output: codegen\n"
                    + "-- input-model: QueryArgs\n-- output-model: App.Models.QueryRow\n-- collection-type: List\n"
                    + "-- type-override: numeric = default\n-- type-override: int4 = long\n"
                    + "SELECT id FROM users WHERE id = @id;\n",
                Default,
                [NotDescribable("codegen", 11)]
            },
            {
                "-- name: Q\nSELECT id FROM {{table}} WHERE {{filter}};\n",
                "-- output: models\n\n-- name: Q -> many\n-- param: @since timestamptz not null\n"
                    + "-- param: @page int\n-- token: {{filter:created_at > @since}}\n"
                    + "SELECT id FROM {{table:users}} WHERE {{filter}};\n",
                [NotDescribable("codegen", 1), NoDefault("table"), NoDefault("filter")],
                [NotDescribable("models", 3)]
            },
        };

    [Theory]
    [InlineData("SqlSourceOutput", "models", "models")]
    [InlineData("SqlSourceOutput", " Code-Gen ", "codegen")]
    [InlineData("SqlSourceDatabase", "not read by the generator, so never wrong", "codegen")]
    [InlineData("SqlSourceInputModelSuffix", "Args", "codegen")]
    [InlineData("SqlSourceOutputModelSuffix", "Row", "codegen")]
    [InlineData("SqlSourceModelNamespace", "App.Models", "codegen")]
    [InlineData("SqlSourceInputModelType", "sealed record", "codegen")]
    [InlineData("SqlSourceOutputModelType", "Class", "codegen")]
    [InlineData("SqlSourceCollectionType", "IReadOnlyList", "codegen")]
    [InlineData("SqlSourceTypeOverrides", "timestamptz=System.DateTimeOffset  int4[]=int?[]", "codegen")]
    public void Run_ValidSettingAsPropertyAndAsMetadata_ChangesNoGeneratedCode(string name, string value, string output)
    {
        var plain = GeneratorHarness.Run(null, Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", Sql, Metadata: new Dictionary<string, string> { [name] = value })],
            properties: new Dictionary<string, string?> { [name] = value }
        );

        plain.Diagnostics.ShouldBe(Default);
        run.Diagnostics.ShouldBe([NotDescribable(output, 1)]);
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
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
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
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
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
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
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
    [InlineData("Output = GeneratorOutput.Sql", null)]
    [InlineData("Output = GeneratorOutput.Models", "models")]
    [InlineData("Output = GeneratorOutput.CodeGen", "codegen")]
    [InlineData("InputModelSuffix = \"Args\", OutputModelSuffix = \"Row\", ModelNamespace = \"App.Models\"", "codegen")]
    [InlineData(
        "InputModelType = GeneratorModelType.Class, OutputModelType = GeneratorModelType.SealedRecord",
        "codegen"
    )]
    [InlineData("CollectionType = GeneratorCollectionType.IReadOnlyList", "codegen")]
    [InlineData("MethodLocation = MethodLocation.Internal", "codegen")]
    [InlineData("InputModelSuffix = \"\", ModelNamespace = \"  \"", "codegen")]
    public void Run_ValidAttributeProperty_ChangesNoGeneratedCode(string argument, string? output)
    {
        var plain = GeneratorHarness.Run(null, Source(null), new SqlFile("/app/Repo/Q.sql", Sql));

        var run = GeneratorHarness.Run(null, Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

        // At sql the query needs nothing that the dialect cannot give.
        run.Diagnostics.ShouldBe(output is null ? [] : [NotDescribable(output, 1)]);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
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
        var run = GeneratorHarness.Run(null, Source(argument), new SqlFile("/app/Repo/Q.sql", Sql));

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
    // one of the same file without them.  The output's markers and the tokens' defaults change only the diagnostics.
    [Theory]
    [MemberData(nameof(MarkersWithoutAnEffect))]
    public void Run_MarkersWithoutAnEffect_GiveTheFileOfTheSameQueryWithoutThem(
        string plainSql,
        string markedSql,
        string[] plainDiagnostics,
        string[] markedDiagnostics
    )
    {
        var plain = GeneratorHarness.Run(null, Source(null), new SqlFile("/app/Repo/Q.sql", plainSql));

        var run = GeneratorHarness.Run(null, Source(null), new SqlFile("/app/Repo/Q.sql", markedSql));

        plain.Diagnostics.ShouldBe(plainDiagnostics);
        run.Diagnostics.ShouldBe(markedDiagnostics);
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldBe(plain.Sources["App.Sample.g.cs"]);
    }

    [Fact]
    public void Run_MethodLocationAsAPropertyOrAMarker_IsNotASetting()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, Source(null))],
            [new SqlFile("/app/Repo/Q.sql", "-- name: Q\n-- method-location: public\nSELECT 1; -- c\n")],
            properties: new Dictionary<string, string?> { ["SqlSourceMethodLocation"] = "nope" }
        );

        // The property is not read, and the line is an ordinary comment.
        run.Diagnostics.ShouldBe(Default);
        run.Sources["App.Sample.g.cs"].ShouldContain("\"SELECT 1;\"");
    }

    // SQLSRC209 for the query Q at this line, under the default dialect.
    private static string NotDescribable(string output, int line) =>
        $"SQLSRC209 /app/Repo/Q.sql({line},10)-({line},11): The output '{output}' needs each query described by its "
        + "database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the "
        + "output to 'sql'.";

    // SQLSRC210 for this token of the query Q at the first line, under the default output.
    private static string NoDefault(string token) =>
        $"SQLSRC210 /app/Repo/Q.sql(1,10)-(1,11): The token '{token}' has no default, which a query whose output is "
        + "'codegen' needs.  Give it a default, or set the output to 'sql'.";

    private static string Source(string? argument) =>
        "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct"
        + (argument is null ? string.Empty : ", " + argument)
        + ")]\npublic partial class Sample;\n";
}
