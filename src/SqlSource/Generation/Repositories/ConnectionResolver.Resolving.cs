using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Diagnostics;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Placement;
using SqlSource.Settings;

namespace SqlSource.Generation.Repositories;

internal static partial class ConnectionResolver
{
    // What the resolution of one type has found so far.
    private sealed class Resolving(TargetType type, IReadOnlyCollection<NeededDatabase> needed)
    {
        private readonly List<ResolvedConnection> _sources = [];
        private readonly List<string> _failed = [];
        private readonly ImmutableArray<DiagnosticInfo>.Builder _diagnostics =
            ImmutableArray.CreateBuilder<DiagnosticInfo>();

        public IReadOnlyList<NeededDatabase> Needed { get; } = [.. needed];

        public string TypeName { get; } = PlacementChecks.NameOf(type);

        // The needed database of the name, by DatabaseName.Comparer.
        public NeededDatabase? Find(string name)
        {
            foreach (var database in Needed)
            {
                if (DatabaseName.Comparer.Equals(database.Name, name))
                {
                    return database;
                }
            }

            return null;
        }

        public void Report(DiagnosticInfo diagnostic) => _diagnostics.Add(diagnostic);

        public bool HasFailed(string name) => _failed.Contains(name, DatabaseName.Comparer);

        public void Fail(string name)
        {
            if (!HasFailed(name))
            {
                _failed.Add(name);
            }
        }

        public void FailEach(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                Fail(name);
            }
        }

        public void FailAll() => FailEach(Needed.Select(static database => database.Name));

        // The name that the member's problem gives it, inside the descriptor's quotes: its own for the type's member,
        // the factory type's and its own, then the holder's, for a held one, which closes and opens the quotes to read
        // 'IDatabaseFactory.Open' reached through 'dbf', with the interface in place of the factory type for one
        // reached through a cast, and the type's and its own for a global.
        public static string MessageNameOf(ConnectionCandidate candidate) =>
            candidate.Holder is { } holder
                ? (candidate.CastTypeName ?? holder.TypeName)
                    + "."
                    + candidate.Name
                    + "' reached through '"
                    + holder.Name
                : candidate.DisplayName;

        // Where the member's problem is reported: at the member of the type, at the holder of a held one, which the
        // type names and whose factory may be in another assembly, and at the type's attribute for a global, which
        // is reported in each repository that would use it.
        public LocationInfo LocationOf(ConnectionCandidate candidate) =>
            candidate.Tier switch
            {
                CandidateTier.Own => candidate.Location,
                CandidateTier.Held => candidate.Holder?.Location ?? candidate.Location,
                CandidateTier.Global => type.AttributeLocation,
                _ => throw UnexpectedEnumValueException.Of(candidate.Tier),
            };

        // The member is the database's connection, of its synchronous methods too unless it returns a task, when it can
        // be one.
        public void Take(NeededDatabase database, ConnectionCandidate candidate)
        {
            if (!IsUsable(database, candidate))
            {
                Fail(database.Name);
                return;
            }

            var source = SourceOf(candidate);
            _sources.Add(new ResolvedConnection(database.Name, source, source.IsAsync ? null : source));
        }

        // The pair, of which one returns a task, are the database's connections, that one of its asynchronous methods
        // and the other of its synchronous ones, when both can be one.  Each is checked, so that each one's problem is
        // reported, and a problem of either fails the database; a holder of both is checked once, with the first.
        public void Take(NeededDatabase database, ConnectionCandidate first, ConnectionCandidate second)
        {
            var firstIsUsable = IsUsable(database, first);
            var secondIsUsable = IsUsable(
                database,
                second,
                checksHolder: second.Holder is null || second.Holder != first.Holder
            );
            if (!firstIsUsable || !secondIsUsable)
            {
                Fail(database.Name);
                return;
            }

            var (asynchronous, synchronous) = first.IsAsync ? (first, second) : (second, first);
            _sources.Add(new ResolvedConnection(database.Name, SourceOf(asynchronous), SourceOf(synchronous)));
        }

