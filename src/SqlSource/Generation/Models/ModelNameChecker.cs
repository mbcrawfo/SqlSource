using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Models;

/// <summary>
/// Finds the models' full names that the project already has, as a type or a namespace, so that no model is generated
/// that would not compile.  It looks in the project's own assembly alone: its types, the attributed types and those
/// the generator adds to every project among them, and its namespaces.
/// </summary>
internal static class ModelNameChecker
{
    /// <summary>
    /// One record for each of <paramref name="fullNames" /> that the project has, in the same order, and
    /// <see cref="EquatableArray{T}.Empty" /> at once for no names.  Of a name's reasons, the first in the order of
    /// <see cref="TakenReason" /> is its record's, and of the parts of its namespace, the shortest.  Never throws but
    /// for <paramref name="cancellationToken" />.
    /// </summary>
    /// <param name="compilation">The project.</param>
    /// <param name="fullNames">The models' full names, from identifier values, as <c>App.Rows.UserRow</c>.</param>
    /// <param name="cancellationToken">Stops the search.</param>
    public static EquatableArray<TakenName> FindTaken(
        Compilation compilation,
        EquatableArray<string> fullNames,
        CancellationToken cancellationToken
    )
    {
        if (fullNames.Count == 0)
        {
            return EquatableArray<TakenName>.Empty;
        }

        var assembly = compilation.Assembly;
        var taken = ImmutableArray.CreateBuilder<TakenName>();
        foreach (var fullName in fullNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Find(assembly, fullName) is { } name)
            {
                taken.Add(name);
            }
        }

        return new EquatableArray<TakenName>(taken.ToImmutable());
    }

    private static TakenName? Find(IAssemblySymbol assembly, string fullName)
    {
        // A namespace-level type that is not generic has its full name as its metadata name.
        if (assembly.GetTypeByMetadataName(fullName) is not null)
        {
            return new TakenName(fullName, TakenReason.Type, fullName);
        }

        if (IsNamespace(assembly.GlobalNamespace, fullName))
        {
            return new TakenName(fullName, TakenReason.Namespace, fullName);
        }

        // Each part of the namespace from its start, the whole namespace last.  A type nested in another is found as
        // the type it is nested in, a shorter part.
        var last = fullName.LastIndexOf('.');
        for (
            var period = fullName.IndexOf('.');
            period > 0 && period <= last;
            period = fullName.IndexOf('.', period + 1)
        )
        {
            var part = fullName.Substring(0, period);
            if (assembly.GetTypeByMetadataName(part) is not null)
            {
                return new TakenName(fullName, TakenReason.NamespacePartIsType, part);
            }
        }

        return null;
    }

    private static bool IsNamespace(INamespaceSymbol global, string fullName)
    {
        var current = global;
        foreach (var part in fullName.Split('.'))
        {
            INamespaceSymbol? next = null;
            foreach (var member in current.GetMembers(part))
            {
                if (member is INamespaceSymbol found)
                {
                    next = found;
                    break;
                }
            }

            if (next is null)
            {
                return false;
            }

            current = next;
        }

        return true;
    }
}
