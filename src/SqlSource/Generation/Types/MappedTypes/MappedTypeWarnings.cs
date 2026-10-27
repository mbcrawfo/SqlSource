using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Generation.Types.MappedTypes;

/// <summary>
/// The ids of the warnings that a use of a mapped type gives, which a model's file disables, and whether a pragma can
/// turn each off.
/// </summary>
/// <remarks>
/// A use of a type marked <c>[Obsolete]</c> or <c>[Experimental]</c>, or nested in one, or of an assembly or module
/// marked <c>[Experimental]</c>, is a warning, or an error that a <c>#pragma warning disable</c> of its id turns off,
/// and the ids of each type of a name are the name's.  A type cannot be a property's type when a use of it is an
/// error that no pragma turns off, as for <c>[Obsolete]</c> as an error (CS0619), or when it is marked under an id
/// that a pragma cannot name, whose warning, or error, a model could not turn off.
/// </remarks>
internal static class MappedTypeWarnings
{
    private const string ObsoleteAttributeName = "ObsoleteAttribute";

    private const string ExperimentalAttributeName = "ExperimentalAttribute";

    private const string ExperimentalAttributeFullName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

    private const string DiagnosticIdArgument = "DiagnosticId";

    private const string SystemNamespace = "System";

    // The ids of a use of a type marked [Obsolete] without a message and with one, and of [Experimental] with no id.
    private const string ObsoleteId = "CS0612";

    private const string ObsoleteWithMessageId = "CS0618";

    private const string ExperimentalId = "CS9204";

    /// <summary>
    /// Adds the ids of the warnings that a use of the symbol gives for its own [Obsolete] or [Experimental], and says
    /// whether a pragma can turn each off.  An [Obsolete] with no DiagnosticId is CS0612 or CS0618 by its message, both
    /// added, and an [Experimental] with no id CS9204.  An argument of another shape gives no id and no error.  A
    /// symbol that is null, as a type of no module, adds none.
    /// </summary>
    public static bool Add(ISymbol? symbol, List<string> warningIds)
    {
        if (symbol is null)
        {
            return true;
        }

        var canBeUsed = true;
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (
                attributeClass is
                {
                    Name: ObsoleteAttributeName,
                    ContainingNamespace: { Name: SystemNamespace, ContainingNamespace.IsGlobalNamespace: true },
                }
            )
            {
                var arguments = attribute.ConstructorArguments;
                if (arguments.Length > 1 && ValueOf(arguments[1]) is true)
                {
                    canBeUsed = false;
                    continue;
                }

                if (DiagnosticIdOf(attribute) is { Length: > 0 } id)
                {
                    canBeUsed &= TryAdd(id, warningIds);
                }
                else
                {
                    warningIds.Add(ObsoleteId);
                    warningIds.Add(ObsoleteWithMessageId);
                }
            }
            else if (
                attributeClass.Name.Equals(ExperimentalAttributeName, StringComparison.Ordinal)
                && attributeClass.ToDisplayString().Equals(ExperimentalAttributeFullName, StringComparison.Ordinal)
            )
            {
                var arguments = attribute.ConstructorArguments;
                if (arguments.Length > 0 && ValueOf(arguments[0]) is string { Length: > 0 } id)
                {
                    canBeUsed &= TryAdd(id, warningIds);
                }
                else
                {
                    warningIds.Add(ExperimentalId);
                }
            }
        }

        return canBeUsed;
    }

    // The named argument's value, or null when it is not given: the default pair's constant is not primitive.
    private static string? DiagnosticIdOf(AttributeData attribute) =>
        ValueOf(
            attribute
                .NamedArguments.FirstOrDefault(static argument =>
                    argument.Key.Equals(DiagnosticIdArgument, StringComparison.Ordinal)
                )
                .Value
        ) as string;

    // The value of a primitive constant, or null for any other.  Value throws for an array, which an attribute of the
    // name declared in the project or a reference can take.
    private static object? ValueOf(TypedConstant constant) =>
        constant.Kind == TypedConstantKind.Primitive && !constant.IsNull ? constant.Value : null;

    // A pragma names an id as an identifier; one that is not, as A-B, is read otherwise, and 1234 as CS1234.
    private static bool TryAdd(string id, List<string> warningIds)
    {
        if (!SyntaxFacts.IsValidIdentifier(id))
        {
            return false;
        }

        warningIds.Add(id);
        return true;
    }

    /// <summary>The ids in ordinal order, each once.</summary>
    public static EquatableArray<string> Distinct(List<string> ids)
    {
        if (ids.Count == 0)
        {
            return EquatableArray<string>.Empty;
        }

        ids.Sort(StringComparer.Ordinal);
        var distinct = ImmutableArray.CreateBuilder<string>(ids.Count);
        foreach (var id in ids)
        {
            if (distinct.Count == 0 || !distinct[distinct.Count - 1].Equals(id, StringComparison.Ordinal))
            {
                distinct.Add(id);
            }
        }

        return new EquatableArray<string>(distinct.ToImmutable());
    }
}
