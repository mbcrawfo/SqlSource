using System;
using System.Collections.Generic;
using System.IO;

namespace SqlSource.Tool.Testing.Fixtures;

// The cases of Fixtures/Bundles/<engine>/, which the build copies to the output: a bundle each, as a bug report's
// unzips, found as the describers' cases are.
internal static class BundleCases
{
    // Where the build puts the cases.
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bundles");

    // The cases of an engine in the order of their names.  An engine with no folder has none.
    public static IReadOnlyList<FixtureCase> Of(string engine) => FixtureCases.InFolder(Path.Combine(Root, engine));
}
