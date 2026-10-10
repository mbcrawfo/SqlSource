using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
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
    }

    /// <summary>A file that a type claims.</summary>
    public static void File(RunTrace trace, PlannedFile file) =>
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

    /// <summary>A selected database: where its connection came from, never what it is.</summary>
    public static void Database(
        RunTrace trace,
        PlannedDatabase database,
        DatabaseConnection connection,
        bool hasDescriber
    ) =>
        trace.Log.Write(
            "database",
            new("name", database.Name),
            new("engine", SqlDialectName.Canonical(database.Dialect)),
            new("connectionSource", connection.Source ?? "none"),
            new("describer", hasDescriber)
        );

    /// <summary>A session that opened.  Every member but the name is the describer's text.</summary>
    public static void Opened(RunTrace trace, PlannedDatabase database, ServerInfo server) =>
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

    /// <summary>A session that did not open.</summary>
    /// <param name="trace">The run's, without the connection's value.</param>
    /// <param name="database">The database.</param>
    /// <param name="error">The id of the error that was reported.</param>
    public static void NotOpened(RunTrace trace, PlannedDatabase database, string error) =>
        trace.Log.Write("database.open", new("name", database.Name), new("error", error));

    /// <summary>What the run decided for a query that needs an entry.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="query">The query, in the state it ended in.</param>
    /// <param name="durationMs">How long its describe took, for one that was described.</param>
    /// <param name="error">The id of the error, for one that failed where a database was asked.</param>
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
    }

    /// <summary>What the run did with a sidecar, or under <c>--check</c> what it found.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the sidecar.</param>
    /// <param name="action">
    /// <c>written</c>, <c>unchanged</c>, <c>deleted</c>, <c>held-back</c> or <c>failed</c>; <c>same</c> or
    /// <c>differs</c> under <c>--check</c>.
    /// </param>
    public static void Sidecar(RunTrace trace, string path, string action) =>
        trace.Log.Write("sidecar", new("path", path), new("action", action));

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

    private static string StateOf(PlannedFileState state) =>
        state switch
        {
            PlannedFileState.Ready => "ready",
            PlannedFileState.HasParseErrors => "has-parse-errors",
            PlannedFileState.NotDescribable => "not-describable",
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };

    // A query that is still to be described has no event yet: it gets one when its describe ends.
    private static string DecisionOf(QueryState state) =>
        state switch
        {
            QueryState.Described => "described",
            QueryState.Skipped => "skipped",
            QueryState.LeftOut => "left-out",
            QueryState.NotNeeded => "failed",
            QueryState.Failed => "failed",
            QueryState.ToDescribe => "failed",
            _ => throw new InvalidOperationException($"Unknown state {state}."),
        };
}
