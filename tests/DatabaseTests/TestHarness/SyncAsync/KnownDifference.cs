using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// A difference between a method and its twin that is understood and accepted, with the rewrite that removes it.
internal sealed record KnownDifference(
    string Name,
    string Reason,
    Func<Scenario, Outcome, Outcome, (Outcome Async, Outcome Sync)> Rewrite
);

internal enum Agreement
{
    Same,
    Known,
    Different,
}

// How the two runs of a pair compare: the known differences that were needed to make them equal, and the fields that
// still differ.
internal sealed record PairVerdict(Agreement Agreement, IReadOnlyList<KnownDifference> Applied, string Report);

internal static class Verdicts
{
    // Same when equal; else applies each known difference in order, keeping those that changed the pair: Known when
    // equal after, Different otherwise.  Report names each field that differs, async then sync, after the rewrites.
    public static PairVerdict Of(
        Scenario scenario,
        Outcome asyncRun,
        Outcome syncRun,
        IReadOnlyList<KnownDifference> known
    )
    {
        if (Equal(asyncRun, syncRun))
        {
            return new PairVerdict(Agreement.Same, [], string.Empty);
        }

        var applied = new List<KnownDifference>();
        foreach (var difference in known)
        {
            var (rewrittenAsync, rewrittenSync) = difference.Rewrite(scenario, asyncRun, syncRun);
            if (Equal(rewrittenAsync, asyncRun) && Equal(rewrittenSync, syncRun))
            {
                continue;
            }

            applied.Add(difference);
            asyncRun = rewrittenAsync;
            syncRun = rewrittenSync;
        }

        var report = ReportOf(asyncRun, syncRun);
        return new PairVerdict(report.Length == 0 ? Agreement.Known : Agreement.Different, applied, report);
    }

    private static bool Equal(Outcome left, Outcome right) =>
        left.Result == right.Result
        && left.Failure == right.Failure
        && left.ConnectionState == right.ConnectionState
        && left.TransactionState == right.TransactionState
        && left.Tables.SequenceEqual(right.Tables);

    private static string ReportOf(Outcome asyncRun, Outcome syncRun)
    {
        var report = new StringBuilder();
        Line(report, "Result", asyncRun.Result, syncRun.Result);
        Line(report, "Failure", Describe(asyncRun.Failure), Describe(syncRun.Failure));
        Line(report, "ConnectionState", asyncRun.ConnectionState, syncRun.ConnectionState);
        Line(report, "TransactionState", asyncRun.TransactionState, syncRun.TransactionState);
        var tables = Math.Max(asyncRun.Tables.Count, syncRun.Tables.Count);
        for (var i = 0; i < tables; i++)
        {
            Line(
                report,
                $"Tables[{i}]",
                i < asyncRun.Tables.Count ? asyncRun.Tables[i] : "<missing>",
                i < syncRun.Tables.Count ? syncRun.Tables[i] : "<missing>"
            );
        }

        return report.ToString();
    }

    private static void Line(StringBuilder report, string field, string? asyncRun, string? syncRun)
    {
        if (asyncRun != syncRun)
        {
            _ = report
                .Append(field)
                .Append(": async = ")
                .Append(asyncRun ?? "<none>")
                .Append("; sync = ")
                .Append(syncRun ?? "<none>")
                .Append('\n');
        }
    }

    private static string? Describe(Failure? failure)
    {
        if (failure is null)
        {
            return null;
        }

        var item = failure.ItemIndex?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var inner = failure.InnerType ?? "none";
        var code = failure.Code ?? "none";
        return $"{failure.Type} (inner {inner}, code {code}, item {item}): {failure.Message}";
    }
}
