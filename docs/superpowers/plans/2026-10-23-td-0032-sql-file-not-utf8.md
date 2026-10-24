# TD-0032 A `.sql` file that is not UTF-8 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `sqlsource` tool reports a `.sql` file that is not UTF-8, and a `Path` written outside ASCII in a C# file that is not UTF-8, as a new error `SQLSRC271` telling the user to save the file as UTF-8, and TD-0032 is resolved.

**Architecture:** `FileText.Read` returns the text and whether the compiler would fall back to another encoding for the file (no byte order mark, bytes not valid UTF-8).  `SqlFileText` reads its file once and exposes that flag; `RunPlanner` turns it into `SQLSRC271` and a new `PlannedFileState.NotUtf8`; `AttributeReader` turns it into `SQLSRC271` at a `Path` that holds a character outside ASCII.  The generator is unchanged.

**Tech Stack:** C# (.NET 8 tool, netstandard2.0 generator), Roslyn `SourceText`, xunit v3 with Shouldly.

**Spec:** `docs/tech-debt/TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md` (its Problem and Proposed fix), with the second, simpler fix: report the file as an error in the tool.

## Decisions

- **What "not UTF-8" means.**  A file the compiler reads differently from the tool: it has no byte order mark and its bytes are not valid UTF-8.  A file with a mark (UTF-8 `EF BB BF`, UTF-16 `FF FE` or `FE FF`) is decoded by its mark, with replacement characters, by both `SourceText.From` and the compiler, so it is never reported.  The flag is `FileContents.CompilerFallsBack`.
- **Which `.sql` files are reported.**  A claimed file whose compiler falls back is `SQLSRC271` unless it parses without errors and no query of it needs an entry.  A file whose every query has the output `sql` builds correctly in the compiler's encoding and the tool has nothing to describe, so it stays `Ready` and silent: reporting it would fail a run, and `--check` in CI, over a file the build is happy with.  A file with parse errors gets `SQLSRC271` instead of those errors: the tool's reading of it is not the build's, so its parse errors may be ones the build does not have (a `-- name: Café` read as `Caf�` is `SQLSRC103` to the tool alone).
- **Reported once, at the file.**  `ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, path)`, no position, at the first query that needs an entry (or at the parse, for a file with parse errors).  It wins over `SQLSRC209`: a file that is both is `SQLSRC271` alone, and `SQLSRC209` shows once it is saved as UTF-8.  `SQLSRC210` for its tokens is still reported, as for a `NotDescribable` file.
- **The file keeps its queries** (state `NotUtf8`), as a `NotDescribable` file does, so its queries count as failed in the summary and its databases stay in the plan; a file with parse errors has none, as today.  Every consumer that tests `State == Ready` already treats it as not described.
- **The C# side is covered, narrowly.**  In a C# file whose compiler falls back, a `Path` literal that holds any character above U+007F is `SQLSRC271` at the `Path` argument's expression, and the claim is not planned (as for `SQLSRC208`).  Any other C# file that is not UTF-8 is read as today: legacy-encoded C# files are common, and only `Path` changes what the tool does.  For a code page that keeps ASCII as ASCII, every byte above 0x7F decodes to a different character in the two readings, so "a character above U+007F" is exactly "the tool's `Path` differs from the build's".
- **The generator does not report it.**  It never sees the bytes, only the compiler's text: with `CodePage` set the compiler decodes every file in that code page without trying UTF-8, so the text's encoding does not tell whether the bytes were UTF-8, and a check on it would flag ASCII files.  The build already fails for exactly the affected queries, with a missing (`SQLSRC241`) or stale (`SQLSRC244`) entry whose fix is to run the tool, and the tool now names the cause.  A file whose queries need no entry builds correctly today and must keep building.
- **Id.**  `SQLSRC271` (SQLSRC270 is the last taken on main; td-0021 took `SQLSRC120`, outside this range).  Re-check `src/SqlSource/AGENTS.md` and `AnalyzerReleases.Unshipped.md` after the last sync before the push.
- **Copy.**  Title `File is not saved as UTF-8`.  Message `'{0}' is not saved as UTF-8, so the tool and the build read different text from it.  Save it as UTF-8.`
- **Log.**  `PlannedFileState.NotUtf8` is `not-utf-8` in the log and `is not saved as UTF-8` in the verbose line.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` on its exact tree.
- A new diagnostic: descriptor written out in full with a literal id, `AnalyzerReleases.Unshipped.md` row, `ToolDiagnostics.All` in id order, a section and a table row in `docs/manual/diagnostics.md` (the `DiagnosticsDocumentTests` hold these together), and the id range in `src/SqlSource/AGENTS.md`.
- User docs describe effects, never internals; no tech-debt id in `docs/manual/`.
- Tests have three-part names (`Subject_Condition_Result`), Shouldly assertions.

## Review Focus

1. A UTF-16 file with a byte order mark: valid, never reported (FileTextTests, Task 1).
2. A UTF-8 file with a byte order mark and an invalid byte: compiler and tool both replace, never reported (FileTextTests, Task 1).
3. A file the system will not let the tool read: no contents, not `CompilerFallsBack`; the planner's existing "parsed as empty" path is unchanged (existing `Plan_ClaimedFileThatIsNotOnTheDisk_...`, Task 3).
4. A Windows-1252 C# file with `é` in a comment and an ASCII `Path`: still a claim, no error (AttributeReaderTests, Task 4).
5. A Windows-1252 `.sql` file at output `sql`: `Ready`, no error, so a solution with such a file still runs clean (RunPlannerErrorTests, Task 3).

---

### Task 1: `FileText` says when the compiler falls back

**Files:**
- Create: `src/SqlSource.Tool/Planning/FileContents.cs`
- Modify: `src/SqlSource.Tool/Planning/FileText.cs`, `src/SqlSource.Tool/Planning/SqlFileText.cs`, `src/SqlSource.Tool/Planning/AttributeReader.cs:46` (read `.Text`, no behaviour change yet)
- Test: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs`

