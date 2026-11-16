using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Reads the members of a repository, of its base types and of its primary constructor that can give its methods a
/// connection, as <see cref="ConnectionCandidate" />s, and holds the rules of what a member gives that
/// <see cref="HeldFactoryReader" /> shares.  Only <see cref="RepositoryShapeReader" /> and
/// <see cref="HeldFactoryReader" /> call it, and it calls neither.
/// </summary>
internal static class ConnectionCandidateReader
{
    private const string AttributeName = "SqlSourceConnectionAttribute";

    private const string DataNamespace = "System.Data.Common";

    private const string DbConnection = "DbConnection";

    private const string DbDataSource = "DbDataSource";

    private const string ThreadingNamespace = "System.Threading";

    private const string TasksNamespace = "System.Threading.Tasks";

    // The names of a string that is a connection string by convention, compared ordinally.
    private static readonly string[] ConventionNames = ["connectionString", "_connectionString", "ConnectionString"];

    /// <summary>
    /// The candidates of the type, in the order they are declared, the type's own members first, then each base
    /// type's, then the primary constructor's parameters.  Only the marked ones when any is, and the parameters only
    /// when no member is one, and then only those that no argument of the base type and no initializer of a field,
    /// property or event of the type reads, since a method that captured one of those would make the compiler warn
    /// in the user's code (CS9107, CS9124).
    /// </summary>
    /// <param name="type">The repository.</param>
    /// <param name="compilation">
    /// The compilation, which says which members of a base type the type can reach and what an initializer reads.
    /// </param>
    /// <param name="fallback">The location of a member that has none in source: the type's attribute.</param>
    /// <param name="heldParameters">
    /// The names of the parameters the conventions would take but that such an argument or initializer reads, in the
    /// order they are declared.  Empty unless the candidates are the parameters.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static EquatableArray<ConnectionCandidate> Read(
        INamedTypeSymbol type,
        Compilation compilation,
        LocationInfo fallback,
        out EquatableArray<string> heldParameters,
        CancellationToken cancellationToken
    )
    {
        heldParameters = EquatableArray<string>.Empty;
        var members = new List<ConnectionCandidate>();
        foreach (var (member, isInherited) in ReachableMembersOf(type, compilation))
        {
            if (ReadMarkedMember(member, isInherited, fallback) is { } candidate)
            {
                members.Add(candidate);
            }
        }

        var primary = PrimaryConstructorOf(type, cancellationToken);
        var parameters = ReadParameters(primary, fallback);
        var all = members.Concat(parameters.Select(static parameter => parameter.Candidate)).ToList();
        if (all.Any(static candidate => candidate.IsMarked))
        {
            return new EquatableArray<ConnectionCandidate>(
                all.Where(static candidate => candidate.IsMarked).ToImmutableArray()
            );
        }

        if (members.Count > 0)
        {
            return new EquatableArray<ConnectionCandidate>(members.ToImmutableArray());
        }

        var taken = ImmutableArray.CreateBuilder<ConnectionCandidate>();
        var held = ImmutableArray.CreateBuilder<string>();
        foreach (var (symbol, candidate) in parameters)
        {
            if (IsReadBesides(symbol, type, compilation, cancellationToken))
            {
                held.Add(candidate.Name);
            }
            else
            {
                taken.Add(candidate);
            }
        }

        heldParameters = new EquatableArray<string>(held.ToImmutable());
        return new EquatableArray<ConnectionCandidate>(taken.ToImmutable());
    }

    /// <summary>
    /// The members of the type and of its base types, the type's own first, with whether each is a base type's: the
    /// type's own all, and a base type's only when the type can reach it and no member of a more derived type has its
    /// name, since such a member overrides or hides it.  Hiding by name alone is TD-0118.
    /// </summary>
    /// <param name="type">The repository.</param>
    /// <param name="compilation">The compilation, which says which members of a base type the type can reach.</param>
    internal static IEnumerable<(ISymbol Member, bool IsInherited)> ReachableMembersOf(
        INamedTypeSymbol type,
        Compilation compilation
    )
    {
        var derivedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            var isInherited = !SymbolEqualityComparer.Default.Equals(current, type);
            var currentMembers = current.GetMembers();
            foreach (var member in currentMembers)
            {
                if (!derivedNames.Contains(member.Name) && (!isInherited || IsReachable(member, type, compilation)))
                {
                    yield return (member, isInherited);
                }
            }

            derivedNames.UnionWith(currentMembers.Select(static member => member.Name));
        }
    }

    /// <summary>
    /// What a member gives, the one rule of what a member gives in every tier: a field, property or method of a
    /// source's shape that is marked is a candidate of its kind, and any other marked member is unsupported, so that no
    /// convention is chosen over a member the user marked.  An unmarked member gives what the conventions accept: a
    /// data source, or a string of a convention's name.
    /// </summary>
    /// <param name="member">The member, of any kind.</param>
    /// <param name="isInherited">Whether it is a member of a base type.</param>
    /// <param name="fallback">The location of a member that has none in source.</param>
    /// <param name="implementer">
    /// The factory whose member it is, for a held one: a member is then marked as the interface member it implements
    /// in the factory is.  Null for a repository's own member, which implementing an interface never marks.
    /// </param>
    /// <returns>The candidate, or null when the member gives none.</returns>
    public static ConnectionCandidate? ReadMarkedMember(
        ISymbol member,
        bool isInherited,
        LocationInfo fallback,
        INamedTypeSymbol? implementer = null
    ) =>
        ValueOf(member) is (var kind, var type)
            ? Held(member, kind, type, member.IsStatic, isInherited, fallback, implementer)
            : member switch
            {
                IMethodSymbol { CanBeReferencedByName: true, MethodKind: MethodKind.Ordinary } method => Method(
                    method,
                    isInherited,
                    fallback,
                    implementer
                ),
                _ => Other(member, isInherited, fallback, implementer),
            };

    /// <summary>
    /// A member whose value a call can read by its name, with its kind and type: a field, or a property with a getter
    /// that is not an indexer.  A write-only property gives nothing, and an indexer needs an argument.
    /// </summary>
    /// <param name="member">The member, of any kind.</param>
    internal static (CandidateMember Member, ITypeSymbol Type)? ValueOf(ISymbol member) =>
        member switch
        {
            IFieldSymbol { CanBeReferencedByName: true } field => (CandidateMember.Field, field.Type),
            IPropertySymbol { CanBeReferencedByName: true, IsIndexer: false, GetMethod: not null } property => (
                CandidateMember.Property,
                property.Type
            ),
            _ => null,
        };

    /// <summary>
    /// Whether the type can use the member: reach it, and call its getter when it is a property.  A member of a factory
    /// is used through the holder's value, of the type <paramref name="throughType" />.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="type">The repository.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="throughType">The type of the value the member is used through, or null for the type itself.</param>
    internal static bool IsReachable(
        ISymbol member,
        INamedTypeSymbol type,
        Compilation compilation,
        ITypeSymbol? throughType = null
    ) =>
        compilation.IsSymbolAccessibleWithin(member, type, throughType)
        && (
            member is not IPropertySymbol { GetMethod: { } getter }
            || compilation.IsSymbolAccessibleWithin(getter, type, throughType)
        );

    /// <summary>Where the symbol is declared in source, or the fallback when it has no such location.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="fallback">The location of a symbol in metadata.</param>
    internal static LocationInfo SourceLocationOr(ISymbol symbol, LocationInfo fallback) =>
        symbol.Locations.FirstOrDefault(static location => location.IsInSource) is { } location
            ? LocationInfo.From(location)
            : fallback;

    // An explicit interface implementation, an operator, an accessor, an indexer, a write-only property or the backing
    // field of a property: unsupported when it is marked, named for its property when it belongs to one.
    private static ConnectionCandidate? Other(
        ISymbol member,
        bool isInherited,
        LocationInfo fallback,
        INamedTypeSymbol? implementer
    )
    {
        if (FindMarking(member, implementer) is not { } marking)
        {
            return null;
        }

        var (kind, type) = member switch
        {
            IFieldSymbol field => (CandidateMember.Field, field.Type),
            IPropertySymbol property => (CandidateMember.Property, property.Type),
            IMethodSymbol method => (CandidateMember.Method, method.ReturnType),
            IEventSymbol @event => (CandidateMember.Field, @event.Type),
            _ => (CandidateMember.Field, null),
        };
        var candidate = Create(
            member,
            kind,
            CandidateType.Unsupported,
            isAsync: false,
            takesCancellationToken: false,
            member.IsStatic,
            isInherited,
            marking,
            type is null ? string.Empty : TargetTypeReader.MetadataNameOf(type),
            fallback
        );
        return OwnerOf(member) is { } owner ? candidate with { Name = owner.Name } : candidate;
    }

    // The primary constructor: the instance constructor that the type's declaration itself declares.  Roslyn 4.14 has
    // no public flag for it.
    internal static IMethodSymbol? PrimaryConstructorOf(INamedTypeSymbol type, CancellationToken cancellationToken) =>
        type.InstanceConstructors.FirstOrDefault(constructor =>
            !constructor.IsImplicitlyDeclared
            && constructor.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax { ParameterList: not null }
            )
        );

    // The primary constructor's parameters that are candidates, each with its symbol.
    private static List<(IParameterSymbol Symbol, ConnectionCandidate Candidate)> ReadParameters(
        IMethodSymbol? primary,
        LocationInfo fallback
    )
    {
        var parameters = new List<(IParameterSymbol, ConnectionCandidate)>();
        foreach (var parameter in primary?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty)
        {
            if (
                Held(parameter, CandidateMember.Parameter, parameter.Type, false, false, fallback, implementer: null) is
                { } candidate
            )
            {
                parameters.Add((parameter, candidate));
            }
        }

        return parameters;
    }

    // Whether an argument that the type's declaration passes to its base type, or the initializer of one of the type's
    // fields, properties or events, reads the parameter: the compiler warns of a parameter that a method captures and
    // that the type also passes on so (CS9107) or stores so (CS9124).  An identifier of the parameter's name is bound
    // only then, so the semantic model is asked of no other.
    internal static bool IsReadBesides(
        IParameterSymbol parameter,
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax declaration)
            {
                continue;
            }

            SemanticModel? model = null;
            foreach (var expression in InitialValuesOf(declaration))
            {
                foreach (var identifier in expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    if (identifier.Identifier.ValueText != parameter.Name)
                    {
                        continue;
                    }

                    model ??= compilation.GetSemanticModel(declaration.SyntaxTree);
                    if (
                        SymbolEqualityComparer.Default.Equals(
                            model.GetSymbolInfo(identifier, cancellationToken).Symbol,
                            parameter
                        )
                    )
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    // The arguments of the declaration's base type, and the initializers of its fields, properties and events.
    private static IEnumerable<ExpressionSyntax> InitialValuesOf(TypeDeclarationSyntax declaration)
    {
        foreach (var baseType in declaration.BaseList?.Types ?? default)
        {
            if (baseType is PrimaryConstructorBaseTypeSyntax { ArgumentList: var arguments })
            {
                foreach (var argument in arguments.Arguments)
                {
                    yield return argument.Expression;
                }
            }
        }

        foreach (var member in declaration.Members)
        {
            if (member is BaseFieldDeclarationSyntax field)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Initializer is { Value: var value })
                    {
                        yield return value;
                    }
                }
            }
            else if (member is PropertyDeclarationSyntax { Initializer.Value: var initial })
            {
                yield return initial;
            }
        }
    }

    // A field, property or parameter: a string named by the conventions, a data source, or, when marked, a connection.
    // A marked one of another type is unsupported.
    private static ConnectionCandidate? Held(
        ISymbol symbol,
        CandidateMember member,
        ITypeSymbol type,
        bool isStatic,
        bool isInherited,
        LocationInfo fallback,
        INamedTypeSymbol? implementer
    )
    {
        var marking = FindMarking(symbol, implementer);
        var kind = HeldKindOf(type);
        if (marking is null)
        {
            var isConvention =
                kind == CandidateType.DataSource
                || (
                    kind == CandidateType.ConnectionString
                    && ConventionNames.Contains(symbol.Name, StringComparer.Ordinal)
                );
            if (!isConvention)
            {
                return null;
            }
        }

        return Create(
            symbol,
            member,
            kind ?? CandidateType.Unsupported,
            isAsync: false,
            takesCancellationToken: false,
            isStatic,
            isInherited,
            marking,
            TargetTypeReader.MetadataNameOf(type),
            fallback
        );
    }

    internal static CandidateType? HeldKindOf(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return CandidateType.ConnectionString;
        }

        if (DerivesFrom(type, DbDataSource))
        {
            return CandidateType.DataSource;
        }

        return DerivesFrom(type, DbConnection) ? CandidateType.Connection : null;
    }

    // A method is a candidate only when it is marked: a factory, a method that returns a connection string, or an
    // unsupported one.
    private static ConnectionCandidate? Method(
        IMethodSymbol method,
        bool isInherited,
        LocationInfo fallback,
        INamedTypeSymbol? implementer
    )
    {
        if (FindMarking(method, implementer) is not { } marking)
        {
            return null;
        }

        var takesToken =
            method.Parameters.Length == 1
            && method.Parameters[0].RefKind == RefKind.None
            && IsNamed(method.Parameters[0].Type, ThreadingNamespace, "CancellationToken");
        var isCallable = !method.IsGenericMethod && (method.Parameters.IsEmpty || takesToken);
        var (kind, isAsync, declared) = ReturnOf(method.ReturnType);
        return Create(
            method,
            CandidateMember.Method,
            isCallable ? kind : CandidateType.Unsupported,
            isAsync && isCallable,
            takesToken,
            method.IsStatic,
            isInherited,
            marking,
            TargetTypeReader.MetadataNameOf(declared),
            fallback
        );
    }

    // What a method returns: a connection string, a connection, or a task of one.
    private static (CandidateType Kind, bool IsAsync, ITypeSymbol Declared) ReturnOf(ITypeSymbol returnType)
    {
        if (returnType.SpecialType == SpecialType.System_String)
        {
            return (CandidateType.ConnectionString, false, returnType);
        }

        if (DerivesFrom(returnType, DbConnection))
        {
            return (CandidateType.Factory, false, returnType);
        }

        if (
            returnType is INamedTypeSymbol { Name: "Task" or "ValueTask", Arity: 1 } task
            && IsNamed(task, TasksNamespace, task.Name)
            && DerivesFrom(task.TypeArguments[0], DbConnection)
        )
        {
            return (CandidateType.Factory, true, task.TypeArguments[0]);
        }

        return (CandidateType.Unsupported, false, returnType);
    }

    private static ConnectionCandidate Create(
        ISymbol symbol,
        CandidateMember member,
        CandidateType kind,
        bool isAsync,
        bool takesCancellationToken,
        bool isStatic,
        bool isInherited,
        AttributeData? marking,
        string declaredTypeName,
        LocationInfo fallback
    ) =>
        new(
            symbol.Name,
            member,
            kind,
            isAsync,
            takesCancellationToken,
            isStatic,
            isInherited,
            marking is not null,
            marking is null ? null : DatabaseOf(marking),
            declaredTypeName,
            SourceLocationOr(symbol, fallback)
        );

    // The member's own attribute, or that of the nearest member it overrides: an override of a marked member is marked
    // as it is, Database too, since the attribute is not inherited and the override hides the member it overrides.
    // Given an implementer, a factory, each of those is also marked as the interface member it implements in the
    // factory is; a repository's own members are not, so that implementing an interface never marks them.
    internal static AttributeData? FindMarking(ISymbol symbol, INamedTypeSymbol? implementer = null)
    {
        for (var current = symbol; current is not null; current = OverriddenOf(current))
        {
            if (MarkingOf(current) is { } marking)
            {
                return marking;
            }

            if (implementer is not null && ImplementedMarkingOf(current, implementer) is { } implemented)
            {
                return implemented;
            }
        }

        return null;
    }

    // The attribute of an interface member that the member implements in the factory: such a member is marked as the
    // interface member is, Database too, as an override of a marked member is.  The mark is asked first, since most
    // interface members bear none.
    private static AttributeData? ImplementedMarkingOf(ISymbol member, INamedTypeSymbol factory)
    {
        foreach (var @interface in factory.AllInterfaces)
        {
            foreach (var implemented in @interface.GetMembers(member.Name))
            {
                if (
                    MarkingOf(implemented) is { } marking
                    && SymbolEqualityComparer.Default.Equals(
                        factory.FindImplementationForInterfaceMember(implemented),
                        member
                    )
                )
                {
                    return marking;
                }
            }
        }

        return null;
    }

    private static ISymbol? OverriddenOf(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };

    // The property or event that an accessor or a backing field belongs to.
    private static ISymbol? OwnerOf(ISymbol member) =>
        member switch
        {
            IFieldSymbol field => field.AssociatedSymbol,
            IMethodSymbol method => method.AssociatedSymbol,
            _ => null,
        };

    // The symbol's own attribute, by the name alone first, so that the full name is made only for an attribute that can
    // be the connection's.
    private static AttributeData? MarkingOf(ISymbol symbol) =>
        symbol
            .GetAttributes()
            .FirstOrDefault(static attribute =>
                attribute.AttributeClass is { Name: AttributeName } attributeClass
                && attributeClass
                    .ToDisplayString()
                    .Equals(AttributeSource.ConnectionAttributeMetadataName, StringComparison.Ordinal)
            );

    private static string? DatabaseOf(AttributeData attribute)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (
                argument.Key == AttributeSource.DatabaseProperty
                && argument.Value is { Kind: not TypedConstantKind.Error, Value: string { Length: > 0 } database }
            )
            {
                return database;
            }
        }

        return null;
    }

    // Whether the type is the class of the name in System.Data.Common or derives from it.
    private static bool DerivesFrom(ITypeSymbol type, string name)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (IsNamed(current, DataNamespace, name))
            {
                return true;
            }
        }

        return false;
    }

    // By the name and the namespace, so that a nullable annotation, which a type's display carries, does not count.
    private static bool IsNamed(ITypeSymbol type, string ns, string name) =>
        type.Name == name && type.ContainingNamespace?.ToDisplayString() == ns;
}
