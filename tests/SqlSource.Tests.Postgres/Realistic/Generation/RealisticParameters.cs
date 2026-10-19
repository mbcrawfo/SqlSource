using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using SqlSource.Generation.Models;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The parameters of a case's query in the run through Dapper.  A sample is a SQL literal for each parameter, so it
// becomes a C# value as the server reads it: cast to the parameter's type and read by GetValue.  The input model is
// built from those values by its constructor, which fails when the driver gives a value of another type than the
// property's.  The query names its parameters in snake_case, which Dapper does not bind to the model's PascalCase
// properties, so the model's values go to Dapper under the SQL's names (D-0004).
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

    // A value as a model holds it: a column's NULL, which the driver reads as DBNull, is null.
    public static object? ValueOf(object value) => value is DBNull ? null : value;

    // The input model of the sample: a record, built by its constructor with each parameter's value as the argument
    // of the same name in PascalCase.
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

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < parameters.Count; ordinal++)
        {
            values.Add(PropertyNames.PascalCase(parameters[ordinal].Name), ValueOf(reader.GetValue(ordinal)));
        }

        var constructor =
            input
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(candidate => candidate.GetParameters().Length == parameters.Count)
            ?? throw new InvalidOperationException(
                $"The input model {input.Name} has no public constructor of {parameters.Count} parameters."
            );
        var arguments = constructor
            .GetParameters()
            .Select(argument =>
                argument.Name is { } argumentName && values.TryGetValue(argumentName, out var value)
                    ? value
                    : throw new InvalidOperationException(
                        $"The constructor of {input.Name} takes {argument.Name}, which is no parameter of the query."
                    )
            )
            .ToArray();
        return constructor.Invoke(arguments);
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
