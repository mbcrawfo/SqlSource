using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlSource.Tool.Testing.Realistic;

// The cases of a corpus, each a folder of its root, read for an engine.  A case folder that is wrong fails naming the
// case and the fault.  Each engine's RealisticCorpus reads its cases through it.
internal static class RealisticCaseReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // The name of every folder of the root, in order: each is a case, and a folder that is no case fails when it is
    // read.  Listing them reads no case.json, so that a broken one fails its own case and not every one.
    public static IReadOnlyList<string> NamesIn(string root) =>
        Directory.Exists(root)
            ?
            [
                .. Directory
                    .GetDirectories(root)
                    .Select(static folder => Path.GetFileName(folder))
                    .Order(StringComparer.Ordinal),
            ]
            : [];

    // Every case of the root, read afresh each time, so that a fault is the fault and not a failed type initializer.
    public static IReadOnlyList<RealisticCase> AllIn(string root, RealisticEngine engine) =>
        [.. NamesIn(root).Select(name => Read(Path.Combine(root, name), engine))];

    // The case of that folder, of the engine's corpus.
    public static RealisticCase Read(string folder, RealisticEngine engine)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        var named = RealisticCase.Named(name);
        if (!File.Exists(Path.Combine(folder, RealisticCase.QueryFile)))
        {
            throw new InvalidOperationException($"The case {named} has no {RealisticCase.QueryFile}.");
        }

        var path = Path.Combine(folder, RealisticCase.CaseFile);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The case {named} has no {RealisticCase.CaseFile}.");
        }

        CaseJson read;
        try
        {
            read =
                JsonSerializer.Deserialize<CaseJson>(File.ReadAllText(path), Options)
                ?? throw new JsonException("it is null");
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"The {RealisticCase.CaseFile} of the case {named} cannot be read: {exception.Message}",
                exception
            );
        }

        return new RealisticCase(
            engine,
            name,
            folder,
            read.MinimumMajor,
            read.Refusal,
            ColumnsOf(read, named),
            SamplesOf(read, named),
            ModelsOf(read, named),
            CheckedFromOf(read, named, engine.RecordedMajors),
            DapperOf(read, named)
        );
    }

    private static List<TruthColumn> ColumnsOf(CaseJson read, string named)
    {
        if (read.Refusal is not null && read.Columns is not null)
        {
            throw Fault(named, "has both a refusal and columns: a refused query has no columns");
        }

        if (read.Refusal is null && read.Columns is null)
        {
            throw Fault(named, "has neither a refusal nor columns");
        }

        var columns = new List<TruthColumn>();
        foreach (var (column, ordinal) in (read.Columns ?? []).Select(static (column, ordinal) => (column, ordinal)))
        {
            if (column.Name is not { Length: > 0 } columnName)
            {
                throw Fault(named, $"has a column {ordinal} with no name");
            }

            if (column.Nullable is not { } nullable)
            {
                throw Fault(named, $"has a column {ordinal} {columnName} that says not whether it is nullable");
            }

            if (nullable && string.IsNullOrWhiteSpace(column.Why))
            {
                throw Fault(named, $"has a column {ordinal} {columnName} that is nullable with no why");
            }

            // An exempt is a reason the seed cannot show a NULL, so it is said, and only of a column that can be NULL.
            if (column.Exempt is { } exempt && string.IsNullOrWhiteSpace(exempt))
            {
                throw Fault(named, $"has a column {ordinal} {columnName} whose exempt gives no reason");
            }

            if (!nullable && column.Exempt is not null)
            {
                throw Fault(named, $"has a column {ordinal} {columnName} that is not nullable and has an exempt");
            }

            columns.Add(new TruthColumn(columnName, nullable, column.Why, column.Exempt));
        }

        return columns;
    }

    private static List<IReadOnlyDictionary<string, string>> SamplesOf(CaseJson read, string named)
    {
        if (read.Samples is null)
        {
            return [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)];
        }

        if (read.Samples.Count == 0)
        {
            throw Fault(named, "has an empty list of samples, which runs nothing");
        }

        var samples = new List<IReadOnlyDictionary<string, string>>();
        foreach (var sample in read.Samples)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (parameter, value) in sample)
            {
                // A JSON null, where the literal "NULL" was meant.
                if (value is null)
                {
                    throw Fault(named, $"has a sample with no literal for @{parameter}: write \"NULL\" for a null");
                }

                if (!values.TryAdd(parameter, value))
                {
                    throw Fault(named, $"has a sample that names the parameter @{parameter} twice");
                }
            }

            samples.Add(values);
        }

        return samples;
    }

    // A refusal of the models is the generator's, of a query the describer accepts, so it is said with its reason.
    private static ModelRefusal? ModelsOf(CaseJson read, string named)
    {
        if (read.Models is not { } models)
        {
            return null;
        }

        if (read.Refusal is not null)
        {
            throw Fault(named, "has both a refusal and a refusal of its models: a refused query has no models");
        }

        if (string.IsNullOrWhiteSpace(models.Refusal))
        {
            throw Fault(named, "says its models are refused, but models gives no refusal");
        }

        if (string.IsNullOrWhiteSpace(models.Why))
        {
            throw Fault(named, "says its models are refused, but models gives no why");
        }

        return new ModelRefusal(models.Refusal, models.Why);
    }

    // The major from which a case's sidecar is checked, for a case that an earlier major describes otherwise: one of
    // the recorded majors, above the first recorded one the case runs on, since a checkedFrom at or below it leaves
    // nothing out, and said why.
    private static CheckedFrom? CheckedFromOf(CaseJson read, string named, IReadOnlyList<int> recordedMajors)
    {
        if (read.CheckedFrom is not { } from)
        {
            return null;
        }

        if (read.Refusal is not null || read.Models is not null)
        {
            throw Fault(named, "puts a checkedFrom on a case with no models, which has no sidecar to check");
        }

        const string Says = "says from which major its sidecar is checked, but";
        if (from.Major is not { } major)
        {
            throw Fault(named, $"{Says} checkedFrom names no major");
        }

        if (!recordedMajors.Contains(major))
        {
            throw Fault(
                named,
                $"{Says} checkedFrom names {major}, which is not a recorded major: "
                    + string.Join(" or ", recordedMajors)
            );
        }

        var first = FirstRecordedMajorOf(read.MinimumMajor, recordedMajors);
        if (major <= first)
        {
            throw Fault(named, $"{Says} checkedFrom is not above {first}, the first recorded major the case runs on");
        }

        if (string.IsNullOrWhiteSpace(from.Why))
        {
            throw Fault(named, $"{Says} checkedFrom gives no why");
        }

        return new CheckedFrom(major, from.Why);
    }

    // The lowest recorded major at or above the minimum, or the lowest of all when the case has no minimum or none
    // is at or above it.
    private static int FirstRecordedMajorOf(int? minimumMajor, IReadOnlyList<int> recordedMajors) =>
        recordedMajors
            .Where(major => minimumMajor is not { } minimum || major >= minimum)
            .DefaultIfEmpty(recordedMajors.Min())
            .Min();

    // Why Dapper cannot send a parameter of a case with models, which the run through Dapper leaves out for it.
    private static string? DapperOf(CaseJson read, string named)
    {
        if (read.Dapper is not { } dapper)
        {
            return null;
        }

        if (read.Refusal is not null || read.Models is not null)
        {
            throw Fault(named, "puts a dapper on a case with no models, which has no models to read rows into");
        }

        return string.IsNullOrWhiteSpace(dapper.Why)
            ? throw Fault(named, "says Dapper cannot send a parameter, but dapper gives no why")
            : dapper.Why;
    }

    private static InvalidOperationException Fault(string named, string fault) =>
        new($"The {RealisticCase.CaseFile} of the case {named} {fault}.");

    // The file as written: every member is optional here, and Read says what is missing.
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "JsonSerializer makes it."
    )]
    private sealed record CaseJson(
        int? MinimumMajor,
        string? Refusal,
        List<ColumnJson>? Columns,
        List<Dictionary<string, string>>? Samples,
        ModelsJson? Models,
        CheckedFromJson? CheckedFrom,
        DapperJson? Dapper
    );

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "JsonSerializer makes it."
    )]
    private sealed record ColumnJson(string? Name, bool? Nullable, string? Why, string? Exempt);

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "JsonSerializer makes it."
    )]
    private sealed record ModelsJson(string? Refusal, string? Why);

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "JsonSerializer makes it."
    )]
    private sealed record CheckedFromJson(int? Major, string? Why);

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "JsonSerializer makes it."
    )]
    private sealed record DapperJson(string? Why);
}
