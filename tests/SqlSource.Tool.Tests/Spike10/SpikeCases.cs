using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Tests.Spike10;

// The cases the phase 10.0 spike reads: the fixtures of both engines, then the realistic corpora captured by the
// container projects.  Everything the spike writes is under SPIKE10_OUT.
internal static class SpikeCases
{
    private const string OutVariable = "SPIKE10_OUT";

    private static string Root =>
        Environment.GetEnvironmentVariable(OutVariable) is { Length: > 0 } root
            ? root
            : throw new InvalidOperationException($"{OutVariable} is not set.");

    public static string CasesFolder => Path.Combine(Root, "spike10", "cases");

    public static IReadOnlyList<FixtureCase> All()
    {
        var all = new List<FixtureCase>();
        all.AddRange(FixtureCases.Of("postgres"));
        all.AddRange(FixtureCases.Of("mssql"));
        foreach (var engine in new[] { "postgres", "mssql" })
        {
            all.AddRange(FixtureCases.InFolder(Path.Combine(CasesFolder, "realistic-" + engine, engine)));
        }

        return all;
    }

    // Whether the case is one of the captured realistic corpora.
    public static bool IsRealistic(FixtureCase fixture) =>
        fixture.Folder.Contains(Path.DirectorySeparatorChar + "realistic-", StringComparison.Ordinal);

    // The case as a new folder, with query.sql or capture.json replaced when given.
    public static FixtureCase Copy(FixtureCase fixture, string name, string? query = null, string? capture = null)
    {
        var folder = Path.Combine(CasesFolder, "work", fixture.Engine, name);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        _ = Directory.CreateDirectory(folder);
        foreach (var file in Directory.GetFiles(fixture.Folder))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }

        if (query is not null)
        {
            File.WriteAllText(Path.Combine(folder, FixtureCase.QueryFile), query);
        }

        if (capture is not null)
        {
            File.WriteAllText(Path.Combine(folder, FixtureCase.CaptureFile), capture);
        }

        return new FixtureCase(fixture.Engine, name, folder);
    }

    // The path of a results file, whose folder is made.
    public static string Out(string file)
    {
        var path = Path.Combine(Root, "spike10", "results", file);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
}
