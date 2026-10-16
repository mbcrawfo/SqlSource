using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Testing.Fixtures;

// The cases of Fixtures/Describers/<engine>/, which the build copies to the output.  A folder under the engine's
// is a case; a file there, the engine's schema.sql for one, is none.
internal static class FixtureCases
{
    // Where the build puts the cases.
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Describers");

    // The cases of an engine in the order of their names.  An engine with no folder has none.
    public static IReadOnlyList<FixtureCase> Of(string engine) => Of(engine, Root);

    // The same under another root, for a test that writes its cases.
    public static IReadOnlyList<FixtureCase> Of(string engine, string root)
    {
        var folder = Path.Combine(root, engine);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. Directory
                .GetDirectories(folder)
                .Select(path => new FixtureCase(engine, Path.GetFileName(path), path))
                .OrderBy(static found => found.Name, StringComparer.Ordinal),
        ];
    }
}
