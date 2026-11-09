using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tool.Testing.Realistic;

// Runs the generator in process on a case's query at Models, for the SQL of its run through Dapper and for the refusal
// of its models.
//
// The run is fed the files a build of the engine's project gives the generator, so that what it writes is what the
// build writes: the case's query.sql verbatim, with its own header markers; its sidecar; Cases/sqlsource.json in the
// folder above the query, which gives the database realistic and the type override; and the project's sqlsource.json
// as the harness's project file, which names the dialect and whose generator parameters reach the run.
//
// The project's file says "output": "Methods", and the harness lets a key of the settings it is given replace the
// output it writes, so the key is removed and the run's OutputKind.Models stands.  At Methods a query has a method and
// no constant.
//
// SQLSRC240, a sidecar written by another version of the tool, is let through: the projects keep it a warning in
// WarningsNotAsErrors, so a raise of the version before the sidecars are described again still builds, and the SQL is
// the query's.  Any other diagnostic, such as a stale entry, fails naming the case, and no SQL is given from it.
internal static class RealisticGeneration
{
    // The generated file of the type that claims the case.
    public const string TypeFile = "App.Realistic.g.cs";

    // Where the generator sees the corpus's settings file and the case's query, in a folder of its own below it, as
    // Realistic/Cases/<case>/query.sql is below Realistic/Cases/sqlsource.json.
    private const string CasesSettingsPath = "/app/Repo/Cases/sqlsource.json";

    private const string QueryPath = "/app/Repo/Cases/Case/query.sql";

    // Without the using, the attribute does not bind and the run claims nothing.
    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = "Cases/Case/query.sql")]
            internal static partial class Realistic;
        }
        """;

    // A settings file's text as the generator reads it: with comments and trailing commas.
    private static readonly JsonDocumentOptions SettingsText = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // The SQL of the case's query, the constant the generator writes for its entry at Models.
    public static string SqlOf(RealisticCorpus corpus, RealisticCase found, SidecarEntry entry)
    {
        var query = QueryOf(found);
        return ConstantOf(RunAtModels(corpus, query, CommittedSidecarOf(found, query), []), found, entry.Name);
    }

    // The case's query.sql, verbatim, where the run sees it.
    public static SqlFile QueryOf(RealisticCase found) => new(QueryPath, File.ReadAllText(found.AsFixture.QueryPath));

    // The case's committed sidecar, unchanged, beside the query.
    public static SqlFile CommittedSidecarOf(RealisticCase found, SqlFile query) =>
        new(query.Path + ".json", File.ReadAllText(found.SidecarPath));

    // A run of the generator at Models over the query and the sidecar, with the settings files of a build of the
    // corpus's project, and the references a run needs beyond the runtime's.
    public static GeneratorRun RunAtModels(
        RealisticCorpus corpus,
        SqlFile query,
        SqlFile sidecar,
        IReadOnlyList<MetadataReference> references
    )
    {
        var cases = GeneratorHarness.SettingsFile(CasesSettingsPath, File.ReadAllText(corpus.CasesSettingsFile));
        var project =
            JsonNode.Parse(File.ReadAllText(corpus.ProjectSettingsFile), documentOptions: SettingsText) as JsonObject
            ?? throw new InvalidOperationException($"{corpus.ProjectSettingsFile} is not a JSON object");
        _ = project.Remove("output");

        return GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [query, sidecar, cases],
            references: [.. references],
            settings: GeneratorHarness.Json(project)
        );
    }

    // The text of the constant of that name in the file of the type that claims the case, once the run reported
    // nothing but SQLSRC240.
    public static string ConstantOf(GeneratorRun run, RealisticCase found, string name)
    {
        if (
            run.Result.Diagnostics.Any(static diagnostic =>
                diagnostic.Id != SqlDiagnostics.SidecarToolVersionDiffers.Id
            )
        )
        {
            throw new InvalidOperationException(
                $"{found} gives diagnostics at Models: " + string.Join("; ", run.Diagnostics)
            );
        }

        var declarators = run.Sources.TryGetValue(TypeFile, out var text)
            ? CSharpSyntaxTree
                .ParseText(text)
                .GetRoot()
                .DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Where(static field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
                .SelectMany(static field => field.Declaration.Variables)
                .Where(variable => variable.Identifier.ValueText == name)
                .ToList()
            : [];

        return declarators is [{ Initializer.Value: LiteralExpressionSyntax literal }]
            ? literal.Token.ValueText
            : throw new InvalidOperationException(
                $"{found} has no constant {name} at Models: a query with a token gets a method, and a name that is "
                    + "not its entry's gets nothing"
            );
    }
}
