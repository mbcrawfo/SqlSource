using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The errors of describing, each at the name of a query.  None holds a connection's value: what a describer gave
/// goes through <see cref="Redaction" /> where it is reported.
/// </summary>
internal static class DescribeErrors
{
    private const string Help = "help";

    /// <summary><c>SQLSRC216</c>: this version has no describer for the dialect of the query's database.</summary>
    public static ToolDiagnostic NoDescriber(PlannedQuery query, SqlDialect dialect) =>
        ToolDiagnostic.At(
            ToolDiagnostics.NoDescriberForDialect,
            query.Query.NameLocation,
            SqlDialectName.Canonical(dialect)
        );

    /// <summary>
    /// <c>SQLSRC213</c>, with the option, the variable and where it is looked up, the names given for no database
    /// that are close to one, the positions of the values whose name is far from every database, which may be a
    /// secret, and the <c>.env</c> files of the database's projects that the run did not read.
    /// </summary>
    /// <param name="query">The first query of the database, where the error is reported.</param>
    /// <param name="connection">The database's connection, which has no value.</param>
    /// <param name="all">The connections of the run, with where they were looked up.</param>
    /// <param name="projectFolders">The folders of the projects of the database's queries.</param>
    /// <param name="readTextFile">The host's, which finds the files that were not read.</param>
    public static ToolDiagnostic NoConnection(
        PlannedQuery query,
        DatabaseConnection connection,
        Connections all,
        IEnumerable<string> projectFolders,
        Func<string, string?> readTextFile
    )
    {
        var environment = all.Environment;
        var lookedFor = environment.LookedFor;
        var help = new StringBuilder("pass --connection ")
            .Append(connection.Database)
            .Append("=<connection string>, or set ")
            .Append(connection.Variable)
            .Append(" in the environment");
        _ = lookedFor is not null
            ? help.Append(" or in ").Append(lookedFor)
            : help.Append(".  The run reads no .env file");
        if (!all.UnusedNames.IsEmpty)
        {
            _ = help.Append(".  --connection was given for ")
                .AppendJoin(", ", all.UnusedNames)
                .Append(all.UnusedNames.Length == 1 ? ", which is no database" : ", which are no databases")
                .Append(" of this run");
        }

        if (!all.UnusedPositions.IsEmpty)
        {
            _ = help.Append(".  ").Append(UnusedPositionsSentence(all.UnusedPositions));
        }

        var notRead = DotEnvSearch.NotRead(environment.UnitFolder, environment.FilePath, projectFolders, readTextFile);
        if (!notRead.IsEmpty)
        {
            // Under "none" the help has said already that the run reads no file.
            _ = help.Append(".  The run did not read ").Append(InWords(notRead));
            _ = lookedFor is not null
                ? help.Append(": it reads ")
                    .Append(lookedFor)
                    .Append(" alone, and --env-file, or SqlSourceEnvFile in a build, names another")
                : help.Append(": --env-file, or SqlSourceEnvFile in a build, names a file to read");
        }

        return ToolDiagnostic
            .At(ToolDiagnostics.DatabaseHasNoConnection, query.Query.NameLocation, connection.Database)
            .WithLines(new ContinuationLine(Help, help.ToString()));
    }

    // The value of --connection at position 3 names no database of this run
    // The values of --connection at positions 3 and 5 name no databases of this run
    // The values of --connection at positions 3, 5 and 7 name no databases of this run
    private static string UnusedPositionsSentence(ImmutableArray<int> positions)
    {
        var numbers = positions.Select(static position => position.ToString(CultureInfo.InvariantCulture)).ToList();
        return numbers.Count == 1
            ? $"The value of --connection at position {numbers[0]} names no database of this run"
            : $"The values of --connection at positions {InWords(numbers)} name no databases of this run";
    }

