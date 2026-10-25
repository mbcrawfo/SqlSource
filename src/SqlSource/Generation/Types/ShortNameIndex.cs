using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Types;

/// <summary>
/// Finds the types that a short name can mean: the types of that name and arity, in any namespace and nested in no
/// other type, of the project's source and of its references, that the project can see.
/// </summary>
/// <remarks>
/// The project's own types are found by the compiler's lookup by name.  Each reference's top-level types are indexed by
/// name and arity the first time a short name asks for them, in a table keyed by the <see cref="MetadataReference" />
/// and held weakly, and the index is read again while the reference is the same object: a run with no short name builds
/// none, and an edit that leaves the references alone walks none.  An index holds metadata names, never a symbol, since
/// a symbol is of one compilation; each name it gives is found again in the current compilation by
/// <see cref="Compilation.GetTypesByMetadataName" />, which is also where its accessibility is checked, since a
/// reference's internal type is accessible only to the projects it gives its internals to.  A reference under an alias
/// other than <c>global</c> is not indexed, since generated code cannot name its types from <c>global::</c>.  The table
/// is safe to read and fill from several threads; two runs that index one reference at once each build an index, and
/// the table keeps one.
/// </remarks>
internal static class ShortNameIndex
{
    private const string GlobalAlias = "global";

    private static readonly ConditionalWeakTable<MetadataReference, ReferenceIndex> Indexes = new();

    /// <summary>
    /// The metadata names, <c>Ns.UserId</c> and <c>Ns.List`1</c>, of the top-level types of the name and arity that
    /// the project can see, of its source and of every reference, each once, in ordinal order.
    /// </summary>
    public static ImmutableArray<string> MetadataNamesOf(Compilation compilation, string name, int arity)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var symbol in compilation.GetSymbolsWithName(name, SymbolFilter.Type))
        {
            if (symbol is INamedTypeSymbol { ContainingType: null } type && type.Arity == arity)
            {
                _ = names.Add(MetadataNameOf(type));
            }
        }

        foreach (var reference in compilation.References)
        {
            if (IndexFor(compilation, reference)?.Find(name, arity) is { } found)
            {
                names.UnionWith(found);
            }
        }

        return names.Where(metadataName => IsAccessible(compilation, metadataName)).ToImmutableArray();
    }

    /// <summary>The index of the reference, or null when none has been built.  For tests.</summary>
    internal static object? IndexOf(MetadataReference reference) =>
        Indexes.TryGetValue(reference, out var index) ? index : null;

    // The reference's index, built on the first ask.  Null for a reference under an alias, or one the compilation
    // gives no symbol for.
    private static ReferenceIndex? IndexFor(Compilation compilation, MetadataReference reference)
    {
        var aliases = reference.Properties.Aliases;
        if (!aliases.IsDefaultOrEmpty && !aliases.Contains(GlobalAlias))
        {
            return null;
        }

        if (Indexes.TryGetValue(reference, out var index))
        {
            return index;
        }

        var root = compilation.GetAssemblyOrModuleSymbol(reference) switch
        {
            IAssemblySymbol assembly => assembly.GlobalNamespace,
            IModuleSymbol module => module.GlobalNamespace,
            _ => null,
        };
        if (root is null)
        {
            return null;
        }

        var built = ReferenceIndex.Build(root);
        return Indexes.GetValue(reference, _ => built);
    }

    // Whether a type of the metadata name is one the project can see.
    private static bool IsAccessible(Compilation compilation, string metadataName) =>
        compilation
            .GetTypesByMetadataName(metadataName)
            .Any(type => compilation.IsSymbolAccessibleWithin(type, compilation.Assembly));

    // The metadata name of a top-level type: its namespaces joined by '.', then its own.
    private static string MetadataNameOf(INamedTypeSymbol type)
    {
        if (type.ContainingNamespace is not { IsGlobalNamespace: false } containing)
        {
            return type.MetadataName;
        }

        var builder = new StringBuilder();
        AppendNamespace(builder, containing);
        return builder.Append('.').Append(type.MetadataName).ToString();
    }

    private static void AppendNamespace(StringBuilder builder, INamespaceSymbol name)
    {
        if (name.ContainingNamespace is { IsGlobalNamespace: false } outer)
        {
            AppendNamespace(builder, outer);
            _ = builder.Append('.');
        }

        _ = builder.Append(name.Name);
    }

    // One reference's top-level types: the metadata names of each name and arity.  Filled once and only read after.
    private sealed class ReferenceIndex
    {
        private readonly Dictionary<(string Name, int Arity), List<string>> _types;

        private ReferenceIndex(Dictionary<(string Name, int Arity), List<string>> types)
        {
            _types = types;
        }

        public static ReferenceIndex Build(INamespaceSymbol root)
        {
            var types = new Dictionary<(string Name, int Arity), List<string>>();
            Add(root, string.Empty, types);
            return new ReferenceIndex(types);
        }

        public List<string>? Find(string name, int arity) =>
            _types.TryGetValue((name, arity), out var found) ? found : null;

        // The namespace's types, then those of each namespace in it, the prefix being the namespace's metadata name
        // and a '.', or empty for the global namespace.
        private static void Add(
            INamespaceSymbol name,
            string prefix,
            Dictionary<(string Name, int Arity), List<string>> types
        )
        {
            foreach (var type in name.GetTypeMembers())
            {
                // <Module> and the compiler's other types have names no C# can write.
                if (!type.CanBeReferencedByName)
                {
                    continue;
                }

                var key = (type.Name, type.Arity);
                if (!types.TryGetValue(key, out var names))
                {
                    names = [];
                    types.Add(key, names);
                }

                names.Add(prefix + type.MetadataName);
            }

            foreach (var inner in name.GetNamespaceMembers())
            {
                Add(inner, prefix + inner.Name + ".", types);
            }
        }
    }
}