**Interfaces:**
- Produces: `internal sealed record FileContents(SourceText Text, bool CompilerFallsBack)`; `FileText.Read(string path) : FileContents?` (null when the system does not let the file be read, as today); `SqlFileText.CompilerFallsBack : bool` (false when the file cannot be read); `SqlFileText.GetText` returns the text of a single read, cached for the instance.

- [ ] **Step 1: Write the failing tests.**  Adjust the existing `Read_SqlFile_...` test to `.Text.ToString()`.  Add:
  - `Read_Utf8WithoutMark_IsTheTextAndTheCompilerDoesNotFallBack`: bytes `Encoding.UTF8.GetBytes("SELECT 'café';")` → `Text.ToString()` is `"SELECT 'café';"`, `CompilerFallsBack` false.
  - `Read_Utf16WithMark_IsTheTextAndTheCompilerDoesNotFallBack`: `Encoding.Unicode.GetPreamble()` followed by `Encoding.Unicode.GetBytes("SELECT 'café';")` → same text, false.
  - `Read_Utf8MarkThenAByteThatIsNotUtf8_TheCompilerDoesNotFallBack`: `EF BB BF` then `Encoding.Latin1.GetBytes("SELECT 'café';")` → false.
  - `Read_Windows1252_HasAReplacementCharacterAndTheCompilerFallsBack`: `Encoding.Latin1.GetBytes("SELECT 'café';")` → text `"SELECT 'caf�';"`, true.
  - `SqlFileText_FileChangedAfterTheFirstRead_GivesTheFirstText` (in the same class): write, `GetText()`, overwrite, `GetText()` again → first text both times.
- [ ] **Step 2: Run them and see them fail.**  `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Planning.FileTextTests"` → build error on `CompilerFallsBack`.
- [ ] **Step 3: Implement.**  `FileText.Read` reads all bytes (same `IOException`/`UnauthorizedAccessException` catch), builds the text with `SourceText.From(bytes, bytes.Length)` (byte order mark decides, else UTF-8 with replacement, as today), and sets `CompilerFallsBack` to "no UTF-8 or UTF-16 byte order mark, and `!System.Text.Unicode.Utf8.IsValid(bytes)`".  Its summary says what the flag means and why a marked file is never one.  `SqlFileText` holds a `Lazy<FileContents?>`; its remark drops the TD-0032 sentence and says a file the compiler falls back for is the planner's `SQLSRC271`.  `AttributeReader` reads `contents.Text`.
- [ ] **Step 4: Run the class again; all pass.**
- [ ] **Step 5: `./pre-commit-validation.sh`, then commit** `Tell when the compiler reads a file in another encoding (TD-0032)`.

