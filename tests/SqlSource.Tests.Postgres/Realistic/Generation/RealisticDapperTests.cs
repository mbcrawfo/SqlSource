using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd;
using SqlSource.Tool.Testing.Realistic;
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
        var found = RealisticCases.Corpus.ReadNamed(name);
        RealisticModels.SkipUnlessReadThroughDapper(found, postgres.Major);
        var entry = RealisticModels.EntryOf(found);
        var output = RealisticCases.Models.OutputModelOf(entry);
        var input = RealisticCases.Models.InputModelOf(entry);
        var sql = RealisticCases.Models.SqlOf(RealisticCases.Corpus, found, entry);
        await using var source = EndToEndSources.Realistic(postgres);

        var rows = 0;
        foreach (var sample in found.Samples)
        {
            var arguments = input is null
                ? null
                : RealisticInputs.ArgumentsOf(
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
}
