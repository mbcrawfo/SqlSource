using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// Each case whose models the generator refuses is refused as its case.json says: its query is described against the
// database "realistic" into a sidecar held in memory, never written, and the generator, given the settings files of
// a build (the project's, which names the engine, and the corpus's own, which gives the database realistic) and the
// attribute's Models, gives exactly one error, of the id its models names.  So a case the generator comes to accept,
// or to refuse otherwise, fails naming itself, and its models member cannot go stale.  Both engines'
// RealisticModelRefusalTests run it, with the references the engine's connection library needs.
internal sealed class RealisticRefusalChecks(
    RealisticDescribe describe,
    int major,
    string connection,
    IReadOnlyList<MetadataReference> references
)
{
    // Where the generator sees the corpus's settings file and the case's query, in a folder of its own below it, as
    // Realistic/Cases/<case>/query.sql is below Realistic/Cases/sqlsource.json.
    private const string CasesSettingsPath = "/app/Repo/Cases/sqlsource.json";

    private const string QueryPath = "/app/Repo/Cases/Case/query.sql";

    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Path = "Cases/Case/query.sql", Output = GeneratorOutput.Models)]
            internal static partial class Refused;
        }
        """;

    public async Task ShouldGiveItsOneModelErrorAsync(string name)
    {
        var found = describe.Corpus.ReadNamed(name);
        if (found.Models is null)
        {
            Assert.Skip($"{found} says the generator does not refuse its models.");
        }

        var refusal = found.Models;

        found.SkipUnlessRunsOn(major);

        var query = new SqlFile(
            QueryPath,
            await File.ReadAllTextAsync(found.AsFixture.QueryPath, TestContext.Current.CancellationToken)
        );

        // The repository's own files, as a build of the project gives them to the generator: the project's file is the
        // harness's, whose keys it takes whole, and the corpus's file is in the folder above the query's.
        var projectSettings = await File.ReadAllTextAsync(
            describe.Corpus.ProjectSettingsFile,
            TestContext.Current.CancellationToken
        );
        var cases = GeneratorHarness.SettingsFile(
            CasesSettingsPath,
            await File.ReadAllTextAsync(describe.Corpus.CasesSettingsFile, TestContext.Current.CancellationToken)
        );
        var sidecar = Sidecars.Of(query, await describe.EntryAsync(found, connection));

        var run = GeneratorHarness.Run(
            output: null,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [query, sidecar, cases],
            references: [.. references],
            settings: projectSettings
        );

        var errors = run
            .Result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        errors.Count.ShouldBe(
            1,
            $"{found} says the generator refuses its models as {refusal.Id}, and it gives "
                + $"{errors.Count} errors: {string.Join("; ", run.Diagnostics)}"
        );
        errors[0].Id.ShouldBe(refusal.Id, $"{found} says the generator refuses its models as {refusal.Id}");
    }
}
