# Query generation, phase 3.1: capture and replay - design

Date: 2026-10-12

Sub-phase 3.1 of the [query generation epic](2026-10-07-query-generation-epic-design.md), and the first of phase 3.  It delivers the two shapes of the exchange that sub-phase 2.5 left out: one that records every call a describer makes into a capture, and one that answers from a capture with no database.  With them, a description made once against a container is a test that runs on every machine, and a user's bug bundle is a regression test by copying one file.

It builds on nothing of phase 3, and touches no engine: the first describer arrives in sub-phase 3.2, which builds on this one.

## Goal

```csharp
// Against a database, once:
var capture = new DescribeCapture("postgres");
var exchange = new CapturingExchange(LiveExchange.Instance, capture);
... describe the query through a describer that is given the exchange ...
File.WriteAllText("capture.json", CaptureJson.Write(capture));

// On every machine, with no database:
var replay = new ReplayingExchange(CaptureJson.Read(File.ReadAllText("capture.json")));
... describe the same query through the same describer, given this exchange: the same description ...
```

Success is:

- A describer written against `IDescribeExchange`, as `FakeDescriber` is, gives the same description through a replaying exchange as it gave through the capturing one, without a change to the describer.
- A capture is a readable JSON file: a reviewer can see each question and each answer.
- A describer that asks a question the capture does not hold fails loudly, so that a change to a pipeline is a visible fixture update and never a silent pass.
- Nothing of a connection ever reaches a capture.

## Decisions

From the epic, unchanged: the seam is one exchange that every engine's calls go through; three shapes, live, recording and replaying; replay answers from a capture keyed by the method and the request, and fails loudly on a call the capture lacks; a request and an answer are plain data; a call that can fail on the server returns the failure as its answer; the fixtures of a describer are `query.sql`, `capture.json`, `expected.json` and an optional `issue.md`; a `captureVersion`.

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| How a request and an answer are written | System.Text.Json, with one shared `JsonSerializerOptions` | The tool is `net8.0`, where the library is the runtime's.  The generator cannot take it, and the capture is the tool's alone. |
| The key of a call | The method's name and the request, serialized | The epic said a hash of the arguments.  The request itself is readable in a bundle and costs nothing more; a hash would hide what was asked. |
| A question asked twice | Its recorded answers are replayed in order; an exhausted key fails as a missing one does | Two requests that serialize the same are one question.  A describer may ask it twice and get two answers, so a replay follows the order of the capture; `prepare` is asked once for each statement, since the name is a counter. |
| Where the fixtures live | `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/<case>/`, as the epic says | `Fixtures/Projects/` is already there for the MSBuild fixtures; a folder beside it keeps the two kinds apart. |
| What `expected.json` holds | A sidecar with one entry, as `SidecarWriter` writes it, with `toolVersion` fixed to `fixture` | The whole entry is compared, provenance included, which is what the fixtures are for.  A fixed `toolVersion` keeps a version bump from touching every fixture. |
| The name of a statement a describer prepares | Deterministic within a session, a counter | A name that is part of a request must be the same when the capture is recorded and when it is replayed. |
| The test helper `RecordingExchange` | Removed; `CliRun.Exchange` becomes a `CapturingExchange` over `LiveExchange.Instance`, and a test that read `Methods` reads the methods of its capture's calls | Two things named "recording" that record different things would confuse. |
| What crosses the exchange | Plain records only, never an `Outcome`, a `DescribeFailure` or a `QueryDescription`; a describer maps between its records and the seam's types on its side of the exchange | `Outcome`, `DiagnosticDescriptor`, the abstract `SidecarType` and `EquatableArray` do not round-trip through System.Text.Json.  `FakeDescriber` is rewritten this way in this sub-phase, so that it proves the harness as a real describer would. |
| The connection value of a replayed `OpenRequest` | The constant `replay` | Nothing opens under replay, and the run's redaction and `EntrySecretCheck` want one fixed value to search for. |
| The clock | `OpenRequest` gains `Time`, a `TimeProvider`, which `DatabaseRuns` sets from `ToolHost.Time` | A describer's events carry durations, and `src/SqlSource.Tool/AGENTS.md` forbids `DateTime` and `Stopwatch`.  `FakeDescriber` and the harness give `TimeProvider.System` or a test clock. |
| Line endings of a capture | `\n`, made so by the writer after System.Text.Json has written, and `*.json` under the fixture folder marked `text eol=lf` in `.gitattributes` | On .NET 9 and later `Utf8JsonWriter` indents with the platform's line ending, and a Windows checkout would otherwise convert the file and break the byte-exact comparison. |

## Out of scope

- A describer for any engine.  Sub-phase 3.2.
- The directory-driven replay test over the fixture folders, and `tools/record-fixtures.sh`.  Sub-phase 3.2, with the first cases.
- Obfuscation of a capture, `sqlsource diagnose` and `sqlsource replay`.  Phase 10, which reuses `ReplayingExchange` as it is.

