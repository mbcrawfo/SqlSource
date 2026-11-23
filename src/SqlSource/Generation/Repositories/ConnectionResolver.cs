using System.Collections.Generic;
using System.Linq;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Decides which member gives a repository's methods their connection for each database its repository queries use,
/// one member or a pair of which one returns a task, from the candidates of its <see cref="RepositoryShape" />, with
/// <c>SQLSRC028</c> to <c>SQLSRC034</c> and <c>SQLSRC036</c> for what it cannot decide.  Every problem but
/// <c>SQLSRC031</c> leaves the databases it is about without a connection.
/// </summary>
internal static partial class ConnectionResolver
{
    /// <summary>
    /// The connection of each database the type needs, from the first tier that has a candidate for it or fails it:
    /// the type's own members, then the marked members of the factories it holds, then the marked statics of the
    /// project.  A static type gets <c>SQLSRC034</c> and none.  In the type's own members, when a member is marked,
    /// each database takes the member marked for it, or the one marked without a <c>Database</c> when the type needs
    /// one database; otherwise the type must need one database, and the candidates of the conventions are its.  In the
    /// two shared tiers, each database takes the marks that name it, or else the marks without a <c>Database</c>.  A
    /// database may take two members of one tier when only one of them returns a task: its asynchronous methods take
    /// that one, and its synchronous methods the other.  Nothing is reported for a type that needs none.
    /// </summary>
    /// <param name="type">The repository.</param>
    /// <param name="shape">Its candidates in each tier, which the readers have filtered.</param>
    /// <param name="needed">
    /// The databases of its repository queries, each once by <see cref="DatabaseName.Comparer" />.
    /// </param>
    public static ConnectionResolution Resolve(
        TargetType type,
        RepositoryShape shape,
        IReadOnlyCollection<NeededDatabase> needed
    )
    {
        var resolving = new Resolving(type, needed, shape.SimpleNameTakenBy);
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
        AddSharedTiers(resolving, shape, byDatabase);
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
                case 2 when members[0].IsAsync != members[1].IsAsync:
                    resolving.Take(database, members[0], members[1]);
                    break;
                default:
                    resolving.Report(
                        DiagnosticInfo.Create(
                            SqlDiagnostics.RepositoryHasManyConnections,
                            type.AttributeLocation,
                            resolving.TypeName,
                            database.Name,
                            QuotedList(members.Select(static member => member.DisplayName))
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

    // Where SQLSRC028 says a marked member can be, after the advice to mark one.
    private const string MarkedPlaces =
        ", whether a member of the type, an instance member of a type it holds in a field, property or "
        + "primary-constructor parameter, or a static member of the project.";

    // What SQLSRC028 says to add: a marked member for this database when the type uses several, a marked member, of
    // the type, of a type it holds or of the project, when one of the type's is marked already, since no convention
    // then applies, a field or property for a parameter the conventions would take but that the type stores or passes
    // on, and the conventions' members or a marked member of any of those places otherwise.
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
                + "member that gives this database's connection [SqlSourceConnection]"
                + MarkedPlaces;
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
            + "DbDataSource type, or mark the member that gives the connection [SqlSourceConnection]"
            + MarkedPlaces;
    }

    private static string QuotedList(IEnumerable<string> names) =>
        string.Join(", ", names.Select(static name => "'" + name + "'"));
}
