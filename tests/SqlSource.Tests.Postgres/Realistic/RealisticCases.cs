using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlSource.Tests.Postgres.Realistic;

// The cases of Realistic/Cases, read from the repository.  A case folder that is wrong fails naming the case and
// the fault.
internal static class RealisticCases
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // The name of every folder of Cases, in order: each is a case, and a folder that is no case fails when it is
    // read.  Listing them reads no case.json, so that a broken one fails its own case and not every one.
    public static IReadOnlyList<string> Names => NamesIn(RepositoryPaths.RealisticCases);

    // Every case of Cases, read afresh each time, so that a fault is the fault and not a failed type initializer.
    public static IReadOnlyList<RealisticCase> All => AllIn(RepositoryPaths.RealisticCases);

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

    public static IReadOnlyList<RealisticCase> AllIn(string root) =>
        [.. NamesIn(root).Select(name => Read(Path.Combine(root, name)))];

    public static RealisticCase Read(string folder)
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
            name,
            folder,
            read.MinimumMajor,
            read.Refusal,
            ColumnsOf(read, named),
            SamplesOf(read, named),
            ModelsOf(read, named)
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
        ModelsJson? Models
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
}
