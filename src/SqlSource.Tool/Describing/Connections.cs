using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Tool.Reporting;

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
        ImmutableArray<string> describable,
        ImmutableArray<string> unusedNames,
        ImmutableArray<int> unusedPositions,
        bool unnamedIsSetAndNotUsed
    )
    {
        _byDatabase = byDatabase;
        Databases = databases;
        Describable = describable;
        UnusedNames = unusedNames;
        UnusedPositions = unusedPositions;
        UnnamedIsSetAndNotUsed = unnamedIsSetAndNotUsed;
    }

    /// <summary>The selected databases, in the plan's order.</summary>
    public ImmutableArray<string> Databases { get; }

    /// <summary>
    /// The selected databases that can be described, in the plan's order: the ones <c>SQLSOURCE_CONNECTION</c>
    /// could be meant for.
    /// </summary>
    public ImmutableArray<string> Describable { get; }

    /// <summary>
    /// The names <c>--connection</c> gave that are no selected database and are close to a database of the plan,
    /// ignoring case, in the order given.  Not an error: a script may give every connection it has.  The help of
    /// <c>SQLSRC213</c> lists them.
    /// </summary>
    public ImmutableArray<string> UnusedNames { get; }

    /// <summary>
    /// The positions of the values of <c>--connection</c> that are no selected database and whose name is far from
    /// every database of the plan, in the order given.  Such a name may be the start of a secret typed in the wrong
    /// place, so the help of <c>SQLSRC213</c> counts it by its position and never prints it.
    /// </summary>
    public ImmutableArray<int> UnusedPositions { get; }

    /// <summary>
    /// Whether <c>SQLSOURCE_CONNECTION</c> is set and the run has more than one selected database that can be
    /// described, so that it is used for none (<c>SQLSRC215</c>).
    /// </summary>
    public bool UnnamedIsSetAndNotUsed { get; }

    /// <summary>The connection of a selected database, by its name in any case.</summary>
    public DatabaseConnection For(string database) => _byDatabase[database];

    /// <summary>
    /// Finds the connection of each selected database: the command line's value for its name, ignoring case; else
    /// the variable of its name; else <c>SQLSOURCE_CONNECTION</c>, when it is the one selected database that can be
    /// described.  A variable that is empty, or holds only white space, is not set: redacting a space would turn
    /// every space of a describer's text into the mark.
    /// </summary>
    /// <param name="selected">The selected databases, in the plan's order, each once ignoring case.</param>
    /// <param name="describable">
    /// Those of <paramref name="selected" /> with a query that the plan did not fail, in the plan's order.
    /// </param>
    /// <param name="planned">
    /// Every database of the plan, selected or not: the names that <c>--connection</c> may be close to.
    /// </param>
    /// <param name="given">The values of <c>--connection</c>, each name once ignoring case.</param>
    /// <param name="getEnvironmentVariable">The host's.</param>
    public static Connections Resolve(
        IReadOnlyList<string> selected,
        IReadOnlyList<string> describable,
        IReadOnlyList<string> planned,
        IReadOnlyList<ConnectionArgument> given,
        Func<string, string?> getEnvironmentVariable
    )
    {
        var onCommandLine = new Dictionary<string, string>(DatabaseName.Comparer);
        foreach (var argument in given)
        {
            onCommandLine[argument.Name] = argument.Value;
        }

        var unnamed = NullIfBlank(getEnvironmentVariable(ConnectionVariables.Unnamed));
        var variables = selected.Select(ConnectionVariables.For).ToArray();
        var byDatabase = new Dictionary<string, DatabaseConnection>(DatabaseName.Comparer);
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
            else if (NullIfBlank(getEnvironmentVariable(variable)) is { } ofName)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, ofName, variable, null);
            }
            else if (unnamed is not null && IsTheOnly(describable, name))
            {
                byDatabase[name] = new DatabaseConnection(name, variable, unnamed, ConnectionVariables.Unnamed, null);
            }
            else
            {
                byDatabase[name] = new DatabaseConnection(name, variable, null, null, null);
            }
        }

        var unused = given.Where(argument => !byDatabase.ContainsKey(argument.Name)).ToList();
        var byNearness = unused.ToLookup(argument => IsNearAny(argument.Name, planned));
        return new Connections(
            byDatabase,
            [.. selected],
            [.. describable],
            [.. byNearness[true].Select(static argument => argument.Name)],
            [.. byNearness[false].Select(static argument => argument.Position)],
            unnamed is not null && describable.Count > 1
        );
    }

    private static bool IsNearAny(string name, IReadOnlyList<string> databases) =>
        databases.Any(database => NearName.IsNear(name, database, ignoreCase: true));

    private static bool IsTheOnly(IReadOnlyList<string> describable, string name) =>
        describable.Count == 1 && DatabaseName.Comparer.Equals(describable[0], name);

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

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
