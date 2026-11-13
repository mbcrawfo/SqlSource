using System;
using System.Collections.Generic;

namespace SqlSource.Repository.Tests.Documentation;

internal static class RepositoryFolders
{
    // The folders that hold no file of this repository that a test reads: git's, the worktrees and settings of local
    // tools, and what a build or a test run writes.
    internal static IReadOnlySet<string> Skipped { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            ".git",
            ".claude",
            ".vs",
            ".idea",
            "bin",
            "obj",
            "node_modules",
            "TestResults",
        };
}