        // Whether the member can be the database's connection, with its problem when it cannot: a parameter that an
        // argument of the methods hides, the member's own or the holder of a held one unless the holder is checked
        // already, or a type of a driver of another engine.
        private bool IsUsable(NeededDatabase database, ConnectionCandidate candidate, bool checksHolder = true)
        {
            var (member, name, location) = candidate.Holder is { } holder
                ? (holder.Member, holder.Name, holder.Location)
                : (candidate.Member, candidate.Name, candidate.Location);
            if (checksHolder && member == CandidateMember.Parameter && IsAnArgumentsName(name, database))
            {
                Report(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionParameterHidden,
                        location,
                        TypeName,
                        name,
                        database.Name
                    )
                );
                return false;
            }

            if (DriverEngineOf(candidate.DeclaredTypeName) is { } other && other != database.Engine)
            {
                Report(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionOfAnotherEngine,
                        LocationOf(candidate),
                        TypeName,
                        MessageNameOf(candidate),
                        candidate.DeclaredTypeName,
                        database.Name,
                        database.Engine.DatabaseName(),
                        database.Engine.DriverName()
                    )
                );
                return false;
            }

            return true;
        }

        // The resolution, its databases in the order they are needed.
        public ConnectionResolution ToResolution() =>
            new(
                new EquatableArray<ResolvedConnection>(_sources.ToImmutableArray()),
                new EquatableArray<string>(
                    Needed.Select(static database => database.Name).Where(HasFailed).ToImmutableArray()
                ),
                new EquatableArray<DiagnosticInfo>(_diagnostics.ToImmutable())
            );

        // A held member is reached through its holder, as the type reaches the holder, and a global through its type in
        // full, which no argument or local can hide.
        private ConnectionSource SourceOf(ConnectionCandidate candidate)
        {
            var source = new ConnectionSource(
                KindOf(candidate.Type),
                candidate.Member,
                candidate.DisplayName,
                AccessTextOf(candidate.Name, candidate.Member, candidate.IsStatic),
                candidate.IsAsync,
                candidate.TakesCancellationToken,
                LeadingNameOf(candidate.Name, candidate.Member, candidate.IsStatic),
                TypeName,
                type.IsStruct
            );
            // The readers give every held candidate its holder and every global its type, so the last arm, which reads
            // either as the type's own member, is never taken; it stands so that a missing one cannot throw.
            return candidate switch
            {
                { Tier: CandidateTier.Held, Holder: { } holder } => HeldThrough(source, candidate, holder),
                { Tier: CandidateTier.Global, GlobalTypeText: { } typeText } => source with
                {
                    AccessText = typeText + "." + Escaped(candidate.Name),
                    LeadingName = null,
                },
                _ => source,
            };
        }

        private ConnectionSource HeldThrough(
            ConnectionSource source,
            ConnectionCandidate candidate,
            CandidateHolder holder
        )
        {
            var holderAccess = AccessTextOf(holder.Name, holder.Member, holder.IsStatic);
            return source with
            {
                MemberName = holder.Name + "." + candidate.Name,
                AccessText = holderAccess + "." + Escaped(candidate.Name),
                LeadingName = LeadingNameOf(holder.Name, holder.Member, holder.IsStatic),
                HolderAccessText = holderAccess,
                HolderName = holder.Name,
                HolderCanBeNull = holder.CanBeNull,
                HolderMember = holder.Member,
                HolderCastText = candidate.CastTypeText,
            };
        }

        // Whether a type parameter of the type, or of a type around it, hides an outer one of its name.  No path from
        // global:: can then name the type as it is constructed, since the outer parameter has no name left to write.
        private bool RepeatsATypeParameter => type.Types.Any(static each => each.RepeatsATypeParameter);

        // The name the access starts with, which an argument or a local of the method could hide: a parameter's, and a
        // static member's when it is reached by its name.  An instance member is reached through this and any other
        // static one from global::, which nothing hides.
        private string? LeadingNameOf(string name, CandidateMember member, bool isStatic) =>
            member == CandidateMember.Parameter || (isStatic && RepeatsATypeParameter) ? name : null;

        // A parameter by its name, an instance member through this, and a static member through the type in full, which
        // reaches a base type's static member too and which a member of the type's name cannot hide.  In a type that
        // repeats a type parameter, a static member is reached by its own name, which binds as the type's code does.
        private string AccessTextOf(string name, CandidateMember member, bool isStatic)
        {
            var escaped = Escaped(name);
            if (member == CandidateMember.Parameter)
            {
                return escaped;
            }

            if (!isStatic)
            {
                return "this." + escaped;
            }

            return RepeatsATypeParameter ? WithAwaitEscaped(escaped) : FullTypeText() + "." + escaped;
        }

        // The type from global::, each name and its type parameters as they are written, so a keyword keeps its @, and
        // an await gets one, which an asynchronous method needs.
        private string FullTypeText() =>
            "global::"
            + WithAwaitEscaped(
                (type.Namespace.Length > 0 ? type.Namespace + "." : string.Empty)
                    + string.Join(
                        ".",
                        type.Types.Select(static declaration => declaration.Name + declaration.TypeParameters)
                    )
            );

        // The text with an @ before each identifier that is exactly await, unless it already has one.
        private static string WithAwaitEscaped(string text)
        {
            const string Await = "await";
            var at = text.IndexOf(Await, StringComparison.Ordinal);
            if (at < 0)
            {
                return text;
            }

            var escaped = new StringBuilder(text.Length + 1);
            var copied = 0;
            for (; at >= 0; at = text.IndexOf(Await, at + Await.Length, StringComparison.Ordinal))
            {
                var end = at + Await.Length;
                var startsAName =
                    at == 0 || (text[at - 1] != '@' && !SyntaxFacts.IsIdentifierPartCharacter(text[at - 1]));
                if (startsAName && (end == text.Length || !SyntaxFacts.IsIdentifierPartCharacter(text[end])))
                {
                    _ = escaped.Append(text, copied, at - copied).Append('@');
                    copied = at;
                }
            }

            return escaped.Append(text, copied, text.Length - copied).ToString();
        }

        private static string Escaped(string name) => SqlIdentifier.IsReservedKeyword(name) ? "@" + name : name;

        private static ConnectionSourceKind KindOf(CandidateType type) =>
            type switch
            {
                CandidateType.ConnectionString => ConnectionSourceKind.ConnectionString,
                CandidateType.DataSource => ConnectionSourceKind.DataSource,
                CandidateType.Factory => ConnectionSourceKind.Factory,
                CandidateType.Connection => ConnectionSourceKind.Borrowed,
                CandidateType.Unsupported => throw UnexpectedEnumValueException.Of(type),
                _ => throw UnexpectedEnumValueException.Of(type),
            };

        // Whether a repository's method of the database can have an argument of the name, other than a token's, which
        // SQLSRC275 refuses: its cancellation token, which every method takes, or its input model, which only a query
        // with parameters has.
        private static bool IsAnArgumentsName(string name, NeededDatabase database) =>
            name == MethodArguments.CancellationToken
            || (name == MethodArguments.Parameters && database.TakesParameters);
    }

    // The engine whose driver declares the type, or null for a type of neither driver, such as one of
    // System.Data.Common: matched by the namespace of the type's metadata name.
    private static DatabaseEngine? DriverEngineOf(string declaredTypeName)
    {
        if (declaredTypeName.StartsWith("Npgsql.", StringComparison.Ordinal))
        {
            return DatabaseEngine.PostgreSql;
        }

        return declaredTypeName.StartsWith("Microsoft.Data.SqlClient.", StringComparison.Ordinal)
            ? DatabaseEngine.SqlServer
            : null;
    }
}
