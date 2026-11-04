using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// Each case whose models the generator refuses is refused as its case.json says: its query is described against the
// database "realistic" into a sidecar held in memory, never written, and the generator, run by RealisticGeneration
// with the settings files of a build (the project's, which names the engine, and the corpus's own, which gives the
// database realistic) at the output Models of that run, never the attribute's, gives exactly one error, of the id its
// models names.  So a case the generator comes to accept, or to refuse otherwise, fails naming itself, and its models
// member cannot go stale.  Both engines' RealisticModelRefusalTests run it, with the references the engine's
// connection library needs.
internal sealed class RealisticRefusalChecks(
    RealisticDescribe describe,
    int major,
    string connection,
    IReadOnlyList<MetadataReference> references
)
{
    public async Task ShouldGiveItsOneModelErrorAsync(string name)
    {
        var found = describe.Corpus.ReadNamed(name);
        if (found.Models is null)
        {
            Assert.Skip($"{found} says the generator does not refuse its models.");
        }

        var refusal = found.Models;

        found.SkipUnlessRunsOn(major);

        var query = RealisticGeneration.QueryOf(found);
        var sidecar = Sidecars.Of(query, await describe.EntryAsync(found, connection));
        var run = RealisticGeneration.RunAtModels(describe.Corpus, query, sidecar, references);

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
