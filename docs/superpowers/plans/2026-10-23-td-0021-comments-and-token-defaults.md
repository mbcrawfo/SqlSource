# TD-0021 Comments and Token Defaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0021: the three ways comments interact with the defaults of tokens without being reported.

**Architecture:** All three stay inside the parse and the type's emitter.  (1) The parser reports a comment that holds `}}` where a token's default is open in the SQL without comments.  (2) `SqlMarkerScope.ReadToken` strips comments from a `-- token:` default with `SqlTextBuilder`, the same builder that strips the SQL, before the default is compared or hashed.  (3) The kept form's own scan errors travel with the query instead of failing the file, and the type's emitter reports them where the type's resolved settings keep comments.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8.0), xunit, Shouldly.

**Spec:** [`docs/tech-debt/TD-0021-comments-and-token-defaults.md`](../../tech-debt/TD-0021-comments-and-token-defaults.md) (Problem and Proposed fix), with [`docs/architecture/generator.md`](../../architecture/generator.md) "A block has two forms of its SQL" and "The settings have two stages".

## Choices

- **Behaviour 1, recommended: report it** (the item's first option), as a new parse error `SQLSRC120`.  The other option, making the lexer read to a token's `}}` before a comment, puts tokens into the lexer, which today knows none (`src/SqlSource/AGENTS.md`: it names no dialect and no marker), adds a search for `{{` to the hot path of every file, and makes a `--` mean something no database reads.  The rule covers every comment, a `/* }} */` too, since a block comment holding `}}` inside a default is read as one token without comments and another with them, the same bug.  **This adds a diagnostic, so it waits for the owner's answer (asked through the manager, 2026-10-23); Task 3 is built only after it.**
- **Behaviour 2 has one fix** (strip a marker's default by the same rules).  No diagnostic, and no SQL changes meaning: comments mean nothing to a database.
- **Behaviour 3, recommended: report where the type's settings are resolved** (the item's first option).  The other, scanning the kept form only for a query that keeps its comments, needs the type's settings in the parse, which the design rules out ("The settings have two stages"), or a scan in every emit of every type.  It adds no diagnostic (it is `SQLSRC114`, reported for fewer cases), so it needs no question.  A type whose query keeps its comments and whose kept form has an error leaves that file out, as `SQLSRC008` and `SQLSRC009` do, so that no type emits from a file with an error for it.

Where merged code differs from the item: none.  The item's "the lexer that decides what a comment is does not know that a token has begun" stays true; the check is after the scan.

No `.sql` file of the repository has a `-- token:` default that holds a comment (`grep -rn --include='*.sql' -E '^\s*--\s*token:.*(/\*|--|#)'` finds none), so no sidecar's hash changes.

## Global Constraints

- `netstandard2.0` only, Roslyn API of 4.8.0, no collection expression targeting `ImmutableArray<T>`.
- Allocate nothing per character or line; a string only where a result needs one.  `SqlFileParserAllocationTests` budgets (12 and 24 bytes per character) hold and are not raised.
- Measure the parse's time and allocation before and after, from a `Stopwatch` loop over a `Release` build, for the PR description.
- Parse errors: a new `SqlParseErrorKind` goes at the end of the enum; a new diagnostic touches `SqlDiagnostics` (descriptor, `All`, `ForParseError`), `AnalyzerReleases.Unshipped.md`, and `docs/manual/diagnostics.md`.
- Comparisons ordinal.  Tests named in three parts (`Subject_Condition_Result`).
- Every commit passes `./pre-commit-validation.sh`.

## Review Focus

- A `--` comment inside a default whose token never closes in the SQL without comments (`{{f:a -- x}}` and no later `}}`): the token vanishes into literal text; it must be reported too (Task 3 test).
- A comment that holds `}}` but stands outside any token (`SELECT 1 -- {{x}}`, `-- see }}`): must not be reported (Task 3 test).
- A marker default whose only content is a comment, `-- token: {{f:/* c */}}`, beside an inline `{{f:/* c */}}`: both are the empty default, no `SQLSRC112` (Task 1 test).
- A parameter inside a marker's default next to a comment, `-- token: {{f:/* c */ @p}}`: `SQLSRC118` still points at `@p` in the file (Task 1 test).
- Two types claim one file, one keeps comments and one does not: only the one that keeps them reports `SQLSRC114` and leaves the file out (Task 2 test).

---

### Task 1: Strip comments from a `-- token:` default

**Files:**
- Modify: `src/SqlSource/Parsing/Tokens/SqlTokenDefault.cs`, `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs` (`ReadToken`), `src/SqlSource/Parsing/Tokens/SqlTokenList.cs`, `src/SqlSource/Parsing/SqlParameterList.cs` (if it reads `Text`)
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`, `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`

**Interfaces:**
- Produces: `SqlTokenDefault(string Name, string Text, string Value, int Offset, SqlMarker Marker)`: `Text` is the default as written (what `Offset` points at; `SqlParameterList` lexes it for parameters), `Value` the default without comments, trimmed; the same string instance as `Text` when the default has no comment.

- [ ] **Step 1: Write the failing tests**
  - `Parse_MarkerDefaultWithACommentBesideTheSameInlineDefault_IsOneDefault`: theory over `a /* c */ b`, `/* c */ a`, `a /* c */`, `/* c */`; text `-- name: Q\n-- token: {{f:<d>}}\nSELECT {{f:<d>}}\n` parses with no error and `Tokens(block)` is `f=<inline default>` (e.g. `f=a   b`, `f=a`, `f=a`, `f=`).
  - `Parse_MarkerDefaultWithALineComment_IsTheDefaultWithoutIt`: `-- token: {{f:a -- c}}` with `SELECT {{f}}` gives `f=a`.
  - `Parse_ParameterInAMarkerDefaultAfterAComment_IsReportedWhereItIsWritten`: `-- name: Q\n-- token: {{f:/* c */ @p}}\nSELECT {{f}}\n` gives `UndeclaredParameter` at `SpanOf(text, "@p")`.
  - In `SqlMarkerScopeTests`: two markers `{{f:a /* x */ b}}` and `{{f:a /* y */ b}}` in one scope are no conflict.
  - In `SqlQueryHashTests`: `Compute_MarkerDefaultsThatDifferOnlyInAComment_AreEqual`.

- [ ] **Step 2: Run them, see them fail** — `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlFileParserTests|FullyQualifiedName~SqlMarkerScopeTests|FullyQualifiedName~SqlQueryHashTests"`.

- [ ] **Step 3: Implement.**  In `ReadToken`, after the existing `SqlLexer.Lex(defaultText, rules)`, keep its lexemes; when one is a `LineComment` or `BlockComment`, `Value` is `SqlText`-trimmed `SqlTextBuilder.Build(defaultText, lexemes, 0, lexemes.Count, keepComments: false).Text`, else `Value = defaultText`.  Compare two markers by `Value`.  `SqlTokenList.Create` uses `Value` for the token's default and the comparison.  `SqlParameterList` keeps `Text` and `Offset`.  Update the `ReadToken` comment and the doc comments of `SqlTokenDefault`.

- [ ] **Step 4: Run the tests and the allocation tests** — same filter plus `SqlFileParserAllocationTests`; all pass.

- [ ] **Step 5: Docs and commit.**  `docs/manual/parameters-and-tokens.md`: drop the known issue "A comment inside a default can make two equal defaults differ", and the clause "Keep comments out of defaults, or two defaults that look the same can differ: see known issues" (line 157); say instead that comments in a default are ignored when defaults are compared, wherever written.  Commit after `./pre-commit-validation.sh`.

### Task 2: Report a kept form's error where the type keeps its comments

**Files:**
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs` (`ResolveSettings`, `BuildKept`, `BuiltQuery`), `src/SqlSource/Parsing/SqlBlock.cs`, `src/SqlSource/Generation/SqlQuery.cs`, `src/SqlSource/Generation/SqlFileReader.cs`, `src/SqlSource/Generation/Emitting/TypeEmitter.cs` (`Emit`, `FindUsableFiles`, `SelectMembers`), `src/SqlSource/Generation/Models/TypeModels.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`, a generator test in `tests/SqlSource.Tests/Generator/FileDiagnosticsTests.cs` (or `GeneratorParameterLevelTests.cs`, wherever keep-comments levels are tested)

**Interfaces:**
- Produces: `SqlBlock.KeptErrors : EquatableArray<SqlParseError>` (last member; the errors of the kept form's scan, spans in the file; `Empty` when none), `SqlQuery.KeptErrors : EquatableArray<DiagnosticInfo>` (last member), `TypeEmitter.FindUsableFiles(TargetType type, EquatableArray<ParsedSqlFile> files, EquatableArray<SettingsLevel> fileSettings, SettingsLevel property, ImmutableArray<DiagnosticInfo>.Builder? diagnostics)`.

- [ ] **Step 1: Write the failing tests**
  - Parser: replace `Parse_ReservedTokenInEitherForm_IsReportedOnce`'s second row and `Parse_ReservedKeywordTokensUnderKeepComments_AreEachReportedOnce` with `Parse_ReservedTokenInAKeptCommentAlone_IsAnErrorOfTheKeptForm`: `-- name: Q\n-- generator: keep-comments\nSELECT 1 -- {{class}}\n` has no errors and its block's `KeptErrors` is `[ReservedTokenName at "{{class}}", "class"]`; the row with `{{class}}` in the SQL stays a file error, and the kept form adds nothing for it (`KeptErrors` of a file with errors is moot: no blocks).
  - Generator: `Run_ReservedTokenInAComment_IsReportedOnlyForATypeThatKeepsComments`: one file `-- name: Q\nSELECT 1 -- {{class}}\n`, the project's generator parameters `keep-comments`, two types claiming it, one with the attribute's parameters `Default` (so no comments kept) and one without; `SQLSRC114` is reported once, at `{{class}}`, the first type has `Q`, and the second type's file has no `Q` member.
  - Generator: with the project not keeping comments and no type keeping them, the same file gives no diagnostic.

- [ ] **Step 2: Run them, see them fail.**

- [ ] **Step 3: Implement.**  `BuildKept` returns the kept segments and its scan's errors mapped to the file (`kept.ToSourceSpan`), without adding them to `_errors`; the stripped form's errors already make the file have no blocks, so the dedupe against them and `BuiltQuery.FirstScanError` go.  `SqlFileReader` maps each to a `DiagnosticInfo` as it does a file's errors.  `FindUsableFiles` also leaves out a file with a query whose `KeptErrors` is not empty and whose `QuerySettings.Resolve(query.Markers, type.Settings, fileSettings[index], property).KeepComments` is true, adding those diagnostics; it resolves settings only for such a query.  `TypeModels.Of` passes its `type.FileSettings` and `property`.

- [ ] **Step 4: Run all of `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`;** fix the tests that pinned the old place.

- [ ] **Step 5: Docs and commit.**  `docs/architecture/generator.md` "A block has two forms of its SQL": the kept form's errors are carried with the query and reported by `TypeEmitter` for a type that keeps the query's comments, which then leaves the file out; drop the TD-0021 sentence once Task 3 lands.  `src/SqlSource/AGENTS.md`: none of its rules change (check).  Manual: drop the known issue "A reserved word in braces in a comment can be an error where no comment is kept".  `./pre-commit-validation.sh`, commit.

### Task 3: Report a comment that hides a token's closing braces (after the owner's answer)

**Files:**
- Modify: `src/SqlSource/Parsing/SqlParseErrorKind.cs` (new last member `CommentInTokenDefault`, argument: the token's name), `src/SqlSource/Parsing/Tokens/TokenScanner.cs`, `src/SqlSource/Parsing/SqlBlockText.cs`, `src/SqlSource/Parsing/SqlFileParser.cs` (`BuildQuery`), `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`, `tests/SqlSource.Tests/Parsing/Tokens/TokenScannerTests.cs`, `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs` (or a `SqlBlockText` test)

**Interfaces:**
- Produces: `SqlBlockText.ToOutput(int sourceOffset) : int` (where text copied from before `sourceOffset` ends in `Text`), `TokenScanner.FindOpenDefault(string sql, int offset, ISet<string> ignoredNames) : string?` (the name of the token whose default is open at `offset`, or null), `SqlDiagnostics.CommentInTokenDefault` (`SQLSRC120`, error, `NotConfigurable`, title "Comment hides the end of a token", message "A comment inside the default of token '{0}' holds '}}}}', which does not end the token. Move the comment out of the default.", final wording per the owner's answer).

- [ ] **Step 1: Write the failing tests**
  - `Parse_LineCommentThatHoldsTheClosingBraces_IsReported`: `-- name: Q\nSELECT 1 {{f:AND a = 1 -- note}}\nORDER BY {{o:name}}\n` gives `[CommentInTokenDefault at SpanOf(text, "-- note}}"), "f"]`.
  - Same with no later token (`{{f:a -- note}}` alone): reported.
  - `{{f:a /* }} */ b}}`: reported at the block comment.
  - Not reported: `SELECT 1 -- {{x}}\n`, `SELECT {{f:a}} -- see }}\n`, `{{f:a -- note\n}}` (the comment holds no braces), `-- token-ignore: f` with `{{f:a -- }}`, and `#` under ansi (not a comment there).
  - Reported under `mysql` for `# }}` inside a default.
  - `FindOpenDefault` theory: `("{{f:a ", 6) -> "f"`, `("{{f:a}} ", 8) -> null`, `("{{1:a ", 6) -> null`, `("{{f a ", 6) -> null`, `("{{f:{{g:a ", 10) -> "f"`.
  - `ToOutput`: built from `SELECT a -- c\nFROM t` stripped, the comment's offset maps to the end of `SELECT a`.

