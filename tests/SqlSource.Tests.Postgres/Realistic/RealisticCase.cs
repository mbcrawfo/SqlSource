using System;
using System.Collections.Generic;
using System.IO;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tests.Postgres.Realistic;

// Whether a column of a case's result can be NULL, as a person reasoned it from the SQL and schema.sql and never
// from the describer's output.  Why says how a column that can be NULL comes to be; Exempt says why the seed cannot
// show it.
internal sealed record TruthColumn(string Name, bool Nullable, string? Why, string? Exempt);

// Why the generator refuses the models of a case that the describer accepts: the id of the diagnostic, and why the
// case keeps the SQL that it refuses.
internal sealed record ModelRefusal(string Id, string Why);

// The first major whose description a case's sidecar holds, for a case that an earlier major describes otherwise,
// and why.
internal sealed record CheckedFrom(int Major, string Why);

// One realistic case: a folder with a query, its truth and its sample values, and the sidecar of its models.  A case
// refused by the describer has the failure's id and no columns; one whose models the generator refuses says so by
// Models, and one whose sidecar an earlier major describes otherwise says from which major it is checked by
// CheckedFrom.  One with a parameter that Dapper cannot send says why by Dapper, which leaves it out of the run
// through Dapper alone.  A sample set holds a SQL literal for each parameter, by its name without @, compared
// ignoring case.
internal sealed record RealisticCase(
    string Name,
    string Folder,
    int? MinimumMajor,
    string? Refusal,
    IReadOnlyList<TruthColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Samples,
    ModelRefusal? Models = null,
    CheckedFrom? CheckedFrom = null,
    string? Dapper = null
)
{
    // A complete SqlSource file with exactly one query, read as FixtureReplay reads a fixture's.
    public const string QueryFile = FixtureCase.QueryFile;

    // The truth and the sample values, written by hand.  No script writes it.
    public const string CaseFile = "case.json";

    // The entry of the query's models, written by tools/describe-end-to-end.sh beside the query it describes.
    public const string SidecarFile = "query.sql.json";

    public string SidecarPath => Path.Combine(Folder, SidecarFile);

    // Whether the case is a Models query of the project, claimed by its class in Realistic/Generation: neither the
    // describer nor the generator refuses it.
    public bool HasModels => Refusal is null && Models is null;

    // The files of the case's folder: its query, its truth, and its sidecar when it has models.
    public IReadOnlyList<string> Files => [QueryFile, CaseFile, .. HasModels ? [SidecarFile] : Array.Empty<string>()];

    // Whether the server of that major runs the case: a case may need a later one, MERGE ... RETURNING for one.
    public bool RunsOn(int major) => MinimumMajor is not { } minimum || minimum <= major;

    // Whether "describe --check" on the server of that major compares the case's sidecar, and RealisticDescriptionTests
    // its committed entry: the case has one, the server runs it, and the server describes it as the sidecar holds.
    public bool IsCheckedOn(int major) =>
        HasModels && RunsOn(major) && (CheckedFrom is not { } from || from.Major <= major);

    // Whether the run through Dapper reads the case's rows on the server of that major: the case has models, the
    // server runs it, and Dapper can send its parameters.
    public bool IsReadThroughDapperOn(int major) => HasModels && Dapper is null && RunsOn(major);

    // The case as FixtureReplay plans one.
    public FixtureCase AsFixture => new(SidecarValues.Engine.Postgres, Name, Folder);

    // How a failure names a case, by the name of its folder: the one spelling, which the reader uses before it has
    // a case.
    public static string Named(string name) => $"realistic/{name}";

    public override string ToString() => Named(Name);
}
