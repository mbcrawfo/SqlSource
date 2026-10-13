# Query generation, phase 3.1: capture and replay - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A describer's calls can be recorded into a readable `capture.json` and answered from one with no database, and the tool's tests have the harness that replays a fixture case to a sidecar's text.

**Architecture:** `src/SqlSource.Tool/Describing/Capture/` holds the capture's model, its file and two more shapes of `IDescribeExchange`: `CapturingExchange`, which wraps another and keeps each request and answer as JSON, and `ReplayingExchange`, which answers from a capture keyed by the method and the serialized request.  `FakeDescriber` is rewritten to pass only plain records through the exchange, as a real describer must, and proves both.  `tests/SqlSource.Tool.Tests/Fixtures/` gains the harness that turns a case folder into the text of a sidecar.

**Tech Stack:** C# (`LangVersion` `preview`) on `net8.0`; System.Text.Json of the runtime; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-1-capture-and-replay-design.md`.  Read it with this plan.

**Branch:** `claude/query-generation-phase-3-1-capture-and-replay`, which Task 1 makes from `main`.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

- **`CaptureJson.Read` returns `Outcome<DescribeCapture, string>` and does not throw.**  `src/SqlSource.Tool/AGENTS.md` wants that shape for a method another file calls that gives one value or one reason.  The reason is a sentence, and it names the version for a `captureVersion` the reader does not know.  The spec's sample passes `Read`'s result straight on; a caller reads `.Value`.
- **The encoder is `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`**, as `Logging/RunLogFile.cs` uses.  The default one writes `"` as `"` and `<` as `<`, and a capture of SQL would not be readable.
- **The file is indented whole; only a key is compact.**  The spec's example with a call on one line is an abbreviation.  A key is `CaptureJson.KeyOf(element)`, and both the recorded request and the asked one become an element first, so the two go one road to their text.
- **`\r\n` is replaced after writing on every runtime.**  `Utf8JsonWriter` indents with the platform's line end on .NET 8 as well as on .NET 9 and later.  A `\r` inside a string value is written as the two characters `\r`, so the replacement cannot touch a value.
- **The whole fixture folder is `text eol=lf`, not only its `*.json`.**  The SQL of `query.sql` is in a request, which keys a recorded answer: a checkout that turns its line ends into `\r\n` would miss every call.
- **`.editorconfig` gets a section for the fixture folder** with `max_line_length = off` and `end_of_line = lf`.  `editorconfig-checker` holds `*.json` to 120 columns, and a line of a capture is as long as its SQL.
- **No fixture folder is committed.**  Git keeps no empty folder.  The project copies `Fixtures/Describers/**` to the output, which is nothing yet, and `FixtureCases.Of` gives no case for a folder that is not there.  `FixtureReplayTests` writes its case into a temporary folder.
- **Equality of a round trip in `CallRecordTests` is equality of the text.**  A record with a list member compares its list by reference, so the test serializes, deserializes, serializes again and compares the two texts.
- **A list in a call record is `IReadOnlyList<T>`.**  The rule's "lists of them" is that one type: System.Text.Json reads it into a `List<T>`, and `CA1002` forbids `List<T>` itself.
- **`FakeOpenRequest` has one static member, `Instance`.**  It serializes as `{}`.  A record with no member at all is `S2094`.
- **What `FakeDescriber` carries through the exchange.**  Its `describe` answer mirrors `QueryDescription`: parameters, columns, origins, the matched table, `PostgresType` with its nested types, and `OtherEngineType`.  A `SqlServerType` throws `NotSupportedException`: no test of the tool gives the fake one, and phase 6 adds it with the first test that does.
- **A failure crosses as the parts of its descriptor**, id, title, message format, category, description and help link, and is built again on the describer's side as an enabled error.
- **The hooks of a describer with a bug apply after the exchange has answered**: `OpensNothing`, `OpenFailsWithNothing`, `OpensWithNoSession` and `DescribeResult`.  A null `Outcome` is no answer of an engine and cannot be written.  `OpenException`, `Exceptions`, `BeforeDescribe`, `OnOpen` and `OnDescribe` stay inside `live`.
- **A session's `Server` is what `open` answered**, no longer `FakeDescriber.Server` read at each use, so that a replay gives the server the capture recorded.
- **An answer of the capture that cannot be read as the answer's type is a `CaptureMissException` too**, with a message that names the method and the type.  An answer that lacks a member of the record is read with the member's default, as System.Text.Json reads it: a capture is read by a later version of the tool, whose records may have gained a member.
- **`FixtureReplay` has two entries.**  `DescribeAsync(case, describer)` is the spec's.  `DescribeThroughAsync(case, describer, connection, exchange)` is the same steps over any exchange, which the test records with.
- **`FixtureReplay` throws `InvalidOperationException` that names the case** for a capture that cannot be read or is of another engine, and for a `query.sql` that is not exactly one query of the engine's dialect.
- **Step 1 of the spec's order changes one row.**  The epic's phase 3 section already holds the table of sub-phases.  The same commit mends the epic's line that says a replay is keyed by "a hash of the arguments".
- **The spec's Documentation section says the outline changes "in steps 1 and 6"; it is steps 1 and 7.**

## Global Constraints

- Nothing under `src/SqlSource/` changes, and no package reference is added: System.Text.Json is the runtime's on `net8.0`.  `packages.lock.json` files do not change.
- No describer is registered: `ToolHost.Create` and `ToolHost.Exchange` are unchanged.
- `DescribeCapture.Version` is `1`, and its key in the file is `captureVersion`.
- A capture is indented, UTF-8, with `\n` line ends on every platform, and ends with one `\n`.
- `CaptureJson.Options`: camel-case names, every member written, `null` included, no indentation.
- Nothing of a connection reaches a capture.  The request of the call that opens a session holds nothing.
- The connection value of a replayed `OpenRequest` is the constant `replay`.  The `toolVersion` of an `expected.json` is `fixture`.
- Nothing in the tool reads `DateTime` or `Stopwatch`: a clock is a `TimeProvider`.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`, `docs/diagnostics.md`, `src/SqlSource/AGENTS.md` and `src/SqlSource.Tool/README.md` are not touched.  Nothing is expected for `docs/tech-debt` or `docs/deferred`; if a task finds a problem it does not fix, it writes it there in its own commit.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A text that is no capture.**  A file that is not JSON, is cut short, or lacks `captureVersion`, `engine`, `calls` or a part of a call.  `CaptureJson.Read` must say what is wrong and never throw a `KeyNotFoundException` or an `InvalidOperationException` of `JsonElement`.  Task 2, `Read_TextThatIsNoCapture_SaysWhatIsWrong`.
- **SQL with quotes, angle brackets, a non-ASCII letter and a `\r\n`.**  It must stay readable in the file, leave no raw `\r` there, and still find its answer after the capture went through a file.  Task 2, `Write_TextWithQuotesAnglesAndALineBreak_StaysReadableAndHoldsNoCarriageReturn`; Task 5, `AskAsync_CaptureReadFromItsFile_FindsARequestWithAwkwardText`.
- **An answer in a capture that is not of the answer's shape**: `null`, or a string where a record stands.  The describer must get a `CaptureMissException` that names the method, not a null to fail on later.  Task 5, `AskAsync_AnswerThatIsNotOfTheAnswersShape_ThrowsNamingTheMethod`.
- **Questions asked in another order than they were recorded.**  Order matters only among calls of one key; a describer that asks B before A must get both.  Task 5, `AskAsync_QuestionsInAnotherOrderThanRecorded_AreEachAnswered`.
- **A case folder that does not hold together**: a capture of another engine, a capture that cannot be read, a `query.sql` with no query or two.  The harness must fail with the case's name and the reason, not with an index out of range.  Task 6, `DescribeAsync_CaptureOfAnotherEngine_FailsNamingBoth`, `DescribeAsync_CaptureThatCannotBeRead_FailsNamingTheCase` and `DescribeAsync_QueryFileThatIsNotOneQuery_FailsNamingTheCase`.

---

### Task 1: The branch, the plan and the outline

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:71` and `:370`
- Add to git: `docs/superpowers/plans/2026-10-12-query-generation-phase-3-1-capture-and-replay.md`

**Interfaces:**
- Produces: the branch every later task commits to.

- [ ] **Step 1: Make the branch**

```bash
git switch -c claude/query-generation-phase-3-1-capture-and-replay
```

- [ ] **Step 2: Set the row of 3.1 to In progress**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, the row of the Phases table that starts `| 3.1 Capture and replay | Not started |` becomes `| 3.1 Capture and replay | In progress |`.  Nothing else of the row changes.

- [ ] **Step 3: Say what a replay is keyed by**

In the same file, in the bullet that starts `- **The seam.**`, replace

```
replaying, which answers from a capture keyed by method and a hash of the arguments, and fails loudly
```

with

```
replaying, which answers from a capture keyed by the method and the request as it is serialized, and fails loudly
```

Run: `grep -n "hash of the arguments" docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
Expected: no line.

- [ ] **Step 4: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-12-query-generation-phase-3-1-capture-and-replay.md
```