    // a; a and b; a, b and c
    private static string InWords(IReadOnlyList<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    /// <summary><c>SQLSRC214</c>: the database's variable is another selected database's too.</summary>
    public static ToolDiagnostic SharedVariable(PlannedQuery query, DatabaseConnection connection, string other) =>
        ToolDiagnostic.At(
            ToolDiagnostics.DatabasesShareConnectionVariable,
            query.Query.NameLocation,
            connection.Database,
            other,
            connection.Variable
        );

    /// <summary>
    /// <c>SQLSRC215</c>, in the place of <c>SQLSRC213</c>, with the variable of each database that can be described.
    /// </summary>
    public static ToolDiagnostic UnnamedNotUsed(PlannedQuery query, Connections all)
    {
        var variables = all.Describable.Select(name => $"{ConnectionVariables.For(name)} for '{name}'");
        return ToolDiagnostic
            .At(
                ToolDiagnostics.UnnamedConnectionNotUsed,
                query.Query.NameLocation,
                all.Describable.Length.ToString(CultureInfo.InvariantCulture),
                string.Join(", ", all.Describable)
            )
            .WithLines(
                new ContinuationLine(
                    Help,
                    $"set {string.Join(" and ", variables)}, or pass --connection <name>=<connection string> for each"
                )
            );
    }

    /// <summary>
    /// A describer's failure, in the format the epic's design gives it
    /// (<c>docs/superpowers/specs/2026-10-07-query-generation-epic-design.md</c>, "Error messages"): <c>query:</c>,
    /// <c>step:</c>, a <c>server:</c> line for each line of the server, and <c>help:</c>.  A part the tool does not
    /// have is left out.
    /// </summary>
    /// <param name="failure">What the describer returned.</param>
    /// <param name="query">The query it is reported at.</param>
    /// <param name="database">The query's database.</param>
    /// <param name="server">The session's server, or null when no session opened.</param>
    /// <param name="online">
    /// What <c>--online</c> said.  The help of a database that cannot be opened then also says how to build without
    /// it, since a build with a connection depends on the database being there.
    /// </param>
    public static ToolDiagnostic Failure(
        DescribeFailure failure,
        PlannedQuery query,
        PlannedDatabase database,
        ServerInfo? server,
        OnlineMode online
    )
    {
        var lines = new List<ContinuationLine> { new("query", QueryLine(query, database, server)) };
        if (failure.Step is { } step)
        {
            lines.Add(new ContinuationLine("step", DescribeStepName.Of(step)));
        }

        lines.AddRange(failure.ServerLines.Select(static line => new ContinuationLine("server", line)));
        if (failure.Help is { } help)
        {
            lines.Add(new ContinuationLine(Help, online == OnlineMode.Off ? help : OnlineHelp(failure, help)));
        }

        return ToolDiagnostic
            .At(failure.Descriptor, query.Query.NameLocation, [.. failure.Arguments])
            .WithLines([.. lines]);
    }

    // The error is all a build's user sees, so the way out that a build offers is added to the describer's help.
    // A replayed run rebuilds its descriptors, so they are known by their id.
    private static string OnlineHelp(DescribeFailure failure, string help) =>
        failure.Descriptor.Id == ToolDiagnostics.DatabaseCannotBeOpened.Id
            ? help + ".  To build without the database, remove its connection or set SqlSourceOnline to false"
            : help;

    // <name>, database <database>, <engine> <server version>, <driver> <version>
    private static string QueryLine(PlannedQuery query, PlannedDatabase database, ServerInfo? server)
    {
        var line = new StringBuilder(query.Query.Name)
            .Append(", database ")
            .Append(database.Name)
            .Append(", ")
            .Append(SqlDialectName.Canonical(database.Dialect));
        if (server is null)
        {
            return line.ToString();
        }

        _ = line.Append(' ').Append(server.Version);
        if (server.Driver is { } driver)
        {
            _ = line.Append(", ").Append(driver);
            if (server.DriverVersion is { } version)
            {
                _ = line.Append(' ').Append(version);
            }
        }

        return line.ToString();
    }
}
