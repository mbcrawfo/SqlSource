using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Generation.Methods;
using SqlSource.Generation.Placement;
using SqlSource.Settings;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Decides which member gives a repository's methods their connection for each database its repository queries use,
/// from the candidates of its <see cref="RepositoryShape" />, with <c>SQLSRC028</c> to <c>SQLSRC034</c> for what it
/// cannot decide.  Every problem but <c>SQLSRC031</c> leaves the databases it is about without a connection.
/// </summary>
internal static class ConnectionResolver
{
    /// <summary>
    /// The connection of each database the type needs.  A static type gets <c>SQLSRC034</c> and none.  When a member is
    /// marked, each database takes the member marked for it, or the one marked without a <c>Database</c> when the type
    /// needs one database; otherwise the type must need one database, and the one candidate of the conventions is its
    /// connection.  Nothing is reported for a type that needs none.
    /// </summary>
    /// <param name="type">The repository.</param>
    /// <param name="shape">Its candidates, which the reader has filtered.</param>
    /// <param name="needed">
    /// The databases of its repository queries, each once by <see cref="DatabaseName.Comparer" />.
    /// </param>
    public static ConnectionResolution Resolve(
        TargetType type,
        RepositoryShape shape,
        IReadOnlyCollection<NeededDatabase> needed
    )
    {
        var resolving = new Resolving(type, needed);
        if (needed.Count == 0)
        {
            return resolving.ToResolution();
        }

        if (type.IsStatic)
        {
            resolving.Report(
                DiagnosticInfo.Create(SqlDiagnostics.RepositoryIsStatic, type.AttributeLocation, resolving.TypeName)
            );
            resolving.FailAll();
            return resolving.ToResolution();
        }

        var marked = shape.Candidates.Any(static candidate => candidate.IsMarked);
        var byDatabase = marked ? Assign(resolving, shape.Candidates) : Conventions(resolving, shape.Candidates);
        foreach (var database in needed)
        {
            if (resolving.HasFailed(database.Name))
            {
                continue;
            }

            var members = byDatabase.TryGetValue(database.Name, out var found) ? found : [];
            switch (members.Count)
            {
                case 0:
                    resolving.Report(
                        DiagnosticInfo.Create(
                            SqlDiagnostics.RepositoryHasNoConnection,
                            type.AttributeLocation,
                            resolving.TypeName,
                            database.Name,
                            NoConnectionAdvice(database.Name, needed.Count, marked, shape.HeldParameters)
                        )
                    );
                    resolving.Fail(database.Name);
                    break;
                case 1:
                    resolving.Take(database, members[0]);
                    break;
                default:
                    resolving.Report(
                        DiagnosticInfo.Create(
                            SqlDiagnostics.RepositoryHasManyConnections,
                            type.AttributeLocation,
                            resolving.TypeName,
                            database.Name,
                            QuotedList(members.Select(static member => member.Name))
                        )
                    );
                    resolving.Fail(database.Name);
                    break;
            }
        }

        return resolving.ToResolution();
    }

    // The marked members by the database each is for: an unsupported one fails its database, or every database when it
    // names none, one without a Database on a type of several databases fails each that no member is marked for, and
    // one for a database the queries do not use is a warning and is for none.
    private static Dictionary<string, List<ConnectionCandidate>> Assign(
        Resolving resolving,
        EquatableArray<ConnectionCandidate> candidates
    )
    {
        var byDatabase = new Dictionary<string, List<ConnectionCandidate>>(DatabaseName.Comparer);
        var named = new HashSet<string>(
            candidates
                .Where(static candidate => candidate.Database is not null)
                .Select(static candidate => candidate.Database!),
            DatabaseName.Comparer
        );
        foreach (var candidate in candidates)
        {
            var databases = DatabasesOf(resolving, candidate, named);
            if (candidate.Type == CandidateType.Unsupported)
            {
                resolving.Report(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionMemberNotSupported,
                        candidate.Location,
                        resolving.TypeName,
                        candidate.Name
                    )
                );
                resolving.FailEach(databases);
                continue;
            }

            foreach (var database in databases)
            {
                Add(byDatabase, database, candidate);
            }
        }

