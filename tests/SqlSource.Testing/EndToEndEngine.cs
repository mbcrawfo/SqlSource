using System;
using System.Collections.Generic;

namespace SqlSource.Testing;

// An engine's end-to-end databases: the folder that holds a folder for each, with its schema.sql and its seed.sql, and
// how a file of that engine is split into the commands it runs as.  Each is made when it is read, so that a project of
// one engine never reads the other's folder, which RepositoryRoot would refuse.
internal sealed record EndToEndEngine(string Folder, Func<string, IReadOnlyList<string>> Batches)
{
    // PostgreSQL's, whose file runs whole as one command.
    public static EndToEndEngine Postgres =>
        new(RepositoryRoot.Path("tests", "DatabaseTests", "Postgres", "EndToEnd"), static script => [script]);

    // SQL Server's, whose file runs batch by batch, split at the lines that hold GO alone.
    public static EndToEndEngine SqlServer =>
        new(RepositoryRoot.Path("tests", "DatabaseTests", "SqlServer", "EndToEnd"), EndToEndFiles.SqlServerBatches);
}