### Task 2: The diagnostic `SQLSRC271`

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (descriptor `FileNotUtf8` after `ColumnHasNoName`, and last in `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, `src/SqlSource/AGENTS.md` (the id-range rule: `SQLSRC200` to `SQLSRC271` taken, later work from `SQLSRC272`)
- Test: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Produces: `ToolDiagnostics.FileNotUtf8` (id `SQLSRC271`, error, `NotConfigurable`, help link `sqlsrc271`, title and message from Decisions; `{0}` is the file's full path).

- [ ] **Step 1: Write the failing test** `FileNotUtf8_Message_HoldsTheFile`: formatting with `"/work/App/Queries/Users.sql"` gives `"'/work/App/Queries/Users.sql' is not saved as UTF-8, so the tool and the build read different text from it.  Save it as UTF-8."`
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-class "SqlSource.Tests.Diagnostics.ToolDiagnosticsTests"` → fails to build.
- [ ] **Step 3: Add the descriptor, the release row** (`SQLSRC271 | SqlSource | Error | File is not saved as UTF-8`) **and the manual.**  In `diagnostics.md`: the table row after `SQLSRC270`; the intro's tool range becomes "Ids from 200 to 236, 262 to 266, and 271"; a `## SQLSRC271` section after `SQLSRC270`'s, starting `**File is not saved as UTF-8**`, saying: the tool reports a `.sql` file saved in another encoding, such as Windows-1252, when a query of it needs an entry or the tool cannot read it as SQL, and a C# file in another encoding whose `[SqlSourceGenerate]` has a `Path` with a character outside ASCII; the build reads such a file in another encoding than the tool, so the two would see different text; the tool describes no query of the file (or plans no type of the C# file) until it is saved as UTF-8; the build does not report it itself, and reports the queries' entries as missing ([SQLSRC241](#sqlsrc241)) or out of date ([SQLSRC244](#sqlsrc244)); a file whose queries all have the output `sql` is not reported.  A `console` sample in the format other file-level tool errors use on the page, then the fix: save the file as UTF-8 (any editor's "Save with encoding").  Add one sentence to the `SQLSRC244` section: when the tool reports `SQLSRC271` for the file, saving it as UTF-8 is the fix.
- [ ] **Step 4: Run** `ToolDiagnosticsTests` and `DiagnosticsDocumentTests`; all pass.
- [ ] **Step 5: `./pre-commit-validation.sh`, then commit** `Add SQLSRC271, a file that is not saved as UTF-8`.

### Task 3: The planner reports a `.sql` file that is not UTF-8

**Files:**
- Modify: `src/SqlSource.Tool/Planning/PlannedFileState.cs` (add `NotUtf8` last), `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/Logging/RunEvents.cs` (`StateOf`, `StateInWords`), `docs/architecture/tool.md` (the plan section: the states sentence and the per-query paragraph; the `AssignDatabases` comment's "SQLSRC209 or a parse error" gains `SQLSRC271`)
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs` (beside the `has-parse-errors` test near line 583)

**Interfaces:**
- Consumes: `SqlFileText.CompilerFallsBack` (Task 1), `ToolDiagnostics.FileNotUtf8` (Task 2).
- Produces: `PlannedFileState.NotUtf8`.

- [ ] **Step 1: Write the failing tests** (Latin-1 bytes through `project.AddSqlBytes`, in the class's `Project()` setup):
  - `Plan_FileNotUtf8WithAQueryThatNeedsAnEntry_IsSqlsrc271AtTheFileAndKeepsItsQueries`: `"-- name: One\nSELECT 'café';\n"` → `Errors` is `[ToolDiagnostic.ForFile(ToolDiagnostics.FileNotUtf8, sql)]`, state `NotUtf8`, one query with `NeedsEntry` true.
  - `Plan_FileNotUtf8WhoseQueriesNeedNoEntry_IsReadyWithNoError`: same file, `SqlSourceOutput=sql` → no errors, `Ready`.
  - `Plan_FileNotUtf8WithAParseError_IsSqlsrc271AloneWithNoQuery`: `"-- name: One\nSELECT 'café\n"` → errors exactly the one `SQLSRC271`, state `NotUtf8`, no queries.
  - `Plan_FileNotUtf8UnderADialectThatCannotBeDescribed_IsSqlsrc271Alone`: `Project(dialect: "ansi")` → only `SQLSRC271`, state `NotUtf8`.
  - In `RunLogTests`: a run over a Latin-1 file logs its `file` event with `state` `not-utf-8` (follow the `has-parse-errors` test).
- [ ] **Step 2: Run** the two classes → the planner tests fail (no error, state `Ready`), the log test fails.
- [ ] **Step 3: Implement.**  `ClaimedFile` gains `SqlFileText Text`, created in `ReadProject` and handed to `ReadMetadata`.  In `PlanFile`, a file whose `Text.CompilerFallsBack` and whose parse has errors adds the one `SQLSRC271` instead of the parse errors and returns state `NotUtf8` with no queries.  In `PlanQuery`, the first query that needs an entry, while the state is `Ready`, makes a fall-back file `NotUtf8` with one `SQLSRC271`; otherwise the existing `SQLSRC209` check runs.  `RunEvents`: `not-utf-8` and `is not saved as UTF-8`.
- [ ] **Step 4: Run** the tool test project in full: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`; all pass.
- [ ] **Step 5: `./pre-commit-validation.sh`, then commit** `Report a .sql file that is not UTF-8 as SQLSRC271 (TD-0032)`.

### Task 4: A `Path` outside ASCII in a C# file that is not UTF-8

**Files:**
- Modify: `src/SqlSource.Tool/Planning/AttributeReader.cs`, `docs/architecture/tool.md` (the claims bullet: such a `Path` is `SQLSRC271` and its claim is not planned)
- Test: `tests/SqlSource.Tool.Tests/Planning/AttributeReaderTests.cs`

**Interfaces:**
- Consumes: `FileText.Read` / `FileContents.CompilerFallsBack` (Task 1), `ToolDiagnostics.FileNotUtf8` (Task 2).

- [ ] **Step 1: Write the failing tests** (a helper writes `Encoding.Latin1.GetBytes(source)` to `Source`):
  - `Read_PathOutsideAsciiInAFileThatIsNotUtf8_IsSqlsrc271AtThePathAndNoClaim`: Latin-1 `OnAClass("[SqlSourceGenerate(Path = \"Requêtes\")]")` → `Errors` is `[ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, Source, new LinePosition(2, PathPrefix.Length), Source)]`, `Claims` empty.
  - `Read_AsciiPathInAFileThatIsNotUtf8_IsAClaim`: Latin-1 `"// café\n" + OnAClass("[SqlSourceGenerate(Path = \"Q\")]")` → no errors, one claim with path `Q`.
  - `Read_PathOutsideAsciiInUtf8_IsAClaimWithThatPath`: UTF-8 `Requêtes` → no errors, claim path `Requêtes` (skip if an existing test already pins it).
- [ ] **Step 2: Run** the class → the first fails.
- [ ] **Step 3: Implement.**  `Read` keeps the `FileContents` and passes `CompilerFallsBack` to `ReadAttribute`; after `TryReadPath` succeeds, a path holding a character above U+007F in such a file adds `ToolDiagnostic.At(ToolDiagnostics.FileNotUtf8, path, StartOf(argument.Expression), path)` and marks the claim unread.  The class remark says so in one sentence.
- [ ] **Step 4: Run** the class; all pass.
- [ ] **Step 5: `./pre-commit-validation.sh`, then commit** `Report a Path outside ASCII in a C# file that is not UTF-8 (TD-0032)`.

### Task 5: Resolve TD-0032

**Files:**
- Delete: `docs/tech-debt/TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md`
- Modify: `docs/tech-debt/README.md` (remove the row), `docs/manual/sql-files.md`

- [ ] **Step 1:** Remove the "A file that is not saved as UTF-8" known issue from `docs/manual/sql-files.md`, and add to the page's opening paragraph one sentence: a `.sql` file is saved as UTF-8, which current editors write, and the tool reports one in another encoding ([SQLSRC271](diagnostics.md#sqlsrc271)).
- [ ] **Step 2:** Delete the item and its row.  `grep -rn "TD-0032" src tests docs --exclude-dir=superpowers` prints nothing (plans and specs under `docs/superpowers` are the record and keep it).
- [ ] **Step 3: `./pre-commit-validation.sh`, then commit** `Resolve TD-0032`.
