using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// Each case with models is run over the seed through its generated method, once for each sample, with its generated
// input model as the parameters, and every row is read into its output model.  It runs what a user's code runs, where
// RealisticDapperChecks runs the same SQL through Dapper, so a case with a parameter that Dapper cannot send runs here
// too.  The method is a second oracle of the models' nullability: it reads a NULL into a property that is not nullable
// by throwing, and the failure names each column that held a NULL over the seed with its property.  The check of the
// truth is still RealisticNullabilityChecks'.  Some sample must read a row, so that a run that reads nothing cannot
// pass, and each sample runs on a connection of its own, in a transaction that is rolled back, since some cases
// write.  Both engines' RealisticMethodTests run it, with the engine's seed and its oracle's run of a sample, on the
// server of that major and the connection to its database "realistic".
internal sealed class RealisticMethodChecks(
    RealisticCorpus corpus,
    RealisticModels models,
    RealisticDescribe describe,
    int major,
    string connection,
    Func<IRealisticSeed> seedOf,
    RealisticSampleRun run
)
{
    public async Task ShouldReadEveryRowIntoItsModelAsync(string name)
    {
        var token = TestContext.Current.CancellationToken;
        var found = corpus.ReadNamed(name);
        RealisticModels.SkipUnlessReadThroughMethod(found, major);
        var entry = RealisticModels.EntryOf(found);
        var output = models.OutputModelOf(entry);
        var input = models.InputModelOf(entry);
        var method = models.MethodOf(corpus, found, entry);
        await using var seed = seedOf();

        var rows = 0;
        for (var k = 0; k < found.Samples.Count; k++)
        {
            var sample = found.Samples[k];
            await using var opened = await seed.OpenAsync(token);
            var model = input is null ? null : await seed.InputModelAsync(opened, input, entry.Parameters, sample);
            await using var transaction = await opened.BeginTransactionAsync(token);
            Exception? nullRead = null;
            var succeeded = false;
            try
            {
                var read = await RealisticCalls.InvokeAsync(method, opened, model, transaction, token);
                read.ShouldAllBe(row => row.GetType() == output, $"{found} read a row that is not a {output.Name}");
                rows += read.Count;
                succeeded = true;
            }
            catch (Exception thrown) when (thrown is InvalidCastException or SqlNullValueException)
            {
                // Npgsql throws InvalidCastException for a NULL it reads as a value that is not nullable, as does the
                // generated check of SQL Server's vector column; SqlClient throws SqlNullValueException for a NULL it
                // reads as a value type or a string, and does not name the column.
                nullRead = thrown;
            }
            finally
            {
                // Not the test's token: a cancelled test still rolls back what its query wrote.  After a failure, a
                // rollback that fails on the broken connection does not take the place of the failure.
                if (succeeded)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                else
                {
                    await seed.RollBackAfterAsync(transaction);
                }
            }

            // After the rollback, since the oracle runs the query again on a connection of its own, which would wait
            // for the locks of what this one wrote.
            if (nullRead is not null)
            {
                Assert.Fail(
                    $"{found} sample {k} threw {nullRead.GetType().Name}: {nullRead.Message}\n"
                        + await NullColumnsAsync(found, output, sample)
                );
            }
        }

        if (rows == 0)
        {
            Assert.Fail($"{found} read no row for any sample");
        }
    }

    // The columns of the sample's run over the seed that held a NULL where the output model's property cannot, run as
    // RealisticNullabilityChecks runs a case, or why that run failed: it is called on a failure, whose message an
    // exception of its own must not take the place of.  The case runs on this server and has models, so its
    // description needs no Start.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure of the run is put in the failure's message, which already holds the read's."
    )]
    private async Task<string> NullColumnsAsync(
        RealisticCase found,
        Type output,
        IReadOnlyDictionary<string, string> sample
    )
    {
        try
        {
            return await OracleColumnsAsync(found, output, sample);
        }
        catch (Exception thrown)
        {
            return $"{found}: the oracle's run of the sample failed too, so no column is named: {thrown}";
        }
    }

    private async Task<string> OracleColumnsAsync(
        RealisticCase found,
        Type output,
        IReadOnlyDictionary<string, string> sample
    )
    {
        var (_, parameters) = await describe.DescribedAsync(found, connection);
        var request = RealisticDescribe.RequestOf(found);
        var ran = await run(request, parameters, sample);
        return RealisticCalls.NullColumnsOf(found, output, ran.Names, ran.SawNull);
    }
}