```bash
git commit -m "Mark sub-phase 3.1 in progress, and key a replay by the request in the outline

The plan of the sub-phase comes with it.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The capture and its file

**Files:**
- Create: `src/SqlSource.Tool/Describing/Capture/CaptureCall.cs`
- Create: `src/SqlSource.Tool/Describing/Capture/DescribeCapture.cs`
- Create: `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Capture/CaptureJsonTests.cs`
- Modify: `.gitattributes`
- Modify: `.editorconfig` (after the section `[schemas/*.json]`)

**Interfaces:**
- Consumes: `Outcome<TValue, TFailure>` of `src/SqlSource.Tool/Outcome.cs`, with `Of(value)`, `Failed(failure)`, `Value`, `Failure`, `Succeeded`.
- Produces, in the namespace `SqlSource.Tool.Describing.Capture`:
  - `sealed record CaptureCall(string Method, JsonElement Request, JsonElement Answer)`
  - `sealed class DescribeCapture(string engine)` with `const int Version = 1`, `string Engine`, `IReadOnlyList<CaptureCall> Calls`, `void Add(CaptureCall call)`
  - `static class CaptureJson` with `JsonSerializerOptions Options`, `JsonElement ToElement<T>(T value)`, `string KeyOf(JsonElement element)`, `string Write(DescribeCapture capture)`, `Outcome<DescribeCapture, string> Read(string text)`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/Describing/Capture/CaptureJsonTests.cs`:

```csharp
using System;
using System.Linq;
using System.Text.Json;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The capture as a file: what CaptureJson writes, what it reads back, and what it says of a text that is none.
public class CaptureJsonTests
{
    // A capture of two calls as its file holds it.
    private const string Text =
        """
            {
              "captureVersion": 1,
              "engine": "postgres",
              "calls": [
                {
                  "method": "open",
                  "request": {},
                  "answer": {
                    "serverVersion": "18.4",
                    "error": null
                  }
                },
                {
                  "method": "prepare",
                  "request": {
                    "sql": "SELECT id FROM users WHERE id = $1",
                    "types": [
                      "unknown"
                    ]
                  },
                  "answer": {
                    "parameterTypes": [
                      23
                    ],
                    "error": null
                  }
                }
              ]
            }
            """
        + "\n";

    private const string Awkward = "SELECT '<é>' AS \"a\"\r\nFROM t";

    private static DescribeCapture TwoCalls()
    {
        var capture = new DescribeCapture("postgres");
        capture.Add(
            new CaptureCall(
                "open",
                CaptureJson.ToElement(new { }),
                CaptureJson.ToElement(new { ServerVersion = "18.4", Error = (string?)null })
            )
        );
        capture.Add(
            new CaptureCall(
                "prepare",
                CaptureJson.ToElement(new { Sql = "SELECT id FROM users WHERE id = $1", Types = new[] { "unknown" } }),
                CaptureJson.ToElement(new { ParameterTypes = new[] { 23 }, Error = (string?)null })
            )
        );
        return capture;
    }

    private static DescribeCapture WithAwkwardText()
    {
        var capture = new DescribeCapture("postgres");
        capture.Add(
            new CaptureCall(
                "prepare",
                CaptureJson.ToElement(new { Sql = Awkward }),
                CaptureJson.ToElement(new { Error = (string?)null })
            )
        );
        return capture;
    }

    [Fact]
    public void Write_CaptureWithTwoCalls_IsIndentedWithLineFeeds()
    {
        var text = CaptureJson.Write(TwoCalls());

        text.ShouldBe(Text);
        text.ShouldNotContain("\r");
    }

    [Fact]
    public void Read_WhatWriteGave_WritesTheSameTextAgain()
    {
        var read = CaptureJson.Read(Text).Value.ShouldNotBeNull();

        read.Engine.ShouldBe("postgres");
        read.Calls.Select(call => call.Method).ShouldBe(["open", "prepare"]);
        CaptureJson.Write(read).ShouldBe(Text);
    }

    [Fact]
    public void Read_CaptureVersionOfTwo_IsAnErrorThatNamesIt()
    {
        var read = CaptureJson.Read(
            Text.Replace("\"captureVersion\": 1", "\"captureVersion\": 2", StringComparison.Ordinal)
        );

        read.Value.ShouldBeNull();
        read.Failure.ShouldBe("The capture's captureVersion is 2, and this version of sqlsource reads 1.");
    }

    [Theory]
    [InlineData("", "The capture is not JSON: ")]
    [InlineData("{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [", "The capture is not JSON: ")]
    [InlineData("[]", "The capture is not a JSON object.")]
    [InlineData("{}", "The capture has no captureVersion.")]
    [InlineData("{\"captureVersion\": \"1\"}", "The capture has no captureVersion.")]
    [InlineData("{\"captureVersion\": 1}", "The capture has no engine.")]
    [InlineData("{\"captureVersion\": 1, \"engine\": 7}", "The capture has no engine.")]
    [InlineData("{\"captureVersion\": 1, \"engine\": \"postgres\"}", "The capture has no calls.")]
    [InlineData("{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": {}}", "The capture has no calls.")]
    [InlineData(
        "{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [7]}",
        "The call 1 of the capture has no method, request or answer."
    )]
    [InlineData(
        "{\"captureVersion\": 1, \"engine\": \"postgres\", \"calls\": [{\"method\": \"open\", \"request\": {}}]}",
        "The call 1 of the capture has no method, request or answer."
    )]
    public void Read_TextThatIsNoCapture_SaysWhatIsWrong(string text, string reason)
    {
        var read = CaptureJson.Read(text);

        read.Value.ShouldBeNull();
        read.Failure.ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Fact]
    public void Options_ObjectWithANestedObjectAListANullAndAnInteger_WritesEveryMemberInCamelCase()
    {
        var value = new
        {
            Name = "a",
            Count = 3,
            Missing = (string?)null,
            Items = new[] { new { Flag = true } },
            Nested = new { Flag = false },
        };

        JsonSerializer
            .Serialize(value, CaptureJson.Options)
            .ShouldBe(
                "{\"name\":\"a\",\"count\":3,\"missing\":null,\"items\":[{\"flag\":true}],\"nested\":{\"flag\":false}}"
            );
    }

    // A request that was read from a file must have the key of the request it was written from.
    [Fact]
    public void KeyOf_ElementReadFromAnIndentedFile_IsTheKeyOfTheRequestItWasWrittenFrom()
    {
        var request = new { Sql = Awkward, Types = new[] { "unknown", "int4" } };

        var read = CaptureJson.Read(CaptureJson.Write(WithRequest(request))).Value.ShouldNotBeNull();

        var key = CaptureJson.KeyOf(read.Calls[0].Request);
        key.ShouldBe(CaptureJson.KeyOf(CaptureJson.ToElement(request)));
        key.ShouldBe(JsonSerializer.Serialize(request, CaptureJson.Options));
    }

    private static DescribeCapture WithRequest<T>(T request)
    {
        var capture = new DescribeCapture("postgres");
        capture.Add(new CaptureCall("prepare", CaptureJson.ToElement(request), CaptureJson.ToElement(new { })));
        return capture;
    }

    // A reviewer reads the SQL of a capture: a quote is \" and nothing else of it is escaped but its line break.
    [Fact]
    public void Write_TextWithQuotesAnglesAndALineBreak_StaysReadableAndHoldsNoCarriageReturn()
    {
        var text = CaptureJson.Write(WithAwkwardText());

        text.ShouldContain("\"sql\": \"SELECT '<é>' AS \\\"a\\\"\\r\\nFROM t\"");
        text.ShouldNotContain("\r");
        var read = CaptureJson.Read(text).Value.ShouldNotBeNull();
        read.Calls[0].Request.GetProperty("sql").GetString().ShouldBe(Awkward);
    }
}
```

- [ ] **Step 2: Run the build to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL, `CS0234`: the namespace `SqlSource.Tool.Describing.Capture` does not exist.

- [ ] **Step 3: Write the model**

Create `src/SqlSource.Tool/Describing/Capture/CaptureCall.cs`:

```csharp
using System.Text.Json;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// One call a describer made through its exchange, as a capture keeps it.
/// </summary>
/// <param name="Method">The name of the question.</param>
/// <param name="Request">What was asked, as <see cref="CaptureJson.Options" /> writes it.</param>
/// <param name="Answer">What was answered, as <see cref="CaptureJson.Options" /> writes it.</param>
internal sealed record CaptureCall(string Method, JsonElement Request, JsonElement Answer);
```

Create `src/SqlSource.Tool/Describing/Capture/DescribeCapture.cs`:

```csharp
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// Every call a describer made while it described, in the order it made them.  <c>CapturingExchange</c> fills one,
/// <see cref="CaptureJson" /> writes and reads it, and <c>ReplayingExchange</c> answers from it.
/// </summary>
/// <remarks>
/// It is for one thread, as a session is: the run describes one query at a time.
/// </remarks>
/// <param name="engine">The canonical name of the dialect whose describer made the calls.</param>
internal sealed class DescribeCapture(string engine)
{
    /// <summary>
    /// The version of the file.  It rises when a capture of the version before can no longer be read.
    /// </summary>
    public const int Version = 1;

    private readonly List<CaptureCall> _calls = [];

    public string Engine => engine;

    public IReadOnlyList<CaptureCall> Calls => _calls;

    public void Add(CaptureCall call) => _calls.Add(call);
}
```

`CapturingExchange` and `ReplayingExchange` are in `<c>` because Tasks 4 and 5 make them: a `<see cref>` to a type that does not exist is `CS1574`.  Task 5 turns the two into `<see cref>`.

- [ ] **Step 4: Write the file's reader and writer**

Create `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs`:

```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// A capture as text: the file of a fixture and of a bug bundle, and the one way a request and an answer are
/// written.
/// </summary>
/// <remarks>
/// The file is indented and its line ends are <c>\n</c> on every platform, so that a capture under version control
/// differs line by line.  The encoder is the relaxed one, as the log's is: a capture holds SQL, and a reviewer reads
/// its quotes and angle brackets as they are.
/// </remarks>
internal static class CaptureJson
{
    private const string VersionKey = "captureVersion";
    private const string EngineKey = "engine";
    private const string CallsKey = "calls";
    private const string MethodKey = "method";
    private const string RequestKey = "request";
    private const string AnswerKey = "answer";

    private static readonly JsonWriterOptions FileOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// How every request and every answer is written: camel-case names, every member, a null one included, and
    /// no indentation.
    /// </summary>
    public static JsonSerializerOptions Options { get; } =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    /// <summary>A request or an answer as a capture keeps it.</summary>
    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    /// <summary>
    /// The text that stands for a request when a recorded answer is looked up: two requests with one key are one
    /// question.  A request that was read from a file and the same request asked anew have one key, whatever the
    /// file's indentation.
    /// </summary>
    public static string KeyOf(JsonElement element) => JsonSerializer.Serialize(element, Options);

    /// <summary>The file of a capture.</summary>
    public static string Write(DescribeCapture capture)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, FileOptions))
        {
            json.WriteStartObject();
            json.WriteNumber(VersionKey, DescribeCapture.Version);
            json.WriteString(EngineKey, capture.Engine);
            json.WriteStartArray(CallsKey);
            foreach (var call in capture.Calls)
            {
                json.WriteStartObject();
                json.WriteString(MethodKey, call.Method);
                json.WritePropertyName(RequestKey);
                call.Request.WriteTo(json);
                json.WritePropertyName(AnswerKey);
                call.Answer.WriteTo(json);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        // The writer indents with the platform's line end.  A line break inside a value is written as an escape,
        // so only the writer's own are replaced.
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    /// <summary>
    /// Reads the file of a capture, or says in a sentence why the text is none.  A <c>captureVersion</c> that
    /// this version does not read is named.
    /// </summary>
    public static Outcome<DescribeCapture, string> Read(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Failed($"The capture is not JSON: {exception.Message}");
        }

        using (document)
        {
            return ReadRoot(document.RootElement);
        }
    }

    private static Outcome<DescribeCapture, string> ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Failed("The capture is not a JSON object.");
        }

        if (
            !root.TryGetProperty(VersionKey, out var version)
            || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var number)
        )
        {
            return Failed("The capture has no captureVersion.");
        }

        if (number != DescribeCapture.Version)
        {
            return Failed(
                $"The capture's captureVersion is {number}, and this version of sqlsource reads "
                    + $"{DescribeCapture.Version}."
            );
        }

        if (TextOf(root, EngineKey) is not { } engine)
        {
            return Failed("The capture has no engine.");
        }

        return root.TryGetProperty(CallsKey, out var calls) && calls.ValueKind == JsonValueKind.Array
            ? ReadCalls(new DescribeCapture(engine), calls)
            : Failed("The capture has no calls.");
    }

    private static Outcome<DescribeCapture, string> ReadCalls(DescribeCapture capture, JsonElement calls)
    {
        var position = 0;
        foreach (var call in calls.EnumerateArray())
        {
            position++;
            if (ReadCall(call) is not { } read)
            {
                return Failed($"The call {position} of the capture has no method, request or answer.");
            }

            capture.Add(read);
        }

        return Outcome<DescribeCapture, string>.Of(capture);
    }

    // An element is cloned: the document it was read from is disposed when the capture is returned.
    private static CaptureCall? ReadCall(JsonElement call) =>
        call.ValueKind == JsonValueKind.Object
        && TextOf(call, MethodKey) is { } method
        && call.TryGetProperty(RequestKey, out var request)
        && call.TryGetProperty(AnswerKey, out var answer)
            ? new CaptureCall(method, request.Clone(), answer.Clone())
            : null;

    private static string? TextOf(JsonElement parent, string key) =>
        parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Outcome<DescribeCapture, string> Failed(string reason) =>
        Outcome<DescribeCapture, string>.Failed(reason);
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Capture.CaptureJsonTests"`
Expected: PASS, 17 tests.

- [ ] **Step 6: Keep the fixture folder's line ends and line lengths**

Append to `.gitattributes`:

```
# A describer's fixture is compared byte for byte, and its SQL is in a request that keys a recorded answer
tests/SqlSource.Tool.Tests/Fixtures/Describers/** text eol=lf
```

In `.editorconfig`, after the section `[schemas/*.json]` and before the comment `# Files formatted by CSharpier`, add:

```
# The fixtures of a describer, as the tool writes them: a line of a capture is as long as its SQL.
[tests/SqlSource.Tool.Tests/Fixtures/Describers/**]
max_line_length = off
end_of_line = lf

```

- [ ] **Step 7: Format and validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 8: Commit**

```bash
git add .gitattributes .editorconfig src/SqlSource.Tool/Describing/Capture tests/SqlSource.Tool.Tests/Describing/Capture
```

```bash
git commit -m "Add the capture of a describer's calls and its file

A capture is the calls in order, each with its request and its answer as JSON,
and a file that is indented with line feeds on every platform.  A text that is
no capture is a reason and not an exception.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The describer of the tests in plain records, and the clock

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Describers/OpenRequest.cs`
- Modify: `src/SqlSource.Tool/Describing/DatabaseRuns.cs:103`
- Create: `tests/SqlSource.Tool.Tests/FakeDescriberCalls.cs`
- Create: `tests/SqlSource.Tool.Tests/FakeCalls.cs`
- Modify: `tests/SqlSource.Tool.Tests/FakeDescriber.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs`

**Interfaces:**
- Consumes: `CaptureJson.Options` of Task 2.
- Produces:
  - `OpenRequest(string Database, string Connection, IDescribeExchange Exchange, TimeProvider Time)`
  - In `SqlSource.Tool.Tests`: `FakeOpenRequest` with `static FakeOpenRequest Instance`; `FakeOpenAnswer(FakeServer? Server, FakeFailure? Error)`; `FakeServer(string Version, string? Driver, string? DriverVersion, IReadOnlyList<FakeSetting> Settings)`; `FakeSetting(string Name, string Value)`; `FakeDescribeRequest(string Name, string Sql, IReadOnlyList<FakeListedParameter> Parameters)`; `FakeListedParameter(string Name, string? Type, bool? Nullable, bool IsDeclared)`; `FakeDescribeAnswer(FakeDescription? Description, FakeFailure? Error)`; `FakeDescription`, `FakeParameter`, `FakeColumn`, `FakeOrigin`, `FakeTable`, `FakeType`, `FakeFailure` as Step 4 gives them
  - `FakeCalls.Plain(...)` and `FakeCalls.Seam(...)`, the maps between those and the seam's types
  - `FakeDescriber.TypedRows`, a `QueryDescription` with every part
  - The methods `FakeDescriber` asks: `"open"` and `"describe"`

- [ ] **Step 1: Write the test of the records**

Create `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// Every record that a describer passes to AskAsync is held to the rule of a capture: a sealed record of strings,
// integers, booleans, nullable ones, lists of them and records of the same kind, which is the same text after a
// round trip through the options of a capture.  A record is found by its name, which ends in Request or Answer, in
// the namespace of an engine's describer or of the describer of the tests.
public class CallRecordTests
{
    private static readonly string[] Namespaces = ["SqlSource.Tool.Describing.Postgres", "SqlSource.Tool.Tests"];

    private static readonly Type[] Scalars = [typeof(string), typeof(int), typeof(long), typeof(bool)];

    private static readonly Dictionary<string, Type> Found = new[]
    {
        typeof(ToolHost).Assembly,
        typeof(FakeDescriber).Assembly,
    }
        .SelectMany(static assembly => assembly.GetTypes())
        .Where(static type =>
            type.IsClass
            && type.Namespace is { } name
            && Namespaces.Contains(name)
            && (
                type.Name.EndsWith("Request", StringComparison.Ordinal)
                || type.Name.EndsWith("Answer", StringComparison.Ordinal)
            )
        )
        .ToDictionary(static type => type.FullName!, StringComparer.Ordinal);

    public static TheoryData<string> Records => [with(Found.Keys.Order(StringComparer.Ordinal))];

    // The search finds something: a rule that holds for no record holds nothing.
    [Fact]
    public void Records_OfTheDescriberOfTheTests_AreFound() =>
        Found
            .Values.Where(static type => type.Namespace == "SqlSource.Tool.Tests")
            .Select(static type => type.Name)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["FakeDescribeAnswer", "FakeDescribeRequest", "FakeOpenAnswer", "FakeOpenRequest"]);

    [Theory]
    [MemberData(nameof(Records))]
    public void Record_EveryMember_IsOfAKindACaptureMayHold(string name) => ShouldBePlain(Found[name], []);

    // Once with every member filled, and once with every nullable one null and every list empty.
    [Theory]
    [MemberData(nameof(Records))]
    public void Record_RoundTripThroughTheOptions_IsTheSameText(string name)
    {
        var type = Found[name];
        foreach (var full in new[] { true, false })
        {
            var text = JsonSerializer.Serialize(Sample(type, nullable: false, full, depth: 0), type, CaptureJson.Options);

            var back = JsonSerializer.Deserialize(text, type, CaptureJson.Options);

            JsonSerializer.Serialize(back, type, CaptureJson.Options).ShouldBe(text);
            foreach (var member in Members(type))
            {
                text.ShouldContain($"\"{JsonNamingPolicy.CamelCase.ConvertName(member.Name)}\":");
            }
        }
    }

    // The seam's own types never cross the exchange, and the rule says why: each holds what a capture may not.
    [Theory]
    [InlineData(typeof(OpenRequest))]
    [InlineData(typeof(DescribeRequest))]
    [InlineData(typeof(DescribeFailure))]
    [InlineData(typeof(QueryDescription))]
    [InlineData(typeof(Outcome<QueryDescription, DescribeFailure>))]
    public void ShouldBePlain_TypeOfTheSeam_Fails(Type type) =>
        Should.Throw<ShouldAssertException>(() => ShouldBePlain(type, []));

    private static PropertyInfo[] Members(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static bool IsList(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    private static void ShouldBePlain(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
        {
            return;
        }

        (type.IsClass && type.IsSealed && type.BaseType == typeof(object)).ShouldBeTrue(
            $"{type.Name} is not a sealed class without a base"
        );
        type.GetMethod("<Clone>$").ShouldNotBeNull($"{type.Name} is not a record");
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        foreach (var member in Members(type))
        {
            ShouldBeAllowed(member.PropertyType, $"{type.Name}.{member.Name}", seen);
        }
    }

    private static void ShouldBeAllowed(Type type, string member, HashSet<Type> seen)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (Scalars.Contains(plain))
        {
            return;
        }

        if (IsList(plain))
        {
            ShouldBeAllowed(plain.GetGenericArguments()[0], member, seen);
            return;
        }

        plain.IsClass.ShouldBeTrue($"{member} is a {plain.Name}, which a capture may not hold");
        ShouldBePlain(plain, seen);
    }

    // A value of the type.  A nullable member is null in the sparse sample, and below a depth, where a record
    // that holds itself must end.
    private static object? Sample(Type type, bool nullable, bool full, int depth)
    {
        if (nullable && (!full || depth > 3))
        {
            return null;
        }

        var plain = Nullable.GetUnderlyingType(type) ?? type;
        if (plain == typeof(string))
        {
            return "text \"quoted\" <é>";
        }

        if (plain == typeof(int))
        {
            return 7;
        }

        if (plain == typeof(long))
        {
            return 7L;
        }

        if (plain == typeof(bool))
        {
            return true;
        }

        if (IsList(plain))
        {
            var item = plain.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(item))!;
            if (full)
            {
                _ = list.Add(Sample(item, nullable: false, full, depth + 1));
            }

            return list;
        }

        var constructor = plain.GetConstructors().OrderByDescending(static found => found.GetParameters().Length).First();
        return constructor.Invoke([
            .. constructor
                .GetParameters()
                .Select(parameter => Sample(parameter.ParameterType, IsNullable(parameter), full, depth + 1)),
        ]);
    }

    private static bool IsNullable(ParameterInfo parameter) =>
        Nullable.GetUnderlyingType(parameter.ParameterType) is not null
        || new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.Nullable;
}
```

- [ ] **Step 2: Write the tests of the describer of the tests**

In `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`, add `using System.Globalization;`, `using System.Linq;` and `using SqlSource.Tests.Snapshot;`, give each of the three `new OpenRequest(...)` a fourth argument `TimeProvider.System`, and add these after `DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow`:

```csharp
    private static readonly DescribeRequest GetUser = new(
        "GetUser",
        "SELECT 1;",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static async Task<IDescribeSession> OpenAsync(FakeDescriber describer)
    {
        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "Host=db", LiveExchange.Instance, TimeProvider.System),
            NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );
        return opened.Value.ShouldNotBeNull();
    }

    // What the describer of the tests is given goes through its plain records and comes back as it was.
    [Fact]
    public async Task DescribeAsync_DescriptionWithEveryPart_ComesBackEqual()
    {
        var describer = new FakeDescriber { Server = FakeDescriber.ServerWith("17.1", "4.5.6") };
        describer.Descriptions["GetUser"] = FakeDescriber.TypedRows;

        await using var session = await OpenAsync(describer);
        var described = await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken);

        described.Value.ShouldBe(FakeDescriber.TypedRows);
        session.Server.ShouldBe(describer.Server);
    }

    [Fact]
    public async Task DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart()
    {
        var describer = new FakeDescriber();
        describer.Failures["GetUser"] = FakeDescriber.Failure(
            "GetUser",
            DescribeStep.Explain,
            "check the SQL",
            "line one",
            "line two"
        );

        await using var session = await OpenAsync(describer);
        var described = await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC999");
        failure
            .Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)
            .ShouldBe("The server rejected the query '{0}'");
        failure.Descriptor.HelpLinkUri.ShouldBe("https://example.test/sqlsrc999");
        failure.Arguments.ToArray().ShouldBe(["GetUser"]);
        failure.Step.ShouldBe(DescribeStep.Explain);
        failure.ServerLines.ToArray().ShouldBe(["line one", "line two"]);
        failure.Help.ShouldBe("check the SQL");
    }

    // The describer of the tests carries the types its tests give it.  Phase 6 adds SQL Server's.
    [Fact]
    public async Task DescribeAsync_DescriptionWithASqlServerType_IsNotCarriedThroughTheExchange()
    {
        var describer = new FakeDescriber();
        describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            Parameters = TestSidecars.Of(new DescribedParameter("id", TestSidecars.SqlServer(), null)),
        };

        await using var session = await OpenAsync(describer);

        _ = await Should.ThrowAsync<NotSupportedException>(async () =>
            await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken)
        );
    }
```

In `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs`, add after `Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten`:

```csharp
    // A describer's events carry durations, and its clock is the run's.
    [Fact]
    public async Task Run_Describer_IsGivenTheClockOfTheRun()
    {
        _ = await _scene.DescribeAsync();

        _scene.Describer.Opened.ShouldHaveSingleItem().Time.ShouldBeSameAs(_scene.Clock);
    }
```

- [ ] **Step 3: Run the build to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL: `OpenRequest` has no constructor of four arguments, and `FakeDescriber.TypedRows` does not exist.

- [ ] **Step 4: Give the request its clock**

Replace `src/SqlSource.Tool/Describing/Describers/OpenRequest.cs` with:

```csharp
using System;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// What a describer needs to open a session.  It is the one thing that holds a connection's value.
/// </summary>
/// <param name="Database">The logical name of the database, as the plan spells it.</param>
/// <param name="Connection">The connection's value.  A describer hands it to its driver and to nothing else.</param>
/// <param name="Exchange">What every call to the engine goes through.</param>
/// <param name="Time">The clock a describer measures with: the run's, so that a test can hold it still.</param>
internal sealed record OpenRequest(string Database, string Connection, IDescribeExchange Exchange, TimeProvider Time)
{
    /// <summary>
    /// The request without its connection: a record prints its members, and that one is a secret.
    /// </summary>
    public override string ToString() => $"OpenRequest {{ Database = {Database} }}";
}
```

In `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, the line

```csharp
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name));
```

becomes

```csharp
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name), host.Time);
```

- [ ] **Step 5: Write the plain records**

Create `tests/SqlSource.Tool.Tests/FakeDescriberCalls.cs`:

```csharp
using System.Collections.Generic;

namespace SqlSource.Tool.Tests;

// What FakeDescriber passes through the exchange, one file as a real describer's are: plain records that a capture
// can hold.  The two calls are "open" and "describe".  FakeCalls maps between these and the seam's types, and
// Describing/Capture/CallRecordTests holds each to the rule.

// The request of "open".  It holds nothing: nothing of a connection may reach a capture.  Its one member is static,
// so it is written as {}.
internal sealed record FakeOpenRequest
{
    public static FakeOpenRequest Instance { get; } = new();
}

// The server of a session that was opened, or why it was not.
internal sealed record FakeOpenAnswer(FakeServer? Server, FakeFailure? Error);

internal sealed record FakeServer(
    string Version,
    string? Driver,
    string? DriverVersion,
    IReadOnlyList<FakeSetting> Settings
);

internal sealed record FakeSetting(string Name, string Value);

// Everything that decides the answer: the query's name, the SQL as sent and the parameter list.
internal sealed record FakeDescribeRequest(string Name, string Sql, IReadOnlyList<FakeListedParameter> Parameters);

internal sealed record FakeListedParameter(string Name, string? Type, bool? Nullable, bool IsDeclared);

// The description of a query, or why it could not be described.
internal sealed record FakeDescribeAnswer(FakeDescription? Description, FakeFailure? Error);

internal sealed record FakeDescription(
    string ResultKind,
    IReadOnlyList<FakeParameter> Parameters,
    IReadOnlyList<FakeColumn>? Columns,
    FakeTable? MatchesTable,
    string? Plan,
    string? TableMatch
);

internal sealed record FakeParameter(string Name, FakeType? Type, string? TypeSource);

internal sealed record FakeColumn(
    int Ordinal,
    string Name,
    FakeType Type,
    bool? Nullable,
    string? NullableSource,
    FakeOrigin? Origin,
    bool? Identity,
    bool? Computed
);

internal sealed record FakeOrigin(string? Schema, string? Table, string? Column);

internal sealed record FakeTable(string? Schema, string? Table);

// A type of PostgreSQL, or with no kind a type of an engine the sidecar's model does not know, of which only the
// name is kept.
internal sealed record FakeType(
    string Name,
    string? Kind,
    string? Schema,
    string? InternalName,
    int? Length,
    int? Precision,
    int? Scale,
    FakeType? Element,
    FakeType? Base,
    IReadOnlyList<string>? Labels,
    FakeType? Subtype
);

// A failure, with the parts of its descriptor: a descriptor is no plain data.
internal sealed record FakeFailure(
    string Id,
    string Title,
    string MessageFormat,
    string Category,
    string Description,
    string HelpLink,
    IReadOnlyList<string> Arguments,
    string? Step,
    IReadOnlyList<string> ServerLines,
    string? Help
);
```

- [ ] **Step 6: Write the maps**

Create `tests/SqlSource.Tool.Tests/FakeCalls.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Tests;

// The maps of FakeDescriber between the seam's types and its plain records.  They run on the describer's side of
// the exchange, as a real describer's do: Plain gives what crosses, and Seam gives it back.
internal static class FakeCalls
{
    public static FakeServer Plain(ServerInfo server) =>
        new(
            server.Version,
            server.Driver,
            server.DriverVersion,
            [.. server.Settings.Select(static setting => new FakeSetting(setting.Name, setting.Value))]
        );

    public static ServerInfo Seam(FakeServer server) =>
        new(
            server.Version,
            server.Driver,
            server.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. server.Settings.Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    public static FakeDescribeRequest Plain(DescribeRequest request) =>
        new(
            request.Name,
            request.Sql,
            [
                .. request.Parameters.Select(static parameter => new FakeListedParameter(
                    parameter.Name,
                    parameter.Type,
                    parameter.Nullable,
                    parameter.IsDeclared
                )),
            ]
        );

    public static FakeDescription Plain(QueryDescription description) =>
        new(
            description.ResultKind.ToString(),
            [
                .. description.Parameters.Select(static parameter => new FakeParameter(
                    parameter.Name,
                    parameter.Type is { } type ? PlainType(type) : null,
                    parameter.TypeSource
                )),
            ],
            description.Columns?.Select(PlainColumn).ToList(),
            description.MatchesTable is { } table ? new FakeTable(table.Schema, table.Table) : null,
            description.Plan,
            description.TableMatch
        );

    public static QueryDescription Seam(FakeDescription description) =>
        new(
            Enum.Parse<SidecarResultKind>(description.ResultKind),
            new EquatableArray<DescribedParameter>([
                .. description.Parameters.Select(static parameter => new DescribedParameter(
                    parameter.Name,
                    parameter.Type is { } type ? SeamType(type) : null,
                    parameter.TypeSource
                )),
            ]),
            SeamColumns(description.Columns),
            description.MatchesTable is { } table ? new SidecarTable(table.Schema, table.Table) : null,
            description.Plan,
            description.TableMatch
        );

    public static FakeFailure Plain(DescribeFailure failure) =>
        new(
            failure.Descriptor.Id,
            failure.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.Category,
            failure.Descriptor.Description.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.HelpLinkUri,
            [.. failure.Arguments],
            failure.Step?.ToString(),
            [.. failure.ServerLines],
            failure.Help
        );

    // A failure of the describer of the tests is an enabled error, so neither is carried.
    public static DescribeFailure Seam(FakeFailure failure) =>
        new(
            new DiagnosticDescriptor(
                failure.Id,
                failure.Title,
                failure.MessageFormat,
                failure.Category,
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: failure.Description,
                helpLinkUri: failure.HelpLink
            ),
            new EquatableArray<string>([.. failure.Arguments]),
            failure.Step is { } step ? Enum.Parse<DescribeStep>(step) : null,
            new EquatableArray<string>([.. failure.ServerLines]),
            failure.Help
        );

    private static FakeColumn PlainColumn(SidecarColumn column) =>
        new(
            column.Ordinal,
            column.Name,
            PlainType(column.Type),
            column.Nullable,
            column.NullableSource,
            column.Origin is { } origin ? new FakeOrigin(origin.Schema, origin.Table, origin.Column) : null,
            column.Identity,
            column.Computed
        );

    private static EquatableArray<SidecarColumn>? SeamColumns(IReadOnlyList<FakeColumn>? columns)
    {
        if (columns is null)
        {
            return null;
        }

        return new EquatableArray<SidecarColumn>([
            .. columns.Select(static column => new SidecarColumn(
                column.Ordinal,
                column.Name,
                SeamType(column.Type),
                column.Nullable,
                column.NullableSource,
                column.Origin is { } origin ? new SidecarOrigin(origin.Schema, origin.Table, origin.Column) : null,
                column.Identity,
                column.Computed
            )),
        ]);
    }

    private static FakeType PlainType(SidecarType type) =>
        type switch
        {
            PostgresType postgres => PlainPostgres(postgres),
            OtherEngineType other => new FakeType(other.Name, null, null, null, null, null, null, null, null, null, null),
            _ => throw new NotSupportedException(
                $"The describer of the tests does not carry a {type.GetType().Name} through the exchange."
            ),
        };

    private static FakeType PlainPostgres(PostgresType type) =>
        new(
            type.Name,
            type.Kind,
            type.Schema,
            type.InternalName,
            type.Length,
            type.Precision,
            type.Scale,
            type.Element is { } element ? PlainPostgres(element) : null,
            type.Base is { } baseType ? PlainPostgres(baseType) : null,
            type.Labels?.ToList(),
            type.Subtype is { } subtype ? PlainPostgres(subtype) : null
        );

    private static SidecarType SeamType(FakeType type) =>
        type.Kind is null ? new OtherEngineType(type.Name) : SeamPostgres(type);

    private static PostgresType SeamPostgres(FakeType type) =>
        new(
            type.Name,
            type.Kind ?? throw new InvalidOperationException($"The type '{type.Name}' has no kind."),
            type.Schema ?? throw new InvalidOperationException($"The type '{type.Name}' has no schema."),
            type.InternalName ?? throw new InvalidOperationException($"The type '{type.Name}' has no internal name."),
            type.Length,
            type.Precision,
            type.Scale,
            type.Element is { } element ? SeamPostgres(element) : null,
            type.Base is { } baseType ? SeamPostgres(baseType) : null,
            SeamLabels(type.Labels),
            type.Subtype is { } subtype ? SeamPostgres(subtype) : null
        );

    private static EquatableArray<string>? SeamLabels(IReadOnlyList<string>? labels)
    {
        if (labels is null)
        {
            return null;
        }

        return new EquatableArray<string>([.. labels]);
    }
}
```

- [ ] **Step 7: Rewrite the describer over them**

In `tests/SqlSource.Tool.Tests/FakeDescriber.cs`:

Add `using SqlSource.Tests.Snapshot;` to the usings.

Replace the comment above the class with:

```csharp
// The describer of the tests.  It is given a description, a failure or an exception for a query's name, and keeps
// what it was asked.  It makes every call through the exchange as a real one must: what crosses is the plain
// records of FakeDescriberCalls.cs, which FakeCalls maps to the seam's types on this side, so a run of it can be
// captured and replayed.  Under a replay its "live" functions never run: it then keeps nothing and its hooks do
// not fire.  A query it was told nothing about is a statement without rows and without typed parameters.
```

Add after the property `Rows`:

```csharp
    // A statement with every part a description has: a parameter with its declared type, columns of a base type,
    // of an array and of an enum, their origins, the table the result matches, and the provenance.
    public static QueryDescription TypedRows { get; } =
        new(
            SidecarResultKind.Rows,
            TestSidecars.Of(
                new DescribedParameter("id", TestSidecars.Postgres(), SidecarValues.TypeSource.Declared)
            ),
            TestSidecars.Of(
                TestSidecars.Column(
                    0,
                    "id",
                    TestSidecars.Postgres(),
                    origin: new SidecarOrigin("public", "users", "id"),
                    identity: true,
                    computed: false
                ),
                TestSidecars.Column(
                    1,
                    "tags",
                    TestSidecars.Postgres(
                        "text[]",
                        SidecarValues.Kind.Array,
                        internalName: "_text",
                        element: TestSidecars.Postgres("text", internalName: "text")
                    ),
                    nullable: true
                ),
                TestSidecars.Column(
                    2,
                    "mood",
                    TestSidecars.Postgres("mood", SidecarValues.Kind.Enum, "public", "mood", labels: ["sad", "happy"]),
                    nullable: null,
                    nullableSource: null
                )
            ),
            new SidecarTable("public", "users"),
            SidecarValues.Plan.Walked,
            SidecarValues.TableMatch.Matched
        );
```

Replace the method `OpenAsync` and the class `Session` with:

```csharp
    public async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        var answer = await request.Exchange.AskAsync(
            "open",
            FakeOpenRequest.Instance,
            (_, _) =>
            {
                Opened.Add(request);
                OnOpen?.Invoke(log, request);
                if (OpenException is { } exception)
                {
                    throw exception;
                }

                return Task.FromResult(
                    OpenFailure is { } failure
                        ? new FakeOpenAnswer(null, FakeCalls.Plain(failure))
                        : new FakeOpenAnswer(FakeCalls.Plain(Server), null)
                );
            },
            cancellationToken
        );

        // What a describer with a bug returns.  None of it is an answer of an engine, so none of it crosses the
        // exchange.
        if (OpensNothing)
        {
            return null!;
        }

        if (OpenFailsWithNothing)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Failed(null!);
        }

        if (OpensWithNoSession)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Of(null!);
        }

        if (answer.Error is { } error)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Failed(FakeCalls.Seam(error));
        }

        var server = FakeCalls.Seam(
            answer.Server ?? throw new InvalidOperationException("The answer to 'open' holds no server and no error.")
        );
        return Outcome<IDescribeSession, DescribeFailure>.Of(new Session(this, request.Exchange, log, server));
    }

    // The server is what "open" answered, so that a replayed session has the server of its capture.
    private sealed class Session(FakeDescriber owner, IDescribeExchange exchange, IRunLog log, ServerInfo server)
        : IDescribeSession
    {
        public ServerInfo Server => server;

        public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
            DescribeRequest request,
            CancellationToken cancellationToken
        )
        {
            var answer = await exchange.AskAsync(
                "describe",
                FakeCalls.Plain(request),
                async (_, token) =>
                {
                    owner.Described.Add(request);
                    owner.OnDescribe?.Invoke(log, request);
                    if (owner.BeforeDescribe is { } before)
                    {
                        await before(request);
                        token.ThrowIfCancellationRequested();
                    }

                    if (owner.Exceptions.TryGetValue(request.Name, out var exception))
                    {
                        throw exception;
                    }

                    return owner.Failures.TryGetValue(request.Name, out var failure)
                        ? new FakeDescribeAnswer(null, FakeCalls.Plain(failure))
                        : new FakeDescribeAnswer(
                            FakeCalls.Plain(owner.Descriptions.GetValueOrDefault(request.Name, NoRows)),
                            null
                        );
                },
                cancellationToken
            );

            // What a describer with a bug returns, in place of the query's result.
            if (owner.DescribeResult is { } result)
            {
                return result()!;
            }

            if (answer.Error is { } error)
            {
                return Outcome<QueryDescription, DescribeFailure>.Failed(FakeCalls.Seam(error));
            }

            return Outcome<QueryDescription, DescribeFailure>.Of(
                FakeCalls.Seam(
                    answer.Description
                        ?? throw new InvalidOperationException(
                            "The answer to 'describe' holds no description and no error."
                        )
                )
            );
        }

        public ValueTask DisposeAsync()
        {
            owner.Closed++;
            return owner.CloseException is { } exception ? throw exception : ValueTask.CompletedTask;
        }
    }
```

- [ ] **Step 8: Run the new tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Capture.CallRecordTests"`
Expected: PASS, 14 tests: one fact, two theories of four records and one of five types.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Describers.DescriberTests"`
Expected: PASS.

- [ ] **Step 9: Run every test of the tool**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.  A test that fails here compared something the rewrite changed: a failure's descriptor is now a new object that is equal to the one given, and a session's server is the one of `open`.  Mend the describer, not the test, unless the test compared a reference.

- [ ] **Step 10: Format and validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 11: Commit**

```bash
git add src/SqlSource.Tool/Describing tests/SqlSource.Tool.Tests
```

```bash
git commit -m "Pass plain records through the exchange in the describer of the tests

The describer of the tests maps the seam's types to records a capture can
hold, as a real describer must, and a test holds every such record to the
rule.  An OpenRequest carries the run's clock.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The capturing exchange

**Files:**
- Create: `src/SqlSource.Tool/Describing/Capture/CapturingExchange.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Capture/CapturingExchangeTests.cs`
- Delete: `tests/SqlSource.Tool.Tests/RecordingExchange.cs`
- Modify: `tests/SqlSource.Tool.Tests/CliRun.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs:27`, `:115`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

**Interfaces:**
- Consumes: `DescribeCapture`, `CaptureCall`, `CaptureJson.ToElement`, `CaptureJson.KeyOf`, `CaptureJson.Write` of Task 2; `FakeDescriber` of Task 3.
- Produces: `sealed class CapturingExchange(IDescribeExchange inner, DescribeCapture capture) : IDescribeExchange`; `CliRun.Capture`, a `DescribeCapture`, and `CliRun.Exchange`, a `CapturingExchange`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/Describing/Capture/CapturingExchangeTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The exchange that records: every call of a describer is in the capture, in order, as it was asked and answered.
public class CapturingExchangeTests
{
    private static readonly DescribeRequest GetUser = new(
        "GetUser",
        "SELECT 1;",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private readonly DescribeCapture _capture = new("postgres");
    private readonly FakeDescriber _describer = new();

    private Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync() =>
        _describer.OpenAsync(
            new OpenRequest(
                "billing",
                Secrets.ConnectionWithMarker,
                new CapturingExchange(LiveExchange.Instance, _capture),
                TimeProvider.System
            ),
            NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task AskAsync_DescriberOverARun_KeepsEachCallInOrderWithItsRequestAndItsAnswer()
    {
        var opened = await OpenAsync();
        await using var session = opened.Value.ShouldNotBeNull();
        _ = await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken);

        _capture
            .Calls.Select(call => $"{call.Method} {CaptureJson.KeyOf(call.Request)} {CaptureJson.KeyOf(call.Answer)}")
            .ShouldBe([
                "open {} {\"server\":{\"version\":\"16.4\",\"driver\":\"FakeDriver\",\"driverVersion\":\"1.2.3\","
                    + "\"settings\":[]},\"error\":null}",
                "describe {\"name\":\"GetUser\",\"sql\":\"SELECT 1;\",\"parameters\":[]} "
                    + "{\"description\":{\"resultKind\":\"None\",\"parameters\":[],\"columns\":null,"
                    + "\"matchesTable\":null,\"plan\":null,\"tableMatch\":null},\"error\":null}",
            ]);
    }

    // The request of "open" holds nothing, so nothing of the connection is in the file.
    [Fact]
    public async Task AskAsync_SessionOpenedWithAConnection_KeepsNothingOfIt()
    {
        var opened = await OpenAsync();
        await using var session = opened.Value.ShouldNotBeNull();

        _describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        CaptureJson.Write(_capture).ShouldNotContain(Secrets.Marker);
    }

    [Fact]
    public async Task AskAsync_LiveThatThrows_AddsNoCallAndLetsTheExceptionPass()
    {
        _describer.OpenException = new IOException("lost");

        var thrown = await Should.ThrowAsync<IOException>(async () => await OpenAsync());

        thrown.Message.ShouldBe("lost");
        _capture.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_FailureOfTheServer_IsKeptAsTheAnswer()
    {
        _describer.OpenFailure = FakeDescriber.Failure("billing", step: null, help: null, "connection refused");

        var opened = await OpenAsync();

        opened.Failure.ShouldNotBeNull();
        var answer = CaptureJson.KeyOf(_capture.Calls.ShouldHaveSingleItem().Answer);
        answer.ShouldStartWith("{\"server\":null,\"error\":{\"id\":\"SQLSRC999\",");
        answer.ShouldContain("\"serverLines\":[\"connection refused\"]");
    }

    [Fact]
    public async Task AskAsync_TwoSessions_KeepsTwoOpenCalls()
    {
        await using var first = (await OpenAsync()).Value.ShouldNotBeNull();
        await using var second = (await OpenAsync()).Value.ShouldNotBeNull();

        _capture.Calls.Select(call => call.Method).ShouldBe(["open", "open"]);
    }
}
```

- [ ] **Step 2: Run the build to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL, `CS0246`: `CapturingExchange` could not be found.

- [ ] **Step 3: Write the exchange**

Create `src/SqlSource.Tool/Describing/Capture/CapturingExchange.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// The exchange that records: it asks another exchange, the live one in a real run, and adds each call with its
/// request and its answer to a capture.
/// </summary>
/// <remarks>
/// A call that throws adds nothing: what a server can cause is an answer, and an exception is a bug of the
/// describer or the end of the run.  It looks at a request and an answer only to write them.
/// </remarks>
/// <param name="inner">The exchange that answers.</param>
/// <param name="capture">Where each call is added, in the order the calls are made.</param>
internal sealed class CapturingExchange(IDescribeExchange inner, DescribeCapture capture) : IDescribeExchange
{
    public async Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        var answer = await inner.AskAsync(method, request, live, cancellationToken);
        capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return answer;
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Capture.CapturingExchangeTests"`
Expected: PASS, 5 tests.

- [ ] **Step 5: Give every run of the tests a capture**

In `tests/SqlSource.Tool.Tests/CliRun.cs`, add `using SqlSource.Tool.Describing.Capture;`, and replace

```csharp
    // Every database of the run calls through this one.
    public RecordingExchange Exchange { get; } = new();
```

with

```csharp
    public CliRun()
    {
        Exchange = new CapturingExchange(LiveExchange.Instance, Capture);
    }

    // What every describer of the run asked and was answered.  The engine is a name only: a run may have several.
    public DescribeCapture Capture { get; } = new("postgres");

    // Every database of the run calls through this one, which asks the describer and keeps the call.
    public CapturingExchange Exchange { get; }
```

Put the constructor where the file's order of members wants it; `./format.sh` does not move members.

- [ ] **Step 6: Replace the helper in the two tests that read its methods**

In `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`, add `using SqlSource.Tool.Describing.Capture;`, and replace

```csharp
    private readonly RecordingExchange _exchange = new();
```

with

```csharp
    private readonly DescribeCapture _capture = new("postgres");
    private readonly CapturingExchange _exchange;
```

Set it first in the constructor:

```csharp
        _exchange = new CapturingExchange(LiveExchange.Instance, _capture);
```

and replace

```csharp
        _exchange.Methods.ShouldBe(["open", "describe", "open", "describe"]);
```

with

```csharp
        _capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "open", "describe"]);
```

In `tests/SqlSource.Tool.Tests/Describing/Describers/DescriberTests.cs`, add `using SqlSource.Tool.Describing.Capture;`.  In `OpenAsync_DescriberOfTheTests_MakesEveryCallThroughTheExchange`, replace

```csharp
        var exchange = new RecordingExchange();
```

with

```csharp
        var capture = new DescribeCapture("postgres");
        var exchange = new CapturingExchange(LiveExchange.Instance, capture);
```

and

```csharp
        exchange.Methods.ShouldBe(["open", "describe"]);
```

with

```csharp
        capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe"]);
```

In `DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow`, `new RecordingExchange()` becomes `LiveExchange.Instance`.

Delete the helper:

```bash
git rm tests/SqlSource.Tool.Tests/RecordingExchange.cs
```

Run: `grep -rn "RecordingExchange" src tests`
Expected: no line.

- [ ] **Step 7: Hold a whole run to the rule of the secret**

In `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, add `using System.Linq;` and `using SqlSource.Tool.Describing.Capture;`, and add after `Run_ValueInTheVariableWithNoNameAndOneDatabase_IsUsedAndNeverPrinted`:

```csharp
    // A capture is a file that a user attaches to a report: nothing of a connection is in what a run recorded.
    [Fact]
    public async Task Run_ThroughACapturingExchange_LeavesNothingOfTheValueInTheCapture()
    {
        var result = await _scene.DescribeAsync("--connection", "postgres=" + Secrets.ConnectionWithMarker);

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        _scene.Run.Capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "describe"]);
        CaptureJson.Write(_scene.Run.Capture).ShouldNotContain(Secrets.Marker);
    }
```

- [ ] **Step 8: Run every test of the tool**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.  Every run of `CliRun` now writes each answer as JSON: a test that fails here gave the describer something `FakeCalls` does not carry.

- [ ] **Step 9: Format and validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 10: Commit**

```bash
git add src/SqlSource.Tool/Describing/Capture tests/SqlSource.Tool.Tests
```

```bash
git commit -m "Add the exchange that records a describer's calls into a capture

It wraps another exchange and keeps each call that answered.  The tests'
helper that kept the names of the calls is gone: a run of the tests records
through the real one.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The replaying exchange

**Files:**
- Create: `src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs`
- Create: `src/SqlSource.Tool/Describing/Capture/ReplayingExchange.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs`
- Modify: `src/SqlSource.Tool/Describing/Capture/DescribeCapture.cs` (the two `<c>` of Task 2 become `<see cref>`)

**Interfaces:**
- Consumes: Tasks 2 to 4.
- Produces:
  - `sealed class ReplayingExchange(DescribeCapture capture) : IDescribeExchange` with `IReadOnlyList<CaptureCall> Unused`
  - `sealed class CaptureMissException : InvalidOperationException` with the three standard constructors, `static CaptureMissException NoAnswer(string method, string request)` and `static CaptureMissException Unreadable(string method, Type answer, string reason)`
  - Messages: `The capture has no further answer to '<method>' for the request <json>.` and `The capture's answer to '<method>' cannot be read as <type>: <reason>`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/Describing/Capture/ReplayingExchangeTests.cs`:

```csharp
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The exchange that replays: it answers from a capture, in the capture's order for a question asked twice, never
// asks the engine, and fails loudly on a question the capture does not hold.
public class ReplayingExchangeTests
{
    private const string Awkward = "SELECT '<é>' AS \"a\"\r\nFROM t";

    private static readonly DescribeRequest GetUser = new(
        "GetUser",
        "SELECT 1;",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private int _live;

    private static FakeDescribeRequest Question(string sql = "SELECT 1;") => new("GetUser", sql, []);

    private static FakeOpenAnswer Answer(string version) => new(new FakeServer(version, null, null, []), null);

    private static DescribeCapture Capture(params (FakeDescribeRequest Request, FakeOpenAnswer Answer)[] calls)
    {
        var capture = new DescribeCapture("postgres");
        foreach (var (request, answer) in calls)
        {
            capture.Add(new CaptureCall("ask", CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        }

        return capture;
    }

    // Asks, and gives the version of the answer's server, which tells one answer from another.  The engine counts
    // how often it is asked, which under a replay is never.
    private async Task<string> AskAsync(ReplayingExchange exchange, FakeDescribeRequest request, string method = "ask")
    {
        var answer = await exchange.AskAsync(
            method,
            request,
            (_, _) =>
            {
                _live++;
                return Task.FromResult(Answer("live"));
            },
            TestContext.Current.CancellationToken
        );
        return answer.Server.ShouldNotBeNull().Version;
    }

    private static async Task<(ServerInfo Server, Outcome<QueryDescription, DescribeFailure> Described)> DescribeAsync(
        FakeDescriber describer,
        IDescribeExchange exchange
    )
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "replay", exchange, TimeProvider.System),
            NullRunLog.Instance,
            token
        );
        await using var session = opened.Value.ShouldNotBeNull();
        return (session.Server, await session.DescribeAsync(GetUser, token));
    }

    [Fact]
    public async Task AskAsync_DescriptionMadeThroughACapture_IsEqualWhenReplayed()
    {
        var told = new FakeDescriber { Server = FakeDescriber.ServerWith("17.1", "4.5.6") };
        told.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var capture = new DescribeCapture("postgres");
        var recorded = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, capture));
        var untold = new FakeDescriber();
        var replay = new ReplayingExchange(capture);

        var replayed = await DescribeAsync(untold, replay);

        replayed.Described.Value.ShouldBe(recorded.Described.Value.ShouldNotBeNull());
        replayed.Described.Value.ShouldBe(FakeDescriber.TypedRows);
        replayed.Server.ShouldBe(told.Server);
        untold.Opened.ShouldBeEmpty();
        untold.Described.ShouldBeEmpty();
        replay.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_FailureThatWasCaptured_IsTheFailureWhenReplayed()
    {
        var told = new FakeDescriber();
        told.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation \"users\" does not exist");
        var capture = new DescribeCapture("postgres");
        _ = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, capture));

        var replayed = await DescribeAsync(new FakeDescriber(), new ReplayingExchange(capture));

        var failure = replayed.Described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC999");
        failure.ServerLines.ToArray().ShouldBe(["42P01: relation \"users\" does not exist"]);
    }

    [Fact]
    public async Task AskAsync_CallTheCaptureLacks_ThrowsWithTheMethodAndTheRequestAndDoesNotAskTheEngine()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1"))));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await AskAsync(exchange, Question("SELECT 2;"))
        );

        thrown.Message.ShouldBe(
            "The capture has no further answer to 'ask' for the request "
                + "{\"name\":\"GetUser\",\"sql\":\"SELECT 2;\",\"parameters\":[]}."
        );
        _live.ShouldBe(0);
    }

    [Fact]
    public async Task AskAsync_SameRequestUnderAnotherMethod_IsACallTheCaptureLacks()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1"))));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await AskAsync(exchange, Question(), "other")
        );

        thrown.Message.ShouldStartWith("The capture has no further answer to 'other' for the request ");
    }

    [Fact]
    public async Task AskAsync_QuestionAskedTwice_GivesItsAnswersInOrderAndThenThrows()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1")), (Question(), Answer("2"))));

        (await AskAsync(exchange, Question())).ShouldBe("1");
        (await AskAsync(exchange, Question())).ShouldBe("2");
        _ = await Should.ThrowAsync<CaptureMissException>(async () => await AskAsync(exchange, Question()));
        _live.ShouldBe(0);
    }

    // A record with a list is equal to no other instance of itself.  The key is the text, so a new one finds it.
    [Fact]
    public async Task AskAsync_SameRequestFromANewInstance_FindsTheCall()
    {
        var recorded = new FakeDescribeRequest("GetUser", "SELECT @id;", [new FakeListedParameter("id", "int4", false, true)]);
        var asked = new FakeDescribeRequest("GetUser", "SELECT @id;", [new FakeListedParameter("id", "int4", false, true)]);
        recorded.ShouldNotBe(asked);
        var exchange = new ReplayingExchange(Capture((recorded, Answer("1"))));

        (await AskAsync(exchange, asked)).ShouldBe("1");
    }

    // Order is kept among the answers of one question, and among nothing else.
    [Fact]
    public async Task AskAsync_QuestionsInAnotherOrderThanRecorded_AreEachAnswered()
    {
        var exchange = new ReplayingExchange(
            Capture((Question("SELECT 1;"), Answer("1")), (Question("SELECT 2;"), Answer("2")))
        );

        (await AskAsync(exchange, Question("SELECT 2;"))).ShouldBe("2");
        (await AskAsync(exchange, Question("SELECT 1;"))).ShouldBe("1");
    }

    [Fact]
    public async Task Unused_CallsNotAsked_AreListed()
    {
        var exchange = new ReplayingExchange(
            Capture((Question("SELECT 1;"), Answer("1")), (Question("SELECT 2;"), Answer("2")))
        );
        exchange.Unused.Count.ShouldBe(2);

        _ = await AskAsync(exchange, Question("SELECT 1;"));

        CaptureJson.KeyOf(exchange.Unused.ShouldHaveSingleItem().Request).ShouldContain("SELECT 2;");
    }

    // A fixture is a file: the request found in it must be the one that was asked when it was recorded.
    [Fact]
    public async Task AskAsync_CaptureReadFromItsFile_FindsARequestWithAwkwardText()
    {
        var text = CaptureJson.Write(Capture((Question(Awkward), Answer("1"))));
        var exchange = new ReplayingExchange(CaptureJson.Read(text).Value.ShouldNotBeNull());

        (await AskAsync(exchange, Question(Awkward))).ShouldBe("1");
    }

    [Theory]
    [InlineData("null", "it is null")]
    [InlineData("\"text\"", "")]
    public async Task AskAsync_AnswerThatIsNotOfTheAnswersShape_ThrowsNamingTheMethod(string answer, string reason)
    {
        using var document = JsonDocument.Parse(answer);
        var capture = new DescribeCapture("postgres");
        capture.Add(new CaptureCall("ask", CaptureJson.ToElement(Question()), document.RootElement.Clone()));
        var exchange = new ReplayingExchange(capture);

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () => await AskAsync(exchange, Question()));

        thrown.Message.ShouldStartWith("The capture's answer to 'ask' cannot be read as FakeOpenAnswer: " + reason);
        _live.ShouldBe(0);
    }

    // The two shapes fit: what is replayed can be recorded again, and is the same file.
    [Fact]
    public async Task AskAsync_CapturedWhileReplaying_GivesTheCaptureAgain()
    {
        var told = new FakeDescriber();
        told.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var first = new DescribeCapture("postgres");
        _ = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, first));
        var second = new DescribeCapture("postgres");

        _ = await DescribeAsync(new FakeDescriber(), new CapturingExchange(new ReplayingExchange(first), second));

        CaptureJson.Write(second).ShouldBe(CaptureJson.Write(first));
    }
}
```

- [ ] **Step 2: Run the build to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL, `CS0246`: `ReplayingExchange` and `CaptureMissException` could not be found.

- [ ] **Step 3: Write the exception**

Create `src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs`:

```csharp
using System;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// A describer under replay asked a question that its capture does not answer: one the capture does not hold, one
/// whose answers were all given, or one whose answer is not of the shape the describer reads.
/// </summary>
/// <remarks>
/// <para>
/// It is a bug or a stale capture, never something a server or a user can cause.  Under a run of the tool it ends
/// the run with <c>SQLSRC200</c>, as any exception of a describer does; under a test it fails the test with the
/// question in its message, so that a change to a describer's calls is a visible update of its fixtures.
/// </para>
/// <para>
/// It derives from <see cref="InvalidOperationException" /> and has the standard constructors for the reasons
/// <c>DescriberFaultException</c> gives: S3871, CA1515 and CA1032.
/// </para>
/// </remarks>
internal sealed class CaptureMissException : InvalidOperationException
{
    public CaptureMissException()
        : this("The capture does not answer a question that was asked.") { }

    public CaptureMissException(string message)
        : base(message) { }

    public CaptureMissException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>The capture holds no call of the method with the request, or every one was given out.</summary>
    /// <param name="method">The name of the question.</param>
    /// <param name="request">The request's JSON, as <see cref="CaptureJson.KeyOf" /> gives it.</param>
    public static CaptureMissException NoAnswer(string method, string request) =>
        new($"The capture has no further answer to '{method}' for the request {request}.");

    /// <summary>The capture holds an answer that cannot be read as the type the describer asked for.</summary>
    public static CaptureMissException Unreadable(string method, Type answer, string reason) =>
        new($"The capture's answer to '{method}' cannot be read as {answer.Name}: {reason}");
}
```

- [ ] **Step 4: Write the exchange**

Create `src/SqlSource.Tool/Describing/Capture/ReplayingExchange.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// The exchange that replays: it answers each question from a capture and never asks the engine, so a describer
/// gives the description it gave when the capture was recorded, with no database.
/// </summary>
/// <remarks>
/// <para>
/// A call is found by its method and by its request as <see cref="CaptureJson.KeyOf" /> writes it.  A question the
/// capture holds more than once gets its answers in the capture's order, one each time it is asked.  A question
/// with no answer left is a <see cref="CaptureMissException" />.
/// </para>
/// <para>
/// A capture that holds more than was asked is no error: <see cref="Unused" /> says what was left.  An answer that
/// lacks a member of its record is read with the member's default, since a capture may be older than the record.
/// </para>
/// </remarks>
internal sealed class ReplayingExchange : IDescribeExchange
{
    private readonly List<Recorded> _calls;

    public ReplayingExchange(DescribeCapture capture)
    {
        _calls = [.. capture.Calls.Select(static call => new Recorded(call, CaptureJson.KeyOf(call.Request)))];
    }

    /// <summary>The calls of the capture that were not asked, in the capture's order.</summary>
    public IReadOnlyList<CaptureCall> Unused =>
        [.. _calls.Where(static recorded => !recorded.Given).Select(static recorded => recorded.Call)];

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        var key = CaptureJson.KeyOf(CaptureJson.ToElement(request));
        var recorded =
            _calls.Find(candidate =>
                !candidate.Given
                && string.Equals(candidate.Call.Method, method, StringComparison.Ordinal)
                && string.Equals(candidate.Key, key, StringComparison.Ordinal)
            ) ?? throw CaptureMissException.NoAnswer(method, key);
        recorded.Given = true;
        return Task.FromResult(Read<TAnswer>(method, recorded.Call.Answer));
    }

    private static TAnswer Read<TAnswer>(string method, JsonElement answer)
    {
        try
        {
            return answer.Deserialize<TAnswer>(CaptureJson.Options) is { } read
                ? read
                : throw CaptureMissException.Unreadable(method, typeof(TAnswer), "it is null");
        }
        catch (JsonException exception)
        {
            throw CaptureMissException.Unreadable(method, typeof(TAnswer), exception.Message);
        }
    }

    // A call of the capture with its key, and whether its answer was given out.
    private sealed class Recorded(CaptureCall call, string key)
    {
        public CaptureCall Call => call;

        public string Key => key;

        public bool Given { get; set; }
    }
}
```

In `src/SqlSource.Tool/Describing/Capture/DescribeCapture.cs`, turn `<c>CapturingExchange</c>` and `<c>ReplayingExchange</c>` into `<see cref="CapturingExchange" />` and `<see cref="ReplayingExchange" />`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Capture.ReplayingExchangeTests"`
Expected: PASS, 12 tests.

- [ ] **Step 6: Format and validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 7: Commit**

```bash
git add src/SqlSource.Tool/Describing/Capture tests/SqlSource.Tool.Tests/Describing/Capture
```

```bash
git commit -m "Add the exchange that answers a describer from a capture

A call is found by its method and its request as JSON, a question asked twice
gets its answers in the capture's order, and a question with no answer left
fails with the question in the message.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The fixture cases and their replay

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCase.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCases.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplayTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`

**Interfaces:**
- Consumes: Tasks 2 to 5; `SqlFileReader.Read(AdditionalText, string, SqlDialectChoice, string?, bool, CancellationToken)`; `SqlFileText(path)`; `SqlDialectName.TryParse` and `Canonical`; `SqlQueryHash.Compute(dialect, segments, tokens, parameters)`; `SampleSql.Build(SqlQuery)`; `EntryBuilder.Build(PlannedQuery, SqlDialect, string, ServerInfo, QueryDescription)`; `SidecarWriter.Write(Sidecar)`.
- Produces, in `SqlSource.Tool.Tests.Fixtures`:
  - `sealed record FixtureCase(string Engine, string Name, string Folder)` with `QueryPath`, `Query`, `Capture`, `Expected` and the constants `QueryFile`, `CaptureFile`, `ExpectedFile`
  - `static class FixtureCases` with `Root`, `Of(string engine)` and `Of(string engine, string root)`, each an `IReadOnlyList<FixtureCase>`
  - `static class FixtureReplay` with `ToolVersion = "fixture"`, `Connection = "replay"`, `Task<string> DescribeAsync(FixtureCase, Func<FixtureCase, IQueryDescriber>)` and `Task<string> DescribeThroughAsync(FixtureCase, Func<FixtureCase, IQueryDescriber>, string connection, IDescribeExchange)`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplayTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The harness of a describer's fixtures, proven with the describer of the tests: a case is three files in a
// folder, and replaying its capture gives the text of its expected sidecar.
public sealed class FixtureReplayTests : IDisposable
{
    private const string Query =
        "-- name: GetUser\n"
        + "-- param: @id int4 not null\n"
        + "SELECT id, tags, mood FROM users WHERE id = @id {{filter:AND active}};\n";

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // A case of postgres under the temporary folder, with its query.sql.
    private FixtureCase Case(string name = "typed", string query = Query)
    {
        _ = _folder.WriteFile($"postgres/{name}/{FixtureCase.QueryFile}", query);
        return new FixtureCase("postgres", name, _folder.PathOf($"postgres/{name}"));
    }

    private void Write(FixtureCase fixture, string file, string text) =>
        _ = _folder.WriteFile($"{fixture.Engine}/{fixture.Name}/{file}", text);

    // Describes the case through a describer that knows the query, and writes the two files a recording writes.
    private async Task<FakeDescriber> RecordAsync(FixtureCase fixture)
    {
        var describer = new FakeDescriber();
        describer.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var capture = new DescribeCapture("postgres");

        var expected = await FixtureReplay.DescribeThroughAsync(
            fixture,
            _ => describer,
            Secrets.ConnectionWithMarker,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(capture));
        Write(fixture, FixtureCase.ExpectedFile, expected);
        return describer;
    }

    [Fact]
    public void Case_ItsThreeFiles_AreReadFromItsFolder()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, "a capture");
        Write(fixture, FixtureCase.ExpectedFile, "a sidecar");

        fixture.Query.ShouldBe(Query);
        fixture.Capture.ShouldBe("a capture");
        fixture.Expected.ShouldBe("a sidecar");
        fixture.ToString().ShouldBe("postgres/typed");
    }

    [Fact]
    public void Of_EngineWithNoFolder_IsEmpty() => FixtureCases.Of("no-such-engine").ShouldBeEmpty();

    [Fact]
    public void Of_EngineWithCases_GivesThemInNameOrder()
    {
        _ = Case("b");
        _ = Case("a");
        _ = _folder.WriteFile("postgres/schema.sql", "CREATE TABLE users ();\n");

        var cases = FixtureCases.Of("postgres", _folder.Path);

        cases.Select(found => found.Name).ShouldBe(["a", "b"]);
        cases.ShouldAllBe(found => found.Engine == "postgres");
        cases[0].Folder.ShouldBe(_folder.PathOf("postgres/a"));
    }

    [Fact]
    public async Task DescribeAsync_CaseRecordedFromADescriber_ReplaysToTheSameText()
    {
        var fixture = Case();
        var recorded = await RecordAsync(fixture);
        var untold = new FakeDescriber();

        var replayed = await FixtureReplay.DescribeAsync(fixture, _ => untold);

        replayed.ShouldBe(fixture.Expected);
        replayed.ShouldContain("\"fixture\"");
        replayed.ShouldContain("\"16.4\"");
        replayed.ShouldContain("_text");
        untold.Opened.ShouldBeEmpty();
        untold.Described.ShouldBeEmpty();
        var asked = recorded.Described.ShouldHaveSingleItem();
        asked.Sql.ShouldContain("WHERE id = @id AND active");
        asked.Parameters.ShouldHaveSingleItem().Type.ShouldBe("int4");
    }

    [Fact]
    public async Task DescribeAsync_CaseRecordedWithAConnection_HoldsNothingOfIt()
    {
        var fixture = Case();
        var recorded = await RecordAsync(fixture);

        recorded.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        fixture.Capture.ShouldNotContain(Secrets.Marker);
        fixture.Expected.ShouldNotContain(Secrets.Marker);
    }

    // Nothing opens under a replay: the connection is one fixed word, and the exchange is the replaying one.
    [Fact]
    public async Task DescribeAsync_Replay_OpensWithTheConstantConnectionAndAReplayingExchange()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        FixtureCase? madeFor = null;
        OpenRequest? seen = null;

        _ = await FixtureReplay.DescribeAsync(
            fixture,
            given =>
            {
                madeFor = given;
                return new Probe(new FakeDescriber(), request => seen = request);
            }
        );

        madeFor.ShouldBe(fixture);
        var request = seen.ShouldNotBeNull();
        request.Connection.ShouldBe(FixtureReplay.Connection);
        FixtureReplay.Connection.ShouldBe("replay");
        request.Database.ShouldBe("postgres");
        request.Exchange.ShouldBeOfType<ReplayingExchange>();
    }

    [Fact]
    public async Task DescribeAsync_CaptureWithAChangedAnswer_GivesAnotherText()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        Write(
            fixture,
            FixtureCase.CaptureFile,
            fixture.Capture.Replace(
                "\"internalName\": \"int4\"",
                "\"internalName\": \"int8\"",
                StringComparison.Ordinal
            )
        );

        var replayed = await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber());

        replayed.ShouldNotBe(fixture.Expected);
        replayed.ShouldContain("int8");
    }

    // A change to what a describer asks is a visible update of its fixtures and never a silent pass.
    [Fact]
    public async Task DescribeAsync_QueryThatChangedSinceItWasRecorded_IsACallTheCaptureLacks()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        Write(fixture, FixtureCase.QueryFile, Query.Replace("AND active", "AND NOT active", StringComparison.Ordinal));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldContain("'describe'");
        thrown.Message.ShouldContain("AND NOT active");
    }

    [Fact]
    public async Task DescribeAsync_CaptureOfAnotherEngine_FailsNamingBoth()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(new DescribeCapture("mssql")));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldBe("The capture of the case postgres/typed is of the engine 'mssql'.");
    }

    [Fact]
    public async Task DescribeAsync_CaptureThatCannotBeRead_FailsNamingTheCase()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, "{");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldStartWith("The capture of the case postgres/typed cannot be read.  The capture is not JSON");
    }

    [Theory]
    [InlineData("")]
    [InlineData("-- name: One\nSELECT 1;\n\n-- name: Two\nSELECT 2;\n")]
    [InlineData("-- dialect: mssql\n-- name: One\nSELECT 1;\n")]
    public async Task DescribeAsync_QueryFileThatIsNotOneQuery_FailsNamingTheCase(string query)
    {
        var fixture = Case("odd", query);
        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(new DescribeCapture("postgres")));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.ShouldNotBeOfType<CaptureMissException>();
        thrown.Message.ShouldContain("query.sql of the case postgres/odd");
    }

    // A describer that shows the request it is opened with, and hands it on.  Under a replay FakeDescriber keeps
    // no request: its "live" never runs.
    private sealed class Probe(IQueryDescriber inner, Action<OpenRequest> opened) : IQueryDescriber
    {
        public SqlDialect Dialect => inner.Dialect;

        public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            OpenRequest request,
            IRunLog log,
            CancellationToken cancellationToken
        )
        {
            opened(request);
            return inner.OpenAsync(request, log, cancellationToken);
        }
    }
}
```

- [ ] **Step 2: Run the build to see it fail**

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: FAIL, `CS0246`: `FixtureCase`, `FixtureCases` and `FixtureReplay` could not be found.

- [ ] **Step 3: Write the case and the list of cases**

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureCase.cs`:

```csharp
using System.IO;

namespace SqlSource.Tool.Tests.Fixtures;

// One case of a describer's fixtures: a folder with a query, the capture of describing it and the sidecar
// expected of it.  An optional issue.md beside them says where the case came from.  Each text is read when it is
// asked for, so a test that writes a file sees what it wrote.
internal sealed record FixtureCase(string Engine, string Name, string Folder)
{
    // A complete SqlSource file with exactly one query, parsed as the tool parses a project's file.
    public const string QueryFile = "query.sql";

    // The calls of describing the query, as CaptureJson writes them.
    public const string CaptureFile = "capture.json";

    // A sidecar with the one entry, as SidecarWriter writes it, with the toolVersion "fixture".
    public const string ExpectedFile = "expected.json";

    public string QueryPath => Path.Combine(Folder, QueryFile);

    public string Query => File.ReadAllText(QueryPath);

    public string Capture => File.ReadAllText(Path.Combine(Folder, CaptureFile));

    public string Expected => File.ReadAllText(Path.Combine(Folder, ExpectedFile));

    // How a failure names the case.
    public override string ToString() => $"{Engine}/{Name}";
}
```

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureCases.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SqlSource.Tool.Tests.Fixtures;

// The cases of Fixtures/Describers/<engine>/, which the build copies to the output.  A folder under the engine's
// is a case; a file there, the engine's schema.sql for one, is none.
internal static class FixtureCases
{
    // Where the build puts the cases.
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Describers");

    // The cases of an engine in the order of their names.  An engine with no folder has none.
    public static IReadOnlyList<FixtureCase> Of(string engine) => Of(engine, Root);

    // The same under another root, for a test that writes its cases.
    public static IReadOnlyList<FixtureCase> Of(string engine, string root)
    {
        var folder = Path.Combine(root, engine);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return
        [
            .. Directory
                .GetDirectories(folder)
                .Select(path => new FixtureCase(engine, Path.GetFileName(path), path))
                .OrderBy(static found => found.Name, StringComparer.Ordinal),
        ];
    }
}
```

- [ ] **Step 4: Write the replay**

Create `tests/SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// Describes the query of a case as a run does, and gives the text of the sidecar that holds its one entry.
// DescribeAsync answers from the case's capture, with no database.  What is wrong with a case fails with the
// case's name.
internal static class FixtureReplay
{
    // The toolVersion of an expected.json: a fixed one, so that a new version of the tool touches no fixture.
    public const string ToolVersion = "fixture";

    // The connection of a replayed session.  Nothing opens, and the run's redaction wants one value to look for.
    public const string Connection = "replay";

    // The text for the case, answered from its capture.  The describer is made for the case, so that a describer
    // can read a setting of the case.
    public static Task<string> DescribeAsync(FixtureCase fixture, Func<FixtureCase, IQueryDescriber> describer)
    {
        var read = CaptureJson.Read(fixture.Capture);
        if (read.Value is not { } capture)
        {
            throw new InvalidOperationException($"The capture of the case {fixture} cannot be read.  {read.Failure}");
        }

        return string.Equals(capture.Engine, fixture.Engine, StringComparison.Ordinal)
            ? DescribeThroughAsync(fixture, describer, Connection, new ReplayingExchange(capture))
            : throw new InvalidOperationException(
                $"The capture of the case {fixture} is of the engine '{capture.Engine}'."
            );
    }

    // The same steps over any exchange and connection: a capturing one records the case.
    public static async Task<string> DescribeThroughAsync(
        FixtureCase fixture,
        Func<FixtureCase, IQueryDescriber> describer,
        string connection,
        IDescribeExchange exchange
    )
    {
        var token = TestContext.Current.CancellationToken;
        var (planned, dialect) = Plan(fixture, token);
        var query = planned.Query;
        var database =
            planned.Database ?? throw new InvalidOperationException($"The case {fixture} has no database.");

        var opened = await describer(fixture)
            .OpenAsync(
                new OpenRequest(database, connection, exchange, TimeProvider.System),
                NullRunLog.Instance,
                token
            );
        if (opened.Value is not { } session)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {fixture}: {opened.Failure?.Descriptor.Id}."
            );
        }

        await using (session)
        {
            var described = await session.DescribeAsync(
                new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters),
                token
            );
            if (described.Value is not { } description)
            {
                throw new InvalidOperationException(
                    $"The query of the case {fixture} was not described: {described.Failure?.Descriptor.Id}."
                );
            }

            var entry = EntryBuilder.Build(planned, dialect, database, session.Server, description);
            return SidecarWriter.Write(
                new Sidecar(SidecarFormat.Version, ToolVersion, new EquatableArray<SidecarEntry>([entry]))
            );
        }
    }

    // The one query of query.sql as the plan of a run holds it: read by the engine's dialect, with the hash of its
    // SQL, for the database its markers name or else the one named as the dialect.
    private static (PlannedQuery Query, SqlDialect Dialect) Plan(FixtureCase fixture, CancellationToken token)
    {
        if (!SqlDialectName.TryParse(fixture.Engine, out var engine))
        {
            throw new InvalidOperationException($"The engine of the case {fixture} is no dialect.");
        }

        var parsed = SqlFileReader.Read(
            new SqlFileText(fixture.QueryPath),
            fixture.QueryPath,
            engine,
            invalidDialect: null,
            commentsWanted: false,
            token
        );
        if (parsed.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"{FixtureCase.QueryFile} of the case {fixture} cannot be parsed: {parsed.Errors[0].Descriptor.Id}."
            );
        }

        if (parsed.Queries.Count != 1)
        {
            throw new InvalidOperationException(
                $"{FixtureCase.QueryFile} of the case {fixture} holds {parsed.Queries.Count} queries, and a case has one."
            );
        }

        if (parsed.Dialect != engine.Dialect)
        {
            throw new InvalidOperationException(
                $"{FixtureCase.QueryFile} of the case {fixture} is read as '{SqlDialectName.Canonical(parsed.Dialect)}'."
            );
        }

        var query = parsed.Queries[0];
        return (
            new PlannedQuery(
                query,
                NeedsEntry: true,
                query.Markers.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters)
            ),
            parsed.Dialect
        );
    }
}
```

`SqlFileReader` is in `SqlSource.Generation` and `SqlQueryHash` in `SqlSource.Parsing`.  If `Xunit` clashes with a name here, take the token as a parameter of the two public methods instead and pass `TestContext.Current.CancellationToken` from the tests.

- [ ] **Step 5: Copy the fixture folder to the output**

In `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, after the line `<None Include="../../global.json" Link="Fixtures/global.json" CopyToOutputDirectory="PreserveNewest" />`, add:

```xml
        <!--
            Fixtures/Describers holds the cases of the describers, under a folder for each engine: a query, the
            capture of describing it and the sidecar expected of it.  Fixtures/FixtureCases.cs finds them in the
            output.  The first case comes with the first describer.
        -->
        <None Remove="Fixtures/Describers/**" />
        <None Include="Fixtures/Describers/**" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Fixtures.FixtureReplayTests"`
Expected: PASS, 13 tests.

If `DescribeAsync_QueryFileThatIsNotOneQuery_FailsNamingTheCase` fails for the empty file with zero queries and no error, the message of the count already names the case and the test holds; if it fails for the `-- dialect: mssql` file, read what `parsed.Dialect` is and mend `Plan`, not the test.

- [ ] **Step 7: Run every test of the tool**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.

- [ ] **Step 8: Format and validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 9: Commit**

```bash
git add tests/SqlSource.Tool.Tests
```

```bash
git commit -m "Add the harness that replays a describer's fixture case to a sidecar

A case is a folder with a query, a capture and the sidecar expected of it.
The harness parses the query as a run does, answers the describer from the
capture, and gives the sidecar's text.  The folder of the cases is copied to
the output; the first case comes with the first describer.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The documents, and the outline's row set to Done

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md` (the bullets that start `**`Describing/Describers/` is the contract` and `**Every call a describer makes`)
- Modify: `src/SqlSource.Tool/Describing/Describers/IDescribeExchange.cs` (its summary and remarks)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:71`

**Interfaces:**
- Consumes: the names of Tasks 2 to 6.
- Produces: nothing a later task uses.

- [ ] **Step 1: Say what the seam now is, in the interface**

In `src/SqlSource.Tool/Describing/Describers/IDescribeExchange.cs`, replace the summary and remarks of the interface with:

```csharp
/// <summary>
/// What every call a describer makes to an engine goes through, so that a run can be recorded and replayed.  It
/// has three shapes: <see cref="LiveExchange" /> asks the engine, <c>CapturingExchange</c> asks another exchange
/// and keeps each call, and <c>ReplayingExchange</c> answers from what was kept.
/// </summary>
/// <remarks>
/// A request and an answer are plain records that <c>CaptureJson.Options</c> writes and reads, and a call that can
/// fail on the server returns the failure as its answer.  No exchange looks at either beyond writing it.  The two
/// other shapes are named and not linked: this folder uses nothing of <c>Describing/Capture/</c>.
/// </remarks>
```

- [ ] **Step 2: Rewrite the two bullets of the tool's guidance**

In `src/SqlSource.Tool/AGENTS.md`, replace the bullet that starts ``- **`Describing/Describers/` is the contract a describer implements**`` with:

```markdown
- **`Describing/Describers/` is the contract a describer implements**: `IQueryDescriber`, `IDescribeSession`, `IDescribeExchange`, and the requests, failures and server information they exchange, with each result an `Outcome` of a `DescribeFailure`.  It uses nothing of the run's steps, only `Outcome`, `Reporting/` and `Logging/`, so a describer of phase 3 depends on no step of the run.  A describer's clock is `OpenRequest.Time`, which `Describing/DatabaseRuns.cs` sets from `ToolHost.Time`: a describer reads no `DateTime` and no `Stopwatch`.
```

Replace the bullet that starts `- **Every call a describer makes to an engine goes through `IDescribeExchange`**` with these five:

```markdown
- **Every call a describer makes to an engine goes through `IDescribeExchange`**, the one it is given in its `OpenRequest`.  The exchange has three shapes: `Describing/Describers/LiveExchange.cs` asks the engine and keeps nothing; `Describing/Capture/CapturingExchange.cs` asks another exchange and adds each call that answered to a `DescribeCapture`; `Describing/Capture/ReplayingExchange.cs` answers from a capture and never asks.  A replay finds a call by its method and by its request as `CaptureJson.KeyOf` writes it, gives the answers of a question asked twice in the capture's order, and throws `CaptureMissException` for a question with no answer left, so that a describer whose calls changed fails its fixtures and never passes silently.  A capture that holds more than was asked is no error; `ReplayingExchange.Unused` says what was left.  `ToolHost.Exchange` gives the live one; nothing in the released tool records yet.
- **A request and an answer are plain records, and nothing of the seam crosses the exchange.**  A record of a call is sealed and holds strings, integers, booleans, nullable ones, `IReadOnlyList<T>` of them and records of the same kind: no driver type, no `DateTime`, no `Exception`, no enum, no abstract base, and never an `Outcome`, a `DescribeFailure`, a `QueryDescription` or an `EquatableArray`, none of which System.Text.Json reads back.  A describer maps between its records and the seam's types on its own side.  A call that can fail on the server has an answer with an `error` member that is null on success: the failure is data, and a replay gives it again.  A request holds everything that decides the answer, and nothing of a connection: not the value, a part of it, the host or the user's name, so the request of the call that opens a session is empty.  The one exception is the driver's own message in an `error`, which `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md` records.  A name that a describer makes up and sends, a prepared statement's for one, is a counter within its session and never random: it is part of a request.  `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` holds every record whose name ends in `Request` or `Answer`, in the namespace of an engine's describer and in that of the tests, to the rule; a describer in a new namespace adds it to `Namespaces` there.
- **A capture is a file that outlives the version that wrote it.**  `Describing/Capture/CaptureJson.cs` writes it indented, in UTF-8, with `\n` line ends on every platform, and reads it into an `Outcome` whose failure is a sentence.  It is the `capture.json` of a fixture and, from phase 10, of a user's bug bundle, so the name of a method and the members of a record are added to and never renamed: an answer that lacks a member is read with the member's default.  `DescribeCapture.Version` rises only when a capture of the version before can no longer be read, and the reader names a version it does not know.  `CaptureJson.Options` is the one set of options for a request and an answer; the key of a call depends on it, so a change to it changes every fixture.
- **`tests/SqlSource.Tool.Tests/FakeDescriber.cs` is a describer written by these rules**, and the tests of the exchanges and of the fixture harness run through it.  Its records are in `tests/SqlSource.Tool.Tests/FakeDescriberCalls.cs` and its maps in `tests/SqlSource.Tool.Tests/FakeCalls.cs`; it carries `PostgresType` and `OtherEngineType` and throws for a `SqlServerType`.  Under a replay its `live` functions never run, so it keeps nothing in `Opened` and `Described` and its hooks do not fire.  Its one descriptor, `SQLSRC999`, exists in the tests only and is listed in `tests/SqlSource.Tool.Tests/AnalyzerReleases.Unshipped.md`, which that project needs for release tracking: a test that adds another descriptor adds its row there.  `FakeDescriber.RejectedWith` makes one from a text, whose id is no constant and has no row.  Every run of `tests/SqlSource.Tool.Tests/CliRun.cs` records through a `CapturingExchange` into `CliRun.Capture`.
- **The fixtures of a describer are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/<case>/`**, beside the engine's `schema.sql`: `query.sql`, a complete SqlSource file with exactly one query; `capture.json`, the calls of describing it as `CaptureJson` writes them; `expected.json`, a sidecar with the one entry as `SidecarWriter` writes it, with the `toolVersion` `fixture` so that a new version of the tool touches no fixture; and an optional `issue.md`.  The files are compared byte for byte and the SQL of `query.sql` is in a request, so the folder is `text eol=lf` in `.gitattributes` and has a section in `.editorconfig`.  `tests/SqlSource.Tool.Tests/Fixtures/FixtureCases.cs` finds the cases in the output folder, and `Fixtures/FixtureReplay.cs` describes one through a `ReplayingExchange`, with the connection `replay`, and gives the sidecar's text.  The folder is empty until the first describer brings its cases.
```

The first three are the rules, the fourth is the describer of the tests and the fifth the fixtures.  Keep the file's maintenance footer as its last line.

- [ ] **Step 3: Check the names the guidance now holds**

Run:

```bash
for path in src/SqlSource.Tool/Describing/Capture/CapturingExchange.cs src/SqlSource.Tool/Describing/Capture/ReplayingExchange.cs src/SqlSource.Tool/Describing/Capture/CaptureJson.cs src/SqlSource.Tool/Describing/Describers/LiveExchange.cs tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs tests/SqlSource.Tool.Tests/FakeDescriberCalls.cs tests/SqlSource.Tool.Tests/FakeCalls.cs tests/SqlSource.Tool.Tests/Fixtures/FixtureCases.cs tests/SqlSource.Tool.Tests/Fixtures/FixtureReplay.cs docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md; do test -f "$path" || echo "missing: $path"; done
```

Expected: no line.

- [ ] **Step 4: Set the row of 3.1 to Done**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, the row that starts `| 3.1 Capture and replay | In progress |` becomes `| 3.1 Capture and replay | Done |`.

- [ ] **Step 5: Look for what the work left stale**

Run: `grep -rn "RecordingExchange\|phase 3 can record" src tests README.md CONTRIBUTING.md docs/tech-debt docs/deferred`
Expected: no line.  Mend any line it finds in the file that holds it.

- [ ] **Step 6: Validate**

Run: `./format.sh`
Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 7: Commit**

```bash
git add src/SqlSource.Tool/AGENTS.md src/SqlSource.Tool/Describing/Describers/IDescribeExchange.cs docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Document the capture, the three exchanges and the fixtures, and close sub-phase 3.1

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The pull request

**Files:** none.

**Interfaces:**
- Consumes: the seven commits of the branch.

- [ ] **Step 1: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

`VersionPrefix` in `Directory.Build.props` is `0.1.0`.  With no `v*` tag there is nothing to compare.  If a tag exists and `VersionPrefix` is not greater than it, ask the user what the new version is and set it in a commit of its own.

- [ ] **Step 2: Check the branch holds what the plan says**

Run: `git log --oneline main..HEAD`
Expected: seven commits, in the order of Tasks 1 to 7.

Run: `git status --short`
Expected: no line.

- [ ] **Step 3: Ask the user, then push and open the pull request**

Pushing publishes the branch.  Ask the user first.  With a yes:

```bash
git push -u origin claude/query-generation-phase-3-1-capture-and-replay
```

```bash
gh pr create --base main --title "Query generation 3.1: capture and replay" --body "$(cat <<'EOF'
Sub-phase 3.1 of the query generation epic: the two shapes of the exchange that sub-phase 2.5 left out.

- `Describing/Capture/`: `DescribeCapture` and its file (`CaptureJson`), `CapturingExchange`, `ReplayingExchange` and `CaptureMissException`.
- `FakeDescriber` passes only plain records through the exchange, and `CallRecordTests` holds every such record to the rule of a capture.
- `OpenRequest.Time`, the run's clock for a describer.
- `tests/SqlSource.Tool.Tests/Fixtures/`: `FixtureCase`, `FixtureCases` and `FixtureReplay`, proven in memory.  The first cases come with the first describer, in 3.2.

Spec: `docs/superpowers/specs/2026-10-12-query-generation-phase-3-1-capture-and-replay-design.md`.  The plan says where it settles what the spec left open.

No engine is touched and no describer is registered: the released tool behaves as before.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Then follow `AGENTS.md` for the pull request: reply to every CodeRabbit finding in its thread.
