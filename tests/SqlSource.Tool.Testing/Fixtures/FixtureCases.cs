using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Testing.Fixtures;

// The cases of Fixtures/Describers/<engine>/, which the build copies to the output.  A folder under the engine's
// that holds a query is a case; a file there, the engine's schema.sql for one, is none, and so is a folder without a
// query, which an incremental build leaves in the output when a case is renamed or removed.
internal static class FixtureCases
{
    // Where the build puts the cases.
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Describers");

    // The cases of an engine in the order of their names.  An engine with no folder has none.
    public static IReadOnlyList<FixtureCase> Of(string engine) => InFolder(Path.Combine(Root, engine));

    // The same in another engine's folder, whose name is the engine: a test's own, or the repository's, which the
    // container projects read and record in.
    public static IReadOnlyList<FixtureCase> InFolder(string engineFolder)
    {
        if (!Directory.Exists(engineFolder))
        {
            return [];
        }

        var engine = Path.GetFileName(Path.TrimEndingDirectorySeparator(engineFolder));
        return
        [
            .. Directory
                .GetDirectories(engineFolder)
                .Where(static path => File.Exists(Path.Combine(path, FixtureCase.QueryFile)))
                .Select(path => new FixtureCase(engine, Path.GetFileName(path), path))
                .OrderBy(static found => found.Name, StringComparer.Ordinal),
        ];
    }
}
