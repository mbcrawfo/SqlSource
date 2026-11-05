using System.Globalization;
using SqlSource.Diagnostics;
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
    /// <param name="member">The member that holds the version: <c>bundleVersion</c> or <c>captureVersion</c>.</param>
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

    /// <summary><c>SQLSRC291</c>: this tool has no describer of the bundle's engine.</summary>
    /// <param name="path">The bundle's full path.</param>
    /// <param name="engine">The engine the bundle names.</param>
    public static ToolDiagnostic NoDescriber(string path, string engine) =>
        ToolDiagnostic.AtFile(ToolDiagnostics.NoDescriberForBundle, path, BundleText.Of(engine));
}
