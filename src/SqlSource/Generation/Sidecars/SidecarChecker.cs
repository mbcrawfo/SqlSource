using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
/// The checks run in a fixed order.  A dialect the tool cannot describe, or one this version has no type map for,
/// stops every check of the file's queries.  A sidecar that cannot be read, or is of another format, has no usable
/// entry and stops the checks of the queries and of the orphans.  A file with parse errors has no queries, so only the
/// checks of the sidecar itself run for it: every entry would otherwise be an orphan until the file parses.  Each
/// query that needs an entry then has none, one of another engine, a stale one, or a usable one, in that order.
/// </remarks>
internal static class SidecarChecker
{
    private static readonly EquatableArray<SidecarEntry> NoEntries = EquatableArray<SidecarEntry>.Empty;

    public static SidecarCheck Check(SidecarCheckInput input)
    {
        var file = input.File;
        if (input.Sidecar is null && input.Needs is null)
        {
            return new SidecarCheck(file.NormalizedPath, EquatableArray<DiagnosticInfo>.Empty, NoEntries);
        }

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var needing = FindNeeding(file, input.Needs);
        var checksQueries = needing.Count > 0 && CanCheckQueries(file.Dialect, needing, diagnostics);
        var sidecarFile = input.Sidecar;
        var sidecar = sidecarFile is null ? null : SidecarFileChecks.Read(sidecarFile, diagnostics);
        if (sidecarFile is not null && sidecar is null)
        {
            return Finish(file, diagnostics, NoEntries);
        }

        var usable = checksQueries ? CheckQueries(file.Dialect, needing, sidecar, diagnostics) : NoEntries;
        if (sidecarFile is not null && sidecar is not null && file.Errors.Count == 0)
        {
            FindOrphans(sidecarFile, sidecar, needing, diagnostics);
        }

        return Finish(file, diagnostics, usable);
    }

    private static SidecarCheck Finish(
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
    // describe can give them an entry, and the file then gets one error, at its first query that needs an entry.
    private static bool CanCheckQueries(
        SqlDialect dialect,
        List<(SqlQuery Query, OutputKind Output)> needing,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        if (!SqlDescribableDialects.Contains(dialect))
        {
            // The generator does not report SQLSRC209 yet.  It goes here, at the first query of needing.
            return false;
        }

        if (!HasTypeMap(dialect))
        {
            diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.EngineHasNoTypeMap,
                    needing[0].Query.NameLocation,
                    SqlDialectName.Canonical(dialect)
                )
            );
            return false;
        }

        return true;
    }

    // The dialects whose types this version can map.  Phase 6 adds mssql.
    private static bool HasTypeMap(SqlDialect dialect) => dialect == SqlDialect.PostgreSql;

    // Each query that needs an entry against its entry.  The hash is computed here and nowhere else in the generator,
    // so that a query that needs no entry never pays for it.
    private static EquatableArray<SidecarEntry> CheckQueries(
        SqlDialect dialect,
        List<(SqlQuery Query, OutputKind Output)> needing,
        Sidecar? sidecar,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics
    )
    {
        var engine = SqlDialectName.Canonical(dialect);
        var usable = ImmutableArray.CreateBuilder<SidecarEntry>();
        foreach (var (query, output) in needing)
        {
            // The generator does not report SQLSRC210 yet.  A query with a token whose default is null gets it here,
            // and is not checked against its entry.
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
