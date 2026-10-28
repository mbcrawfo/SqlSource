using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.SqlServer.Realistic;

// The batch that runs a case's query with its sample values, as the oracle's check does, once RealisticSampleChecks
// of tests/SqlSource.Tool.Testing has checked them.  Text declares a variable for each sample and passes it to the
// query through sp_executesql, so that the server runs it with parameters of the types described for it and the
// query's text is sent as a parameter of its own.
internal sealed record SampleBatch(string Text, string Statement, string Declarations);

internal static class RealisticSamples
{
    // The batch of the query for the parameters as described, in the order given.  The type of each is declared as
    // the describer typed it, and its value is the sample literal found by its name, as written then ignoring case.
    public static SampleBatch Batch(
        string sql,
        IReadOnlyDictionary<string, string> sample,
        IReadOnlyList<DescribedParameter> parameters
    )
    {
        var lines = new List<string>();
        var declarations = new List<string>();
        var passed = new List<string>();
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (parameter.Type is not SqlServerType type)
            {
                throw new InvalidOperationException($"No SQL Server type is given for @{parameter.Name}.");
            }

            var value =
                RealisticSampleChecks.Find(sample, parameter.Name)
                ?? throw new InvalidOperationException($"The sample has no value of @{parameter.Name}.");
            var declared = SqlServerTypes.Declared(type);
            var variable = $"@sample_{index}";

            // The value on lines of its own, so that a sample that ends in a comment does not take the parenthesis.
            lines.Add($"DECLARE {variable} {declared} = (\n{value}\n);");
            declarations.Add($"@{parameter.Name} {declared}");
            passed.Add($"@{parameter.Name} = {variable}");
        }

        var call =
            parameters.Count == 0
                ? "EXEC sys.sp_executesql @statement;"
                : $"EXEC sys.sp_executesql @statement, @declarations, {string.Join(", ", passed)};";
        return new SampleBatch(string.Join("\n", lines.Append(call)), sql, string.Join(", ", declarations));
    }
}
