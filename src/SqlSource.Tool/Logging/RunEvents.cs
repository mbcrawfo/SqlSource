using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The events of a run, each built in one place, so that an event has one set of members wherever it is written.
/// </summary>
/// <remarks>
/// A member is added and never renamed: people read the log, and so does phase 10's bundle.  None of these is
/// handed a connection's value.  The ones of a database after its connection is found are written through
/// <see cref="RunTrace.Without" />, since they hold what a describer gave.
/// </remarks>
internal static class RunEvents
{
    private static readonly (string Logged, string Said) SidecarFailed = ("failed", "could not be changed");

    /// <summary>A project of the unit.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the project file.</param>
    /// <param name="evaluation">What MSBuild said of it, or null for one that <c>--project</c> leaves out.</param>
    public static void Project(RunTrace trace, string path, ProjectEvaluation? evaluation)
    {
        var members = new List<LogMember> { new("path", path), new("inRun", evaluation is not null) };
        if (evaluation is not null)
        {
            members.Add(new("usesSqlSource", UsesSqlSource(evaluation.State)));
        }

        if (evaluation?.Manifest is { } manifest)
        {
            members.Add(new("targetFramework", manifest.TargetFramework));
            members.Add(new("sqlFiles", manifest.Files.Length));
            members.Add(new("compileFiles", manifest.CompileFiles.Length));
        }

        trace.Log.Write("project", [.. members]);
        trace.Say(
            evaluation switch
            {
                null => $"project '{path}' is left out: --project does not name it",
                { Manifest: { } read } => string.Create(
                    CultureInfo.InvariantCulture,
                    $"project '{path}': {read.TargetFramework}, {read.Files.Length} .sql files"
                ),
                { State: ProjectState.DoesNotUseSqlSource } =>
                    $"project '{path}' is left out: it does not use SqlSource",
                _ => $"project '{path}' could not be read",
            }
        );
    }

    /// <summary>A file that a type claims.</summary>
    public static void File(RunTrace trace, PlannedFile file)
    {
        trace.Log.Write(
            "file",
            new("path", file.Path),
            new("project", file.ProjectPath),
            new("dialect", SqlDialectName.Canonical(file.Dialect)),
            new("state", StateOf(file.State)),
            new(
                "claims",
                LogValue.ListOf(
                    file.Claims.Select(static claim =>
                        LogValue.MapOf(
                            new("project", claim.ProjectPath),
                            new("source", claim.SourcePath),
                            new("line", claim.Position.Line + 1)
                        )
                    )
                )
            ),
            new("queries", file.Queries.Count)
        );
        var claims = file.Claims.Select(static claim =>
            string.Create(CultureInfo.InvariantCulture, $"{claim.SourcePath}({claim.Position.Line + 1})")
        );
        trace.Say(
            $"file '{file.Path}': {SqlDialectName.Canonical(file.Dialect)}, {StateInWords(file.State)}, "
                + $"claimed by {string.Join(", ", claims)}"
        );
    }

    /// <summary>A selected database: where its connection came from, never what it is.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="database">The database.</param>
    /// <param name="connectionSource">Where the connection came from, or null when there is none.</param>
    /// <param name="hasDescriber">Whether a describer is registered for the database's engine.</param>
    public static void Database(RunTrace trace, PlannedDatabase database, string? connectionSource, bool hasDescriber)
    {
        trace.Log.Write(
            "database",
            new("name", database.Name),
            new("engine", SqlDialectName.Canonical(database.Dialect)),
            new("connectionSource", connectionSource ?? "none"),
            new("describer", hasDescriber)
        );
        trace.Say(
            $"database '{database.Name}' ({SqlDialectName.Canonical(database.Dialect)}): "
                + (connectionSource is { } source ? $"connection from {source}" : "no connection")
        );
    }

    /// <summary>A session that opened.  Every member but the name is the describer's text.</summary>
    public static void Opened(RunTrace trace, PlannedDatabase database, ServerInfo server)
    {
        trace.Log.Write(
            "database.open",
            new("name", database.Name),
            new("serverVersion", server.Version),
            new("driver", server.Driver),
            new("driverVersion", server.DriverVersion),
            new(
                "settings",
                LogValue.MapOf([
                    .. server.Settings.Select(static setting => new LogMember(setting.Name, setting.Value)),
                ])
            )
        );
        trace.Say($"database '{database.Name}': server {server.Version}");
    }

    /// <summary>A session that did not open.</summary>
    /// <param name="trace">The run's, without the connection's value.</param>
    /// <param name="database">The database.</param>
    /// <param name="error">The id of the error that was reported.</param>
    public static void NotOpened(RunTrace trace, PlannedDatabase database, string error)
    {
        trace.Log.Write("database.open", new("name", database.Name), new("error", error));
        trace.Say($"database '{database.Name}': no session");
    }