- [ ] **Step 2: Run them, see them fail.**

- [ ] **Step 3: Implement.**  In `BuildQuery`, after the scan, for each lexeme of the block that is a `LineComment` or `BlockComment` holding `}}` (`IndexOf("}}", start, length, Ordinal)`; nothing else is examined, so a typical file pays one search per comment), call `FindOpenDefault(sql.Text, sql.ToOutput(lexeme.Span.Start), ignored)`; a name is an error at the comment's span.  `FindOpenDefault`: the last `}}` before the offset bounds the search; the first `{{` after it that opens `{{ name :` with a valid, unignored name gives the name.  Factor the opener's reading out of `TryReadToken` so both share it.  `ToOutput`: binary search of the runs by `Source`, the run's output plus the distance, capped at the run's end.  Descriptor, `All`, `ForParseError`, release notes, manual section `SQLSRC120` with an example and the fix, and its table row.

- [ ] **Step 4: Run all tests, the allocation tests among them.**

- [ ] **Step 5: Docs and commit.**  Manual: drop the known issue "A `--` comment inside a token's default swallows its closing braces"; in "Defaults", say a comment in a default must not hold `}}` (SQLSRC120).  Delete `docs/tech-debt/TD-0021-comments-and-token-defaults.md`, its row in `docs/tech-debt/README.md`, the TD-0021 sentence in `docs/architecture/generator.md`; `grep -rn TD-0021 .` finds only the old plans.  `./pre-commit-validation.sh`, commit.

### Task 4: Measure

- [ ] Before Task 1 (on `main`) and after Task 3, run a `Stopwatch` loop of `SqlFileParser.Parse` over the allocation test's file, plain and keep-comments, 2000 parses after 200 of warm-up, from a throwaway console project in the scratchpad that loads the `Release` build of `src/SqlSource` and calls the internal `Parse` by reflection.  Record µs per parse and bytes per character for the PR description.
