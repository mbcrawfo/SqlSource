using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.SqlServer.EndToEnd;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// Each case with models is run over the seed through Dapper into its generated output model, once for each sample,
// with its generated input model's values as the parameters under the SQL's names, and every row is read.  It asserts
// no value, which is the oracle's business, but that some sample reads a row, so that a run that reads nothing cannot
// pass.  A case runs in a transaction that is rolled back, since some write, and that every Dapper call is given, since
// SqlClient refuses a command on a connection that holds a transaction it was not given.  The sample's values are
// read on the same connection before the transaction begins.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticDapperTests(SqlServerContainer sqlServer)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task QueryAsync_Case_ReadsEveryRowIntoItsModel(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        SkipUnlessRead(found, sqlServer.Major);
        var entry = RealisticModels.EntryOf(found);
        var output = RealisticCases.Models.OutputModelOf(entry);
        var input = RealisticCases.Models.InputModelOf(entry);
        var sql = SqlOf(found, entry);
        await using var connection = EndToEndSources.Realistic(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var rows = 0;
        foreach (var sample in found.Samples)
        {
            var arguments = input is null
                ? null
                : RealisticParameters.ArgumentsOf(
                    await RealisticParameters.InputModelAsync(connection, input, entry.Parameters, sample),
                    entry.Parameters
                );
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

        RealisticModels.SkipUnlessHasModels(found);
        found.SkipUnlessRunsOn(major);
        Assert.Skip($"{found} has a parameter that Dapper cannot send: {found.Dapper}.");
    }

    // The SQL of the case's query, the constant of its claiming class named as the query, which SqlLocation.Direct
    // writes on the class itself.
    private static string SqlOf(RealisticCase found, SidecarEntry entry)
    {
        var claim = RealisticClaims.Of(RealisticCases.Corpus, found);
        var assembly = typeof(RealisticDapperTests).Assembly;
        var type =
            assembly.GetType($"{typeof(RealisticDapperTests).Namespace}.{claim.TypeName}")
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} is not compiled");
        return type.GetField(entry.Name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(null) as string
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} has no SQL {entry.Name}");
    }
}
