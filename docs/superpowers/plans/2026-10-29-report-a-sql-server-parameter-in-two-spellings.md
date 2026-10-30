# Report a SQL Server parameter in two spellings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An `mssql` query that writes one parameter in two spellings, `@Id` and `@id`, gets a build warning, `SQLSRC121`, at the second spelling, so that the `Msg 137` a case-sensitive SQL Server gives at run time surfaces at build time.

**Architecture:** The parser finds the second spelling where `SqlParameterList` already matches a name ignoring case, and records it in a new list of warnings beside the errors, `SqlFileParseResult.Warnings`.  The kind is a new last `SqlParseErrorKind`, so `ForParseError` and the id rule give it `SQLSRC121`.  The generator carries the warnings in `ParsedSqlFile.Warnings` and reports them where it reports parse errors; a warning never stops a block.

**Tech Stack:** C#, Roslyn incremental generator, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0092-case-sensitive-sql-server-reads-two-spellings-as-two-parameters.md` (deleted by Task 3), and the brief's unit instructions.

**Owner answers this plan assumes** (sent as Q1–Q5, blocking the merge): Q1(a) the parser reports it for every `mssql` query; Q2(a) a configurable warning, `SQLSRC121`; Q3(a) turned off by `NoWarn` or `.editorconfig` only; Q4 the SQL's spellings and those of token defaults count, a `-- param:` marker's does not, once per extra spelling per query; Q5(a) no case-sensitive container test.  A different answer reworks Task 1's detection or adds a describer task.

**Where the code differs from the spec:** the brief expects an id after `SQLSRC284`; `src/SqlSource/Diagnostics/AGENTS.md` puts a `.sql` file's diagnostics in 101–199 and the parser's ids follow `SqlParseErrorKind`'s order, so a parser report is `SQLSRC121`.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`; run `./format.sh -- <changed .cs files>` (the repo's formatter, see `CONTRIBUTING.md`) before it.
- Dialect `SqlDialect.SqlServer` only.  Names still compare by `SqlParameterName.Comparison`; the list keeps the SQL's first spelling.
- Diagnostic: id `SQLSRC121`, title `Parameter is written in two spellings`, `DiagnosticSeverity.Warning`, no `NotConfigurable` tag, a comment on the descriptor saying why it may be configured, help link `HelpLinkBase + "sqlsrc121"`.
- Message format: `"The parameter '{1}' is also written '{0}'.  SqlSource binds it as '{1}', and a SQL Server whose server collation is case-sensitive reads '{0}' as another variable, which the query does not declare.  Write it '{1}' throughout the query."`  `{0}` is the spelling at the warning's place and `{1}` the list's, both with their `@`.
- `src/SqlSource/AGENTS.md` and `tests/AGENTS.md` have no room: rules go in folder `AGENTS.md` files or `docs/architecture/`.
- Edit only TD-0092's row of `docs/tech-debt/README.md` (td-0079-small edits the same file).

## Review Focus

- `@Id` and `@id` under `postgres` or `ansi`: no warning.  Test in Task 1.
- A variable the query declares in one spelling and uses in another (`DECLARE @n int; SELECT @N`): no warning, it is not a parameter.  Test in Task 1.
- A spelling inside a string or a comment (`'@id'`, `-- @id`, with `keep-comments` too): no warning.  Test in Task 1.
- Two queries of one file, `A` with `@Id` and `B` with `@id`: no warning, each query is its own batch.  Test in Task 1.
- A query in one spelling allocates nothing more in the parse: `SqlFileParserAllocationTests` stays green unchanged.  Run in Task 1.

---

### Task 1: The parser finds a second spelling, and SQLSRC121 describes it

**Files:**
- Modify: `src/SqlSource/Parsing/SqlParseErrorKind.cs` (new last member)
- Modify: `src/SqlSource/Parsing/SqlFileParseResult.cs`, `src/SqlSource/Parsing/SqlFileParser.cs`, `src/SqlSource/Parsing/SqlParameterList.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`, `tests/SqlSource.Tests/Parsing/ParseOf.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`

**Interfaces:**
- Produces: `SqlParseErrorKind.ParameterInTwoSpellings` (last member; doc: "A parameter of a SQL Server query is written in a spelling other than its first, a warning.  Arguments: this spelling and the first, each with its prefix.").
- Produces: `SqlFileParseResult(EquatableArray<SqlBlock> Blocks, EquatableArray<SqlParseError> Errors, EquatableArray<SqlParseError> Warnings, SqlDialect Dialect)`.  `Warnings` is empty whenever `Errors` is not, ordered by position otherwise.
- Produces: `SqlParameterList.Create(..., List<SqlParseError> errors, List<SqlParseError> warnings)`.
- Produces: `SqlDiagnostics.ParameterInTwoSpellings`, in `All` and in `ForParseError`.
- Produces (test helper): `ParseOf.Warnings(string text, SqlDialectChoice dialect = default) : SqlParseError[]`, which asserts no errors.

