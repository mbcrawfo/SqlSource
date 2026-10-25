using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Tests.SqlServer.EndToEnd;
using SqlSource.Tests.SqlServer.Realistic.Describer;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// Each case with models is run over the seed through its generated method, once for each sample, with its generated
// input model as the parameters, and every row is read into its output model.  It runs what a user's code runs, where
// RealisticDapperTests runs the same SQL through Dapper, so a case with a parameter that Dapper cannot send runs here
// too.  The method is a second oracle of the models' nullability: it reads a NULL into a property that is not nullable
// by throwing, and the failure names each column that held a NULL over the seed with its property.  The check of the
// truth is still RealisticNullabilitySafetyTests'.  Some sample must read a row, so that a run that reads nothing
// cannot pass, and each sample runs in a transaction that is rolled back, since some cases write.  The sample's values
// are read on the same connection before the transaction begins, since SqlClient refuses a command that is not given
// the transaction its connection holds.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticMethodTests(SqlServerContainer sqlServer)
{
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Method_Case_ReadsEveryRowIntoItsModel(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        RealisticModels.SkipUnlessReadThroughMethod(found, sqlServer.Major);
        var entry = RealisticModels.EntryOf(found);
        var output = RealisticCases.Models.OutputModelOf(entry);
        var input = RealisticCases.Models.InputModelOf(entry);
        var method = RealisticCases.Models.MethodOf(RealisticCases.Corpus, found, entry);
        await using var connection = EndToEndSources.Realistic(sqlServer);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var rows = 0;
        for (var k = 0; k < found.Samples.Count; k++)
        {
            var sample = found.Samples[k];
            var model = input is null
                ? null
                : await RealisticParameters.InputModelAsync(connection, input, entry.Parameters, sample);
            await using var transaction = (SqlTransaction)
                await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            Exception? nullRead = null;
            var succeeded = false;
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
                succeeded = true;
            }
            catch (Exception thrown) when (thrown is InvalidCastException or SqlNullValueException)
            {
                // SqlClient throws SqlNullValueException for a NULL it reads as a value type or a string, and does not
                // name the column; the generated check of a vector column throws InvalidCastException.
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
                    await RealisticNullabilityOracle.RollBackAfterAsync(transaction);
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
    // RealisticNullabilitySafetyTests runs a case, or why that run failed: it is called on a failure, whose message an
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
        var (_, parameters) = await RealisticCases.Describe.DescribedAsync(found, sqlServer.RealisticConnectionString);
        var request = RealisticDescribe.RequestOf(found);
        var run = await RealisticNullabilityOracle.RunAsync(
            sqlServer.RealisticConnectionString,
            RealisticSamples.Batch(request.Sql, sample, parameters)
        );
        return RealisticCalls.NullColumnsOf(found, output, run.Names, run.SawNull);
    }
}
