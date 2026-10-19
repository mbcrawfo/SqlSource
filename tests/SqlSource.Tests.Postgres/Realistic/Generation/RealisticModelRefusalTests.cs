using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Npgsql;
using Shouldly;
using SqlSource.Testing.Generator;
using SqlSource.Tests.Postgres.Describer;
using SqlSource.Tests.Postgres.Realistic.Describer;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// Each case whose models the generator refuses is refused as its case.json says: its query is described against the
// database "realistic" into a sidecar held in memory, never written, and the generator, at Models under postgres,
// gives exactly one error, of the id its models names.  So a case the generator comes to accept, or to refuse
// otherwise, fails naming itself, and its models member cannot go stale.
[Collection(UsesPostgres.Name)]
public sealed class RealisticModelRefusalTests(PostgresContainer postgres)
{
    // Where the generator sees the case's query, beside the class that claims it, as RealisticQueries.cs claims one.
    private const string QueryPath = "/app/Repo/Cases/query.sql";

    private const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Path = "Cases/query.sql", Output = GeneratorOutput.Models)]
            internal static partial class Refused;
        }
        """;

    // The metadata that the project gives the corpus's queries.
    private static readonly IReadOnlyDictionary<string, string> CorpusMetadata = new Dictionary<string, string>
    {
        ["SqlSourceDatabase"] = PostgresContainer.RealisticDatabase,
        ["SqlSourceTypeOverrides"] = "timestamptz=default",
    };

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Run_CaseWithRefusedModels_GivesItsOneModelError(string name)
    {
        var found = RealisticCases.ReadNamed(name);
        if (found.Models is null)
        {
            Assert.Skip($"{found} says the generator does not refuse its models.");
        }

        var refusal = found.Models;

        if (!found.RunsOn(postgres.Major))
        {
            Assert.Skip($"{found} needs PostgreSQL {found.MinimumMajor} or later, and the server is {postgres.Major}.");
        }

        var query = new SqlFile(
            QueryPath,
            await File.ReadAllTextAsync(found.AsFixture.QueryPath, TestContext.Current.CancellationToken),
            Metadata: CorpusMetadata
        );
        var sidecar = await SidecarOfAsync(found, query);

        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [query, sidecar],
            references: [MetadataReference.CreateFromFile(typeof(NpgsqlConnection).Assembly.Location)],
            dialect: "postgres"
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

    // The sidecar of the case's one query, described as a run describes it, against the database "realistic".
    private async Task<SqlFile> SidecarOfAsync(RealisticCase found, SqlFile query)
    {
        var opened = await Sessions.OpenAsync(
            postgres.RealisticConnectionString,
            database: PostgresContainer.RealisticDatabase
        );
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {found}:\n{RealisticDescribe.RenderFailure(opened.Failure)}"
            );
        }

        await using var session = opened.Value;
        var described = await session.DescribeAsync(
            RealisticDescribe.RequestOf(found),
            TestContext.Current.CancellationToken
        );
        if (!described.Succeeded)
        {
            Assert.Fail($"{found} was refused:\n{RealisticDescribe.RenderFailure(described.Failure)}");
        }

        return Sidecars.Of(query, RealisticDescribe.EntryOf(found, session.Server, described.Value));
    }
}
