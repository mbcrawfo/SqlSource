using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Postgres.EndToEnd;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// Each case with models is run over the seed through Dapper into its generated output model, once for each sample,
// with its generated input model's values as the parameters under the SQL's names, and every row is read.  It asserts
// no value, which is the oracle's business, but that some sample reads a row, so that a run that reads nothing cannot
// pass.  A case runs in a transaction that is rolled back, since some write.
[Collection(UsesPostgres.Name)]
public sealed class RealisticDapperTests(PostgresContainer postgres)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task QueryAsync_Case_ReadsEveryRowIntoItsModel(string name)
    {
        var found = RealisticCases.ReadNamed(name);
        SkipUnlessRead(found, postgres.Major);
        var entry = RealisticModels.EntryOf(found);
        var output = RealisticModels.OutputModelOf(entry);
        var input = RealisticModels.InputModelOf(entry);
        var sql = SqlOf(found, entry);
        await using var source = EndToEndSources.Realistic(postgres);

        var rows = 0;
        foreach (var sample in found.Samples)
        {
            var arguments = input is null
                ? null
                : RealisticParameters.ArgumentsOf(
                    await RealisticParameters.InputModelAsync(source, input, entry.Parameters, sample),
                    entry.Parameters
                );
            await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var read = (await connection.QueryAsync(output, sql, arguments, transaction)).ToList();
            read.ShouldAllBe(row => row.GetType() == output, $"{found} read a row that is not a {output.Name}");
            rows += read.Count;

            // Not the test's token: a cancelled test still rolls back what its query wrote.
            await transaction.RollbackAsync(CancellationToken.None);
        }

        if (rows == 0)
        {
            Assert.Fail($"{found} read no row for any sample");
        }
    }

    // Skips a case that the run does not read, with the reason.
    private static void SkipUnlessRead(RealisticCase found, int major)
    {
        if (found.IsReadThroughDapperOn(major))
        {
            return;
        }

        if (found.Refusal is not null)
        {
            Assert.Skip($"{found} is refused as {found.Refusal}, so it has no models.");
        }

        if (found.Models is { } models)
        {
            Assert.Skip($"{found} has no models: the generator gives {models.Id}.");
        }

        if (!found.RunsOn(major))
        {
            Assert.Skip($"{found} needs PostgreSQL {found.MinimumMajor} or later, and the server is {major}.");
        }

        Assert.Skip($"{found} has a parameter that Dapper cannot send: {found.Dapper}.");
    }

    // The SQL of the case's query, the constant of its claiming class named as the query, which SqlLocation.Direct
    // writes on the class itself.
    private static string SqlOf(RealisticCase found, SidecarEntry entry)
    {
        var claim = RealisticClaims.Of(found);
        var assembly = typeof(RealisticDapperTests).Assembly;
        var type =
            assembly.GetType($"{typeof(RealisticDapperTests).Namespace}.{claim.TypeName}")
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} is not compiled");
        return type.GetField(entry.Name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(null) as string
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} has no SQL {entry.Name}");
    }
}
