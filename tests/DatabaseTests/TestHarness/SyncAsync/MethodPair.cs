using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// A synchronous method and its asynchronous twin, declared by one type.
internal sealed record MethodPair(
    Type Host,
    string Method,
    MethodKind Kind,
    bool IsInstance,
    MethodInfo Sync,
    MethodInfo Async
)
{
    // Calls the method with arguments by parameter name: connection, parameters, each token, transaction,
    // commandTimeout, cancellationToken (CancellationToken.None); awaits a Task, reads Task<T>.Result.  Returns the
    // value, or rethrows the method's own exception (unwrapping TargetInvocationException).
    public async Task<object?> InvokeAsync(
        MethodInfo method,
        object? instance,
        DbConnection connection,
        DbTransaction? transaction,
        Scenario scenario
    )
    {
        if (method != Sync && method != Async)
        {
            throw new ArgumentException($"{method.Name} is not a method of the pair {Method}.", nameof(method));
        }

        var arguments = method
            .GetParameters()
            .Select(parameter => ArgumentOf(parameter, connection, transaction, scenario))
            .ToArray();

        object? returned;
        try
        {
            returned = method.Invoke(instance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }

        if (returned is not Task task)
        {
            return returned;
        }

        await task.ConfigureAwait(false);
        return task.GetType().IsGenericType ? task.GetType().GetProperty(nameof(Task<>.Result))!.GetValue(task) : null;
    }

    private static object? ArgumentOf(
        ParameterInfo parameter,
        DbConnection connection,
        DbTransaction? transaction,
        Scenario scenario
    )
    {
        switch (parameter.Name)
        {
            case "connection":
                return connection;
            case "parameters":
                return scenario.Parameters;
            case "transaction":
                return transaction;
            case "commandTimeout":
                return scenario.CommandTimeout;
            case "cancellationToken":
                return CancellationToken.None;
            default:
                break;
        }

        return scenario.Tokens is not null && scenario.Tokens.TryGetValue(parameter.Name!, out var token)
            ? token
            : throw new InvalidOperationException(
                $"{scenario.Name}: no argument for parameter '{parameter.Name}' of {parameter.Member.Name}."
            );
    }
}

internal static class MethodPairs
{
    private const BindingFlags AllMethods =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Static
        | BindingFlags.Instance
        | BindingFlags.DeclaredOnly;

    // Every method M and MAsync declared on one non-interface type of the assembly (any accessibility, static or
    // instance), where MAsync returns Task or Task<T> and takes M's parameters plus a trailing CancellationToken.
    // Kind: Collection when "parameters" is IEnumerable<T> of a model, else Query; IsInstance for a repository's
    // instance methods.  A pair is keyed by Host, Method and Kind, since a collection method overloads the query's.
    public static IReadOnlyList<MethodPair> Find(Assembly assembly)
    {
        var pairs = new List<MethodPair>();
        foreach (var host in assembly.GetTypes().Where(static type => !type.IsInterface))
        {
            var methods = host.GetMethods(AllMethods);
            foreach (var sync in methods)
            {
                var twin = methods.FirstOrDefault(candidate => IsTwin(sync, candidate));
                if (twin is not null)
                {
                    pairs.Add(new MethodPair(host, sync.Name, KindOf(sync), !sync.IsStatic, sync, twin));
                }
            }
        }

        var duplicate = pairs
            .GroupBy(static pair => (pair.Host, pair.Method, pair.Kind))
            .FirstOrDefault(static g => g.Count() > 1);
        return duplicate is null
            ?
            [
                .. pairs
                    .OrderBy(static pair => pair.Host.FullName, StringComparer.Ordinal)
                    .ThenBy(static pair => pair.Method, StringComparer.Ordinal)
                    .ThenBy(static pair => pair.Kind),
            ]
            : throw new InvalidOperationException(
                $"{duplicate.Key.Host} has two {duplicate.Key.Kind} pairs for {duplicate.Key.Method}."
            );
    }

    private static bool IsTwin(MethodInfo sync, MethodInfo candidate)
    {
        if (
            candidate.Name != sync.Name + "Async"
            || candidate.IsStatic != sync.IsStatic
            || !typeof(Task).IsAssignableFrom(candidate.ReturnType)
        )
        {
            return false;
        }

        var expected = sync.GetParameters().Select(static parameter => parameter.ParameterType).ToList();
        expected.Add(typeof(CancellationToken));
        return candidate.GetParameters().Select(static parameter => parameter.ParameterType).SequenceEqual(expected);
    }

    private static MethodKind KindOf(MethodInfo sync)
    {
        var parameters = sync.GetParameters().FirstOrDefault(static parameter => parameter.Name == "parameters");
        return parameters is not null && IsCollection(parameters.ParameterType)
            ? MethodKind.Collection
            : MethodKind.Query;
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) && (IsEnumerableOf(type) || type.GetInterfaces().Any(IsEnumerableOf));

    private static bool IsEnumerableOf(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>);
}
