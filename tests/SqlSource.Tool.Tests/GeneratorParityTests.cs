using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Tests.Generator;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan must agree with the generator on which type claims which file with which output: a query the tool does
// not describe is a build error from phase 5, and one it describes for nothing costs a description.  The generator
// reads symbols and the tool reads syntax, so each case runs both over the same sources and file list and compares
// what they find.
public sealed class GeneratorParityTests : IDisposable
{
    private const string Using = "using SqlSource;\n";

    private static readonly Dictionary<string, Case> Cases = new()
    {
        ["no path"] = new(
            1,
            [("Repo/Repository.cs", Using + "[SqlSourceGenerate]\ninternal partial class Repository;\n")],
            ["Repo/A.sql", "Repo/b.sql", "Repo/Sub/C.sql", "Other/D.sql", "E.sql"]
        ),
        ["a folder, a file and a path that leaves its folder"] = new(
            4,
            [
                (
                    "Repo/Types.cs",
                    Using
                        + "[SqlSourceGenerate(Path = \"Queries\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = @\"Queries\\Users.sql\")]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Path = \"../Shared/\")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Path = \"Nowhere\")]\ninternal partial class D;\n"
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
                        + "[SqlSourceGenerate(Path = \"\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = null)]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Path = \" \")]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Path = \"\"\"Q\"\"\")]\ninternal partial class D;\n"
                        + "[SqlSourceGenerate(Path = \"\\u0051\")]\ninternal partial class E;\n"
                        + "[SqlSourceGenerate(Path = default)]\ninternal partial class F;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["each output"] = new(
            5,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Output = GeneratorOutput.Sql)]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Output = GeneratorOutput.Models)]\ninternal partial class B;\n"
                        + "[SqlSourceGenerate(Output = SqlSource.GeneratorOutput.CodeGen)]\ninternal partial class C;\n"
                        + "[SqlSourceGenerate(Output = global::SqlSource.GeneratorOutput.Models, Path = \"Q\")]\n"
                        + "internal partial class D;\n"
                        + "[SqlSourceGenerate(Parameters = \"keep-comments\", ModelNamespace = \"App.Models\")]\n"
                        + "internal partial class E;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["each name of the attribute"] = new(
            4,
            [
                (
                    "Types.cs",
                    "[SqlSource.SqlSourceGenerateAttribute]\ninternal partial class A;\n"
                        + "[global::SqlSource.SqlSourceGenerate]\ninternal partial class B;\n"
                        + "[type: SqlSource.SqlSourceGenerate]\ninternal partial class C;\n"
                        + "[System.Serializable, SqlSource.SqlSourceGenerate(Path = \"Q\")]\n"
                        + "internal partial class D;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
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
                        + "        [SqlSourceGenerate(Path = \"Q\")]\n        private partial class Inner;\n    }\n"
                        + "    [SqlSourceGenerate]\n    internal static partial class E;\n"
                        + "    [SqlSourceGenerate]\n    internal partial interface INotOne;\n"
                        + "}\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
        ),
        ["a type the generator rejects"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate]\ninternal class NotPartial;\n"
                        + "[SqlSourceGenerate(Path = \"Q\")]\nfile partial class FileLocal;\n"
                ),
            ],
            ["Root.sql", "Q/One.sql"]
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
            ["Root.sql"],
            ["FEATURE"]
        ),
        ["paths that differ only by case"] = new(
            2,
            [
                (
                    "Types.cs",
                    Using
                        + "[SqlSourceGenerate(Path = \"q\")]\ninternal partial class A;\n"
                        + "[SqlSourceGenerate(Path = \"Q/USERS.sql\")]\ninternal partial class B;\n"
                ),
            ],
            ["Q/Users.sql", "Q/users.SQL", "Q/Orders.sql"]
        ),
        ["two source files in two folders"] = new(
            2,
            [
                ("A/One.cs", Using + "[SqlSourceGenerate]\ninternal partial class One;\n"),
                ("B/Two.cs", Using + "[SqlSourceGenerate(Path = \"../A\")]\ninternal partial class Two;\n"),
                ("B/None.cs", "internal partial class None;\n"),
            ],
            ["A/A.sql", "B/B.sql"]
        ),
    };

    private readonly TempFolder _folder = new();

    public static TheoryData<string> CaseNames => [with(Cases.Keys)];

    public void Dispose() => _folder.Dispose();

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Claims_OfTheSameSourcesAndFiles_AreTheTypeFilesOfTheGenerator(string name)
    {
        var (types, sources, sqlFiles, constants) = Cases[name];
        // The harness of the generator parses C# 12, the newest that its oldest Roslyn reads.
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

    // What the generator's own step gives for each attributed type.
    private static Claimed[] FromGenerator(SourceFile[] sources, string[] sqlFiles, string[] constants)
    {
        var options = GeneratorHarness.ParseOptions.WithPreprocessorSymbols(constants);
        var compilation = GeneratorHarness.CreateCompilation(sources, parseOptions: options);
        var driver = GeneratorHarness
            .CreateDriver(sqlFiles.Select(static path => new SqlFile(path, "SELECT 1;").ToAdditionalText()), options)
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
                    type.Type.Path,
                    type.Type.Settings.Output?.ToString(),
                    string.Join('|', type.Files)
                ))
        );
    }

    // What the tool's reader and the planner's own list of files give for the same project.
    private static Claimed[] FromTool(ProjectManifest manifest)
    {
        var claims = AttributeReader.Read(manifest);
        claims.Errors.ShouldBeEmpty();
        var listed = ListedFiles.Read(manifest);
        return Ordered(
            claims.Claims.Select(claim => new Claimed(
                claim.SourcePath,
                claim.Position.Line,
                claim.Path,
                claim.Output?.ToString(),
                string.Join('|', listed.ClaimedBy(claim))
            ))
        );
    }

    private static Claimed[] Ordered(IEnumerable<Claimed> claims) =>
        [.. claims.OrderBy(claim => claim.SourcePath, StringComparer.Ordinal).ThenBy(claim => claim.Line)];

    // One type as each side sees it: the file and the line of its attribute, what the attribute sets, and the .sql
    // files it claims, in member order.
    private sealed record Claimed(string SourcePath, int Line, string? Path, string? Output, string Files);

    private sealed record Case(
        int Types,
        (string Path, string Text)[] Sources,
        string[] SqlFiles,
        string[]? Constants = null
    );
}
