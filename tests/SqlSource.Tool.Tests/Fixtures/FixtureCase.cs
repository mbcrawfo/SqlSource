using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Tests.Fixtures;

// One case of a describer's fixtures: a folder with a query, the capture of describing it and the sidecar
// expected of it.  An optional issue.md beside them says where the case came from.  An optional
// explain-settings.txt holds the settings of the planner the case was recorded with.  Each text is read when it is
// asked for, so a test that writes a file sees what it wrote.
internal sealed record FixtureCase(string Engine, string Name, string Folder)
{
    // A complete SqlSource file with exactly one query, parsed as the tool parses a project's file.
    public const string QueryFile = "query.sql";

    // The calls of describing the query, as CaptureJson writes them.
    public const string CaptureFile = "capture.json";

    // A sidecar with the one entry, as SidecarWriter writes it, with the toolVersion "fixture".
    public const string ExpectedFile = "expected.json";

    // The settings of the planner that the case's "explain" carries, one name=value on a line: a case whose name
    // ends in a join method turns the other two off.  Most cases have no such file.
    public const string ExplainSettingsFile = "explain-settings.txt";

    public string QueryPath => Path.Combine(Folder, QueryFile);

    public string Query => File.ReadAllText(QueryPath);

    public string Capture => File.ReadAllText(Path.Combine(Folder, CaptureFile));

    public string Expected => File.ReadAllText(Path.Combine(Folder, ExpectedFile));

    public IReadOnlyList<string> ExplainSettings =>
        File.Exists(Path.Combine(Folder, ExplainSettingsFile))
            ?
            [
                .. File.ReadAllLines(Path.Combine(Folder, ExplainSettingsFile))
                    .Select(static line => line.Trim())
                    .Where(static line => line.Length > 0),
            ]
            : [];

    // How a failure names the case.
    public override string ToString() => $"{Engine}/{Name}";
}
