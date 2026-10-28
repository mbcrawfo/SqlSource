using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// The parameters of a case's query in the runs through its method and through Dapper.  A sample is a SQL literal for
// each parameter, so it becomes a C# value as the server reads it: cast to the parameter's type and read by GetValue.
// The input model is built from those values by RealisticInputs of tests/SqlSource.Tool.Testing, which fails when the
// driver gives a value of another type than the property's.  The method is given the model as it is, and Dapper its
// values under the SQL's names by ArgumentsOf (D-0004).
internal static class RealisticParameters
{
    // A select of each of the sample's literals cast to its parameter's type, in the parameters' order.
    public static string SelectOf(
        IReadOnlyDictionary<string, string> sample,
        IReadOnlyList<SidecarParameter> parameters
    ) =>
        "SELECT "
        + string.Join(
            ", ",
            parameters.Select(parameter =>
            {
                var type =
                    parameter.Type?.Name
                    ?? throw new InvalidOperationException($"The parameter @{parameter.Name} has no type to cast to.");
                return sample.TryGetValue(parameter.Name, out var literal)
                    ? $"({literal})::{type}"
                    : throw new InvalidOperationException($"The sample gives no literal for @{parameter.Name}.");
            })
        );

    // The input model of the sample, built by RealisticInputs.ModelOf from the values the server reads its literals as.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a case's own sample literals, from the repository, cast to its parameters' types."
    )]
    public static async Task<object> InputModelAsync(
        NpgsqlDataSource source,
        Type input,
        IReadOnlyList<SidecarParameter> parameters,
        IReadOnlyDictionary<string, string> sample
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = source.CreateCommand(SelectOf(sample, parameters));
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
        {
            throw new InvalidOperationException("The select of the sample's literals gave no row.");
        }

        var values = new object?[parameters.Count];
        for (var ordinal = 0; ordinal < parameters.Count; ordinal++)
        {
            values[ordinal] = RealisticInputs.ValueOf(reader.GetValue(ordinal));
        }

        return RealisticInputs.ModelOf(input, parameters, values);
    }
}