## The capture

In `src/SqlSource.Tool/Describing/Capture/`.

```csharp
internal sealed class DescribeCapture(string engine)
{
    public const int Version = 1;
    public string Engine { get; }
    public IReadOnlyList<CaptureCall> Calls { get; }
    public void Add(CaptureCall call);
}

internal sealed record CaptureCall(string Method, JsonElement Request, JsonElement Answer);
```

The file:

```json
{
  "captureVersion": 1,
  "engine": "postgres",
  "calls": [
    { "method": "open", "request": {}, "answer": { "serverVersion": "18.4", "driverVersion": "10.0.3.0", "settings": [ { "name": "search_path", "value": "\"$user\", public" } ] } },
    { "method": "prepare", "request": { "statement": "sqlsource_1", "sql": "SELECT id FROM users WHERE id = $1", "types": [ "unknown" ] }, "answer": { "parameterTypes": [ 23 ], "error": null } }
  ]
}
```

- `CaptureJson.Write(capture)` and `CaptureJson.Read(text)` read and write the file, indented, UTF-8, `\n` line endings whatever the platform, so that a capture under version control diffs line by line.  A `captureVersion` the reader does not know is an error that names the version.  A `JsonElement` read from a document is cloned before the document is disposed.
- `CaptureJson.Options` is the one `JsonSerializerOptions` of the tool for requests and answers: camel-case names, every member written, `null` included, no indentation inside a key.  A request's key is `JsonSerializer.Serialize(request, Options)`: two requests that serialize the same are one question.
- The calls are kept in the order they were made.

### What a request and an answer may hold

A rule for every describer, which the capture relies on and which the tests of sub-phase 3.2 hold the first describer to:

- A `record` whose members are strings, integers, booleans, nullable versions of those, lists of them, and records of the same kind.  No driver type, no `DateTime`, no `Exception`, no abstract base: System.Text.Json writes what it is given, and a reader of a bundle must be able to read it.
- Nothing of a connection: not the value, not a part of it, not the host or the user's name.  The request of the call that opens a session holds nothing at all; `FakeDescriber`'s, which passed the database's name, becomes empty.  The one exception is the driver's own message in the `error` of a call that failed, which may name the host or the user: `docs/tech-debt/TD-0034` records it, and sub-phase 3.2 says where it reaches.
- A call that can fail on the server has an answer with an `error` member that is null on success: the failure is data, and a replay reproduces it.
- The request of a call holds everything that decides the answer: a statement's name, the SQL as sent, a type list, a set of OIDs.

### The three exchanges

| Exchange | `AskAsync` |
|----|----|
| `LiveExchange` | Calls `live` and keeps nothing.  Sub-phase 2.5's, unchanged. |
| `CapturingExchange(IDescribeExchange inner, DescribeCapture capture)` | Calls `inner`, then adds a call with the request and the answer serialized.  It wraps the live one, and in a test any other. |
| `ReplayingExchange(DescribeCapture capture)` | Never calls `live`.  It finds the first call of the capture with the same method and the same serialized request that it has not yet given out, and returns its answer deserialized as `TAnswer`.  No such call is a `CaptureMissException` whose message names the method and holds the request's JSON. |

`CapturingExchange` is in `src/SqlSource.Tool`, not in the tests: `tools/record-fixtures.sh` and, in phase 10, `sqlsource diagnose` record with it.

- `CaptureMissException` derives from `InvalidOperationException`, as `DescriberFaultException` does and for the same reasons.  Under a run of the tool, phase 10's `replay`, it is a bug or a stale capture, and `SQLSRC200` reports it; under a test it fails the test with the question in the message.
- A replaying exchange that was not asked every call of its capture is not an error: a fixture may hold more than one case asks.  `ReplayingExchange.Unused` gives the calls that were not asked, for a test that wants to know.
- Neither exchange looks at `TRequest` or `TAnswer` beyond serializing them.  A describer's exchange records are its own.  `CallRecordTests` in the tool's tests holds every record that is passed to `AskAsync`, found by reflection in each engine's namespace, `SqlSource.Tool.Describing.Postgres` from sub-phase 3.2, and in `FakeDescriber`'s, by a `Request` or `Answer` suffix, to the rule above: public members of the allowed kinds only, and a round trip through `CaptureJson.Options` that is equal.  The seam's own records in `Describing/Describers/`, `OpenRequest` with its exchange and clock and `DescribeRequest` with its lexemes, never cross the exchange and are not capture records.

### The host

`ToolHost.Exchange` is already a function from a database's name to its exchange; the real host gives `LiveExchange.Instance`, and a test gives what it wants.  Nothing changes here.

## Fixtures

The layout, so that sub-phase 3.2 fills it:

```
tests/SqlSource.Tool.Tests/Fixtures/Describers/
    postgres/
        schema.sql                 The schema every case of the engine is recorded against
        <case>/
            query.sql              A complete SqlSource file with exactly one query
            capture.json           The calls of describing it, as CaptureJson writes them
            expected.json          A sidecar with the one entry, toolVersion "fixture"
            issue.md               Optional: the report the case came from
```

- `query.sql` is parsed as the tool parses a project's file, so its markers work: `-- dialect:`, `-- name:`, `-- param:`, `-- token:`, `-- database:`.  Its dialect is the engine's; its database, when none is given, is the dialect's name.
- `expected.json` is the entry the describer gave, built by `EntryBuilder` and written by `SidecarWriter` into a `Sidecar` of the current format version with `toolVersion` `fixture`.  The comparison is of the text, so provenance, `origin`, `identity` and `computed` are compared too.  `serverVersion` is the version the case was recorded against, so re-recording against another server changes it, which is right: the fixture says what server said it.
- The folder is copied to the test project's output as `Fixtures/Projects/` is.

### The harness

In `tests/SqlSource.Tool.Tests/Fixtures/`:

| Type | Does |
|----|----|
| `FixtureCase` | One case: its engine, its name, its folder, and the texts of its three files, read on demand |
| `FixtureCases.Of(engine)` | The cases under the engine's folder, in name order, found from the output folder |
| `FixtureReplay.DescribeAsync(FixtureCase, Func<FixtureCase, IQueryDescriber>)` | Parses `query.sql` through `SqlFileText` and the parser with the engine's dialect, builds the `DescribeRequest` with `SampleSql` and the parameter list, makes the describer for the case, opens a session with an `OpenRequest` whose connection is `replay`, a `ReplayingExchange` over the case's capture and a `NullRunLog`, describes, builds the entry with `EntryBuilder` for the database the file names, and returns the text `SidecarWriter` gives for it.  The factory takes the case so that sub-phase 3.5 can read a case's planner settings |

The theory that runs every case of every engine arrives in sub-phase 3.2 with the first describer and the first cases: a theory with no data fails, and this sub-phase has no engine.  This sub-phase proves the harness's parts in memory, through `FakeDescriber`.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.1 says In progress, and the phase 3 section holds the table of sub-phases.
2. `DescribeCapture`, `CaptureCall`, `CaptureJson`: the model and the file, with its version, and the `.gitattributes` line.
3. `FakeDescriber`'s records as plain data, `Time` on `OpenRequest`, and `CallRecordTests`.
4. `CapturingExchange`, and the test helper `RecordingExchange` replaced by it in `CliRun` and its tests.
5. `ReplayingExchange` and `CaptureMissException`.
6. `FixtureCase`, `FixtureCases`, `FixtureReplay`, and the fixture folder with its copy to the output.
7. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `CaptureJsonTests` | A capture with two calls round-trips through the text byte for byte.  The text is indented, holds no `\r`, and ends in `\n`.  A `captureVersion` of 2 is an error naming it.  A request with a nested record, a list, a null and an integer serializes with every member and camel-case names |
| `CallRecordTests` | Every request and answer record of the describers, by reflection: allowed member kinds only, and an equal round trip |
| `CapturingExchangeTests` | Each call of `FakeDescriber` over a run, `open` and `describe`, is in the capture in order with its request and its answer.  A `live` that throws adds no call and the exception passes through.  Two sessions give two `open` calls |
| `ReplayingExchangeTests` | A description made through a capturing exchange, replayed, is equal.  A call the capture lacks throws `CaptureMissException` naming the method and the request's JSON, and `live` was not called.  A question asked twice with two answers replays both in order; a third time throws.  The same request serialized from a new instance finds the call.  `Unused` lists the calls not asked |
| `FixtureReplayTests` | `FixtureCase` reads the three files; `FixtureCases.Of` for an engine with no folder is empty.  A case written by the test, `query.sql` with a `-- param:` and a token default, a capture recorded from `FakeDescriber` and an `expected.json` written from its entry, replays to the same text.  A capture with a changed answer gives another text |
| `SecretTests` of sub-phase 2.5 | A run through a capturing exchange, with the connection value as a marker word: the capture's text does not hold it |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: the three exchanges and `Describing/Capture/`; the rule for what a request and an answer may hold, that nothing of the seam's types crosses the exchange, that `CallRecordTests` holds every record to it, and that the request of the call that opens a session is empty; that a statement's name is a counter because it is part of a request; that a describer's clock is `OpenRequest.Time`; that a capture is read by phase 10's bundle, so a method's name and a record's members are added and not renamed; where the fixtures live, what the three files are, and that their line endings are `\n`.
- `src/SqlSource/AGENTS.md`: nothing changes.
- `src/SqlSource.Tool/README.md`, the repository's `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`, `docs/diagnostics.md`: nothing changes.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 6.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
