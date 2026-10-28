using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Testing.Generator;
using SqlSource.DatabaseTests.SqlServer.Describer;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// Each case whose models the generator refuses is refused as its case.json says: its query is described against the
// database "realistic" into a sidecar held in memory, never written, and the generator, given the settings files of
// a build (the project's, which says mssql, and the corpus's own, which gives the database realistic) and the
// attribute's Models, gives exactly one error, of the id its models names.  So a case the generator comes to accept,
// or to refuse otherwise, fails naming itself, and its models member cannot go stale.  No case of the corpus has one
// today, so every case skips, until one comes to need it.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticModelRefusalTests(SqlServerContainer sqlServer)
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

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Run_CaseWithRefusedModels_GivesItsOneModelError(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        if (found.Models is null)
        {
            Assert.Skip($"{found} says the generator does not refuse its models.");
        }

        var refusal = found.Models;

        found.SkipUnlessRunsOn(sqlServer.Major);

        var query = new SqlFile(
            QueryPath,
            await File.ReadAllTextAsync(found.AsFixture.QueryPath, TestContext.Current.CancellationToken)
        );

        // The repository's own files, as a build of the project gives them to the generator: the project's file is the
        // harness's, whose keys it takes whole, and the corpus's file is in the folder above the query's.
        var projectSettings = await File.ReadAllTextAsync(
            RealisticCases.Corpus.ProjectSettingsFile,
            TestContext.Current.CancellationToken
        );
        var cases = GeneratorHarness.SettingsFile(
            CasesSettingsPath,
            await File.ReadAllTextAsync(RealisticCases.Corpus.CasesSettingsFile, TestContext.Current.CancellationToken)
        );
        var sidecar = await SidecarOfAsync(found, query);

        var run = GeneratorHarness.Run(
            output: null,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [query, sidecar, cases],
            references: [SqlClientReferences.SqlClient7],
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

    // The sidecar of the case's one query, described as a run describes it, against the database "realistic".
    private async Task<SqlFile> SidecarOfAsync(RealisticCase found, SqlFile query)
    {
        var opened = await Sessions.OpenAsync(
            sqlServer.RealisticConnectionString,
            database: SqlServerContainer.RealisticDatabase
        );
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {found}:\n{RealisticDescriptions.RenderFailure(opened.Failure)}"
            );
        }

        await using var session = opened.Value;
        var described = await session.DescribeAsync(
            RealisticDescribe.RequestOf(found),
            TestContext.Current.CancellationToken
        );
        if (!described.Succeeded)
        {
            Assert.Fail($"{found} was refused:\n{RealisticDescriptions.RenderFailure(described.Failure)}");
        }

        return Sidecars.Of(query, RealisticCases.Describe.EntryOf(found, session.Server, described.Value));
    }
}
