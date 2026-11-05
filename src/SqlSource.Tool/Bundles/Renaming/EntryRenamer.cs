using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The entry of <c>expected.json</c> renamed by the bundle's mapping: its members by the table of the sidecar's
/// format, which is the same for every engine, and the members of a type record by the engine's part.
/// </summary>
/// <remarks>
/// A member's path is from the entry, as <c>columns[].origin.table</c>; a type record's member is asked of the engine
/// by the method <see cref="BundleFiles.Expected" /> and its path, as <c>columns[].type.element.name</c>.  A string
/// member that neither covers is refused, as a capture's is.  The query's name, the entry's key, is a query's.
/// </remarks>
internal static class EntryRenamer
{
    /// <summary>The place of the entry's members, as a refusal names it before a member's path.</summary>
    public const string Place = BundleFiles.Expected;

    // Where the type records are; each holds members of the engine's.
    private static readonly string[] TypeRecords =
    [
        JsonStrings.MemberOf(JsonStrings.ElementOf(SidecarKeys.Parameters), SidecarKeys.Type) + ".",
        JsonStrings.MemberOf(JsonStrings.ElementOf(SidecarKeys.Columns), SidecarKeys.Type) + ".",
    ];

    // The members every entry has, whatever its engine.
    private static readonly Dictionary<string, FieldRule> Neutral = new(StringComparer.Ordinal)
    {
        [SidecarKeys.Hash] = FieldRule.Fixed,
        [SidecarKeys.Engine] = FieldRule.Fixed,
        [SidecarKeys.Database] = FieldRule.Name(NameClass.Database),
        [SidecarKeys.ServerVersion] = FieldRule.Fixed,
        [SidecarKeys.ResultKind] = FieldRule.Closed,
        [SidecarKeys.Plan] = FieldRule.Fixed,
        [SidecarKeys.TableMatch] = FieldRule.Fixed,
        [Path(SidecarKeys.MatchesTable, SidecarKeys.Schema)] = FieldRule.Name(NameClass.Schema),
        [Path(SidecarKeys.MatchesTable, SidecarKeys.Table)] = FieldRule.Name(NameClass.Table),
        [Element(SidecarKeys.Parameters, SidecarKeys.Name)] = FieldRule.Parameter,
        [Element(SidecarKeys.Parameters, SidecarKeys.TypeSource)] = FieldRule.Fixed,
        [Element(SidecarKeys.Columns, SidecarKeys.Name)] = FieldRule.Name(NameClass.Column),
        [Element(SidecarKeys.Columns, SidecarKeys.NullableSource)] = FieldRule.Fixed,
        [Origin(SidecarKeys.Schema)] = FieldRule.Name(NameClass.Schema),
        [Origin(SidecarKeys.Table)] = FieldRule.Name(NameClass.Table),
        [Origin(SidecarKeys.Column)] = FieldRule.Name(NameClass.Column),
    };

    /// <summary>
    /// The rule of a member of the entry by its path from the entry: the table's, the engine's for a member of a type
    /// record, or null when neither has one.  The entry's <c>database</c> is a logical database, renamed as
    /// <see cref="RenamingRun.Database" /> renames one.
    /// </summary>
    public static FieldRule? RuleOf(IEngineRenaming engine, string path)
    {
        if (Neutral.TryGetValue(path, out var rule))
        {
            return rule;
        }

        return TypeRecords.Any(record => path.StartsWith(record, StringComparison.Ordinal))
            ? engine.RuleOf(BundleFiles.Expected, path)
            : null;
    }

    /// <summary>
    /// The renamed <c>expected.json</c>: a sidecar of the one entry, renamed, with the raw file's format and tool
    /// versions, and the hash of the renamed query as a replay computes it.  Null when the run refused, at the member
    /// that has no rule, or at <c>query.sql</c> when the renamed query cannot be planned.
    /// </summary>
    /// <param name="engine">The engine's part.</param>
    /// <param name="run">The run, whose mapping holds the names of the capture and the query.</param>
    /// <param name="expected">The raw <c>expected.json</c>, which <c>BundleReplay.ExpectedOf</c> reads.</param>
    /// <param name="query">The renamed <c>query.sql</c>.</param>
    /// <param name="token">Ends the plan of the query.</param>
    /// <exception cref="ArgumentException">The raw file is no sidecar of one entry that this tool reads.</exception>
    public static string? Rename(
        IEngineRenaming engine,
        RenamingRun run,
        string expected,
        string query,
        CancellationToken token
    )
    {
        if (SidecarReader.Read(expected).Sidecar is not { Queries.Count: 1 } raw)
        {
            throw new ArgumentException("The raw expected.json is no sidecar of one entry.", nameof(expected));
        }

        var root = JsonNode.Parse(expected)!.AsObject();
        var queries = root[SidecarKeys.Queries]!.AsObject();
        var (name, node) = queries.Single();
        var entry = node!.AsObject();
        JsonStrings.Visit(entry, string.Empty, found => Member(engine, run, found));
        var key = run.Field(FieldRule.Name(NameClass.Query), Field(name, string.Empty, null), engineRow: false, Place);
        _ = queries.Remove(name);
        queries[key] = entry;
        if (run.Refusal is not null)
        {
            return null;
        }

        var bundle = new Bundle(null, query, string.Empty, null, []);
        var engineName = SqlDialectName.Canonical(engine.Dialect);
        var planned = BundleReplay.Plan(bundle, engineName, RenamingProof.NoPath, token);
        if (!planned.Succeeded)
        {
            run.Refuse(RenamingProof.Unreadable(BundleFiles.Query, planned.Failure));
            return null;
        }

        var renamed =
            SidecarReader.Read(root.ToJsonString()).Sidecar?.Queries.Single()
            ?? throw new InvalidOperationException("The renamed entry is no longer a sidecar's.");
        var hash = planned.Value.Query.Hash ?? throw new InvalidOperationException("The renamed query has no hash.");
        return SidecarWriter.Write(
            new Sidecar(
                raw.FormatVersion,
                raw.ToolVersion,
                new EquatableArray<SidecarEntry>([renamed with { Hash = hash }])
            )
        );
    }

    private static string? Member(IEngineRenaming engine, RenamingRun run, JsonString found)
    {
        var place = Place + " " + found.Path;
        if (found.Path == SidecarKeys.Database)
        {
            return run.Database(found.Value);
        }

        if (RuleOf(engine, found.Path) is not { } rule)
        {
            run.Refuse(new RenamingRefusal(RefusalStep.Token, place, found.Value));
            return null;
        }

        return run.Field(rule, Field(found.Value, found.Path, found.Row), engineRow: false, place);
    }

    // A member of the entry, which is of no call; the entry's key is at the empty path.
    private static RenamingField Field(string value, string path, JsonObject? row) =>
        new(RenamingField.NoCall, BundleFiles.Expected, path, value, row);

    private static string Path(string parent, string key) => JsonStrings.MemberOf(parent, key);

    private static string Element(string array, string key) => JsonStrings.MemberOf(JsonStrings.ElementOf(array), key);

    private static string Origin(string key) =>
        JsonStrings.MemberOf(Element(SidecarKeys.Columns, SidecarKeys.Origin), key);
}
