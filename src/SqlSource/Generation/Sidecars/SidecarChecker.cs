using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// Checks one claimed file's sidecar against what the types that claim the file need, once for the file whatever the
/// number of its types, and gives the entries that passed every check.
/// </summary>
/// <remarks>
/// The checks run in a fixed order.  A dialect the tool cannot describe, or one this version has no type map for, stops
/// the check of each query against the sidecar, while the tokens of the queries are still checked.  A token without a
/// default is reported whatever the dialect and the sidecar, as the tool reports it, and stops the check of its query.
/// A sidecar that cannot be read, or is of another format, has no usable entry and stops the checks of the queries and
/// of the orphans.  A file with parse errors has no queries, so only the checks of the sidecar itself run for it: every
/// entry would otherwise be an orphan until the file parses.  Each query that needs an entry then has none, one of
/// another engine, a stale one, or a usable one, in that order.  A query stopped by a check is still an entry's query,
/// so its entry is no orphan.
/// </remarks>
internal static class SidecarChecker
{
    private static readonly EquatableArray<SidecarEntry> NoEntries = EquatableArray<SidecarEntry>.Empty;

    public static SidecarCheckResult Check(SidecarCheckInput input)
    {
        var file = input.File;
        if (input.Sidecar is null && input.Needs is null)
        {
            return new SidecarCheckResult(file.NormalizedPath, EquatableArray<DiagnosticInfo>.Empty, NoEntries);
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var needing = FindNeeding(file, input.Needs);
        var checksQueries = needing.Count > 0 && CanCheckQueries(file.Dialect, needing, diagnostics);
        var stopped = FindTokensWithoutDefault(needing, diagnostics);
        var sidecarFile = input.Sidecar;
        var sidecar = sidecarFile is null ? null : SidecarFileChecks.Read(sidecarFile, diagnostics);
        if (sidecarFile is not null && sidecar is null)
        {
            return Finish(file, diagnostics, NoEntries);
        }

        var usable = checksQueries ? CheckQueries(file.Dialect, needing, stopped, sidecar, diagnostics) : NoEntries;
        if (sidecarFile is not null && sidecar is not null && file.Errors.Count == 0)
        {
            FindOrphans(sidecarFile, sidecar, needing, diagnostics);
        }

        return Finish(file, diagnostics, usable);
    }

    private static SidecarCheckResult Finish(
        ParsedSqlFile file,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        EquatableArray<SidecarEntry> usable
    ) => new(file.NormalizedPath, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()), usable);

    // The queries of the file that need an entry, in file order, each with its output.
    private static List<(SqlQuery Query, OutputKind Output)> FindNeeding(ParsedSqlFile file, FileNeeds? needs)
    {
        var needing = new List<(SqlQuery Query, OutputKind Output)>();
        if (needs is null)
        {
            return needing;
        }

        var outputs = new Dictionary<string, OutputKind>(StringComparer.Ordinal);
        foreach (var need in needs.Queries)
        {
            outputs[need.Name] = need.Output;
        }

        foreach (var query in file.Queries)
        {
            if (outputs.TryGetValue(query.Name, out var output))
            {
                needing.Add((query, output));
            }
        }

        return needing;
    }

    // Whether the queries that need an entry are checked against the sidecar.  They are not when no sqlsource
    // describe can give them an entry, and the file then gets one error, at its first query that needs an entry,
    // where the tool reports it: one setting mends the file.
    private static bool CanCheckQueries(
        SqlDialect dialect,
        List<(SqlQuery Query, OutputKind Output)> needing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var (first, output) = needing[0];
        if (!SqlDescribableDialects.Contains(dialect))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    ToolDiagnostics.OutputNeedsDescribableDialect,
                    first.NameLocation,
                    SettingValue.NameOf(output),
                    SqlDialectName.Canonical(dialect)
                )
            );
            return false;
        }

        if (!HasTypeMap(dialect))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.EngineHasNoTypeMap,
                    first.NameLocation,
                    SqlDialectName.Canonical(dialect)
                )
            );
            return false;
        }

        return true;
    }

    // The dialects whose types this version can map: both engines it describes.  SQLSRC243 waits for an engine whose
    // describer lands before its map, and no dialect of this release reports it.
    private static bool HasTypeMap(SqlDialect dialect) => dialect is SqlDialect.PostgreSql or SqlDialect.SqlServer;

    // One error for each token without a default of a query that needs an entry, at the query, in file order: no
    // sqlsource describe can give the query an entry until it has one.  An empty default is one.  Gives the names of
    // the queries it stopped.
    private static HashSet<string> FindTokensWithoutDefault(
        List<(SqlQuery Query, OutputKind Output)> needing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var stopped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (query, output) in needing)
        {
            foreach (var token in query.Tokens.Where(static token => token.Default is null))
            {
                _ = stopped.Add(query.Name);
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        ToolDiagnostics.TokenHasNoDefault,
                        query.NameLocation,
                        token.Name,
                        SettingValue.NameOf(output)
                    )
                );
            }
        }

        return stopped;
    }

    // Each query that needs an entry against its entry.  The hash is computed here and nowhere else in the generator,
    // so that a query that needs no entry never pays for it.
    private static EquatableArray<SidecarEntry> CheckQueries(
        SqlDialect dialect,
        List<(SqlQuery Query, OutputKind Output)> needing,
        HashSet<string> stopped,
        Sidecar? sidecar,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var engine = SqlDialectName.Canonical(dialect);
        var usable = ImmutableArray.CreateBuilder<SidecarEntry>();
        foreach (var (query, output) in needing)
        {
            if (stopped.Contains(query.Name))
            {
                // Its tokens are SQLSRC210, and its entry is neither checked nor used.
                continue;
            }

            if (sidecar?.Find(query.Name) is not { } entry)
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.QueryHasNoEntry,
                        query.NameLocation,
                        query.Name,
                        SettingValue.NameOf(output)
                    )
                );
            }
            else if (!string.Equals(entry.Engine, engine, StringComparison.Ordinal))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.EntryEngineDiffers,
                        query.NameLocation,
                        query.Name,
                        entry.Engine,
                        engine
                    )
                );
            }
            else if (
                !string.Equals(
                    entry.Hash,
                    SqlQueryHash.Compute(dialect, query.Segments, query.Tokens, query.Parameters),
                    StringComparison.Ordinal
                )
            )
            {
                diagnostics.Add(DiagnosticInfo.Create(SqlDiagnostics.EntryIsStale, query.NameLocation, query.Name));
            }
            else
            {
                usable.Add(entry);
            }
        }

        return new EquatableArray<SidecarEntry>(usable.ToImmutable());
    }

    // Each entry that names no query that needs one, names compared ordinally: of a query that is gone, renamed, or
    // that every claiming type now resolves to Sql.
    private static void FindOrphans(
        SidecarFile file,
        Sidecar sidecar,
        List<(SqlQuery Query, OutputKind Output)> needing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (query, _) in needing)
        {
            _ = names.Add(query.Name);
        }

        for (var index = 0; index < sidecar.Queries.Count; index++)
        {
            var entry = sidecar.Queries[index];
            if (!names.Contains(entry.Name))
            {
                diagnostics.Add(
                    DiagnosticInfo.Create(SqlDiagnostics.OrphanEntry, file.EntryLocations[index], entry.Name)
                );
            }
        }
    }
}
