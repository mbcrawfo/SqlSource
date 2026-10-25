using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;
using SqlSource.Generation.Types;

namespace SqlSource.Generation.Converters;

/// <summary>
/// Reads a method marked <c>[SqlSourceConverter]</c> into a <see cref="Converter" />, with the first reason it is not
/// one.  It is one of the few steps that look at symbols and syntax; every step after it works on data.  A method of
/// the project's source and one of a mapped type of a referenced assembly are checked alike.
/// </summary>
internal static class ConverterReader
{
    // A keyword bare and every other type from global::, with ? where it is annotated, as ClrTypeName.Text writes it.
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
                | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
        );

    // The method's name alone, with @ when it is a keyword.
    private static readonly SymbolDisplayFormat NameFormat = new(
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    // App.Email.FromDatabase(string), as a message names the method.
    private static readonly SymbolDisplayFormat DisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    // A local function by its name and parameters, after the members it is in.
    private static readonly SymbolDisplayFormat LocalFunctionFormat = DisplayFormat.WithMemberOptions(
        SymbolDisplayMemberOptions.IncludeParameters
    );

    // A member that a local function is in, App.Conversions.Outer, without its parameters.
    private static readonly SymbolDisplayFormat OuterFormat = DisplayFormat.WithMemberOptions(
        SymbolDisplayMemberOptions.IncludeContainingType
    );

    /// <summary>
    /// Whether a node that carries the attribute is read: a method, or a local function, which is reported.  A lambda
    /// is not.
    /// </summary>
    public static bool IsCandidate(SyntaxNode node) => node is MethodDeclarationSyntax or LocalFunctionStatementSyntax;

    /// <summary>
    /// Reads a method or local function of the project's source, the target of the attribute's step, located at its
    /// name.
    /// </summary>
    public static Converter Read(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identifier = context.TargetNode is LocalFunctionStatementSyntax local
            ? local.Identifier
            : ((MethodDeclarationSyntax)context.TargetNode).Identifier;
        return Read(
            (IMethodSymbol)context.TargetSymbol,
            context.SemanticModel.Compilation,
            ConverterOrigin.Project,
            identifier.GetLocation()
        );
    }

    /// <summary>
    /// Reads a method found in <paramref name="origin" />, at <paramref name="location" />: null for a method of a
    /// referenced assembly, which has no position in the project.
    /// </summary>
    public static Converter Read(
        IMethodSymbol method,
        Compilation compilation,
        ConverterOrigin origin,
        Location? location
    )
    {
        var argument = method.Parameters.Length > 0 ? method.Parameters[0] : null;
        var warningIds = new List<string>();
        var fault = FindFault(method, compilation, warningIds);
        return new Converter(
            method.ContainingType.ToDisplayString(TypeFormat) + "." + method.ToDisplayString(NameFormat),
            DisplayNameOf(method),
            argument?.Type.ToDisplayString(TypeFormat) ?? string.Empty,
            argument?.Type.IsValueType ?? false,
            method.ReturnType.ToDisplayString(TypeFormat),
            method.ReturnType.IsValueType,
            MappedTypeWarnings.Distinct(warningIds),
            fault,
            origin,
            location is null ? null : LocationInfo.From(location)
        );
    }

    // App.Conversions.Outer.Local(string) for a local function, which its own display would name as Local(string).
    private static string DisplayNameOf(IMethodSymbol method) =>
        method.MethodKind == MethodKind.LocalFunction && method.ContainingSymbol is IMethodSymbol outer
            ? OuterNameOf(outer) + "." + method.ToDisplayString(LocalFunctionFormat)
            : method.ToDisplayString(DisplayFormat);

    private static string OuterNameOf(IMethodSymbol method) =>
        method.MethodKind == MethodKind.LocalFunction && method.ContainingSymbol is IMethodSymbol outer
            ? OuterNameOf(outer) + "." + method.ToDisplayString(NameFormat)
            : method.ToDisplayString(OuterFormat);

    // The first fault, in the order the manual lists them.  The warnings of the method and of its containing types are
    // added whatever the fault, since each is read once.
    private static string? FindFault(IMethodSymbol method, Compilation compilation, List<string> warningIds)
    {
        var canBeCalled = MappedTypeWarnings.Add(method, warningIds);
        var inGenericType = false;
        var inFileLocalType = false;
        for (var type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            canBeCalled &= MappedTypeWarnings.Add(type, warningIds);
            inGenericType |= type.IsGenericType;
            inFileLocalType |= type.IsFileLocal;
        }

        if (method.MethodKind == MethodKind.LocalFunction)
        {
            return "it is a local function";
        }

        if (!method.IsStatic)
        {
            return "it is not static";
        }

        // A static abstract or virtual member of an interface cannot be called through the interface (CS8926).
        if (method.IsAbstract || method.IsVirtual)
        {
            return "it is abstract or virtual";
        }

        if (method.IsGenericMethod)
        {
            return "it is generic";
        }

        if (inGenericType)
        {
            return "it is in a generic type";
        }

        if (method.Parameters.Length != 1)
        {
            return "it does not take exactly one argument";
        }

        if (method.Parameters[0] is { RefKind: not RefKind.None } or { IsParams: true } or { IsOptional: true })
        {
            return "its argument is ref, out, in, params or optional";
        }

        if (method.ReturnsVoid || IsTask(method.ReturnType))
        {
            return "it returns nothing";
        }

        if (
            method.ReturnType.NullableAnnotation == NullableAnnotation.Annotated
            || method.ReturnType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        )
        {
            return "its return type is annotated as nullable";
        }

        // Generated code in another file cannot name a file-local type.
        if (inFileLocalType || !compilation.IsSymbolAccessibleWithin(method, compilation.Assembly))
        {
            return "this project cannot call it";
        }

        return canBeCalled ? null : "it is marked [Obsolete] as an error";
    }

    // Task or ValueTask of System.Threading.Tasks, generic or not.
    private static bool IsTask(ITypeSymbol type) =>
        type
            is INamedTypeSymbol
            {
                Name: "Task" or "ValueTask",
                ContainingNamespace:
                {
                    Name: "Tasks",
                    ContainingNamespace:
                    {
                        Name: "Threading",
                        ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
                    },
                },
            };
}
