using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Generation.Chains;

namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// The names of the referenced assemblies that only a NuGet package the project does not list itself brings.  A
/// short type name that binds to a type of one of them is warned of, since the package that brings the assembly can
/// stop bringing it, or bring another type of that name, when it is updated.  The generator reads them from the
/// compiler's options with <see cref="Read" />.
/// </summary>
/// <param name="Names">
/// The names, sorted and distinct ignoring case, the first spelling of each kept, so that two builds of one list are
/// equal.
/// </param>
internal sealed record TransitivePackageAssemblies(EquatableArray<string> Names)
{
    /// <summary>
    /// The property that holds the names, which <c>build/SqlSource.targets</c> sets in the target
    /// <c>SqlSourceFindTransitivePackageAssemblies</c> and <c>build/SqlSource.props</c> shows to the compiler: the
    /// file name of each such assembly, without its extension, escaped and joined by <c>|</c>.
    /// </summary>
    public const string PropertyName = "SqlSourceTransitivePackageAssemblies";

    // Where the compiler puts the property: build/SqlSource.props shows it as a CompilerVisibleProperty.
    private const string OptionKey = "build_property." + PropertyName;

    private const char Delimiter = '|';

    // An assembly's name is compared ignoring case, as the runtime binds one and as MSBuild's Distinct compares.
    private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// No assembly, as a build whose references were not resolved gives, which warns of no short name.
    /// </summary>
    public static TransitivePackageAssemblies None { get; } = new(EquatableArray<string>.Empty);

    /// <summary>The names of the project's property in <paramref name="globalOptions" />.</summary>
    public static TransitivePackageAssemblies Read(AnalyzerConfigOptions globalOptions) =>
        Of(globalOptions.TryGetValue(OptionKey, out var value) ? value : null);

    /// <summary>
    /// The names of the property as the build gives it: entries joined by <c>|</c>, each with its <c>%</c>,
    /// <c>|</c>, <c>#</c> and <c>;</c> written <c>%25</c>, <c>%7C</c>, <c>%23</c> and <c>%3B</c>, as the build's
    /// folders are, which this decodes with <see cref="BuildFolders.Unescape" />; any other <c>%</c> is kept.  An
    /// empty entry is left out.
    /// </summary>
    public static TransitivePackageAssemblies Of(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return None;
        }

        var seen = new HashSet<string>(Comparer);
        var names = ImmutableArray.CreateBuilder<string>();
        var start = 0;
        while (start <= value!.Length)
        {
            var end = value.IndexOf(Delimiter, start);
            if (end < 0)
            {
                end = value.Length;
            }

            if (end > start)
            {
                var name = BuildFolders.Unescape(value.Substring(start, end - start));
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }

            start = end + 1;
        }

        if (names.Count == 0)
        {
            return None;
        }

        names.Sort(Comparer);
        return new TransitivePackageAssemblies(new EquatableArray<string>(names.ToImmutable()));
    }

    /// <summary>Whether <paramref name="assemblyName" /> is one of the names, ignoring case.</summary>
    public bool Contains(string assemblyName) =>
        SortedSearch.IndexOf(Names, assemblyName, static name => name, Comparer) >= 0;
}
