using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SqlSource.Tool.Tests.Fixtures;

namespace SqlSource.Tests.Postgres.Realistic;

// Whether a column of a case's result can be NULL, as a person reasoned it from the SQL and schema.sql and never
// from the describer's output.  Why says how a column that can be NULL comes to be; Exempt says why the seed cannot
// show it.
internal sealed record TruthColumn(string Name, bool Nullable, string? Why, string? Exempt);

// One realistic case: a folder with a query, its truth and its sample values, and the description recorded of it on
// each major version of the server.  A case refused by the describer has the failure's id and no columns.  A sample
// set holds a SQL literal for each parameter, by its name without @, compared ignoring case.
internal sealed record RealisticCase(
    string Name,
    string Folder,
    int? MinimumMajor,
    string? Refusal,
    IReadOnlyList<TruthColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Samples
)
{
    // A complete SqlSource file with exactly one query, read as FixtureReplay reads a fixture's.
    public const string QueryFile = FixtureCase.QueryFile;

    // The truth and the sample values, written by hand.  Recording never writes it.
    public const string CaseFile = "case.json";

    // The description as RealisticRun renders it, recorded on the server of that major version.
    public string ExpectedPath(int major) =>
        Path.Combine(Folder, string.Create(CultureInfo.InvariantCulture, $"expected-pg{major}.txt"));

    // The case as FixtureReplay plans one.
    public FixtureCase AsFixture => new("postgres", Name, Folder);

    // How a failure names a case, by the name of its folder: the one spelling, which the reader uses before it has
    // a case.
    public static string Named(string name) => $"realistic/{name}";

    public override string ToString() => Named(Name);
}
