using System.Collections.Generic;
using System.Linq;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Repositories;

internal static partial class ConnectionResolver
{
    // Adds the candidates of the held factories, then of the globals, for each database that the type's own members do
    // not decide, from the first of the two that has a candidate for it or fails it.  The own members decide a
    // database that they have a candidate for or fail, and every database when they are none and a primary-constructor
    // parameter is left out of them, which keeps SQLSRC028 naming the parameter.
    private static void AddSharedTiers(
        Resolving resolving,
        RepositoryShape shape,
        Dictionary<string, List<ConnectionCandidate>> byDatabase
    )
    {
        if (shape.Candidates.Count == 0 && shape.HeldParameters.Count > 0)
        {
            return;
        }

        var undecided = resolving
            .Needed.Where(database => !byDatabase.ContainsKey(database.Name) && !resolving.HasFailed(database.Name))
            .ToList();
        EquatableArray<ConnectionCandidate>[] tiers = [shape.HeldCandidates, shape.GlobalCandidates];
        foreach (var tier in tiers)
        {
            if (undecided.Count == 0)
            {
                return;
            }

            if (tier.Count == 0)
            {
                continue;
            }

            ReportUnsupported(resolving, tier, undecided);
            foreach (var database in undecided.Where(database => !resolving.HasFailed(database.Name)))
            {
                if (SharedCandidatesOf(tier, database) is { Count: > 0 } members)
                {
                    byDatabase.Add(database.Name, members);
                }
            }

            _ = undecided.RemoveAll(database =>
                byDatabase.ContainsKey(database.Name) || resolving.HasFailed(database.Name)
            );
        }
    }

    // The tier's candidates for the database: the marks that name it, or else the marks without a Database, but those
    // of a driver of another engine than the database's, so that a type shared by repositories of both engines needs no
    // Database on each of its marks.  A mark for a database that the type does not use is for none, silently, since
    // one factory or global serves many repositories.
    private static List<ConnectionCandidate> SharedCandidatesOf(
        EquatableArray<ConnectionCandidate> tier,
        NeededDatabase database
    )
    {
        var named = tier.Where(candidate =>
                candidate.Type != CandidateType.Unsupported
                && candidate.Database is { } name
                && DatabaseName.Comparer.Equals(name, database.Name)
            )
            .ToList();
        if (named.Count > 0)
        {
            return named;
        }

        return tier.Where(candidate =>
                candidate is { Type: not CandidateType.Unsupported, Database: null }
                && (DriverEngineOf(candidate.DeclaredTypeName) is not { } engine || engine == database.Engine)
            )
            .ToList();
    }

    // An unsupported member is reported once, at its holder, when it would be a candidate for a database still to
    // decide, and fails each such database: its Database, or every one when it names none.
    private static void ReportUnsupported(
        Resolving resolving,
        EquatableArray<ConnectionCandidate> tier,
        List<NeededDatabase> undecided
    )
    {
        foreach (var candidate in tier.Where(static candidate => candidate.Type == CandidateType.Unsupported))
        {
            var databases = undecided
                .Where(database =>
                    candidate.Database is not { } name || DatabaseName.Comparer.Equals(name, database.Name)
                )
                .Select(static database => database.Name)
                .ToList();
            if (databases.Count == 0)
            {
                continue;
            }

            resolving.Report(
                DiagnosticInfo.Create(
                    SqlDiagnostics.ConnectionMemberNotSupported,
                    resolving.LocationOf(candidate),
                    resolving.TypeName,
                    Resolving.MessageNameOf(candidate)
                )
            );
            resolving.FailEach(databases);
        }
    }
}
