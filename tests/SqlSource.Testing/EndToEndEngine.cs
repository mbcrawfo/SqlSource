using System;
using System.Collections.Generic;
using System.IO;

namespace SqlSource.Testing;

// An engine's end-to-end databases: the folder that holds a folder for each, with its schema.sql and its seed.sql, and
// how a file of that engine is split into the commands it runs as.
internal sealed record EndToEndEngine(string Folder, Func<string, IReadOnlyList<string>> Batches)
{
    // PostgreSQL's, whose file runs whole as one command.
    public static EndToEndEngine Postgres { get; } =
        new(
            Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.Postgres", "EndToEnd"),
            static script => [script]
        );

    // SQL Server's, whose file runs batch by batch, split at the lines that hold GO alone.
    public static EndToEndEngine SqlServer { get; } =
        new(
            Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.SqlServer", "EndToEnd"),
            EndToEndFiles.SqlServerBatches
        );
}
