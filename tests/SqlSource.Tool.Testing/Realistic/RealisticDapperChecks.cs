using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// Each case with models is run over the seed through Dapper into its generated output model, once for each sample,
// with its generated input model's values as the parameters under the SQL's names, and every row is read.  The SQL is
// what the generator writes for the case at Models, from an in-process run by RealisticGeneration.  It asserts no
// value, which is the oracle's business, but that some sample reads a row, so that a run that reads nothing cannot
// pass.  Each sample runs on a connection of its own, in a transaction that is rolled back, since some cases write,
// and that every Dapper call is given, since SqlClient refuses a command on a connection that holds a transaction it
// was not given.  Both engines' RealisticDapperTests run it, with the engine's seed.
internal sealed class RealisticDapperChecks(
    RealisticCorpus corpus,
    RealisticModels models,
    int major,
    Func<IRealisticSeed> seedOf
)
{
    public async Task ShouldReadEveryRowIntoItsModelAsync(string name)
    {
        var token = TestContext.Current.CancellationToken;
        var found = corpus.ReadNamed(name);
        RealisticModels.SkipUnlessReadThroughDapper(found, major);
        var entry = RealisticModels.EntryOf(found);
        var output = models.OutputModelOf(entry);
        var input = models.InputModelOf(entry);
        var sql = RealisticGeneration.SqlOf(corpus, found, entry);
        await using var seed = seedOf();

        var rows = 0;
        foreach (var sample in found.Samples)
        {
            await using var connection = await seed.OpenAsync(token);
            var arguments = input is null
                ? null
                : RealisticInputs.ArgumentsOf(
                    await seed.InputModelAsync(connection, input, entry.Parameters, sample),
                    entry.Parameters
                );
            await using var transaction = await connection.BeginTransactionAsync(token);
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
