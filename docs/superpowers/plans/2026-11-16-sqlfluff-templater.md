# sqlfluff templater Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `sqlfluff-templater-sqlsource`, a sqlfluff templater named `sqlsource` that reads SqlSource `.sql` files exactly as SqlSource does, so `sqlfluff lint`, `fix` and `format` work on them without ever changing a token, a default, a parameter, a list or a marker.

**Architecture:** The C# parser gains an opt-in "templating plan" (the spans of a file sqlfluff must not see as written, in file offsets) and a test that records the plan of every `.sql` file in the repository and of the parser's own test cases into a committed JSON corpus.  A Python port of the lexer, dialect rules, token scanner, the markers it needs, the list rule and the settings-file dialect lookup builds the same plan, checked case by case against the corpus.  A sqlfluff templater renders the plan into a `TemplatedFile`.

**Tech Stack:** C# (.NET, xUnit v3, as the repository uses), Python 3.14.7 (development pin), uv, ruff, pytest, sqlfluff `>=4.0,<5`, GitHub Actions, PyPI trusted publishing.

**Spec:** `docs/superpowers/specs/2026-11-16-sqlfluff-templater-design.md`.  Read it first; this plan argues from it and does not repeat its reasons.

## Global Constraints

- Package name `sqlfluff-templater-sqlsource`; import package `sqlfluff_templater_sqlsource`; templater name `sqlsource`; entry point group `sqlfluff`.
- `requires-python = ">=3.10"`; `.python-version` holds `3.14.7`; dependency `sqlfluff>=4.0,<5`.
- The project lives in `src/sqlfluff-templater-sqlsource/` with its own `.editorconfig` (`root = true`), `.gitignore`, `.gitattributes`.
- ruff for linting and formatting, configured in `pyproject.toml`; pytest; uv for environment, lock, run and build.  No type checker.
- The Python project is checked by `pre-commit-validation.sh`, formatted by `format.sh`, and runs on every CI run, like the rest of the code.
- Released in lockstep with SqlSource by `publish.yml`, same version, PEP 440: tag `v0.2.0` → `0.2.0`; suffix `beta.123` → `0.2.0b123`; suffix `pr-42.123` → `0.2.0.dev123` (never published).
- PyPI trusted publishing only: environment `pypi`, `id-token: write`, `pypa/gh-action-pypi-publish` pinned by SHA.  No stored token.
- Every action is pinned by full SHA with a version comment, as the existing workflows do.
- Corpus offsets count Unicode code points, never UTF-16 units.
- Exactness is promised for files SqlSource accepts.  The errors that stop a plan are exactly: the lexer's unterminated quoted region or block comment, an invalid `-- dialect:` marker, `SQLSRC120`.
- User documentation describes effects, never internals (root `AGENTS.md`, rule 4).
- Every commit passes `./pre-commit-validation.sh`; every push passes `./pre-commit-validation.sh --full`.  Run commands through a login shell (`zsh -lc '...'`) so `~/.zprofile` is loaded.

## Review Focus

1. **CRLF files**, which Visual Studio writes: sqlfluff normalises `\r\n` to `\n` before templating, so the plan must be built on the text the templater receives, and `fix` must still write the file back with its tokens intact.  Test in Task 7.
2. **A UTF-8 BOM** at the start of a file, which Visual Studio also writes: the plan must not be shifted by one.  Test in Task 7.
3. **Characters outside the Basic Multilingual Plane** (an emoji in a string) before a token: C# and Python offsets differ unless converted.  A fidelity case in Task 2, checked in Task 5.
4. **Several projects with different dialects under one sqlfluff run**: each `.sql` file resolves its own `sqlsource.json`.  Test in Task 6.
5. **A file SqlSource rejects** (an empty block, SQL before the first `-- name:`, conflicting defaults): the templater must give a best-effort plan and never raise.  Test in Task 7.

---

### Task 1: The templating plan in the C# parser