        return byDatabase;
    }

    // The needed databases that a marked member is for, with its problem when it is for none that it can be.
    private static IReadOnlyList<string> DatabasesOf(
        Resolving resolving,
        ConnectionCandidate candidate,
        HashSet<string> named
    )
    {
        if (candidate.Database is { } database)
        {
            if (resolving.Find(database) is { } found)
            {
                return [found.Name];
            }

            resolving.Report(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConnectionDatabaseNotUsed,
                    candidate.Location,
                    resolving.TypeName,
                    candidate.Name,
                    database,
                    QuotedList(resolving.Needed.Select(static needed => needed.Name))
                )
            );
            return [];
        }

        if (resolving.Needed.Count == 1)
        {
            return [resolving.Needed[0].Name];
        }

        // An unsupported member is reported as one, and stands for every database, each of which it fails.
        if (candidate.Type == CandidateType.Unsupported)
        {
            return [.. resolving.Needed.Select(static needed => needed.Name)];
        }

        resolving.Report(
            DiagnosticInfo.Create(
                SqlDiagnostics.ConnectionNeedsDatabase,
                candidate.Location,
                resolving.TypeName,
                candidate.Name,
                QuotedList(resolving.Needed.Select(static needed => needed.Name))
            )
        );

        // It could be the connection of any database that no member names, so each of those fails.
        resolving.FailEach(resolving.Needed.Select(static needed => needed.Name).Where(name => !named.Contains(name)));
        return [];
    }

    // The candidates of the conventions, each for the type's one database.  A type of several databases has none: no
    // convention says which database a member is for.
    private static Dictionary<string, List<ConnectionCandidate>> Conventions(
        Resolving resolving,
        EquatableArray<ConnectionCandidate> candidates
    )
    {
        var byDatabase = new Dictionary<string, List<ConnectionCandidate>>(DatabaseName.Comparer);
        if (resolving.Needed.Count == 1)
        {
            foreach (var candidate in candidates)
            {
                Add(byDatabase, resolving.Needed[0].Name, candidate);
            }
        }

        return byDatabase;
    }

    private static void Add(
        Dictionary<string, List<ConnectionCandidate>> byDatabase,
        string database,
        ConnectionCandidate candidate
    )
    {
        if (!byDatabase.TryGetValue(database, out var members))
        {
            members = [];
            byDatabase.Add(database, members);
        }

        members.Add(candidate);
    }

    // What SQLSRC028 says to add: a marked member for this database when the type uses several, a marked member when
    // one is marked already, since no convention then applies, a field or property for a parameter the conventions
    // would take but that the type stores or passes on, and the conventions' members otherwise.
    private static string NoConnectionAdvice(
        string database,
        int databases,
        bool marked,
        EquatableArray<string> heldParameters
    )
    {
        if (databases > 1)
        {
            return "It uses more than one database: mark the member that gives this one's connection "
                + "[SqlSourceConnection(Database = \""
                + database
                + "\")].";
        }

        if (marked)
        {
            return "A member is marked [SqlSourceConnection], so no other is chosen by its name or type: mark the "
                + "member that gives this database's connection [SqlSourceConnection].";
        }

        if (heldParameters.Count > 0)
        {
            var one = heldParameters.Count == 1;
            return "The primary-constructor "
                + (one ? "parameter " : "parameters ")
                + QuotedList(heldParameters)
                + (one ? " is" : " are")
                + " not chosen by "
                + (one ? "its" : "their")
                + " name or type, since the type also uses "
                + (one ? "it" : "them")
                + " to initialize a field or property, or passes "
                + (one ? "it" : "them")
                + " to its base type: keep the value in a field or property named connectionString or of a "
                + "DbDataSource type, or mark the parameter [SqlSourceConnection].";
        }

        return "Add a string field, property or primary-constructor parameter named connectionString, or one of a "
            + "DbDataSource type, or mark the member that gives the connection [SqlSourceConnection].";
    }

    private static string QuotedList(IEnumerable<string> names) =>
        string.Join(", ", names.Select(static name => "'" + name + "'"));

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

        // The member is the database's connection, unless it is a parameter that an argument of the methods hides, or
        // its type is a driver's of another engine.
        public void Take(NeededDatabase database, ConnectionCandidate candidate)
        {
            if (candidate.Member == CandidateMember.Parameter && IsAnArgumentsName(candidate.Name, database))
            {
                Report(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionParameterHidden,
                        candidate.Location,
                        TypeName,
                        candidate.Name,
                        database.Name
                    )
                );
                Fail(database.Name);
                return;
            }

            if (DriverEngineOf(candidate.DeclaredTypeName) is { } other && other != database.Engine)
            {
                Report(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ConnectionOfAnotherEngine,
                        candidate.Location,
                        TypeName,
                        candidate.Name,
                        candidate.DeclaredTypeName,
                        database.Name,
                        database.Engine.DatabaseName(),
                        database.Engine.DriverName()
                    )
                );
                Fail(database.Name);
                return;
            }

            _sources.Add(new ResolvedConnection(database.Name, SourceOf(candidate)));
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

        private ConnectionSource SourceOf(ConnectionCandidate candidate) =>
            new(
                KindOf(candidate.Type),
                candidate.Member,
                candidate.Name,
                AccessTextOf(candidate),
                candidate.IsAsync,
                candidate.TakesCancellationToken,
                LeadingNameOf(candidate),
                TypeName,
                type.IsStruct
            );

        // The name the access starts with, which an argument or a local of the method could hide: a parameter's own,
        // and the type's for a static member of a type that is not generic.  An instance member is reached through
        // this, which nothing hides.
        private string? LeadingNameOf(ConnectionCandidate candidate)
        {
            if (candidate.Member == CandidateMember.Parameter)
            {
                return candidate.Name;
            }

            // A local of the type's name hides it only when the type is not generic: a name with type arguments is
            // never a local's.
            return candidate.IsStatic && type.Types[type.Types.Count - 1].Arity == 0 ? type.Name : null;
        }

        // A parameter by its name, a static member through the type as its own code writes it, which reaches a base
        // type's static member too, and an instance member through this.  The type's name and type parameters are as
        // they are written, so a keyword among them keeps its @.
        private string AccessTextOf(ConnectionCandidate candidate)
        {
            var name = SqlIdentifier.IsReservedKeyword(candidate.Name) ? "@" + candidate.Name : candidate.Name;
            if (candidate.Member == CandidateMember.Parameter)
            {
                return name;
            }

            if (candidate.IsStatic)
            {
                var declaration = type.Types[type.Types.Count - 1];
                return declaration.Name + declaration.TypeParameters + "." + name;
            }

            return "this." + name;
        }

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
}
