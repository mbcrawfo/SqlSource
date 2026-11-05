using System;
using System.Collections.Generic;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The describer a bundle is replayed with: the host's for its dialect, or, for a bundle with
/// <c>explain-settings.txt</c>, PostgreSQL's made with those settings of the planner.
/// </summary>
/// <remarks>
/// The registry holds PostgreSQL's describer with no settings, which a capture recorded with some does not answer, so
/// this is the one place that makes a describer with a folder's settings.  Each line is checked against
/// <see cref="PlannerSettings" />' closed list before the describer is made, so a line that is none is
/// <c>SQLSRC288</c> and never the describer's exception.
/// </remarks>
internal static class BundleDescribers
{
    /// <summary>
    /// The describer, or the error at the bundle: <c>SQLSRC291</c> for a dialect with no describer, and
    /// <c>SQLSRC288</c> for settings that cannot be given to it.
    /// </summary>
    /// <param name="dialect">The dialect of the bundle's query, as <c>BundleReplay.Plan</c> gives it.</param>
    /// <param name="registry">The host's describers.</param>
    /// <param name="explainSettings">The lines of the bundle's <c>explain-settings.txt</c>, or none.</param>
    /// <param name="engine">The engine the bundle names, which <c>SQLSRC291</c> quotes.</param>
    /// <param name="path">The bundle's full path, where an error is.</param>
    public static Outcome<IQueryDescriber, ToolDiagnostic> For(
        SqlDialect dialect,
        DescriberRegistry registry,
        IReadOnlyList<string> explainSettings,
        string engine,
        string path
    )
    {
        if (!SqlDescribableDialects.Contains(dialect) || registry.Find(dialect) is not { } describer)
        {
            return Outcome<IQueryDescriber, ToolDiagnostic>.Failed(BundleErrors.NoDescriber(path, engine));
        }

        if (explainSettings.Count == 0)
        {
            return Outcome<IQueryDescriber, ToolDiagnostic>.Of(describer);
        }

        var describes = SqlDescribableDialects.EngineOf(dialect);
        return describes switch
        {
            DatabaseEngine.PostgreSql => WithSettings(explainSettings, path),
            DatabaseEngine.SqlServer => Failed(path, $"a SQL Server bundle has no {BundleFiles.ExplainSettings}"),
            _ => throw UnexpectedEnumValueException.Of(describes),
        };
    }

    // PostgreSQL's describer with the settings, once each line is one of the list.
    private static Outcome<IQueryDescriber, ToolDiagnostic> WithSettings(IReadOnlyList<string> settings, string path)
    {
        foreach (var setting in settings)
        {
            try
            {
                _ = PlannerSettings.Checked([setting]);
            }
            catch (ArgumentException)
            {
                return Failed(path, $"the line '{setting}' of {BundleFiles.ExplainSettings} is no planner setting");
            }
        }

        return Outcome<IQueryDescriber, ToolDiagnostic>.Of(new PostgresDescriber(settings));
    }

    private static Outcome<IQueryDescriber, ToolDiagnostic> Failed(string path, string reason) =>
        Outcome<IQueryDescriber, ToolDiagnostic>.Failed(BundleErrors.CannotBeRead(path, reason));
}
