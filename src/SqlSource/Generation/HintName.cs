using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SqlSource.Generation;

/// <summary>
/// The name of a type's generated file.  The compiler compares these names ignoring case and throws on the second of
/// two that are equal, which costs every type its generated code, so a name that would clash is made unique.
/// </summary>
internal static class HintName
{
    private const string Extension = ".g.cs";

    /// <summary>
    /// The namespace and each type name joined with dots.  A generic type's name is followed by its arity, so that
    /// <c>Repository</c> and <c>Repository&lt;T&gt;</c> get different names.  Every character is one a hint name may
    /// hold: the names are the identifiers' values, without the <c>@</c> or the Unicode escapes they may be written
    /// with.
    /// </summary>
    public static string Create(TargetType type)
    {
        var name = new StringBuilder(type.Namespace.Replace("@", string.Empty));
        foreach (var declaration in type.Types)
        {
            if (name.Length > 0)
            {
                _ = name.Append('.');
            }

            _ = name.Append(declaration.ValueName);
            if (declaration.Arity > 0)
            {
                _ = name.Append('-').Append(declaration.Arity.ToString(CultureInfo.InvariantCulture));
            }
        }

        return name.Append(Extension).ToString();
    }

    /// <summary>
    /// The names that are equal, ignoring case, to another name or to the name of the generated attribute's file.
    /// Almost always none.
    /// </summary>
    public static EquatableArray<string> FindAmbiguous(ImmutableArray<string> hintNames) =>
        new(
            hintNames
                .GroupBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .Where(static group =>
                    group.Skip(1).Any()
                    || string.Equals(group.Key, AttributeSource.HintName, StringComparison.OrdinalIgnoreCase)
                )
                .SelectMany(static group => group)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToImmutableArray()
        );

    /// <summary>
    /// Returns <paramref name="hintName" />, with a hash of its exact spelling added when it is one of
    /// <paramref name="ambiguous" />.
    /// </summary>
    public static string MakeUnique(string hintName, EquatableArray<string> ambiguous)
    {
        if (
            ambiguous.Count == 0
            || !ambiguous.Any(name => string.Equals(name, hintName, StringComparison.OrdinalIgnoreCase))
        )
        {
            return hintName;
        }

        return hintName.Substring(0, hintName.Length - Extension.Length)
            + "."
            + Hash(hintName).ToString("X8", CultureInfo.InvariantCulture)
            + Extension;
    }

    // FNV-1a.  string.GetHashCode differs from one process to the next, and a file name must not.
    private static uint Hash(string text)
    {
        var hash = 2166136261;
        foreach (var character in text)
        {
            hash = unchecked((hash ^ character) * 16777619);
        }

        return hash;
    }
}