- [ ] **Step 1: Write the failing tests** in `SqlFileParserParameterTests`:
  - `Parse_SqlServerParameterInASecondSpelling_IsAWarningAtThatSpelling`: `"-- name: Q\nUPDATE dbo.t SET a = @Id WHERE b = @id\n"` under `SqlDialect.SqlServer` gives one warning, kind `ParameterInTwoSpellings`, span the text of `@id`, arguments `["@id", "@Id"]`; the block's parameters are still `["Id"]`.
  - `Parse_EachExtraSpelling_IsWarnedOnceAtItsFirstPlace`: `"SELECT @Id, @id, @ID, @id"` gives two warnings, for `@id` (first one) and `@ID`.
  - `Parse_SpellingInATokenDefault_IsWarned`: a default in the SQL, `"SELECT a FROM t WHERE a = @Id {{f:AND b = @id}}"`, warns at `@id` in the file; and a declared parameter `-- param: @Id int` with a marker default `-- token: {{f:AND b = @id}}` over `SELECT a FROM t WHERE 1 = 1 {{f}}` warns at the default's `@id`.
  - `Parse_ParamMarkerInAnotherSpelling_IsNotWarned`: `-- param: @Id int` over `SELECT @id` gives no warning, parameter `id`.
  - The Review Focus cases as one `[Theory]`, `Parse_NoSecondSpellingOfAParameter_IsNotWarned`: `@Id`/`@id` under `postgres` and `ansi`; `DECLARE @n int = 1; SELECT @N` under `mssql`; `SELECT @Id, '@id' -- @id` under `mssql`, plain and with `-- generator: keep-comments`; and two queries `A`/`B` with one spelling each.
  - `Parse_FileWithAnErrorAndASecondSpelling_HasNoWarnings`: add an error (e.g. an unterminated quote after the query) and assert `Warnings` is empty.
  - In `SqlDiagnosticsTests`: add `["SQLSRC121"] = DiagnosticSeverity.Warning` to `Configurable`; `ForParseError_ParameterInTwoSpellings_IsSqlsrc121`; `ParameterInTwoSpellings_Message_HoldsBothSpellings` with `Format(SqlDiagnostics.ParameterInTwoSpellings, "@id", "@Id")` equal to the Global Constraints message.

- [ ] **Step 2: Run them to see them fail**: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlFileParserParameterTests|FullyQualifiedName~SqlDiagnosticsTests"`; expect compile errors on the new members.

- [ ] **Step 3: Implement.**  `Parser` holds a `_warnings` list beside `_errors` and passes it to `Create`; `Run` returns it sorted by `Span.Start` only when there are no errors, and the lexer-error path gives `Empty`.  In `SqlParameterList.Create`, when `rules.Dialect == SqlDialect.SqlServer`: a static parameter whose `IndexOf` finds an entry whose `Name` differs ordinal from the spelling warns at `sql.ToSourceSpan(span)`; `ReportUndeclared`'s loop does the same for a default's parameter it finds, at the span it already computes for `SQLSRC118`.  Keep a spelling only once per query (a list made on the first warning).  Allocate nothing on the path with no second spelling.  Add the descriptor after `TokenCrossesComment`'s, the `ForParseError` case, its `All` entry, the `AnalyzerReleases.Unshipped.md` row `SQLSRC121 | SqlSource | Warning | Parameter is written in two spellings` after `SQLSRC120`, and the `## SQLSRC121` section of `docs/manual/diagnostics.md` after `SQLSRC120`'s: what it means, a `console` sample of the warning at a `.sql` path, the fix (one spelling), and that a project may list it in `NoWarn`, or set `dotnet_diagnostic.SQLSRC121.severity` in `.editorconfig`, when its servers ignore case.  Its SQL sample is `sql error`.  Update the `SqlParameterList` and `SqlFileParseResult` doc comments.

- [ ] **Step 4: Run the tests to see them pass**, plus `--filter "FullyQualifiedName~SqlSource.Tests.Parsing|FullyQualifiedName~SqlSource.Tests.Diagnostics|FullyQualifiedName~UserDocs"`; `SqlFileParserAllocationTests` must pass unchanged.

- [ ] **Step 5: Commit**: `feat: warn of an mssql parameter written in two spellings (SQLSRC121)`.

### Task 2: The generator reports the warning and still builds the query

