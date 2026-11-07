using System;
using System.Globalization;
using SqlSource.Diagnostics;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The errors of reading and replaying a bundle, each at the bundle's path, so that the command and the tests share
/// their texts.  A text the tool did not write, a reason of the system or a name from the bundle, is written through
/// <see cref="BundleText" />, so that a control character of the bundle is printed as its escape, and a line break
/// too, which keeps the text on its line.
/// </summary>
internal static class BundleErrors
{
    /// <summary><c>SQLSRC288</c>: the bundle cannot be read, and why.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="reason">
    /// Why, in words that start in lower case, such as <c>there is no query.sql at the root of the zip</c>.
    /// </param>
    public static ToolDiagnostic CannotBeRead(string path, string reason) =>
        ToolDiagnostic.AtFile(ToolDiagnostics.BundleCannotBeRead, path, BundleText.Of(reason));

    /// <summary><c>SQLSRC289</c>: a version of the bundle's form is newer than this tool reads.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="member">
    /// The member that holds the version: <c>bundleVersion</c>, <c>captureVersion</c>, or <c>formatVersion</c>, the
    /// sidecar format of the expected answer.
    /// </param>
    /// <param name="found">The version the bundle has.</param>
    /// <param name="read">The version this tool reads.</param>
    public static ToolDiagnostic OfNewerTool(string path, string member, int found, int read) =>
        ToolDiagnostic.AtFile(
            ToolDiagnostics.BundleOfNewerTool,
            path,
            member,
            found.ToString(CultureInfo.InvariantCulture),
            read.ToString(CultureInfo.InvariantCulture)
        );

    /// <summary><c>SQLSRC290</c>: the query asks what the bundle holds no answer to.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="method">The call that has no answer, such as <c>columns</c>.</param>
    /// <param name="madeBy">What made the bundle, as <see cref="MadeBy" /> says it.</param>
    public static ToolDiagnostic CallMissing(string path, string method, string madeBy) =>
        ToolDiagnostic.AtFile(
            ToolDiagnostics.BundleCallMissing,
            path,
            BundleText.Of(method),
            BundleText.Of(madeBy),
            OneLine.Of(Cli.Version)
        );

    /// <summary>
    /// What made a bundle, as <c>SQLSRC290</c> names it: <c>sqlsource</c> and the version of its <c>bundle.json</c>, or
    /// <c>another version of sqlsource</c> for a bundle without one.
    /// </summary>
    /// <param name="toolVersion">
    /// The <c>toolVersion</c> of the bundle's <c>bundle.json</c>, or null when it has none.
    /// </param>
    public static string MadeBy(string? toolVersion) =>
        toolVersion is null ? "another version of sqlsource" : "sqlsource " + toolVersion;

    /// <summary>
    /// <c>SQLSRC293</c> or <c>SQLSRC294</c>: the renaming refused the bundle, which is not written.  A refusal of the
    /// replay is <c>SQLSRC294</c>, at its place; any other is <c>SQLSRC293</c>, with its reason, its place and the text
    /// found, which is the user's own.  Each says how to send a raw bundle instead.
    /// </summary>
    /// <param name="path">The full path the bundle would have been written at.</param>
    /// <param name="refusal">The renaming's refusal.</param>
    public static ToolDiagnostic CannotBeMadeSafe(string path, RenamingRefusal refusal)
    {
        ToolDiagnostic error;
        if (refusal.Step == RefusalStep.Replay)
        {
            error = ToolDiagnostic.AtFile(
                ToolDiagnostics.RenamedBundleDoesNotReproduce,
                path,
                BundleText.Of(refusal.Place)
            );
        }
        else
        {
            var found = refusal.Token is { } token ? BundleText.Of(token) : "(none)";
            error = ToolDiagnostic.AtFile(
                ToolDiagnostics.BundleCannotBeMadeSafe,
                path,
                ReasonOf(refusal.Step),
                BundleText.Of(refusal.Place),
                found
            );
        }

        return error.WithLines(
            new ContinuationLine(
                "help",
                "make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw "
                    + "bundle is ready, without attaching it: a maintainer will say how to send it"
            )
        );
    }

    /// <summary>
    /// <c>SQLSRC296</c>: <c>--scrub</c> was given a raw bundle whose answer this version does not reproduce from its
    /// capture, which is not renamed.
    /// </summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="difference">The first difference of the replay, as <c>replay</c> prints it.</param>
    /// <param name="madeBy">What made the bundle, as <see cref="MadeBy" /> says it.</param>
    public static ToolDiagnostic NotReproduced(string path, string difference, string madeBy) =>
        ToolDiagnostic.AtFile(
            ToolDiagnostics.RawBundleNotReproduced,
            path,
            BundleText.Of(difference),
            BundleText.Of(madeBy),
            OneLine.Of(Cli.Version)
        );

    /// <summary><c>SQLSRC295</c>: <c>--scrub</c> was given a bundle that is not raw.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="isRenamed">
    /// Whether its <c>bundle.json</c> says it is renamed; otherwise it has none that says it is raw.
    /// </param>
    public static ToolDiagnostic NotRaw(string path, bool isRenamed) =>
        ToolDiagnostic.AtFile(
            ToolDiagnostics.BundleIsNotRaw,
            path,
            BundleText.Of(path),
            isRenamed ? "it is renamed already" : $"it has no {BundleFiles.Info} that says it is raw"
        );

    // Why a step of the renaming refused, as SQLSRC293 says it.
    private static string ReasonOf(RefusalStep step) =>
        step switch
        {
            RefusalStep.Token => "a word is not one of the new names",
            RefusalStep.RequestSql => "the query the database was asked is not the renamed query",
            RefusalStep.PlanString => "a text the database gave does not read the same once renamed",
            RefusalStep.Replay => throw new ArgumentException("A refusal of the replay is SQLSRC294.", nameof(step)),
            _ => throw UnexpectedEnumValueException.Of(step),
        };

    /// <summary><c>SQLSRC291</c>: this tool has no describer of the bundle's engine.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="engine">The engine the bundle names.</param>
    public static ToolDiagnostic NoDescriber(string path, string engine) =>
        ToolDiagnostic.AtFile(ToolDiagnostics.NoDescriberForBundle, path, BundleText.Of(engine));
}
