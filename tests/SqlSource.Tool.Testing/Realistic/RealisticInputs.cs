using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper;
using SqlSource.Generation.Models;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Testing.Realistic;

// The input model of a case's sample in the run through Dapper, whatever the engine: built by its constructor from the
// values the server reads the sample as, which fails when the driver gives a value of another type than the
// property's, and the arguments Dapper is given from it.  The query names its parameters in snake_case, which Dapper
// does not bind to the model's PascalCase properties, so the model's values go to Dapper under the SQL's names
// (D-0004).  Each engine's RealisticParameters reads the values on its own driver.
internal static class RealisticInputs
{
    // A value as a model holds it: a column's NULL, which the driver reads as DBNull, is null.
    public static object? ValueOf(object value) => value is DBNull ? null : value;

    // An input model, a record, built by its constructor with each parameter's value, in the parameters' order, as
    // the argument named as the parameter in PascalCase.  A value must be of the argument's type, and a null must be
    // one the argument can hold: Invoke would give 0 for a null where the type is a value type that cannot be null.
    // There is one value for each parameter.
    public static object ModelOf(Type input, IReadOnlyList<SidecarParameter> parameters, IReadOnlyList<object?> values)
    {
        if (values.Count != parameters.Count)
        {
            throw new InvalidOperationException(
                $"The input model {input.Name} takes one value for each parameter, but the count of values is "
                    + $"{values.Count} and the count of parameters is {parameters.Count}."
            );
        }

        var byName = new Dictionary<string, (SidecarParameter Parameter, object? Value)>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < parameters.Count; ordinal++)
        {
            byName.Add(PropertyNames.PascalCase(parameters[ordinal].Name), (parameters[ordinal], values[ordinal]));
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
            {
                if (argument.Name is not { } argumentName || !byName.TryGetValue(argumentName, out var given))
                {
                    throw new InvalidOperationException(
                        $"The constructor of {input.Name} takes {argument.Name}, which is no parameter of the query."
                    );
                }

                var type = argument.ParameterType;
                if (given.Value is null && type.IsValueType && Nullable.GetUnderlyingType(type) is null)
                {
                    throw new InvalidOperationException(
                        $"@{given.Parameter.Name} is NULL, and {input.Name}.{argumentName} is a {type.Name}, which "
                            + "cannot be null."
                    );
                }

                if (given.Value is { } value && !type.IsInstanceOfType(value))
                {
                    throw new InvalidOperationException(
                        $"@{given.Parameter.Name} is read as a {value.GetType().Name}, and {input.Name}.{argumentName} "
                            + $"is a {type.Name}."
                    );
                }

                return given.Value;
            })
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
