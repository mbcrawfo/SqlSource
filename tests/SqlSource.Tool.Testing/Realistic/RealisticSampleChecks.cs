using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;

namespace SqlSource.Tool.Testing.Realistic;

// A case's sample values checked against the parameters of its query, before a test of an engine puts them in its SQL
// or sends them, and a value found by its parameter's name.
internal static class RealisticSampleChecks
{
    // Fails naming the case and the key when a sample of the case names no parameter of its query, as a key that is
    // misspelt does: its value would be used nowhere.  A key is compared ignoring case, as Find finds it.
    public static void CheckKeys(RealisticCase found, IEnumerable<string> parameters)
    {
        var named = parameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in found.Samples.SelectMany(static sample => sample.Keys))
        {
            if (!named.Contains(key))
            {
                throw new InvalidOperationException(
                    $"A sample of the case {found} names @{key}, which is no parameter of its query."
                );
            }
        }
    }

    // Fails naming the case and the parameter when a sample gives NULL to a parameter whose -- param: marker does
    // not say null: generated code never sends one, so the server would run what no caller can.  The literal NULL
    // alone is read, ignoring case and white space around it.
    public static void CheckNulls(RealisticCase found, IReadOnlyList<SqlQueryParameter> parameters)
    {
        foreach (var (key, value) in found.Samples.SelectMany(static sample => sample))
        {
            if (
                string.Equals(value.Trim(), "NULL", StringComparison.OrdinalIgnoreCase)
                && parameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, key, StringComparison.OrdinalIgnoreCase)
                )
                    is { Nullable: not true }
            )
            {
                throw new InvalidOperationException(
                    $"A sample of the case {found} gives @{key} NULL, and no -- param: marker says it is null."
                );
            }
        }
    }

    // By the name as written, then ignoring case, whatever the dictionary compares with.
    public static string? Find(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value)
            ? value
            : values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
