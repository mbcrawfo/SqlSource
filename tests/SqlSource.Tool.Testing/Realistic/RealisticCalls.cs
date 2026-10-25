using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Testing.EndToEnd;

namespace SqlSource.Tool.Testing.Realistic;

// The call of a case's generated method, which a test finds by reflection since the corpus's tests are shared and the
// method is of each engine's project: the check that it has the signature the generator writes, the call itself, and
// how a failure names a column that held a NULL where its property cannot hold one.  The reflection is the test's; no
// generated or shipped code uses it.
internal static class RealisticCalls
{
    private const string Connection = "connection";
    private const string Parameters = "parameters";
    private const string Transaction = "transaction";
    private const string CommandTimeout = "commandTimeout";
    private const string CancellationToken = "cancellationToken";

    // Fails unless the method has the generated signature of a query that reads its rows into the output model, with
    // the input model when the query has parameters: the parameters by name and type in their order, and the task of
    // a read-only list of rows.  The failure names the method and what differs.
    public static void CheckSignature(MethodInfo method, Type output, Type? input)
    {
        var expected = ParametersOf(input);
        var actual = method
            .GetParameters()
            .Select(static parameter => (parameter.ParameterType, parameter.Name))
            .ToList();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidOperationException(
                $"{NameOf(method)} has the parameters ({Spelled(actual)}), and a generated method has "
                    + $"({Spelled(expected)})"
            );
        }

        var returns = typeof(Task<>).MakeGenericType(typeof(IReadOnlyList<>).MakeGenericType(output));
        if (method.ReturnType != returns)
        {
            throw new InvalidOperationException(
                $"{NameOf(method)} returns {NameOf(method.ReturnType)}, and a generated method returns "
                    + NameOf(returns)
            );
        }
    }

    // Calls a method that CheckSignature has passed, with no command timeout, and gives back every row it read.  An
    // exception that the method throws before it gives a task surfaces as itself, never as TargetInvocationException,
    // as one that its task throws does.
    public static async Task<IReadOnlyList<object>> InvokeAsync(
        MethodInfo method,
        DbConnection connection,
        object? input,
        DbTransaction transaction,
        CancellationToken token
    )
    {
        var arguments = method
            .GetParameters()
            .Select(parameter =>
                parameter.Name switch
                {
                    Connection => connection,
                    Parameters => input,
                    Transaction => transaction,
                    CommandTimeout => null,
                    CancellationToken => token,
                    _ => throw new InvalidOperationException(
                        $"{NameOf(method)} has a parameter {parameter.Name}, which a generated method has not"
                    ),
                }
            )
            .ToArray();

        var task = Start(method, arguments);
        await task;
        var rows = method.ReturnType.GetProperty(nameof(Task<>.Result))!.GetValue(task);
        return [.. ((IEnumerable)rows!).Cast<object>()];
    }

    // How a failure names each column that held a NULL over the seed where the output model's property cannot hold
    // one, a line each, or says that there is none.  A column is paired with the property at its position, and a run
    // of another count of columns than the model's properties says so first, since it is called on a failure whose
    // own message must not be lost to an exception of its own.
    public static string NullColumnsOf(
        RealisticCase found,
        Type output,
        IReadOnlyList<string> names,
        IReadOnlyList<bool> sawNull
    )
    {
        var properties = ModelReflection.PropertiesOf(output);
        var lines = new List<string>();
        if (sawNull.Count != properties.Count)
        {
            lines.Add(
                $"{found} read {sawNull.Count} columns over the seed, and {output.Name} has "
                    + $"{properties.Count} properties"
            );
        }

        for (var index = 0; index < Math.Min(sawNull.Count, properties.Count); index++)
        {
            var property = properties[index];
            if (sawNull[index] && !ModelReflection.IsNullable(property))
            {
                lines.Add(
                    $"{found} column {index} {names[index]} is the property {output.Name}.{property.Name} of "
                        + $"{property.PropertyType.Name}, which is not nullable, and held a NULL over the seed"
                );
            }
        }

        return lines.Count > 0
            ? string.Join('\n', lines)
            : $"{found}: the oracle saw no NULL in a column whose property is not nullable";
    }

    // The parameters of the generated signature, by type and name.
    private static List<(Type Type, string? Name)> ParametersOf(Type? input) =>
        [
            (typeof(DbConnection), Connection),
            .. input is null ? [] : new[] { (input, (string?)Parameters) },
            (typeof(DbTransaction), Transaction),
            (typeof(int?), CommandTimeout),
            (typeof(CancellationToken), CancellationToken),
        ];

    // The task the method gives.  Invoke wraps what the method throws before then in TargetInvocationException, which
    // is taken off with the exception's stack kept.
    private static Task Start(MethodInfo method, object?[] arguments)
    {
        try
        {
            return (Task)method.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException thrown) when (thrown.InnerException is { } inner)
        {
            ExceptionDispatchInfo.Capture(inner).Throw();
            throw;
        }
    }

    private static string Spelled(IEnumerable<(Type Type, string? Name)> parameters) =>
        string.Join(", ", parameters.Select(static parameter => $"{NameOf(parameter.Type)} {parameter.Name}"));

    private static string NameOf(MethodInfo method) => $"{method.DeclaringType?.Name}.{method.Name}";

    // A type as C# writes it, without namespaces: Task<List<Row>>.
    private static string NameOf(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<"
                + string.Join(", ", type.GetGenericArguments().Select(NameOf))
                + ">"
            : type.Name;
}