    /// <summary>What the run decided for a query that needs an entry.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="query">The query, in the state it ended in.</param>
    /// <param name="durationMs">How long its describe took, for one that was described.</param>
    /// <param name="error">
    /// The id of the error, for one that failed when its database was run: no describer, no connection, no session,
    /// or its describe.
    /// </param>
    public static void Query(RunTrace trace, QueryWork query, long? durationMs = null, string? error = null)
    {
        var planned = query.Planned;
        var members = new List<LogMember>
        {
            new("file", planned.Query.NameLocation.Path),
            new("name", planned.Query.Name),
            new("database", planned.Database),
            new("hash", planned.Hash),
            new("selected", planned.IsSelected),
            new("decision", DecisionOf(query.State)),
        };
        if (durationMs is { } duration)
        {
            members.Add(new("durationMs", duration));
        }

        if (error is not null)
        {
            members.Add(new("error", error));
        }

        trace.Log.Write("query", [.. members]);
        string decision;
        if (!planned.IsSelected)
        {
            decision = "not selected";
        }
        else if (query.State == QueryState.Described)
        {
            decision = string.Create(CultureInfo.InvariantCulture, $"described in {durationMs ?? 0} ms");
        }
        else
        {
            decision = query.State == QueryState.Skipped ? "skipped, its entry is current" : "failed";
        }

        trace.Say($"query '{planned.Query.Name}' in '{planned.Query.NameLocation.Path}': {decision}");
    }

    /// <summary>What the run did with a sidecar.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the sidecar.</param>
    /// <param name="action">
    /// What the run set out to do: <c>written</c>, <c>deleted</c>, <c>held-back</c> and <c>unchanged</c> are the
    /// actions of that name, and <see cref="FileAction.Unreadable" /> is <c>failed</c>.  <see cref="FileAction.None" />
    /// has no event.
    /// </param>
    /// <param name="failed">
    /// Whether the write or the delete the action names failed: the event is then <c>failed</c>, since
    /// <c>written</c> or <c>deleted</c> would say what did not happen.
    /// </param>
    public static void Sidecar(RunTrace trace, string path, FileAction action, bool failed)
    {
        var (logged, said) = failed ? SidecarFailed : SidecarActionOf(action);
        SidecarEvent(trace, path, logged, said);
    }

    /// <summary>What <c>--check</c> found of a sidecar: <c>differs</c> or <c>same</c>.</summary>
    /// <remarks>
    /// There is no event for a file one of whose queries could not be checked: it is neither the same nor different.
    /// </remarks>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the sidecar.</param>
    /// <param name="differs">Whether the file differs from what the run would write.</param>
    public static void SidecarChecked(RunTrace trace, string path, bool differs)
    {
        var (found, said) = differs ? ("differs", "different") : ("same", "the same");
        SidecarEvent(trace, path, found, said);
    }

    /// <summary>
    /// The unit of the run and how it was found.  The log has it in <c>arguments</c> and its projects.
    /// </summary>
    /// <param name="trace">The run's.</param>
    /// <param name="unit">The solution or project the run is on.</param>
    /// <param name="wasGiven">Whether the command line gave it, and not the working directory.</param>
    public static void Unit(RunTrace trace, RunUnit unit, bool wasGiven) =>
        trace.Say(
            $"unit '{unit.Path}', a {(unit.Kind == RunUnitKind.Solution ? "solution" : "project")}, "
                + (wasGiven ? "from the path given" : "found in the working directory")
        );

    /// <summary>How many <c>.sql</c> files of a project no type claims: the tool reads none of them.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="projectPath">The full path of the project file.</param>
    /// <param name="files">How many files of the project no type claims.</param>
    public static void Unclaimed(RunTrace trace, string projectPath, int files)
    {
        if (files > 0)
        {
            trace.Say(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"project '{projectPath}': {files} .sql files that no type claims"
                )
            );
        }
    }

    // Nothing says whether a project that was not restored, or that MSBuild failed on, uses SqlSource.
    private static LogValue UsesSqlSource(ProjectState state) =>
        state switch
        {
            ProjectState.UsesSqlSource => true,
            ProjectState.DoesNotUseSqlSource => false,
            ProjectState.NotRestored => LogValue.Null,
            ProjectState.Failed => LogValue.Null,
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };

    private static void SidecarEvent(RunTrace trace, string path, string action, string said)
    {
        trace.Log.Write("sidecar", new("path", path), new("action", action));
        trace.Say($"sidecar '{path}': {said}");
    }

    // The log's word for the action and the word of the --verbose line.
    private static (string Logged, string Said) SidecarActionOf(FileAction action) =>
        action switch
        {
            FileAction.Write => ("written", "written"),
            FileAction.Delete => ("deleted", "deleted"),
            FileAction.HeldBack => ("held-back", "left as it was"),
            FileAction.Unchanged => ("unchanged", "unchanged"),
            // The action is itself the failure: a sidecar that could not be read was neither written nor deleted.
            FileAction.Unreadable => SidecarFailed,
            FileAction.None => throw new InvalidOperationException("A file with no action has no event."),
            _ => throw new InvalidOperationException($"Unknown action {action}."),
        };

    private static string StateOf(PlannedFileState state) =>
        state switch
        {
            PlannedFileState.Ready => "ready",
            PlannedFileState.HasParseErrors => "has-parse-errors",
            PlannedFileState.NotDescribable => "not-describable",
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };

    private static string StateInWords(PlannedFileState state) =>
        state switch
        {
            PlannedFileState.Ready => "ready",
            PlannedFileState.HasParseErrors => "has parse errors",
            PlannedFileState.NotDescribable => "cannot be described",
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };

    // A query that is still to be described has no event yet: it gets one when its describe ends.
    private static string DecisionOf(QueryState state) =>
        state switch
        {
            QueryState.Described => "described",
            QueryState.Skipped => "skipped",
            QueryState.LeftOut => "left-out",
            QueryState.Failed => "failed",
            QueryState.NotNeeded or QueryState.ToDescribe => throw new InvalidOperationException(
                $"A query in the state {state} has no event."
            ),
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };
}