**Files:**
- Create: `src/SqlSource/Parsing/Templating/SqlTemplatingPlan.cs`
- Create: `src/SqlSource/Parsing/Templating/SqlTemplatingSpan.cs`
- Create: `src/SqlSource/Parsing/Templating/SqlTemplatingSpanKind.cs`
- Modify: `src/SqlSource/Parsing/SqlFileParser.cs` (a second entry point; the private `Parser` takes an optional span collector)
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs` (expose the marker-form test without the first-on-line condition)
- Test: `tests/SqlSource.Tests/Parsing/Templating/SqlTemplatingPlanTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  internal enum SqlTemplatingSpanKind { Token, Parameter, List, MarkerComment, QueryEnd }

  // Name: token or parameter name without prefix; Default: the token's resolved default, trimmed, or null.
  // For QueryEnd the span is empty, at the offset right after the query's last SQL lexeme.
  internal readonly record struct SqlTemplatingSpan(SqlTemplatingSpanKind Kind, TextSpan Span, string? Name, string? Default);

  // Spans sorted by start, then by kind order above; parameters inside a token's default are listed after the token
  // and lie inside its span.  Error is null when the plan is exact, or the stopping error.
  internal sealed record SqlTemplatingPlan(
      SqlDialectChoice Dialect,
      EquatableArray<SqlLexeme> Lexemes,
      EquatableArray<SqlTemplatingSpan> Spans,
      SqlParseError? Error,
      bool HasOtherErrors);

  internal static class SqlFileParser
  {
      public static SqlTemplatingPlan PlanTemplating(string text, string fileName, SqlDialectChoice dialect);
  }

  internal static class SqlMarkerReader
  {
      // True for a line comment that reads `--`, optional blanks, a known keyword and a colon, wherever it stands.
      public static bool HasMarkerForm(string text, SqlLexeme lexeme);
  }
  ```

What goes in the plan, per query block (lexemes `[start, end)` of `ReadBlock`):
- **Token**: each `SqlTokenOccurrence` of `BuildQuery`'s scan, mapped to the file by `SqlBlockText.ToSourceSpan` (verify it maps both ends of a token that spans lines; if it does not, map `Start` and `End - 1` separately), with the default `SqlTokenList.Create` resolved for that name.
- **Parameter** / **List**: each `SqlBlockText.Parameters[i]` mapped to the file, kind `List` when `SqlListUses.Find`'s `uses[i]` is `List` or `NegatedList` (an empty `uses` means none is a list).  Declared variables are included as `Parameter`.
- **MarkerComment**: each lexeme of the block's range with `HasMarkerForm`.
- **QueryEnd**: for every block but the last of the file, when the content of the block's last content lexeme (`FindLastContent`) does not end in `;`.
- Lexemes: the whole file's, from the lexer.
- `Error`: the lexer's error, the error of an invalid `-- dialect:` marker, or `SQLSRC120` (`FindHiddenTokenEnds`/`OpenDefaultFinder`), whichever comes first by position; with it, `Spans` is empty.  `HasOtherErrors`: any other file error; the spans are still collected (best effort).

The ordinary `Parse` path must allocate nothing new: the collector is null there.  `SqlFileParserAllocationTests` must still pass unchanged.

- [ ] **Step 1: Write the failing tests** in `SqlTemplatingPlanTests.cs`, one `[Fact]` or `[Theory]` per behaviour, asserting on `Spans` as `(Kind, text[Span], Name, Default)` tuples:
  - `Plan_TokenWithInlineDefault_IsOneTokenSpan`: `"SELECT {{t:users}};"` → `[(Token, "{{t:users}}", "t", "users")]`.
  - `Plan_TokenDefaultFromMarker_AppliesToItsQueryOnly`: the two-query `cond` example of the spec's Fidelity section → `ByA`'s token default `a = 1`, `ByB`'s `AND b = 2`.
  - `Plan_IgnoredName_IsNoSpan`: `-- token-ignore: name` with `'Hello, {{name}}'` → no Token span.
  - `Plan_TokenInComment_IsNoSpan` and `Plan_UnclosedOpenerInComment_SwallowsNothing`: the spec's `-- TODO: add {{filter: ...` case → one Token span, `{{cond:id > 0}}`.
  - `Plan_ParameterInDefault_IsInsideTheToken`: `{{paging:LIMIT 20 OFFSET @page}}` with `-- param: @page int` → Token then `(Parameter, "@page", "page", null)` inside it.
  - `Plan_List_IsListSpan`, `Plan_ListAfterComment_IsListSpan` (`IN /* c */ @ids`), `Plan_Position_IsNoList` (`position('@' IN @email)` → Parameter).
  - `Plan_SystemVariable_IsNoParameter` (`@@rowcount`, mssql) and `Plan_AtInString_IsNoParameter` (`'a@b.c'`).
  - `Plan_QueryWithoutSemicolon_HasQueryEnd`: two queries without `;` → one `QueryEnd`, at the end of the first query's last SQL; none after the last query; none when the first ends in `;`.
  - `Plan_MarkerForm_IsProtected`: a `-- token:` marker line and an inline `SELECT 1 -- summary: x` both give `MarkerComment`; `-- just a note` gives none.
  - `Plan_UnterminatedString_IsError`, `Plan_InvalidDialectMarker_IsError`, `Plan_CommentHoldingCloseBraces_IsSqlsrc120`: `Error` set, `Spans` empty.
  - `Plan_OtherError_IsBestEffort`: a duplicated query name → `Error` null, `HasOtherErrors` true, spans present.
  - `Plan_DialectMarker_IsTheResolvedDialect`: `-- dialect: mysql ansi_quotes` → `Dialect` is MySql with `AnsiQuotes`.

- [ ] **Step 2: Run them and see them fail** (no `PlanTemplating`): `zsh -lc 'dotnet test --project tests/SqlSource.Tests --filter-class "*SqlTemplatingPlanTests"'` (use the filter syntax the repository's test runner takes; see `tests/README.md`).  Expected: build error.

- [ ] **Step 3: Implement** the three types, `HasMarkerForm` (reuse `Keywords`; `Read` keeps calling it with its first-on-line check), and `PlanTemplating`: it runs the same lexer, `SqlDialectMarker.Apply` and `Parser`, with a `List<SqlTemplatingSpan>` collector that `ReadBlock`/`BuildQuery` fill only when non-null.

- [ ] **Step 4: Run the new tests and the parser's existing tests**, `tests/SqlSource.Tests` filtered to `SqlSource.Tests.Parsing`.  Expected: all pass, allocation tests included.

- [ ] **Step 5: Add a rule to `src/SqlSource/Parsing/AGENTS.md`**: `PlanTemplating` gives what the sqlfluff templater mirrors; a change to `SqlLexer`, `SqlDialectRules`, `Parsing/Quoting/`, `Parsing/Tokens/`, `SqlMarkerReader`, the `-- name:`, `-- dialect:`, `-- token:` or `-- token-ignore:` markers, `SqlDialectName`, or which use `SqlListUses.Find` gives as a list, changes `src/sqlfluff-templater-sqlsource/` in the same pull request and regenerates its corpus (Task 2's command).  Link `docs/architecture/templater.md` (written in Task 10).  Keep the file under 8 KB.

- [ ] **Step 6: Validate and commit.**  `./pre-commit-validation.sh`, then:
  ```bash
  git add src/SqlSource/Parsing tests/SqlSource.Tests/Parsing/Templating
  git commit -m "Give the parser a templating plan for the sqlfluff templater"
  ```

### Task 2: The corpus, written and checked by C#

**Files:**
- Create: `tests/SqlSource.Tests/Parsing/Templating/TemplatingCorpusTests.cs`
- Create: `tests/SqlSource.Tests/Parsing/Templating/TemplatingCorpus.cs` (the case sources and the JSON writer)
- Create: `tests/SqlSource.Tests/Parsing/Templating/Cases/*.sql` (the fidelity cases)
- Create (generated): `src/sqlfluff-templater-sqlsource/tests/corpus/**/*.json`
- Modify: `tests/README.md` (how to regenerate), `tests/SqlSource.Tests` project file if the cases folder must be copied or linked

**Interfaces:**
- Consumes: `SqlFileParser.PlanTemplating` (Task 1); `SettingsFileReader.Read` and `Generation/Chains/SettingsFileSet` for a case's dialect.
- Produces, the corpus format both sides read.  One file per case, UTF-8, LF, keys in this order, pretty-printed with 2-space indent so diffs read well:
  ```json
  {
    "fileName": "Users.sql",
    "settings": ["{ \"dialect\": \"postgres\" }"],
    "input": "...",
    "expected": {
      "startDialect": "postgres",
      "startOptions": [],
      "dialect": "postgres",
      "options": [],
      "lexemes": [["Text", 0, 7], ["Parameter", 7, 10]],
      "spans": [{ "kind": "token", "start": 7, "end": 18, "name": "t", "default": "users" }],
      "error": null,
      "hasOtherErrors": false
    }
  }
  ```
  `settings` lists the texts of the `sqlsource.json` files above the case, nearest first, as far as the chain reads (through the first `"root": true`).  `startDialect`/`startOptions` are what the chain gives, the dialect the parse starts with; `dialect`/`options` are the plan's, after any `-- dialect:` marker.  Dialects are `SqlDialectName.Canonical`; options the option names `SqlDialectName.Options` uses; a lexeme kind is the C# enum member name (`Text`, `Quoted`, `LineComment`, `BlockComment`, `Hint`, `Parameter`); a span `kind` is the enum member in lower camel case (`token`, `parameter`, `list`, `markerComment`, `queryEnd`); `error` is `{ "code": "SQLSRC1nn", "start": n, "end": n }` or null.  Offsets are code points.
- Layout: `tests/corpus/repo/<repository path>.json` for repository files; `tests/corpus/lexer/<dialect>/<n>.json` for each expanded row of `Lex_Construct_IsReadByTheRulesOfTheDialect`; `tests/corpus/cases/<name>.json` for the fidelity cases.

Case sources:
- **Repository**: every `*.sql` under the repository root read through `RepositoryRoot.Path`, skipping `bin/`, `obj/`, `.git/`, `node_modules/` and `src/sqlfluff-templater-sqlsource/`.  A file whose plan has `HasOtherErrors` is left out (the spec promises nothing for it); one with a stopping `Error` is kept.
- **Lexer rows**: the `[InlineData]` attributes of `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`, read by reflection, one case per dialect in the row's comma list, dialect given by a settings text `{ "dialect": "<canonical name>" }`.
- **Fidelity cases**: the `.sql` files of `Cases/`, each starting with a `-- dialect:` marker.  At least: every case of the spec's Fidelity section, a non-BMP character (`'😀'`) before a token, a token name with `Ä`, CRLF line endings, a PostgreSQL `$$ -- x $$` string before a token, a SQL Server `[a--b]` name before a token, a MySQL `#` comment holding `{{f:`, nested PostgreSQL block comments holding a token, MySQL `ansi_quotes` and `no_backslash_escapes` strings, and a `-- token:` marker whose default holds a block comment.

Behaviour of the test:
- `Corpus_EveryCase_MatchesTheParser`: for each case, render the expected JSON and compare with the committed file.  Under `SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1` write it instead.
- `Corpus_HasNoOrphanFiles`: every committed corpus file has a case; under the variable, delete orphans.
- Failure messages name the file and say to run with `SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1`, as `GoldenFiles` does for `SQLSOURCE_UPDATE_GOLDEN`.

- [ ] **Step 1: Write the two tests and the case sources** with the writer.
- [ ] **Step 2: Run them; expect failures** for every case (no corpus yet).
- [ ] **Step 3: Generate**: `zsh -lc 'SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1 dotnet test --project tests/SqlSource.Tests --filter-class "*TemplatingCorpusTests"'`.
- [ ] **Step 4: Run again without the variable.**  Expected: pass.  Read a handful of generated files by eye: one repository query with a token, one lexer row, the non-BMP case (its offsets must be code points).
- [ ] **Step 5: Document regeneration** in `tests/README.md` beside the golden corpus, and add `none src/sqlfluff-templater-sqlsource/*` to `tools/test-scopes.txt` (before any broader line that would match it).
- [ ] **Step 6: Validate and commit** (`./pre-commit-validation.sh`; add the tests, the cases, the corpus, `tests/README.md`, `tools/test-scopes.txt`).  Message: "Record the templating plan of every query as the templater's corpus".

### Task 3: The Python project skeleton

**Files:**
- Create in `src/sqlfluff-templater-sqlsource/`: `pyproject.toml`, `uv.lock`, `.python-version`, `.editorconfig`, `.gitignore`, `.gitattributes`, `README.md`, `AGENTS.md`, `sqlfluff_templater_sqlsource/__init__.py`, `sqlfluff_templater_sqlsource/templater.py`, `tests/test_registration.py`, `tests/conftest.py`

**Interfaces:**
- Produces:
  ```python
  # sqlfluff_templater_sqlsource/__init__.py
  @hookimpl
  def get_templaters() -> list[type[RawTemplater]]: ...   # returns [SqlSourceTemplater]

  # sqlfluff_templater_sqlsource/templater.py
  class SqlSourceTemplater(RawTemplater):
      name = "sqlsource"
  ```
  `tests/conftest.py` provides `corpus_cases() -> list[CorpusCase]` with `CorpusCase(path: Path, file_name: str, settings: list[str], input: str, expected: dict)` reading `tests/corpus/**/*.json`, for Tasks 4–7.

Decisions:
- `pyproject.toml`: build backend `hatchling`; `version = "0.0.0.dev0"` (CI sets the real one, Task 9; a comment says never to edit it); `[project.entry-points.sqlfluff] sqlfluff_templater_sqlsource = "sqlfluff_templater_sqlsource"`; `[dependency-groups] dev = ["pytest", "ruff"]` with exact pins; ruff `target-version = "py310"`, `line-length = 120`, lint `select = ["E", "F", "W", "I", "UP", "B", "SIM", "RUF"]`.
- `.editorconfig`: `root = true`, UTF-8, LF, 4 spaces for `.py`, 2 for `.toml`/`.json`/`.yml`, final newline; the corpus JSON is generated, so its rules must match what Task 2 writes.
- `.gitattributes`: `* text=auto eol=lf`; `tests/corpus/**/*.json -diff linguist-generated` is not wanted (diffs of the corpus are what a reviewer reads), so only `eol=lf`.
- `.gitignore`: `.venv/`, `__pycache__/`, `dist/`, `.pytest_cache/`, `.ruff_cache/`.
- `README.md`: the PyPI page.  What it does, install (`pip install sqlfluff sqlfluff-templater-sqlsource`), the two `.sqlfluff` lines, and absolute links to the site's guide (`https://mbcrawfo.github.io/SqlSource/guides/sqlfluff.html`).  Effects only.
- `AGENTS.md` (≤ 8 KB, maintenance footer): the corpus is written by Task 2's C# test and never edited by hand; each module mirrors the C# file it names in its docstring, and a change there changes the module; the commands (`uv run ruff check`, `uv run ruff format`, `uv run pytest`), which `pre-commit-validation.sh` and `format.sh` also run; never edit `version` in `pyproject.toml`.

- [ ] **Step 1: Write `tests/test_registration.py`**:
  ```python
  def test_sqlfluff_finds_the_templater():
      from sqlfluff.core import FluffConfig
      config = FluffConfig(overrides={"dialect": "ansi", "templater": "sqlsource"})
      assert config.get_templater_class().name == "sqlsource"

  def test_corpus_is_present():
      assert len(corpus_cases()) > 100
  ```
- [ ] **Step 2: Run** `zsh -lc 'cd src/sqlfluff-templater-sqlsource && uv run pytest'`.  Expected: fails until the package and entry point exist.
- [ ] **Step 3: Create the files above**; `uv lock`; the templater's `process` may raise `NotImplementedError` for now.
- [ ] **Step 4: Run** `uv run pytest`, `uv run ruff check`, `uv run ruff format --check`.  Expected: pass.
- [ ] **Step 5: Validate and commit.**  The project is not yet in `pre-commit-validation.sh` (Task 8), so its files are of no known kind and the run is full; that is expected for this commit.  Message: "Add the sqlfluff templater project".

### Task 4: Port the dialects, quote readers and lexer

**Files:**
- Create: `sqlfluff_templater_sqlsource/dialects.py` (mirrors `SqlDialect.cs`, `SqlDialectOptions.cs`, `SqlDialectChoice.cs`, `SqlDialectName.cs`)
- Create: `sqlfluff_templater_sqlsource/rules.py` (mirrors `SqlDialectRules.cs`, `SqlText.cs`)
- Create: `sqlfluff_templater_sqlsource/quoting.py` (mirrors `Parsing/Quoting/*.cs`)
- Create: `sqlfluff_templater_sqlsource/lexer.py` (mirrors `SqlLexer.cs`, `SqlLexeme.cs`, `SqlLexemeKind.cs`, `SqlParameterName.cs`)
- Test: `tests/test_lexer_corpus.py`, `tests/test_dialect_names.py`

All paths below are inside `src/sqlfluff-templater-sqlsource/`.

**Interfaces:**
- Produces:
  ```python
  class Dialect(enum.Enum): ANSI; SQL_SERVER; POSTGRESQL; MYSQL; MARIADB; SQLITE; ORACLE; COCKROACHDB
  class DialectOptions(enum.Flag): NONE = 0; ANSI_QUOTES = 1; NO_BACKSLASH_ESCAPES = 2
  @dataclass(frozen=True) class DialectChoice: dialect: Dialect; options: DialectOptions = DialectOptions.NONE
  def parse_dialect_name(value: str) -> DialectChoice | None
  def canonical_name(dialect: Dialect) -> str
  def option_names(options: DialectOptions) -> list[str]

  class DialectRules:            # one instance per choice, cached
      @staticmethod
      def for_choice(choice: DialectChoice) -> "DialectRules"
      dialect: DialectChoice

  class LexemeKind(enum.Enum): TEXT = "Text"; QUOTED = "Quoted"; LINE_COMMENT = "LineComment"; BLOCK_COMMENT = "BlockComment"; HINT = "Hint"; PARAMETER = "Parameter"
  @dataclass(frozen=True) class Lexeme: kind: LexemeKind; start: int; end: int
  @dataclass(frozen=True) class LexError: code: str; start: int; end: int
  class Lexer:
      def __init__(self, text: str, rules: DialectRules) -> None
      rules: DialectRules                       # settable, as SqlDialectMarker.Apply switches it
      def try_read_leading_comment(self) -> Lexeme | None
      def read_to_end(self) -> tuple[list[Lexeme], LexError | None]
  ```
  Python `str` indexes are code points, which is what the corpus uses.  Where C# tests a `char` against a Unicode category (SQL Server's name characters, `QuoteReader.IsNameCharacterOfSqlServer`), use `unicodedata.category` on the code point and check a non-BMP case against the corpus.

- [ ] **Step 1: Write `test_lexer_corpus.py`**: parametrised over every corpus case; start from `expected["startDialect"]`/`expected["startOptions"]` (the settings port is Task 6), run `SqlDialectMarker`'s header step (port it in this task as `apply_dialect_marker(lexer, text) -> int` in `lexer.py`), lex, and assert `[[l.kind.value, l.start, l.end] for l in lexemes] == expected["lexemes"]` and the error code and span when `expected["error"]` is the lexer's.
- [ ] **Step 2: Write `test_dialect_names.py`**: every name and option name of `SqlDialectName.Names`/`Options` parses as C# does, case-insensitively, and `sql-server` does not.
- [ ] **Step 3: Run; expect failures.**
- [ ] **Step 4: Port** the modules, file by file, each module's docstring naming the C# files it mirrors.
- [ ] **Step 5: Run** `uv run pytest tests/test_lexer_corpus.py tests/test_dialect_names.py`.  Expected: every case passes.
- [ ] **Step 6: ruff, validate, commit.**  Message: "Port the lexer and the dialect rules to the templater".

### Task 5: Port the plan: text builder, tokens, markers, lists

**Files:**
- Create: `sqlfluff_templater_sqlsource/text_builder.py` (mirrors `SqlTextBuilder.cs` without the kept form, `SqlBlockText.cs`)
- Create: `sqlfluff_templater_sqlsource/tokens.py` (mirrors `Tokens/TokenScanner.cs`, `Tokens/SqlTokenList.cs`, `Tokens/OpenDefaultFinder.cs`)
- Create: `sqlfluff_templater_sqlsource/markers.py` (mirrors `SqlMarkerReader.cs` and the `-- name:`, `-- dialect:`, `-- token:`, `-- token-ignore:` parts of `SqlMarkerScope.cs`)
- Create: `sqlfluff_templater_sqlsource/list_uses.py` (mirrors the `List`/`NegatedList` decision of `SqlListUses.Find` only: the word before a parameter is `IN`, comments and hints count as spaces, and the `position(` exception)
- Create: `sqlfluff_templater_sqlsource/plan.py` (mirrors `PlanTemplating` and the block splitting of `SqlFileParser`)
- Test: `tests/test_plan_corpus.py`

**Interfaces:**
- Consumes: Task 4's `Lexer`, `DialectRules`, `DialectChoice`, `apply_dialect_marker`.
- Produces:
  ```python
  class SpanKind(enum.Enum): TOKEN = "token"; PARAMETER = "parameter"; LIST = "list"; MARKER_COMMENT = "markerComment"; QUERY_END = "queryEnd"
  @dataclass(frozen=True) class PlanSpan: kind: SpanKind; start: int; end: int; name: str | None; default: str | None
  @dataclass(frozen=True) class PlanError: code: str; start: int; end: int
  @dataclass(frozen=True)
  class Plan:
      dialect: DialectChoice
      lexemes: list[Lexeme]
      spans: list[PlanSpan]          # ordered as Task 1 orders them
      error: PlanError | None
      has_other_errors: bool
  def build_plan(text: str, file_name: str, dialect: DialectChoice) -> Plan
  ```
  `build_plan` never raises on any input; a bug that would raise is caught at the templater (Task 7) only as a last resort.

`has_other_errors` needs no port of SqlSource's validation: it is checked against the corpus only where Task 2 records it, which is never true for a recorded repository file.  Set it for what the port meets anyway (a duplicated name, SQL before the first `-- name:`, an empty block, two different defaults), and test that the plan is still built.

- [ ] **Step 1: Write `test_plan_corpus.py`**: parametrised over every corpus case; `build_plan(case.input, case.file_name, <the choice of expected["startDialect"] and expected["startOptions"]>)`; assert `plan.dialect` is `expected["dialect"]`/`expected["options"]`, spans as dicts equal `expected["spans"]`, `error` equal `expected["error"]`, and `has_other_errors` equal `expected["hasOtherErrors"]`.  Add `test_rejected_file_gives_best_effort_plan` (Review Focus 5) over the five rejected inputs named above: no exception, spans non-empty.
- [ ] **Step 2: Run; expect failures.**
- [ ] **Step 3: Port** the modules.
- [ ] **Step 4: Run** `uv run pytest`.  Expected: every case passes, the non-BMP case included (Review Focus 3).
- [ ] **Step 5: ruff, validate, commit.**  Message: "Port the templating plan to the templater".

### Task 6: Port the dialect lookup of `sqlsource.json`

**Files:**
- Create: `sqlfluff_templater_sqlsource/settings.py` (mirrors the dialect and `root` reading of `SettingsFiles/SettingsFileReader.cs` and the chain rule of `Generation/Chains/SettingsFileSet.cs`)
- Test: `tests/test_settings.py`

**Interfaces:**
- Produces:
  ```python
  @dataclass(frozen=True) class SettingsDialect: root: bool | None; dialect: DialectChoice | None
  def read_settings(text: str) -> SettingsDialect          # JSONC; never raises
  def dialect_of_chain(texts: list[str]) -> DialectChoice   # nearest first; stops after root true; default ANSI
  def resolve_dialect(sql_path: Path) -> DialectChoice      # the file's folder up to the filesystem root, file name matched exactly
  ```
  The `-- dialect:` marker is applied by `build_plan` over the dialect `resolve_dialect` gives, as SqlSource does.

- [ ] **Step 1: Write tests**: `test_chain_matches_corpus` (every corpus case: `dialect_of_chain(case.settings)` equals the choice of `expected["startDialect"]` and `expected["startOptions"]`); and unit tests for JSONC comments and trailing commas, `"root": true` stopping, a file that is not one JSON object (root unknown, search goes on), `"dialect": 5`, `"dialect": ""`, an invalid name, a file named `SqlSource.json` not matching, and **Review Focus 4**: two sibling folders under one temporary root, each with its own `sqlsource.json` (`postgres`, `mssql`), each `.sql` file resolving its own.
- [ ] **Step 2: Run; expect failures.  Step 3: Implement.  Step 4: Run; pass.**
- [ ] **Step 5: ruff, validate, commit.**  Message: "Port the dialect lookup of sqlsource.json".

### Task 7: The templater

**Files:**
- Modify: `sqlfluff_templater_sqlsource/templater.py`
- Create: `sqlfluff_templater_sqlsource/render.py`
- Create: `sqlfluff_templater_sqlsource/stand_ins.py`
- Test: `tests/test_render.py`, `tests/test_sqlfluff_end_to_end.py`, `tests/test_examples_parse.py`

**Interfaces:**
- Consumes: `build_plan` (Task 5), `resolve_dialect` (Task 6).
- Produces:
  ```python
  def stand_in(name: str, sqlfluff_dialect: str) -> str
  def compatible(sqlsource: DialectChoice, sqlfluff_dialect: str) -> bool
  def render(text: str, plan: Plan, sqlfluff_dialect: str, fname: str) -> TemplatedFile

  class SqlSourceTemplater(RawTemplater):
      name = "sqlsource"
      @large_file_check
      def process(self, *, in_str: str, fname: str, config: FluffConfig | None = None,
                  formatter: FormatterInterface | None = None) -> tuple[TemplatedFile | None, list[SQLTemplaterError]]
  ```

Rendering rules (from the spec; values decided here):
- Token → its default, trimmed, with the parameter spans inside it replaced by their stand-ins; no default → its name.  One `templated` slice over the whole `{{...}}`.
- Parameter → `stand_in`; List → `"(" + stand_in + ")"`.
- MarkerComment → itself, as a `templated` slice.
- QueryEnd → the white space and comments between the query's last SQL and the next SQL or `-- name:` become one `templated` slice rendered as `";" + that text`.  If that text is empty, render `;` over a zero-length source slice only if the tests below pass with it; otherwise extend the slice over the preceding lexeme's last character.
- Stand-ins, starting table (settle with the tests; record the final table in `stand_ins.py`): `@name` as written for `tsql`, `mysql`, `mariadb`, `sqlite`, `duckdb`, `snowflake`; `:name` for `postgres`, `oracle`; a literal for `ansi`, `db2` and any dialect not listed (start with `0`, check `'x'` and `p_<name>` if `0` sets off a rule).
- `compatible`: SqlSource `ansi` pairs with any sqlfluff dialect; `mssql` with `tsql`; `postgres` and `cockroachdb` with `postgres`, `duckdb`, `redshift`, `greenplum`, `materialize`; `mysql` with `mysql`, `mariadb`; `mariadb` with `mariadb`, `mysql`; `sqlite` with `sqlite`; `oracle` with `oracle`.  An incompatible pair → `SQLTemplaterError` naming both, no file.
- `plan.error` → `SQLTemplaterError` whose message starts with the code (`SQLSRC120: ...`) at the error's line and position; no file.
- Any exception from `build_plan` or `render` → `SQLTemplaterError("sqlsource templater failed: ...")`, never a crash.

- [ ] **Step 1: Write `test_render.py`**: each rendering rule on a small plan, asserting `templated_str` and that every templated slice's `source_slice` covers exactly the plan span.
- [ ] **Step 2: Write `test_sqlfluff_end_to_end.py`** using `sqlfluff.core.Linter` with `templater = sqlsource` and each dialect pair of the starting table:
  - `test_fix_never_changes_a_token_default_parameter_list_or_marker`: lint-fix a messy file (lower-case keywords, bad spacing) holding each kind; assert the fixed file is reformatted around them and every planned span's text is unchanged.
  - `test_inserted_semicolon_is_never_written` and `test_cv06_neither_fires_nor_adds_at_an_inserted_semicolon`.
  - `test_unclosed_comment_opener_does_not_swallow_sql`, `test_scoped_defaults_parse`, `test_parameter_in_default_parses_in_postgres`, `test_non_ascii_token_name_parses` (the four prototype failures).
  - `test_stand_in_sets_off_no_rule` per stand-in dialect: a query whose parameters sit after `=`, `LIKE`, `IN`, `LIMIT` and in a select list lints with no violation that the same query with literals would not have.
  - `test_crlf_file_keeps_its_tokens` (Review Focus 1): a CRLF file through `fix`, tokens intact.
  - `test_bom_file_is_not_shifted` (Review Focus 2).
  - `test_rejected_file_lints_without_crash` (Review Focus 5).
  - `test_dialect_mismatch_is_a_templating_error` and `test_stopping_error_names_its_code`.
- [ ] **Step 3: Write `test_examples_parse.py`**: every corpus case under `repo/examples/` renders and parses with no `PRS` or `TMP` violation under its natural sqlfluff dialect (`postgres` for `postgres`, `tsql` for `mssql`).  A case sqlfluff's own grammar cannot parse goes in `tests/examples_unparsable.txt`, one path per line with a `#` reason naming the sqlfluff construct; the file starts empty and every line is justified in review.
- [ ] **Step 4: Run; expect failures.  Step 5: Implement.  Step 6: Run** `uv run pytest`; all pass.
- [ ] **Step 7: ruff, validate, commit.**  Message: "Render the templating plan for sqlfluff".

### Task 8: Make the project part of the repository's checks

**Files:**
- Modify: `pre-commit-validation.sh`, `format.sh`, `tools/README.md`, `CONTRIBUTING.md`, root `AGENTS.md`

Decisions:
- `pre-commit-validation.sh`:
  - `is_known_kind`: every path under `src/sqlfluff-templater-sqlsource/` is known.
  - `needs_packages`: a path under `src/sqlfluff-templater-sqlsource/` does not need the packages (it is not packed by `dotnet pack`); place the case before `src/*`.
  - A `templater_trigger`, set by any path under `src/sqlfluff-templater-sqlsource/`, decided with the skip reason `nothing under src/sqlfluff-templater-sqlsource/ changed`.
  - Steps `templater lint` (`uv run ruff check`), `templater format` (`uv run ruff format --check`) and `templater tests` (`uv run pytest`), each run with `--directory src/sqlfluff-templater-sqlsource` (or `cd` in a subshell), after `actionlint` and before `site`, each `|| true` like the other checks.
  - The toolchain check: fail early with a clear message when `uv` is not on the path, as for Docker.
  - The header comment lists the templater checks.
- `format.sh`: a fourth part, `ruff`, which runs `uv run ruff check --fix` then `uv run ruff format` in the project (with `--check`: `ruff check` and `ruff format --check`).  It runs when no part is named.  Files after `--` limit the C# parts only; `ruff` ignores them.  `pre-commit-validation.sh` keeps calling `format.sh` for C# and runs ruff itself, so that the templater has its own steps in the summary.
- `CONTRIBUTING.md`: uv as a required tool (it installs Python 3.14.7 from `.python-version`), one sentence for the project, its three commands, and the corpus command of Task 2.
- Root `AGENTS.md`, Commits rule 4: "The script fails when Docker is not running, uv is not installed or the local tools have not been restored."  Keep the file under 16 KB.
- `tools/README.md`: the Formatting section names the `ruff` part.

- [ ] **Step 1: Check the plan output first**: `./pre-commit-validation.sh --plan src/sqlfluff-templater-sqlsource/sqlfluff_templater_sqlsource/lexer.py` must show the three templater steps run, no database pair, no package checks, and not `--full`.  Expected before the change: "of no known kind", full.
- [ ] **Step 2: Make the changes.**
- [ ] **Step 3: Re-run the plan check** (expected: as Step 1 says), `./pre-commit-validation.sh --plan src/SqlSource/Parsing/SqlLexer.cs` (no templater steps), and `tools/shellcheck.sh`, `tools/shfmt.sh --check`.
- [ ] **Step 4: `./format.sh ruff` leaves the tree unchanged; `./format.sh --check` passes.**
- [ ] **Step 5: Run the repository tests that read these files** (`tests/SqlSource.Repository.Tests`, which checks `AGENTS.md` sizes, `test-scopes.txt` and `CONTRIBUTING.md` links).
- [ ] **Step 6: `./pre-commit-validation.sh --full`, commit.**  Message: "Check and format the templater with the rest of the repository".

### Task 9: CI, nightly and versions

**Files:**
- Modify: `.github/workflows/build.yml`, `.github/workflows/nightly.yml`, `.github/workflows/README.md`
- Create: `tools/templater-version.sh`

**Interfaces:**
- Produces: `tools/templater-version.sh` prints the PEP 440 version from `VersionPrefix` in `Directory.Build.props` and the `VersionSuffix` environment variable: empty → `X.Y.Z`; `beta.N` → `X.Y.ZbN`; `pr-P.N` → `X.Y.Z.devN`; anything else → exit 1 with a message.  The `templater` job's artifact is named `templater` and holds `dist/*.whl` and `dist/*.tar.gz`.

Decisions:
- `build.yml`, job `templater`, alongside `lint`, no `needs`, `timeout-minutes: 10`: check out; `astral-sh/setup-uv` pinned by SHA, with the uv version pinned and caching on `src/sqlfluff-templater-sqlsource/uv.lock`; `uv python install` (reads `.python-version`); `uv run ruff check`; `uv run ruff format --check`; `uv run pytest`; the floor: `uv run --isolated --no-project --with-editable . --with "sqlfluff==4.0.0" --with pytest pytest` (if 4.0.0 fails for a reason in sqlfluff, raise the floor in `pyproject.toml` and here to the lowest 4.x that passes, and say so in the commit); `uv version "$(../../tools/templater-version.sh)" --frozen` (or the uv command of the pinned version that sets the project version without syncing); `uv build`; upload the artifact.  Every check step `if: ${{ !cancelled() }}`, as `lint` does.
- The header comment of `build.yml` lists the job; the floor run is CI's alone, as the floor images are.
- Root `AGENTS.md`, Commits rule 2: name the floor sqlfluff run beside the floor images ("after a change to the templater, also run its tests against `sqlfluff==4.0.0` with the command in `src/sqlfluff-templater-sqlsource/AGENTS.md`"), and put that command in the project's `AGENTS.md`.
- `nightly.yml`, job `templater-newest-sqlfluff`: the same setup, then `uv run --isolated --no-project --with-editable . --with "sqlfluff<5" --with pytest pytest`, so it takes the newest 4.x unlocked.
- `.github/workflows/README.md`: both jobs, and the artifact.

- [ ] **Step 1: Write the script and test it**: `VersionSuffix= tools/templater-version.sh` → `0.1.0`; `VersionSuffix=beta.7` → `0.1.0b7`; `VersionSuffix=pr-42.123` → `0.1.0.dev123`; `VersionSuffix=x` → exit 1.  Run shellcheck and shfmt on it.
- [ ] **Step 2: Add the jobs.  Step 3: `tools/actionlint.sh`.**
- [ ] **Step 4: Run each job's commands locally** in the project folder, the floor and newest commands included; build a wheel with a beta version and install it into a fresh `uv venv`, then `sqlfluff render` a corpus input with `templater = sqlsource`.
- [ ] **Step 5: `./pre-commit-validation.sh`, commit.**  Message: "Check, build and nightly-test the templater in CI".

### Task 10: Publishing and documentation

**Files:**
- Modify: `.github/workflows/publish.yml`, `docs/publishing.md`
- Create: `site/guides/sqlfluff.md`; Modify: `site/guides/toc.yml`
- Create: `docs/architecture/templater.md`; Modify: `docs/architecture/generator.md` (link from the parsing section) and any index of `docs/architecture/`
- Modify: root `AGENTS.md` (rule 4: the templater among the places a change to SqlSource's syntax updates)

Decisions:
- `publish.yml`, job `publish-pypi`: `needs: build`, `environment: pypi`, `permissions: id-token: write`, download the `templater` artifact to `dist/`; on a tag, fail unless the wheel and sdist are exactly `sqlfluff_templater_sqlsource-<tag without v>-py3-none-any.whl` and `sqlfluff_templater_sqlsource-<tag without v>.tar.gz`; then `pypa/gh-action-pypi-publish` pinned by SHA, with `skip-existing: true` so that a rerun after a partial failure is safe.  The `release` job needs `[publish, publish-pypi]` and attaches the wheel and sdist beside the `.nupkg` files.  The header comment says the `pypi` environment and the file name are part of the PyPI trusted publisher, and not to rename either.
- `docs/publishing.md`: the release is now three packages; the PEP 440 column in the version table; the one-time PyPI setup (a pending publisher for `sqlfluff-templater-sqlsource` naming `mbcrawfo/SqlSource`, `publish.yml` and environment `pypi`, and the `pypi` environment in the repository settings); rerunning the `publish-pypi` job after a failure.
- `site/guides/sqlfluff.md`, "Lint and format with sqlfluff": install (pip, uv tool with `--with`, pipx `inject`, pre-commit `additional_dependencies`), the `.sqlfluff` lines, a derived Docker image (`FROM sqlfluff/sqlfluff:<version>` and `RUN pip install sqlfluff-templater-sqlsource==<version>`), install the version equal to SqlSource's, migrations in a separate run with their own `.sqlfluff`, what a user sees (tokens, defaults, parameters, lists and marker comments are never changed; a query is checked with each token's default in place; a query without `;` is checked as if it had one; a mismatch of dialects and SqlSource errors that stop reading are reported as `TMP`).  Effects only: no plan, corpus, stand-in or slice.  Samples obey the site's rules (a `sql` sample names its dialect).
- `docs/architecture/templater.md`: the plan, the C# entry point, the corpus and its regeneration, the port's module-to-file map, rendering and stand-ins, and how the two sides keep each other honest.

- [ ] **Step 1: Write the docs and the workflow job.**
- [ ] **Step 2: `tools/actionlint.sh`; `tools/build-site.sh`; `tests/SqlSource.Repository.Tests`** (site structure, links, toc, AGENTS sizes).  Expected: pass.
- [ ] **Step 3: `./pre-commit-validation.sh`, commit.**  Message: "Publish the templater to PyPI with each release, and document it".

### Task 11: Whole-branch check

- [ ] **Step 1: `git fetch --tags origin`** and compare `VersionPrefix` with the highest `v*` tag; if it is not greater, ask the user for the new version (root `AGENTS.md`, Pull requests rule 1).
- [ ] **Step 2: `./pre-commit-validation.sh --full`**, and the templater's floor run.  Expected: pass.
- [ ] **Step 3: Run `branch-review:review`** and address its findings until it is clean, before offering a pull request.
