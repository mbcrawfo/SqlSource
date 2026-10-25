using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tests.Postgres.EndToEnd;
using SqlSource.Tests.Postgres.Realistic.Describer;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// Each case with models is run over the seed through its generated method, once for each sample, with its generated
// input model as the parameters, and every row is read into its output model.  It runs what a user's code runs, where
// RealisticDapperTests runs the same SQL through Dapper, so a case with a parameter that Dapper cannot send runs here
// too.  The method is a second oracle of the models' nullability: it reads a NULL into a property that is not nullable
// by throwing, and the failure names each column that held a NULL over the seed with its property.  The check of the
// truth is still RealisticNullabilitySafetyTests'.  Some sample must read a row, so that a run that reads nothing
// cannot pass, and each sample runs in a transaction that is rolled back, since some cases write.
[Collection(UsesPostgres.Name)]
public sealed class RealisticMethodTests(PostgresContainer postgres)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Method_Case_ReadsEveryRowIntoItsModel(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        RealisticModels.SkipUnlessReadThroughMethod(found, postgres.Major);
        var entry = RealisticModels.EntryOf(found);
        var output = RealisticCases.Models.OutputModelOf(entry);
        var input = RealisticCases.Models.InputModelOf(entry);
        var method = RealisticCases.Models.MethodOf(RealisticCases.Corpus, found, entry);
        await using var source = EndToEndSources.Realistic(postgres);

        var rows = 0;
        for (var k = 0; k < found.Samples.Count; k++)
        {
            var sample = found.Samples[k];
            var model = input is null
                ? null
                : await RealisticParameters.InputModelAsync(source, input, entry.Parameters, sample);
            await using var connection = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            InvalidCastException? cast = null;
            try
            {
                var read = await RealisticCalls.InvokeAsync(
                    method,
                    connection,
                    model,
                    transaction,
                    TestContext.Current.CancellationToken
                );
                read.ShouldAllBe(row => row.GetType() == output, $"{found} read a row that is not a {output.Name}");
                rows += read.Count;
            }
            catch (InvalidCastException thrown)
            {
                cast = thrown;
            }
            finally
            {
                // Not the test's token: a cancelled test still rolls back what its query wrote.
                await transaction.RollbackAsync(CancellationToken.None);
            }

            // After the rollback, since the oracle runs the query again on a connection of its own, which would wait
            // for the locks of what this one wrote.
            if (cast is not null)
            {
                Assert.Fail(
                    $"{found} sample {k} threw {cast.Message}\n{await NullColumnsAsync(found, output, sample)}"
                );
            }
        }

        if (rows == 0)
        {
            Assert.Fail($"{found} read no row for any sample");
        }
    }

    // The columns of the sample's run over the seed that held a NULL where the output model's property cannot, run as
    // RealisticNullabilitySafetyTests runs a case.  The case runs on this server and has models, so its description
    // needs no Start.
    private async Task<string> NullColumnsAsync(
        RealisticCase found,
        Type output,
        IReadOnlyDictionary<string, string> sample
    )
    {
        var (_, parameters) = await RealisticCases.Describe.DescribedAsync(found, postgres.RealisticConnectionString);
        var request = RealisticDescribe.RequestOf(found);
        var types = parameters
            .Where(static parameter => parameter.Type is not null)
            .ToDictionary(static parameter => parameter.Name, static parameter => parameter.Type!.Name);
        var run = await RealisticNullabilityOracle.RunAsync(
            postgres.RealisticConnectionString,
            RealisticSamples.Substitute(request.Sql, sample, types)
        );
        return RealisticCalls.NullColumnsOf(found, output, run.Names, run.SawNull);
    }
}
