using System;
using System.IO;
using System.Reflection;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Generation;

// The models the generator writes for a case with models, found as it names them from the case's sidecar entry, in
// this namespace, which is that of the classes of RealisticQueries.cs.  The models are read from this assembly by
// reflection, never from the sidecar: a check of them can fail where "describe --check" passes.
internal static class RealisticModels
{
    // The suffix of an input model: the project's SqlSourceInputModelSuffix.
    public const string InputSuffix = "Args";

    // The suffix of an output model: the default, which the project and the corpus leave unset.
    public const string OutputSuffix = QuerySettings.DefaultOutputModelSuffix;

    private static readonly Assembly Models = typeof(RealisticModels).Assembly;

    // Skips a check of the models for a case that has none, with the reason: the describer refuses it, or the
    // generator does.
    public static void SkipUnlessHasModels(RealisticCase found)
    {
        found.SkipIfRefused();
        if (found.Models is { } models)
        {
            Assert.Skip($"{found} has no models: the generator gives {models.Id}.");
        }
    }

    // The one entry of the case's sidecar, which holds the case's one query.
    public static SidecarEntry EntryOf(RealisticCase found)
    {
        var read = SidecarReader.Read(File.ReadAllText(found.SidecarPath));
        if (read.Sidecar is not { } sidecar)
        {
            throw new InvalidOperationException(
                $"{found} has a sidecar {found.SidecarPath} that does not read: "
                    + (read.Error?.ToString() ?? $"format version {read.FormatVersion}")
            );
        }

        return sidecar.Queries.Count == 1
            ? sidecar.Queries[0]
            : throw new InvalidOperationException(
                $"{found} has a sidecar with {sidecar.Queries.Count} entries, and its query is one"
            );
    }

    // The output model of the entry: the table's model when the result is a table's columns and the table gives a
    // name, the query's otherwise, as the generator names them.  The corpus has no output-model marker and leaves
    // no-table-models unset, so the table's or the query's name is the model's.
    public static Type OutputModelOf(SidecarEntry entry) =>
        ModelNamed(
            (entry.MatchesTable is { } table ? PropertyNames.FromTable(table, DatabaseEngine.PostgreSql) : null)
                ?? entry.Name,
            OutputSuffix
        );

    // The input model of the entry, which a query without parameters has not.
    public static Type? InputModelOf(SidecarEntry entry) =>
        entry.Parameters.Count == 0 ? null : ModelNamed(entry.Name, InputSuffix);

    private static Type ModelNamed(string name, string suffix)
    {
        var full = $"{typeof(RealisticModels).Namespace}.{name}{suffix}";
        return Models.GetType(full)
            ?? throw new InvalidOperationException($"The model {full} is not in {Models.GetName().Name}");
    }
}
