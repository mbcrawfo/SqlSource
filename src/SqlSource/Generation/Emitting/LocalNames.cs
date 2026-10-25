using System;
using System.Collections.Generic;
using System.Globalization;

namespace SqlSource.Generation.Emitting;

/// <summary>
/// Gives the locals of a generated method names that nothing else in it has, so that no argument hides a local and no
/// two locals share a name.
/// </summary>
/// <param name="taken">The names the method already uses, its arguments'.</param>
internal sealed class LocalNames(IEnumerable<string> taken)
{
    private readonly HashSet<string> _taken = new(taken, StringComparer.Ordinal);

    /// <summary>
    /// Takes the name, or else the name with the first number, from 1, that is not taken, and records it.
    /// </summary>
    public string Take(string name)
    {
        var candidate = name;
        var suffix = 0;
        while (!_taken.Add(candidate))
        {
            suffix++;
            candidate = name + suffix.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }
}
