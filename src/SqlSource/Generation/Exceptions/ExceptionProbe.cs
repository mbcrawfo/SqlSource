using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace SqlSource.Generation.Exceptions;

/// <summary>
/// Whether a referenced assembly gives the project its exceptions: a copy of <c>UnexpectedRowCountException</c>,
/// <c>TransactionRolledBackException</c> or <c>CollectionItemException</c> in an assembly the project references, which
/// it sees because the copy is public or because the assembly grants it <c>InternalsVisibleTo</c>.  A copy it cannot
/// see is no copy.  The value is equal from one edit to the next, so nothing after the probe runs again.
/// </summary>
/// <param name="Origin">Where the project's exceptions come from.</param>
/// <param name="Assemblies">
/// The names of the referenced assemblies that have a copy the project sees, in ordinal order: none for
/// <see cref="ExceptionOrigin.Own" />, one for <see cref="ExceptionOrigin.Referenced" /> and
/// <see cref="ExceptionOrigin.OtherShape" />, and two or more for <see cref="ExceptionOrigin.SeenTwice" />.
/// </param>
internal sealed record ExceptionProbe(ExceptionOrigin Origin, EquatableArray<string> Assemblies)
{
    private static readonly ExceptionProbe None = new(ExceptionOrigin.Own, EquatableArray<string>.Empty);

    /// <summary>
    /// Reads the probe from the compilation's references: two lookups by metadata name, and nothing for each syntax
    /// tree, since it runs on every edit.
    /// </summary>
    public static ExceptionProbe Read(Compilation compilation, CancellationToken cancellationToken)
    {
        var rowCounts = Seen(compilation, ExceptionSource.UnexpectedRowCountMetadataName);
        var rolledBacks = Seen(compilation, ExceptionSource.TransactionRolledBackMetadataName);
        var collectionItems = Seen(compilation, ExceptionSource.CollectionItemMetadataName);
        if (rowCounts.IsEmpty && rolledBacks.IsEmpty && collectionItems.IsEmpty)
        {
            return None;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var assemblies = rowCounts
            .Concat(rolledBacks)
            .Concat(collectionItems)
            .Select(static type => type.ContainingAssembly.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();
        return new ExceptionProbe(
            OriginOf(compilation, assemblies.Length, rowCounts, rolledBacks, collectionItems),
            new(assemblies)
        );
    }

    // Two or more assemblies are SeenTwice.  One is Referenced when it gives all three, each an exception that can be
    // created with the constructor the generated methods call, and OtherShape otherwise.
    private static ExceptionOrigin OriginOf(
        Compilation compilation,
        int assemblies,
        ImmutableArray<INamedTypeSymbol> rowCounts,
        ImmutableArray<INamedTypeSymbol> rolledBacks,
        ImmutableArray<INamedTypeSymbol> collectionItems
    )
    {
        if (assemblies > 1)
        {
            return ExceptionOrigin.SeenTwice;
        }

        return
            rowCounts.Length == 1
            && rolledBacks.Length == 1
            && collectionItems.Length == 1
            && IsThrowable(rowCounts[0])
            && IsThrowable(rolledBacks[0])
            && IsThrowable(collectionItems[0])
            && HasConstructor(compilation, rowCounts[0], IsBoolean)
            && HasConstructor(compilation, rolledBacks[0], IsException)
            && HasCollectionItemConstructor(compilation, collectionItems[0])
            ? ExceptionOrigin.Referenced
            : ExceptionOrigin.OtherShape;
    }

    // The types of the name in a referenced assembly that the project can see, and name from global::, as the generated
    // methods do.
    private static ImmutableArray<INamedTypeSymbol> Seen(Compilation compilation, string metadataName)
    {
        var found = compilation.GetTypesByMetadataName(metadataName);
        if (found.IsEmpty)
        {
            return found;
        }

        return found.RemoveAll(type =>
            SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)
            || !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly)
            || OnlyUnderAnAlias(compilation, type.ContainingAssembly)
        );
    }

    // Whether the assembly's reference has extern aliases and global is not among them, so that global:: does not reach
    // its types.
    private static bool OnlyUnderAnAlias(Compilation compilation, IAssemblySymbol assembly)
    {
        var aliases = compilation.GetMetadataReference(assembly)?.Properties.Aliases ?? ImmutableArray<string>.Empty;
        return !aliases.IsEmpty && !aliases.Contains("global");
    }

    // Whether the type has a constructor that the project can call with a string and a second argument of the type
    // that isSecond accepts, as the generated methods call it.
    private static bool HasConstructor(Compilation compilation, INamedTypeSymbol type, Func<ITypeSymbol, bool> isSecond)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            var parameters = constructor.Parameters;
            if (
                parameters.Length == 2
                && parameters[0].RefKind == RefKind.None
                && parameters[0].Type.SpecialType == SpecialType.System_String
                && parameters[1].RefKind == RefKind.None
                && isSecond(parameters[1].Type)
                && compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly)
            )
            {
                return true;
            }
        }

        return false;
    }

    // Whether the type has a constructor that the project can call as CollectionItemException(string queryName,
    // int itemIndex, System.Data.Common.DbException inner).
    private static bool HasCollectionItemConstructor(Compilation compilation, INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            var parameters = constructor.Parameters;
            if (
                parameters.Length == 3
                && parameters.All(static parameter => parameter.RefKind == RefKind.None)
                && parameters[0].Type.SpecialType == SpecialType.System_String
                && parameters[1].Type.SpecialType == SpecialType.System_Int32
                && IsDbException(parameters[2].Type)
                && compilation.IsSymbolAccessibleWithin(constructor, compilation.Assembly)
            )
            {
                return true;
            }
        }

        return false;
    }

    // Whether a method can create the type and throw it: a class that is not abstract and derives from
    // System.Exception.
    private static bool IsThrowable(INamedTypeSymbol type)
    {
        if (type.IsAbstract)
        {
            return false;
        }

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (IsException(baseType))
            {
                return true;
            }
        }

        return false;
    }

    // UnexpectedRowCountException(string queryName, bool moreThanOne).
    private static bool IsBoolean(ITypeSymbol type) => type.SpecialType == SpecialType.System_Boolean;

    // The parameter of CollectionItemException(string queryName, int itemIndex, System.Data.Common.DbException inner).
    private static bool IsDbException(ITypeSymbol type) => IsTopLevel(type, "DbException", "System", "Data", "Common");

    // TransactionRolledBackException(string queryName, System.Exception innerException).
    private static bool IsException(ITypeSymbol type) => IsTopLevel(type, "Exception", "System");

    // Whether the type is the non-generic type of the name, not nested, in the namespace of the parts, outermost first.
    private static bool IsTopLevel(ITypeSymbol type, string name, params string[] namespaceParts)
    {
        if (type is not INamedTypeSymbol { Arity: 0, ContainingType: null } named || named.Name != name)
        {
            return false;
        }

        var container = named.ContainingNamespace;
        for (var i = namespaceParts.Length - 1; i >= 0; i--)
        {
            if (container is null || container.Name != namespaceParts[i])
            {
                return false;
            }

            container = container.ContainingNamespace;
        }

        return container is { IsGlobalNamespace: true };
    }
}
