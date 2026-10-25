using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.EndToEnd;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// The models the generator writes for a case with models, found as it names them from the case's sidecar entry, in
// the assembly and the namespace of an engine's project that hold the classes of its RealisticQueries.cs, named for
// that engine.  The models are read from that assembly by reflection, never from the sidecar: a check of them can fail
// where "describe --check" passes.
internal sealed class RealisticModels(Assembly assembly, string @namespace, DatabaseEngine engine)
{
    // The suffix of an input model: the project's SqlSourceInputModelSuffix.
    public const string InputSuffix = "Args";

    // The suffix of an output model: the default, which the project and the corpus leave unset.
    public const string OutputSuffix = QuerySettings.DefaultOutputModelSuffix;

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

    // Skips a case that the run through Dapper does not read, with the reason: it has no models, the server does not
    // run it, or it has a parameter that Dapper cannot send.
    public static void SkipUnlessReadThroughDapper(RealisticCase found, int major)
    {
        if (found.IsReadThroughDapperOn(major))
        {
            return;
        }

        SkipUnlessHasModels(found);
        found.SkipUnlessRunsOn(major);
        Assert.Skip($"{found} has a parameter that Dapper cannot send: {found.Dapper}.");
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
    public Type OutputModelOf(SidecarEntry entry) =>
        ModelNamed(
            (entry.MatchesTable is { } table ? PropertyNames.FromTable(table, engine) : null) ?? entry.Name,
            OutputSuffix
        );

    // The input model of the entry, which a query without parameters has not.
    public Type? InputModelOf(SidecarEntry entry) =>
        entry.Parameters.Count == 0 ? null : ModelNamed(entry.Name, InputSuffix);

    // The SQL of the case's query, the constant of its claiming class named as the query, which SqlLocation.Direct
    // writes on the class itself.  The class is the corpus's claim of the case, in the namespace of the models.
    public string SqlOf(RealisticCorpus corpus, RealisticCase found, SidecarEntry entry)
    {
        var claim = RealisticClaims.Of(corpus, found, RealisticClaims.ModelsOutput);
        var type =
            assembly.GetType($"{@namespace}.{claim.TypeName}")
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} is not compiled");
        return type.GetField(entry.Name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(null) as string
            ?? throw new InvalidOperationException($"The class {claim.TypeName} of {found} has no SQL {entry.Name}");
    }

    // The case's output model is as nullable as its truth, or wider: a column the truth says can be NULL has a property
    // that can hold null.  Each column is paired with the property at its position, which must be named after it, so a
    // reordering cannot pass.  A case without models skips.
    public void ShouldBeAsNullableAsItsTruth(RealisticCase found)
    {
        SkipUnlessHasModels(found);

        var model = OutputModelOf(EntryOf(found));
        var properties = ModelReflection.PropertiesOf(model);

        properties.Count.ShouldBe(
            found.Columns.Count,
            $"{found} has {found.Columns.Count} columns in its truth, and {model.Name} {properties.Count} properties"
        );
        var failures = new List<string>();
        for (var index = 0; index < found.Columns.Count; index++)
        {
            var column = found.Columns[index];
            var property = properties[index];
            var expected = PropertyNames.PascalCase(column.Name);
            if (property.Name != expected)
            {
                failures.Add(
                    $"{found} column {index} {column.Name} is the property {model.Name}.{property.Name}, which "
                        + $"should be named {expected}"
                );
            }

            if (column.Nullable && !ModelReflection.IsNullable(property))
            {
                failures.Add(
                    $"{found} column {index} {column.Name} can be NULL ({column.Why}), and the property "
                        + $"{model.Name}.{property.Name} of {property.PropertyType.Name} is not nullable"
                );
            }
        }

        if (failures.Count > 0)
        {
            Assert.Fail(string.Join('\n', failures));
        }
    }

    private Type ModelNamed(string name, string suffix)
    {
        var full = $"{@namespace}.{name}{suffix}";
        return ModelReflection.ModelNamed(assembly, full)
            ?? throw new InvalidOperationException($"The model {full} is not in {assembly.GetName().Name}");
    }
}
