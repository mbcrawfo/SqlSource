using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The connections of a run's selected databases.  It reports nothing: a database with no connection is an error
/// only when a query of it must be described, which <see cref="DatabaseRuns" /> knows.
/// </summary>
internal sealed class Connections
{
    public const string CommandLineSource = "--connection";

    private readonly Dictionary<string, DatabaseConnection> _byDatabase;

    private Connections(
        Dictionary<string, DatabaseConnection> byDatabase,
        ImmutableArray<string> databases,
        ImmutableArray<string> unusedNames,
        bool unnamedIsSetAndNotUsed
    )
    {
        _byDatabase = byDatabase;
        Databases = databases;
        UnusedNames = unusedNames;
        UnnamedIsSetAndNotUsed = unnamedIsSetAndNotUsed;
    }

    /// <summary>The selected databases, in the plan's order.</summary>
    public ImmutableArray<string> Databases { get; }

    /// <summary>
    /// The names <c>--connection</c> gave that are no selected database, in the order given.  Not an error: a
    /// script may give every connection it has.  The help of <c>SQLSRC213</c> lists them.
    /// </summary>
    public ImmutableArray<string> UnusedNames { get; }

    /// <summary>
    /// Whether <c>SQLSOURCE_CONNECTION</c> is set and the run has more than one selected database, so that it is
    /// used for none (<c>SQLSRC215</c>).
    /// </summary>
    public bool UnnamedIsSetAndNotUsed { get; }

    /// <summary>The connection of a selected database, by its name in any case.</summary>
    public DatabaseConnection For(string database) => _byDatabase[database];

    /// <summary>
    /// Finds the connection of each selected database: the command line's value for its name, ignoring case; else
    /// the variable of its name; else <c>SQLSOURCE_CONNECTION</c>, when exactly one database is selected.  A
    /// variable that is empty is not set.
    /// </summary>
    /// <param name="selected">The selected databases, in the plan's order, each once ignoring case.</param>
    /// <param name="given">The values of <c>--connection</c>, each name once ignoring case.</param>
    /// <param name="getEnvironmentVariable">The host's.</param>
    public static Connections Resolve(
        IReadOnlyList<string> selected,
        IReadOnlyList<ConnectionArgument> given,
        Func<string, string?> getEnvironmentVariable
    )
    {
        var onCommandLine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in given)
        {
            onCommandLine[argument.Name] = argument.Value;
        }

        var unnamed = NullIfEmpty(getEnvironmentVariable(ConnectionVariables.Unnamed));
        var variables = selected.Select(ConnectionVariables.For).ToArray();
        var byDatabase = new Dictionary<string, DatabaseConnection>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < selected.Count; index++)
        {
            var name = selected[index];
            var variable = variables[index];
            if (SharerOf(index, selected, variables, onCommandLine) is { } other)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, null, null, other);
            }
            else if (onCommandLine.TryGetValue(name, out var value))
            {
                byDatabase[name] = new DatabaseConnection(name, variable, value, CommandLineSource, null);
            }
            else if (NullIfEmpty(getEnvironmentVariable(variable)) is { } ofName)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, ofName, variable, null);
            }
            else if (selected.Count == 1 && unnamed is not null)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, unnamed, ConnectionVariables.Unnamed, null);
            }
            else
            {
                byDatabase[name] = new DatabaseConnection(name, variable, null, null, null);
            }
        }

        return new Connections(
            byDatabase,
            [.. selected],
            [.. given.Select(static argument => argument.Name).Where(name => !byDatabase.ContainsKey(name))],
            unnamed is not null && selected.Count > 1
        );
    }

    // The first other selected database with the same variable, when the variable would be read for either.
    private static string? SharerOf(
        int index,
        IReadOnlyList<string> selected,
        string[] variables,
        Dictionary<string, string> onCommandLine
    )
    {
        for (var other = 0; other < selected.Count; other++)
        {
            if (
                other != index
                && string.Equals(variables[other], variables[index], StringComparison.Ordinal)
                && !(onCommandLine.ContainsKey(selected[index]) && onCommandLine.ContainsKey(selected[other]))
            )
            {
                return selected[other];
            }
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
