using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Sidecars;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Testing;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Tests.Planning;

// The plan must agree with the generator on which type claims which file, and on which of its queries need an entry: a
// query the tool does not describe is a build error, SQLSRC241, and one it describes for nothing costs a description.
// The generator reads symbols and the tool reads syntax, so each case runs both over the same sources and file list and
// compares what they find.  Both report SQLSRC209 and SQLSRC210, which must be at the same places with the same text.
// The errors of the settings files, SQLSRC015 to SQLSRC020, are compared the same way, where the two reach the same
// files.
public sealed class GeneratorParityTests : IDisposable
{
    private const string Using = "using SqlSource;\n";

    private static readonly Dictionary<string, Case> Cases = new()
    {
        ["the folder"] = new(
            1,
            [
                (
                    "Repo/Repository.cs",
                    Using + "[SqlSourceGenerate(Source = \".\")]\ninternal partial class Repository;\n"
                ),
            ],
            ["Repo/A.sql", "Repo/b.sql", "Repo/Sub/C.sql", "Other/D.sql", "E.sql"]
        ),
        ["the default, each type its own file"] = new(
            3,
            [
                (
                    "Repo/Types.cs",
                    Using
                        + "[SqlSourceGenerate]\ninternal partial class UserRepository;\n"
                        + "[SqlSourceGenerate]\ninternal partial class OrderRepository;\n"
                        + "[SqlSourceGenerate]\ninternal partial class Repo<T>;\n"
                ),
            ],
            [
                "Repo/UserRepository.sql",
                "Repo/orderrepository.SQL",
                "Repo/Repo.sql",
                "Repo/Other.sql",
                "UserRepository.sql",
            ]
        ),
        ["the default, nested and escaped"] = new(
            3,
            [
                (
                    "Types.cs",
                    Using
                        + "internal partial class Outer\n{\n"
                        + "    [SqlSourceGenerate]\n    private partial class Inner;\n}\n"
                        + "[SqlSourceGenerate]\ninternal partial class @event;\n"
                        + "[SqlSourceGenerate]\ninternal partial class \\u0041pp;\n"
                ),
            ],
            ["Inner.sql", "Outer.sql", "event.sql", "App.sql"]
        ),
        ["the default, the attribute in one part of a partial type"] = new(
            1,
            [
                ("Repo/UserRepository.cs", "internal partial class UserRepository;\n"),
                (
                    "Repo/Queries/UserRepository.Queries.cs",
                    Using + "[SqlSourceGenerate]\ninternal partial class UserRepository;\n"
                ),
            ],
            ["Repo/UserRepository.sql", "Repo/Queries/UserRepository.sql"]
        ),
        ["the folder and the default in one folder"] = new(
            2,
            [
                (
                    "Repo/Types.cs",
                    Using
                        + "[SqlSourceGenerate(Source = \".\")]\ninternal partial class All;\n"
                        + "[SqlSourceGenerate]\ninternal partial class UserRepository;\n"
                ),
            ],
            ["Repo/UserRepository.sql", "Repo/Orders.sql"]
        ),
        ["the default with no file"] = new(
            1,
            [("Repo/Missing.cs", Using + "[SqlSourceGenerate]\ninternal partial class Missing;\n")],
            ["Repo/Other.sql", "Missing.sql"]
        ),
        ["a folder, a file and a path that leaves its folder"] = new(
            4,
            [
                (
                    "Repo/Types.cs",
                    Using
                        + "[SqlSourceGenerate(Source = \"Queries\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Source = @\"Queries\\Users.sql\")]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Source = \"../Shared/\")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Source = \"Nowhere\")]\ninternal partial class D;\n"
                ),
            ],
            ["Repo/Queries/Orders.sql", "Repo/Queries/Users.sql", "Repo/Queries/Deep/X.sql", "Shared/S.sql"]
        ),
        ["a path written each way"] = new(
            6,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Source = \"\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Source = null)]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Source = \" \")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Source = \"\"\"Q\"\"\")]\ninternal partial class D;\n"
                        + "[SqlSourceGenerate(Source = \"\\u0051\")]\ninternal partial class E;\n"
                        + "[SqlSourceGenerate(Source = default)]\ninternal partial class F;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql", "A.sql", "B.sql", "F.sql"]
        ),
        ["each name of the attribute"] = new(
            4,
            [
                (
                    "Types.cs",
                    "[SqlSource.SqlSourceGenerateAttribute]\ninternal partial class A;\n"
                        + "[global::SqlSource.SqlSourceGenerate]\ninternal partial class B;\n"
                        + "[type: SqlSource.SqlSourceGenerate]\ninternal partial class C;\n"
                        + "[System.Serializable, SqlSource.SqlSourceGenerate(Source = \"Q\")]\n"
                        + "internal partial class D;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql", "A.sql", "B.sql", "C.sql"]
        ),
        ["each kind of type"] = new(
            5,
            [
                (
                    "Types.cs",
                    Using
                        + "namespace App\n{\n"
                        + "    [SqlSourceGenerate]\n    internal partial struct A { }\n"
                        + "    [SqlSourceGenerate]\n    internal partial record B;\n"
                        + "    [SqlSourceGenerate]\n    internal partial record struct C;\n"
                        + "    internal partial class Outer\n    {\n"
                        + "        [SqlSourceGenerate(Source = \"Q\")]\n        private partial class Inner;\n    }\n"
                        + "    [SqlSourceGenerate]\n    internal static partial class E;\n"
                        + "    [SqlSourceGenerate]\n    internal partial interface INotOne;\n"
                        + "}\n"
                ),
            ],
            ["Root.sql", "Q/One.sql", "A.sql", "B.sql", "C.sql", "E.sql"]
        ),
        ["a type the generator rejects"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate]\ninternal class NotPartial;\n"
                        + "[SqlSourceGenerate(Source = \"Q\")]\nfile partial class FileLocal;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql", "NotPartial.sql"]
        ),
        ["an attribute inside #if"] = new(
            1,
            [
                (
                    "Types.cs",
                    "#if FEATURE\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class A;\n"
                        + "#if OTHER\n[SqlSource.SqlSourceGenerate]\n#endif\ninternal partial class B;\n"
                ),
            ],
            ["Root.sql", "A.sql", "B.sql"],
            ["FEATURE"]
        ),
        ["paths that differ only by case"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Source = \"q\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Source = \"Q/USERS.sql\")]\ninternal partial class B;\n"
                ),
            ],
            ["Q/Users.sql", "Q/users.SQL", "Q/Orders.sql"]
        ),
        ["two source files in two folders"] = new(
            2,
            [
                ("A/One.cs", Using + "[SqlSourceGenerate]\ninternal partial class One;\n"),
                ("B/Two.cs", Using + "[SqlSourceGenerate(Source = \"../A\")]\ninternal partial class Two;\n"),
                ("B/None.cs", "internal partial class None;\n"),
            ],
            ["A/A.sql", "A/One.sql", "B/B.sql"]
        ),
    };

    // The errors that both report for a file: one query or more that needs an entry under a dialect that cannot
    // give one, and tokens without a default.  Each case has one type at the project's root, which claims every file.
    private static readonly Dictionary<string, ErrorCase> ErrorCases = new()
    {
        ["an ansi file at models"] = new(
            "ansi",
            OutputKind.Models,
            [
                ("One.sql", "-- name: First\nSELECT 1;\n\n-- name: Second\nSELECT 2;\n"),
                ("Two.sql", "-- name: Third\nSELECT 3;\n"),
            ]
        ),
        ["an ansi file whose first query is at sql"] = new(
            "ansi",
            OutputKind.Methods,
            [("One.sql", "-- name: First\n-- output: sql\nSELECT 1;\n\n-- name: Second\nSELECT 2;\n")]
        ),
        ["tokens without a default under postgres"] = new(
            "postgres",
            OutputKind.Models,
            [
                (
                    "Find.sql",
                    "-- name: Find\nSELECT id FROM users {{where}} {{order}} {{limit:LIMIT 10}} {{tail:}};\n\n"
                        + "-- name: Plain\n-- output: sql\nSELECT {{columns}} FROM users;\n\n"
                        + "-- name: Later\n-- token: {{order:ORDER BY id}}\nSELECT id FROM users {{order}} {{page}};\n"
                ),
            ]
        ),
        ["both in one file"] = new(
            "sqlite",
            OutputKind.Methods,
            [
                (
                    "Find.sql",
                    "-- name: Plain\n-- output: sql\nSELECT {{columns}};\n\n-- name: Find\nSELECT 1 {{where}};\n\n"
                        + "-- name: Count\nSELECT 2 {{where}} {{group}};\n"
                ),
            ]
        ),
    };

    // The errors of the settings files that both report, with the files that the case reaches them from.  The paths
    // are from the folder of the temporary files, where the project's own file is App.csproj, and none has a value of
    // a setting that only one side reads, such as a database or a type override.
    private static readonly Dictionary<string, SettingsCase> SettingsCases = new()
    {
        ["errors of the file"] = new(
            3,
            [("App/Queries.cs", Plans.Type)],
            [],
            [
                (
                    "App/sqlsource.json", /*lang=json*/
                    "{\n  \"Dialect\": \"postgres\",\n  \"output\": 1,\n  \"output\": \"sql\",\n}"
                ),
            ]
        ),
        ["invalid dialect and output"] = new(
            2,
            [("App/Queries.cs", Plans.Type)],
            [],
            [
                (
                    "App/sqlsource.json", /*lang=json,strict*/
                    "{\"dialect\": \"pgsql\", \"output\": \"model\"}"
                ),
            ]
        ),
        ["text that is not JSON, and the farther file through it"] = new(
            2,
            [],
            ["App/Queries/X.sql"],
            [
                ("App/Queries/sqlsource.json", "{ nope"),
                (
                    "App/sqlsource.json", /*lang=json,strict*/
                    "{\"unknown\": 1}"
                ),
            ]
        ),
        ["a file above the root file"] = new(
            1,
            [("App/Queries.cs", Plans.Type)],
            [],
            [
                ("sqlsource.json", "{ nope"),
                (
                    "App/sqlsource.json", /*lang=json,strict*/
                    "{\"root\": true, \"Root\": 1}"
                ),
            ]
        ),
        ["reached by a C# file alone"] = new(
            1,
            [("App/Code/Repository.cs", "internal sealed class Repository;\n")],
            [],
            [
                (
                    "App/Code/sqlsource.json", /*lang=json,strict*/
                    "{\"unknown\": 1}"
                ),
            ]
        ),
        ["reached by an unclaimed .sql file"] = new(
            1,
            [("App/Queries.cs", Plans.TypeNamed("Queries", "Source = \"Queries\""))],
            ["App/Other/X.sql"],
            [
                (
                    "App/Other/sqlsource.json", /*lang=json,strict*/
                    "{\"unknown\": 1}"
                ),
            ]
        ),
        ["a C# file under a build folder"] = new(
            1,
            [("App/obj/Gen.cs", "internal sealed class Gen;\n"), ("App/Queries.cs", Plans.Type)],
            [],
            [
                (
                    "App/obj/sqlsource.json", /*lang=json,strict*/
                    "{\"obj\": 1}"
                ),
                (
                    "App/sqlsource.json", /*lang=json,strict*/
                    "{\"unknown\": 1}"
                ),
            ],
            ["App/obj"]
        ),
    };

    private readonly TempFolder _folder = new();

    public static TheoryData<string> CaseNames => [with(Cases.Keys)];

    public static TheoryData<string> ErrorCaseNames => [with(ErrorCases.Keys)];

    public static TheoryData<string> SettingsCaseNames => [with(SettingsCases.Keys)];

    public void Dispose() => _folder.Dispose();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Claims_OfTheSameSourcesAndFiles_AreTheTypeFilesOfTheGenerator(string name)
    {
        var (types, sources, sqlFiles, constants) = Cases[name];
        // The harness of the generator parses C# 12, the language of generated code, and the project is given the same.
        var project = new TestProject(_folder) { LangVersion = "12.0" };
        project.Constants.AddRange(constants ?? []);
        var written = sources.Select(source => new SourceFile(
            project.AddSource(source.Path, source.Text),
            source.Text
        ));
        var listed = sqlFiles.Select(path => project.AddSql(path, "SELECT 1;"));

        var expected = FromGenerator([.. written], [.. listed], constants ?? []);
        var actual = FromTool(project.Manifest());

        actual.ShouldBe(expected);
        // A case in which neither finds a type would prove nothing.
        expected.Length.ShouldBe(types);
    }

    // Two types claim one file, and its header gives the output, so the two sides agree on which of its queries need
    // an entry: none under sql, and each under models.
    [Theory]
    [InlineData("sql")]
    [InlineData("models")]
    public void TwoClaims_OfOneFile_DescribeWhatItsHeaderSays(string output)
    {
        var project = new TestProject(_folder) { LangVersion = "12.0" };
        project.Settings["dialect"] = "postgres";
        const string First = Using + "[SqlSourceGenerate(Source = \"Q\")]\ninternal partial class A;\n";
        const string Second = Using + "[SqlSourceGenerate(Source = \"Q/One.sql\")]\ninternal partial class B;\n";
        SourceFile[] sources =
        [
            new(project.AddSource("A.cs", First), First),
            new(project.AddSource("B.cs", Second), Second),
        ];
        var text = "-- output: " + output + "\n\n-- name: Find\nSELECT 1;\n";
        var sql = new SqlFile(project.AddSql("Q/One.sql", text), text);

        var plan = RunPlanner.Plan([project.Manifest()], RunFilters.None, TestContext.Current.CancellationToken);
        var settings = GeneratorHarness.SettingsFile(project.SettingsPath, File.ReadAllText(project.SettingsPath));
        var run = GeneratorHarness.Run(null, sources, [sql, settings]);

        string[] expected =
        [
            .. StepOutputs
                .Of<EquatableArray<FileNeeds>>(run, TrackingNames.FileNeeds)
                .SelectMany(files => files)
                .SelectMany(file => file.Queries)
                .Select(query => query.Name),
        ];
        string[] actual =
        [
            .. plan
                .Plan.Files.SelectMany(file => file.Queries)
                .Where(query => query.NeedsEntry)
                .Select(query => query.Query.Name),
        ];

        // Both types claim the file, or the case would prove nothing of two claims.
        plan.Plan.Files.ShouldHaveSingleItem().Claims.Count.ShouldBe(2);
        actual.ShouldBe(expected);
        expected.ShouldBe(output == "sql" ? [] : ["Find"]);
    }

    [Theory]
    [MemberData(nameof(ErrorCaseNames))]
    public void Errors_OfTheSameSourcesAndFiles_AreTheGeneratorsSqlsrc209And210(string name)
    {
        var (dialect, output, sqlFiles) = ErrorCases[name];
        var project = new TestProject(_folder) { LangVersion = "12.0" };
        project.Settings["dialect"] = dialect;
        project.Settings["output"] = SettingValue.NameOf(output);
        var source = new SourceFile(project.AddSource("Queries.cs", Plans.Type), Plans.Type);
        var listed = sqlFiles.Select(file => new SqlFile(project.AddSql(file.Path, file.Text), file.Text)).ToArray();

        // The manifest writes the project's settings file, and the generator is given that file, by its path and its
        // text, and none that the tool's side cannot read.
        var plan = RunPlanner.Plan([project.Manifest()], RunFilters.None, TestContext.Current.CancellationToken);
        var settings = GeneratorHarness.SettingsFile(project.SettingsPath, File.ReadAllText(project.SettingsPath));
        var run = GeneratorHarness.Run(null, [source], [.. listed, settings]);

        // Each side is compared by its message, as Reported.MessageOf says.
        var expected = ReportedByGenerator(run, IsCompared);
        var actual = ReportedByTool(plan, IsCompared);

        actual.ShouldBe(expected);
        // A case in which neither reports an error would prove nothing, and each case has one of the two ids.
        expected.ShouldNotBeEmpty();
        // Only the two errors are compared; neither side has another for these files.
        run.Diagnostics.Count.ShouldBe(expected.Length);
        plan.Errors.Count.ShouldBe(actual.Length);
    }

    // The errors of the settings files, with the files each side reaches them from and the build's folders that a
    // compile file reaches nothing under.  Neither side reports a value that only the other reads, so the two lists
    // are compared whole.
    [Theory]
    [MemberData(nameof(SettingsCaseNames))]
    public void Errors_OfSettingsFiles_AreTheGeneratorsAtTheSamePlaces(string name)
    {
        var (errors, sources, sqlFiles, settingsFiles, buildFolders) = SettingsCases[name];
        var project = new TestProject(_folder, directory: "") { LangVersion = "12.0" };
        if (buildFolders is not null)
        {
            // The package's format: each folder after a bar, with a separator at its end.
            project.BuildFolders = string.Concat(
                buildFolders.Select(folder => "|" + project.PathOf(folder) + Path.DirectorySeparatorChar)
            );
        }

        var written = sources
            .Select(source => new SourceFile(project.AddSource(source.Path, source.Text), source.Text))
            .ToArray();
        const string Query = "SELECT 1;";
        var listed = sqlFiles.Select(path => new SqlFile(project.AddSql(path, Query), Query)).ToArray();
        var settings = settingsFiles
            .Select(file => GeneratorHarness.SettingsFile(project.AddSettings(file.Path, file.Json), file.Json))
            .ToArray();

        var run = GeneratorHarness.Run(
            null,
            written,
            [.. listed, .. settings],
            properties: new Dictionary<string, string?> { [BuildFolders.PropertyName] = project.BuildFolders }
        );
        var plan = RunPlanner.Plan([project.Manifest()], RunFilters.None, TestContext.Current.CancellationToken);

        var expected = ReportedByGenerator(run, IsSettingsFileError);
        var actual = ReportedByTool(plan, IsSettingsFileError);

        actual.ShouldBe(expected);
        // A case in which neither side reports would prove nothing.
        expected.Length.ShouldBe(errors);
    }

    // The errors that the files' dialects and outputs make: the dialect and the output of a folder's file are the
    // generator's own, so the two sides give the same SQLSRC209 and SQLSRC210 for the queries of each folder.
    [Fact]
    public void Errors_OfDialectAndOutputFromSettingsFiles_AreTheGeneratorsSqlsrc209And210()
    {
        var project = new TestProject(_folder, directory: "") { LangVersion = "12.0" };
        var source = new SourceFile(project.AddSource("App/Queries.cs", Plans.Type), Plans.Type);
        // A type claims the files of its own folder alone, so the file of the folder below has a type of its own.
        var lite = Plans.TypeNamed("Lite");
        var liteSource = new SourceFile(project.AddSource("App/Lite/Lite.cs", lite), lite);
        const string Ansi = "-- name: First\nSELECT 1 {{where}};\n";
        const string Lite = "-- name: Second\nSELECT 2 {{where}} {{order:ORDER BY 1}};\n";
        const string Json = /*lang=json,strict*/
            "{\"dialect\": \"ansi\", \"output\": \"models\"}";
        const string LiteJson = /*lang=json,strict*/
            "{\"dialect\": \"sqlite\"}";
        SqlFile[] files =
        [
            new(project.AddSql("App/First.sql", Ansi), Ansi),
            new(project.AddSql("App/Lite/Second.sql", Lite), Lite),
            GeneratorHarness.SettingsFile(project.AddSettings("App/sqlsource.json", Json), Json),
            GeneratorHarness.SettingsFile(project.AddSettings("App/Lite/sqlsource.json", LiteJson), LiteJson),
        ];

        var run = GeneratorHarness.Run(null, [source, liteSource], files);
        var plan = RunPlanner.Plan([project.Manifest()], RunFilters.None, TestContext.Current.CancellationToken);

        var expected = ReportedByGenerator(run, IsCompared);
        var actual = ReportedByTool(plan, IsCompared);

        actual.ShouldBe(expected);
        // A case in which neither reports an error would prove nothing, and the file of each folder has both.
        expected
            .Select(error => $"{error.Id} {Path.GetFileName(error.Path)}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ShouldBe(["SQLSRC209 First.sql", "SQLSRC209 Second.sql", "SQLSRC210 First.sql", "SQLSRC210 Second.sql"]);
    }

    private static bool IsCompared(DiagnosticDescriptor descriptor) =>
        descriptor.Id == ToolDiagnostics.OutputNeedsDescribableDialect.Id
        || descriptor.Id == ToolDiagnostics.TokenHasNoDefault.Id;

    // The errors of the settings files, SQLSRC015 to SQLSRC020.
    private static bool IsSettingsFileError(DiagnosticDescriptor descriptor) =>
        string.CompareOrdinal(descriptor.Id, "SQLSRC015") >= 0
        && string.CompareOrdinal(descriptor.Id, "SQLSRC020") <= 0;

    // What the generator reported of the kind, as each is compared: by its place and its message.
    private static Reported[] ReportedByGenerator(GeneratorRun run, Func<DiagnosticDescriptor, bool> compared) =>
        InOrder(
            run.Result.Diagnostics.Where(diagnostic => compared(diagnostic.Descriptor))
                .Select(diagnostic =>
                {
                    var span = diagnostic.Location.GetLineSpan();
                    return new Reported(
                        diagnostic.Id,
                        span.Path,
                        span.StartLinePosition,
                        diagnostic.GetMessage(CultureInfo.InvariantCulture)
                    );
                })
        );

    private static Reported[] ReportedByTool(RunPlanResult plan, Func<DiagnosticDescriptor, bool> compared) =>
        InOrder(plan.Errors.Where(error => compared(error.Descriptor)).Select(Reported.Of));

    private static Reported[] InOrder(IEnumerable<Reported> reported) =>
        [
            .. reported
                .OrderBy(error => error.Path, StringComparer.Ordinal)
                .ThenBy(error => error.Position?.Line)
                .ThenBy(error => error.Position?.Character)
                .ThenBy(error => error.Id, StringComparer.Ordinal)
                .ThenBy(error => error.Message, StringComparer.Ordinal),
        ];

    // What the generator's own step gives for each attributed type.
    private static Claimed[] FromGenerator(SourceFile[] sources, string[] sqlFiles, string[] constants)
    {
        var options = GeneratorHarness.ParseOptions.WithPreprocessorSymbols(constants);
        var compilation = GeneratorHarness.CreateCompilation(sources, parseOptions: options);
        var driver = GeneratorHarness
            .CreateDriver(
                sqlFiles.Select(static path => new SqlFile(path, "SELECT 1;").ToAdditionalText()),
                new TestOptionsProvider(),
                options
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        if (!steps.TryGetValue(TrackingNames.TypeFiles, out var runs))
        {
            return [];
        }

        return Ordered(
            runs.SelectMany(run => run.Outputs)
                .Select(output => (TypeFiles)output.Value)
                .Select(type => new Claimed(
                    type.Type.FilePath,
                    type.Type.AttributeLocation.LineSpan.Start.Line,
                    type.Type.Name,
                    type.Type.Source,
                    string.Join('|', type.Files)
                ))
        );
    }

    // What the tool's reader and the planner's own list of files give for the same project.
    private static Claimed[] FromTool(ProjectManifest manifest)
    {
        var claims = AttributeReader.Read(manifest, TestContext.Current.CancellationToken);
        claims.Errors.ShouldBeEmpty();
        var listed = ListedFiles.Read(manifest);
        return Ordered(
            claims.Claims.Select(claim => new Claimed(
                claim.SourcePath,
                claim.Position.Line,
                claim.TypeName,
                claim.Source,
                string.Join('|', listed.ClaimedBy(claim))
            ))
        );
    }

    private static Claimed[] Ordered(IEnumerable<Claimed> claims) =>
        [.. claims.OrderBy(claim => claim.SourcePath, StringComparer.Ordinal).ThenBy(claim => claim.Line)];

    // One type as each side sees it: the file and the line of its attribute, the type's name, its Source, and the .sql
    // files it claims, in member order.
    private sealed record Claimed(string SourcePath, int Line, string Name, string? Source, string Files);

    private sealed record ErrorCase(string Dialect, OutputKind Output, (string Path, string Text)[] SqlFiles);

    private sealed record SettingsCase(
        int Errors,
        (string Path, string Text)[] Sources,
        string[] SqlFiles,
        (string Path, string Json)[] SettingsFiles,
        string[]? BuildFolders = null
    );

    private sealed record Case(
        int Types,
        (string Path, string Text)[] Sources,
        string[] SqlFiles,
        string[]? Constants = null
    );
}
