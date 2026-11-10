using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What the generated methods of every type, compared, leave out: the methods that clash with another type's
/// (<c>SQLSRC274</c>), the types whose extension class cannot be written (<c>SQLSRC276</c>), and the types whose
/// repository interface cannot be (<c>SQLSRC035</c>).
/// </summary>
/// <param name="Dropped">The methods to leave out, by their type's key, then by name.</param>
/// <param name="BlockedTypes">The keys of the types that get no extension class, in ordinal order.</param>
/// <param name="BlockedInterfaces">The keys of the types that get no interface, in ordinal order.</param>
/// <param name="Diagnostics">The problems, each once, in the order of <see cref="MethodPlanner.Distinct" />.</param>
internal sealed record MethodConflicts(
    EquatableArray<DroppedMethod> Dropped,
    EquatableArray<string> BlockedTypes,
    EquatableArray<string> BlockedInterfaces,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    private static readonly MethodConflicts None = new(
        EquatableArray<DroppedMethod>.Empty,
        EquatableArray<string>.Empty,
        EquatableArray<string>.Empty,
        EquatableArray<DiagnosticInfo>.Empty
    );

    /// <summary>
    /// The clashes of the methods of two types, and the extension classes that cannot be written.  A clash is two
    /// methods of one name in one extension class, from two types, or in two extension classes of one namespace with
    /// one parameter list, which a call could not tell apart; both are dropped.  An extension class cannot be written
    /// when the project or a model has its full name, or when two types give it with two accessibilities, each its
    /// widest method's, since widening it across types would make one type's output depend on another's; each type
    /// that gives it is blocked, and its methods clash with nothing.  An interface cannot be written when a model, an
    /// extension class that is written or another type's interface has its full name and arity, or a namespace of
    /// models its full name; each type that gives it is blocked.  The value does not depend on the order of
    /// <paramref name="signatures" /> and <paramref name="interfaces" />.
    /// </summary>
    /// <param name="signatures">Every type's methods, as <see cref="MethodSignature.Of" /> gives them.</param>
    /// <param name="interfaces">Every type's interface, as <see cref="InterfaceSignature.Of" /> gives it.</param>
    /// <param name="takenExtensionClasses">
    /// The extension classes' full names that the project has, as <see cref="ModelNameChecker.FindTaken" /> gives them.
    /// </param>
    /// <param name="modelNames">The full names of the models SqlSource writes.</param>
    public static MethodConflicts Find(
        EquatableArray<MethodSignature> signatures,
        EquatableArray<InterfaceSignature> interfaces,
        EquatableArray<TakenName> takenExtensionClasses,
        EquatableArray<string> modelNames
    )
    {
        // Only an extension method has a signature, and only it can clash with another type's.
        var extensions = signatures.ToList();
        if (extensions.Count == 0 && interfaces.Count == 0)
        {
            return None;
        }

        var diagnostics = new List<DiagnosticInfo>();
        var models = ModelNamesOf(modelNames);
        var blocked = FindBlocked(extensions, takenExtensionClasses, models, diagnostics);
        var written = extensions.Where(signature => !blocked.Contains(signature.TypeKey)).ToList();
        var dropped = FindClashes(written, diagnostics);
        var blockedInterfaces = FindBlockedInterfaces(interfaces, written, models, diagnostics);
        if (diagnostics.Count == 0)
        {
            return None;
        }

        return new MethodConflicts(
            new EquatableArray<DroppedMethod>(
                dropped
                    .OrderBy(static method => method.TypeKey, StringComparer.Ordinal)
                    .ThenBy(static method => method.Name, StringComparer.Ordinal)
                    .ToImmutableArray()
            ),
            new EquatableArray<string>(blocked.OrderBy(static key => key, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<string>(
                blockedInterfaces.OrderBy(static key => key, StringComparer.Ordinal).ToImmutableArray()
            ),
            MethodPlanner.Distinct(
                ImmutableArray.Create(new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()))
            )
        );
    }

    /// <summary>The names of the type's methods to leave out, in ordinal order.  Almost always none.</summary>
    public EquatableArray<string> DroppedOf(string typeKey)
    {
        if (Dropped.Count == 0)
        {
            return EquatableArray<string>.Empty;
        }

        return new EquatableArray<string>(
            Dropped
                .Where(method => string.Equals(method.TypeKey, typeKey, StringComparison.Ordinal))
                .Select(static method => method.Name)
                .ToImmutableArray()
        );
    }

    /// <summary>Whether the type gets no extension class.</summary>
    public bool IsBlocked(string typeKey) =>
        SortedSearch.IndexOf(BlockedTypes, typeKey, static key => key, StringComparer.Ordinal) >= 0;

    /// <summary>Whether the type gets no interface.</summary>
    public bool IsInterfaceBlocked(string typeKey) =>
        SortedSearch.IndexOf(BlockedInterfaces, typeKey, static key => key, StringComparer.Ordinal) >= 0;

    // SQLSRC276 at each type whose extension class cannot be written, and the keys of those types.
    private static HashSet<string> FindBlocked(
        List<MethodSignature> extensions,
        EquatableArray<TakenName> taken,
        (HashSet<string> Names, HashSet<string> Namespaces) models,
        List<DiagnosticInfo> diagnostics
    )
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (
            var extensionClass in extensions.GroupBy(
                static signature => signature.ExtensionClassFullName,
                StringComparer.Ordinal
            )
        )
        {
            // One signature for each type that gives the class, which has the type's name, accessibility and place.
            var types = extensionClass
                .GroupBy(static signature => signature.TypeKey, StringComparer.Ordinal)
                .Select(static type => type.First())
                .OrderBy(static type => type.TypeKey, StringComparer.Ordinal)
                .ToList();
            var reason = TakenReasonOf(extensionClass.Key, taken, models);
            foreach (var type in types)
            {
                var because = reason ?? AccessibilityReasonOf(type, types);
                if (because is null)
                {
                    continue;
                }

                _ = blocked.Add(type.TypeKey);
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ExtensionClassNameTaken,
                        type.AttributeLocation,
                        type.TypeName,
                        extensionClass.Key,
                        because
                    )
                );
            }
        }

        return blocked;
    }

    // What has the extension class's full name, as SQLSRC276 says it, or null when nothing does.
    private static string? TakenReasonOf(
        string fullName,
        EquatableArray<TakenName> taken,
        (HashSet<string> Names, HashSet<string> Namespaces) models
    )
    {
        if (taken.FirstOrDefault(name => string.Equals(name.FullName, fullName, StringComparison.Ordinal)) is { } name)
        {
            return name.Reason switch
            {
                TakenReason.Namespace => "the project has a namespace of that name",
                TakenReason.NamespacePartIsType => "'" + name.Part + "' is a type of the project",
                TakenReason.Type => "the project has a type of that name",
                _ => throw UnexpectedEnumValueException.Of(name.Reason),
            };
        }

        if (models.Names.Contains(fullName))
        {
            return "SqlSource writes a model of that name";
        }

        return models.Namespaces.Contains(fullName) ? "SqlSource writes a model in a namespace of that name" : null;
    }

    // SQLSRC035 at each type whose interface has the full name of something else that SqlSource writes, and the keys
    // of those types.
    private static List<string> FindBlockedInterfaces(
        EquatableArray<InterfaceSignature> interfaces,
        List<MethodSignature> written,
        (HashSet<string> Names, HashSet<string> Namespaces) models,
        List<DiagnosticInfo> diagnostics
    )
    {
        var blocked = new List<string>();
        if (interfaces.Count == 0)
        {
            return blocked;
        }

        var extensionClasses = new HashSet<string>(
            written.Select(static signature => signature.ExtensionClassFullName),
            StringComparer.Ordinal
        );
        foreach (var contract in interfaces)
        {
            if (InterfaceTakenBy(contract, interfaces, extensionClasses, models) is not { } takenBy)
            {
                continue;
            }

            blocked.Add(contract.TypeKey);
            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.RepositoryInterfaceNameTaken,
                    contract.AttributeLocation,
                    contract.TypeName,
                    contract.Name,
                    takenBy
                )
            );
        }

        return blocked;
    }

    // What SqlSource writes of the interface's full name, as SQLSRC035 says it, or null.  A model and an extension
    // class have no type parameters, so they take the name of an interface of none.
    private static string? InterfaceTakenBy(
        InterfaceSignature contract,
        EquatableArray<InterfaceSignature> interfaces,
        HashSet<string> extensionClasses,
        (HashSet<string> Names, HashSet<string> Namespaces) models
    )
    {
        var fullName = contract.FullName;
        if (contract.Arity == 0 && models.Names.Contains(fullName))
        {
            return "the model " + fullName + " that SqlSource writes";
        }

        if (contract.Arity == 0 && extensionClasses.Contains(fullName))
        {
            return "the extension class " + fullName + " that SqlSource writes";
        }

        if (models.Namespaces.Contains(fullName))
        {
            return "the namespace " + fullName + ", which SqlSource writes models in";
        }

        var other = interfaces.FirstOrDefault(other =>
            !string.Equals(other.TypeKey, contract.TypeKey, StringComparison.Ordinal)
            && other.Arity == contract.Arity
            && string.Equals(other.FullName, fullName, StringComparison.Ordinal)
        );
        return other is null ? null : "the interface " + fullName + " that SqlSource writes for " + other.TypeName;
    }

    // The first other type that gives the class with the other accessibility, as SQLSRC276 says it, or null.
    private static string? AccessibilityReasonOf(MethodSignature type, List<MethodSignature> types) =>
        types.FirstOrDefault(other => other.IsPublic != type.IsPublic) is { } other
            ? "'"
                + other.TypeName
                + "' puts its methods there too, and the class would be public for one and internal for the other"
            : null;

    // SQLSRC274 at each method that a method of another type clashes with, and the methods to drop.
    private static List<DroppedMethod> FindClashes(List<MethodSignature> extensions, List<DiagnosticInfo> diagnostics)
    {
        var dropped = new List<DroppedMethod>();
        foreach (var name in extensions.GroupBy(static signature => (signature.Namespace, signature.Name)))
        {
            var methods = name.OrderBy(static method => method.TypeKey, StringComparer.Ordinal).ToList();
            if (methods.Count < 2)
            {
                continue;
            }

            foreach (var method in methods)
            {
                if (ClashOf(method, methods) is not { } because)
                {
                    continue;
                }

                dropped.Add(new DroppedMethod(method.TypeKey, method.Name));
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.MethodNameTaken,
                        method.NameLocation,
                        method.QueryName,
                        method.Name,
                        because
                    )
                );
            }
        }

        return dropped;
    }

    // The first method of another type that clashes with the method, as SQLSRC274 says it, or null: one of the same
    // class, or else one of another class with the same parameters.
    private static string? ClashOf(MethodSignature method, List<MethodSignature> methods)
    {
        var sameClass = methods.FirstOrDefault(other =>
            !string.Equals(other.TypeKey, method.TypeKey, StringComparison.Ordinal)
            && string.Equals(other.ExtensionClass, method.ExtensionClass, StringComparison.Ordinal)
        );
        if (sameClass is not null)
        {
            return "the method of the query '"
                + sameClass.QueryName
                + "' of '"
                + sameClass.TypeName
                + "' has that name too, in the class '"
                + sameClass.ExtensionClassFullName
                + "'";
        }

        var sameParameters = methods.FirstOrDefault(other =>
            !string.Equals(other.ExtensionClass, method.ExtensionClass, StringComparison.Ordinal)
            && string.Equals(other.ParameterKey, method.ParameterKey, StringComparison.Ordinal)
        );
        return sameParameters is null
            ? null
            : "the method of the query '"
                + sameParameters.QueryName
                + "' of '"
                + sameParameters.TypeName
                + "', in the class '"
                + sameParameters.ExtensionClassFullName
                + "' of the same namespace, has that name and the same parameters";
    }

    // The models' full names, and every namespace they are in, from its first part.
    private static (HashSet<string> Names, HashSet<string> Namespaces) ModelNamesOf(EquatableArray<string> fullNames)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fullName in fullNames)
        {
            _ = names.Add(fullName);
            for (var period = fullName.IndexOf('.'); period > 0; period = fullName.IndexOf('.', period + 1))
            {
                _ = namespaces.Add(fullName.Substring(0, period));
            }
        }

        return (names, namespaces);
    }
}

/// <summary>
/// A method that a clash leaves out, known by its type and its name, which is unique in the type once
/// <see cref="MethodPlanner" /> has planned it.
/// </summary>
/// <param name="TypeKey">The type, as <see cref="TypeMethods.TypeKey" /> gives it.</param>
/// <param name="Name">The method's <see cref="QueryMethod.Name" />.</param>
internal sealed record DroppedMethod(string TypeKey, string Name);
