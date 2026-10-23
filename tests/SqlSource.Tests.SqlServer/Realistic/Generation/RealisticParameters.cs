using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SqlSource.Generation.Models;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Generation;

// The parameters of a case's query in the run through Dapper.  A sample is a T-SQL expression for each parameter, so
// it becomes a C# value as the server reads it: cast to the parameter's type and read by GetValue.  The input model is
// built from those values by RealisticInputs of tests/SqlSource.Tool.Testing, which fails when the driver gives a value
// of another type than the property's.  The query names its parameters in snake_case, which Dapper does not bind to
// the model's PascalCase properties, so the model's values go to Dapper under the SQL's names (D-0004).
internal static class RealisticParameters
{
    // A select of each of the sample's expressions cast to its parameter's type, in the parameters' order, the type as
    // the sidecar writes it.  The sample is found by its parameter's name as written, then ignoring case, and stands
    // on lines of its own, so that a sample that ends in a comment does not take the parenthesis.
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
                return RealisticSampleChecks.Find(sample, parameter.Name) is { } expression
                    ? $"CAST((\n{expression}\n) AS {type})"
                    : throw new InvalidOperationException($"The sample gives no expression for @{parameter.Name}.");
            })
        );

    // The input model of the sample, built by RealisticInputs.ModelOf from the values the server reads its expressions
    // as, on an open connection that holds no transaction, since SqlClient refuses a command that is not given the one
    // it holds.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a case's own samples, from the repository, cast to its parameters' types."
    )]
    public static async Task<object> InputModelAsync(
        SqlConnection connection,
        Type input,
        IReadOnlyList<SidecarParameter> parameters,
        IReadOnlyDictionary<string, string> sample
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = new SqlCommand(SelectOf(sample, parameters), connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
        {
            throw new InvalidOperationException("The select of the sample's expressions gave no row.");
        }

        var values = new object?[parameters.Count];
        for (var ordinal = 0; ordinal < parameters.Count; ordinal++)
        {
            values[ordinal] = RealisticInputs.ValueOf(reader.GetValue(ordinal));
        }

        return RealisticInputs.ModelOf(input, parameters, values);
    }

    // The model's values, each under its parameter's name in the SQL.
    public static DynamicParameters ArgumentsOf(object model, IReadOnlyList<SidecarParameter> parameters)
    {
        var type = model.GetType();
        var arguments = new DynamicParameters();
        foreach (var parameter in parameters)
        {
            var name = PropertyNames.PascalCase(parameter.Name);
            var property =
                type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"The input model {type.Name} has no property {name} for @{parameter.Name}."
                );
            arguments.Add(parameter.Name, property.GetValue(model));
        }

        return arguments;
    }
}
