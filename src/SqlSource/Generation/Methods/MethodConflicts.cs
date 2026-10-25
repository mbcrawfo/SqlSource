using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Models;

namespace SqlSource.Generation.Methods;

/// <summary>
/// What the generated methods of every type, compared, leave out: the methods that clash with another type's
/// (<c>SQLSRC274</c>), and the types whose extension class cannot be written (<c>SQLSRC276</c>).
/// </summary>
/// <param name="Dropped">The methods to leave out, by their type's key, then by place.</param>
/// <param name="BlockedTypes">The keys of the types that get no extension class, in ordinal order.</param>
/// <param name="Diagnostics">The problems, each once, in the order of <see cref="MethodPlanner.Distinct" />.</param>
internal sealed record MethodConflicts(
    EquatableArray<DroppedMethod> Dropped,
    EquatableArray<string> BlockedTypes,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    private static readonly MethodConflicts None = new(
        EquatableArray<DroppedMethod>.Empty,
        EquatableArray<string>.Empty,
        EquatableArray<DiagnosticInfo>.Empty
    );

    /// <summary>
    /// The clashes of the methods of two types, and the extension classes that cannot be written.  A clash is two
    /// methods of one name in one extension class, from two types, or in two extension classes of one namespace with
    /// one parameter list, which a call could not tell apart; both are dropped.  An extension class cannot be written
    /// when the project or a model has its full name, or when two types give it with two accessibilities; each type
    /// that gives it is blocked, and its methods clash with nothing.  The value does not depend on the order of
    /// <paramref name="signatures" />.
    /// </summary>
    /// <param name="signatures">Every type's methods, as <see cref="MethodSignature.Of" /> gives them.</param>
    /// <param name="takenExtensionClasses">
    /// The extension classes' full names that the project has, as <see cref="ModelNameChecker.FindTaken" /> gives them.
    /// </param>
    /// <param name="modelNames">The full names of the models SqlSource writes.</param>
    public static MethodConflicts Find(
        EquatableArray<MethodSignature> signatures,
        EquatableArray<TakenName> takenExtensionClasses,
        EquatableArray<string> modelNames
    )
    {
        var extensions = signatures.Where(static signature => signature.ExtensionClass is not null).ToList();
        if (extensions.Count == 0)
        {
            return None;
        }

        var diagnostics = new List<DiagnosticInfo>();
        var blocked = FindBlocked(extensions, takenExtensionClasses, modelNames, diagnostics);
        var dropped = FindClashes(
            [.. extensions.Where(signature => !blocked.Contains(signature.TypeKey))],
            diagnostics
        );
        if (diagnostics.Count == 0)
        {
            return None;
        }

        return new MethodConflicts(
            new EquatableArray<DroppedMethod>(
                dropped
                    .OrderBy(static method => method.TypeKey, StringComparer.Ordinal)
                    .ThenBy(static method => method.NameLocation.Path, StringComparer.Ordinal)
                    .ThenBy(static method => method.NameLocation.Span.Start)
                    .ToImmutableArray()
            ),
            new EquatableArray<string>(blocked.OrderBy(static key => key, StringComparer.Ordinal).ToImmutableArray()),
            MethodPlanner.Distinct(
                ImmutableArray.Create(new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()))
            )
        );
    }

    /// <summary>The places of the type's methods to leave out.  Almost always none.</summary>
    public EquatableArray<LocationInfo> DroppedOf(string typeKey)
    {
        if (Dropped.Count == 0)
        {
            return EquatableArray<LocationInfo>.Empty;
        }

        return new EquatableArray<LocationInfo>(
            Dropped
                .Where(method => string.Equals(method.TypeKey, typeKey, StringComparison.Ordinal))
                .Select(static method => method.NameLocation)
                .ToImmutableArray()
        );
    }

    /// <summary>Whether the type gets no extension class.</summary>
    public bool IsBlocked(string typeKey) =>
        SortedSearch.IndexOf(BlockedTypes, typeKey, static key => key, StringComparer.Ordinal) >= 0;

    // SQLSRC276 at each type whose extension class cannot be written, and the keys of those types.
    private static HashSet<string> FindBlocked(
        List<MethodSignature> extensions,
        EquatableArray<TakenName> taken,
        EquatableArray<string> modelNames,
        List<DiagnosticInfo> diagnostics
    )
    {
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var models = ModelNamesOf(modelNames);
        foreach (
            var extensionClass in extensions.GroupBy(
                static signature => signature.ExtensionClassFullName!,
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
                TakenReason.Type or _ => "the project has a type of that name",
            };
        }

        if (models.Names.Contains(fullName))
        {
            return "SqlSource writes a model of that name";
        }

        return models.Namespaces.Contains(fullName) ? "SqlSource writes a model in a namespace of that name" : null;
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

                dropped.Add(new DroppedMethod(method.TypeKey, method.NameLocation));
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

/// <summary>A method that a clash leaves out.</summary>
/// <param name="TypeKey">The type, as <see cref="TypeMethods.TypeKey" /> gives it.</param>
/// <param name="NameLocation">The method's <see cref="QueryMethod.NameLocation" />.</param>
internal sealed record DroppedMethod(string TypeKey, LocationInfo NameLocation);