**Files:**
- Modify: `src/SqlSource/Generation/ParsedSqlFile.cs`, `src/SqlSource/Generation/SqlFileReader.cs`, `src/SqlSource/SqlSourceGenerator.cs` (the `parsedFiles` output in `ParseFiles`)
- Test: a generator driver test beside the existing parse-error driver tests in `tests/SqlSource.Tests/Generator/` (find the one that asserts `SQLSRC118`), and `tests/SqlSource.Tests/Documentation/UserDocsSamplesTests.cs`

**Interfaces:**
- Consumes: `SqlFileParseResult.Warnings` (Task 1).
- Produces: `ParsedSqlFile(..., EquatableArray<DiagnosticInfo> Errors, EquatableArray<DiagnosticInfo> Warnings, SqlDialect Dialect)`.  `SqlFileReader` maps both with `DiagnosticsOf`.  The tool reads `Errors` only and is unchanged.

- [ ] **Step 1: Write the failing tests.**
  - `Generate_SqlServerParameterInTwoSpellings_WarnsAndStillGeneratesTheQuery`: a type claiming an `mssql` file with `UPDATE dbo.t SET a = @Id WHERE b = @id` (output `sql`, so no sidecar is needed) reports exactly one `SQLSRC121`, severity `Warning`, located at `@id`'s line and column in the `.sql` path, and the type's constant is still generated.
  - `Generate_SqlServerParameterInTwoSpellings_IsHiddenBySuppressingTheId`: the same with `SQLSRC121` set to `ReportDiagnostic.Suppress` in the compilation's `SpecificDiagnosticOptions` reports nothing.
  - In `UserDocsSamplesTests.SqlSamples_OfTheUserDocs_Parse`, a dialect sample's warnings are problems too (a sample a user copies must not warn); `SQLSRC121`'s own sample is `sql error` and skipped.

- [ ] **Step 2: Run them to see them fail.**

- [ ] **Step 3: Implement**: the record's new member, its doc comment, `SqlFileReader` passing `DiagnosticsOf(file.Path, text, result.Warnings)`, and the output reporting `file.Warnings` after `file.Errors` for each collected file, so a file listed twice is reported once.

- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tests` and the tool's tests (`tests/SqlSource.Tool.Tests`, which construct `ParsedSqlFile` or read parse results); fix constructor call sites.  Build `examples/` and `tests/EndToEnd*` (the solution build); a `SQLSRC121` there is a sample that writes two spellings: correct its SQL to one spelling and re-run `tools/describe-end-to-end.sh` only if its hash changes.

- [ ] **Step 5: Commit**: `feat: report SQLSRC121 from the build`.

### Task 3: Docs, guidance, and resolving TD-0092

**Files:**
- Modify: `docs/manual/parameters-and-tokens.md` (line "`@Id` and `@id` are one parameter", and the `mssql` known issue on two spellings), `docs/manual/sqlserver.md` (remove the known issue on two spellings; say where `SQLSRC121` covers it if the page lists `mssql` warnings), `docs/manual/generator.md` if it names TD-0092's behavior, `docs/architecture/generator.md` (the parsing section: warnings travel beside errors and never stop a block; the parameter list), `src/SqlSource/Parsing/AGENTS.md`, `src/SqlSource/Diagnostics/AGENTS.md`, `src/SqlSource/Parsing/SqlParameterName.cs` (remark: drop "(TD-0092)", name `SQLSRC121`)
- Delete: `docs/tech-debt/TD-0092-case-sensitive-sql-server-reads-two-spellings-as-two-parameters.md` and its row in `docs/tech-debt/README.md`

- [ ] **Step 1:** Manual: `parameters-and-tokens.md` keeps "names are compared ignoring case" and adds that under `mssql` a second spelling is warned as [SQLSRC121](diagnostics.md#sqlsrc121), since a case-sensitive server reads it as another variable; drop the known-issue bullet.  `sqlserver.md`: drop the known issue.  User docs say effects only, never the parser.
- [ ] **Step 2:** Guidance: `Parsing/AGENTS.md` — "A result with errors has no blocks" gains that a warning (`SqlFileParseResult.Warnings`) never stops a block and is empty when there are errors; `Diagnostics/AGENTS.md` — "for a parser error, `SqlDiagnostics.ForParseError`" covers a parser warning too, and the configurable list rule is unchanged.  Keep each bullet ≤ 80 words.
- [ ] **Step 3:** Resolve TD-0092 per `docs/tech-debt/AGENTS.md`: delete the file and its row, then `grep -rn "TD-0092" --exclude-dir=bin --exclude-dir=obj .` returns only `docs/superpowers/` history.
- [ ] **Step 4:** Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"`.
- [ ] **Step 5: Commit**: `docs: document SQLSRC121 and resolve TD-0092`.

### Final: full validation

- [ ] `./format.sh`, then `./pre-commit-validation.sh --full` with a 600000 ms timeout; it must pass.  No describer changes, so no floor-image run.
