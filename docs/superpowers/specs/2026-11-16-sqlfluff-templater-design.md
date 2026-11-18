# A sqlfluff templater for SqlSource files

## Intent

Teams that use SqlSource want to lint and format their `.sql` files with [sqlfluff](https://sqlfluff.com).  Today they cannot.  A SqlSource file holds three things sqlfluff cannot parse with any templater it ships:

- **Tokens with defaults**, `{{name:default}}`.  The default `jinja` templater fails the whole file on the `:`, so one token hides every query in the file.  The `placeholder` templater can match a token but replaces it with its name, never its default, so a clause token (`WHERE 1 = 1 {{filter:AND deleted_at IS NULL}}`) does not parse.
- **Parameters**, `@id`.  sqlfluff's `ansi`, `postgres`, `oracle` and `db2` dialects cannot parse them.
- **Lists**, `IN @ids`.  No sqlfluff dialect parses `IN` without parentheses.

sqlfluff runs one templater per run, so a user cannot combine the placeholder templater for parameters with another for tokens.  We ship a templater, `sqlsource`, that reads a file exactly as SqlSource does and shows sqlfluff a query it can parse, so that `sqlfluff lint`, `fix` and `format` work on SqlSource files and never change a token, a default or a parameter.

A prototype (a subclass of `PlaceholderTemplater` that substituted each token's default by regex) showed that sqlfluff's slice mapping does what we need: `fix` and `format` reformat the SQL around tokens and leave every token as written.  It also showed why a regex is not enough (see [Fidelity](#fidelity)).

## Goals

- `sqlfluff lint`, `fix` and `format` work on SqlSource `.sql` files of every SqlSource dialect, with `templater = sqlsource`.
- The templater reads a file **exactly** as SqlSource's parser does: comments, quoted regions, markers, tokens, defaults, parameters and lists, by the rules of the file's SqlSource dialect.
- sqlfluff never rewrites a token, a default, a parameter or a list.
- Exactness is enforced by tests: a change to SqlSource's parser that changes what the templater must produce cannot merge without the templater changing to match.
- The package installs into any environment sqlfluff 4 runs in, the official `sqlfluff/sqlfluff` Docker image included (through a derived image), with no .NET.
- It is released in lockstep with SqlSource: same version, same tag, same workflow.

## Non-goals

- **Migration scripts.**  DbUp (`$name$`), Flyway (`${name}`), grate and RoundhousE (`{{Token}}`) and sqlcmd (`$(Var)`) variables are not handled.  A repository that lints migrations as well runs sqlfluff once per folder, each with its own `.sqlfluff`.  The user guide says so.  Nothing applies a second templater or a placeholder style.
- **The `keep-comments` generator parameter.**  The templater serves the file itself, not the SQL that SqlSource generates, so what SqlSource does with comments does not matter to it.
- **Linting what the caller passes at run time.**  A default is a sample; sqlfluff checks the query with the sample in place.
- **SqlSource's diagnostics.**  The templater is not a second validator.  It reports a SqlSource error only where it cannot build a plan (see [Errors](#errors)).
- **A differential or fuzz test against the C# parser** (approach B below).  It can be added later if the corpus proves to have gaps.
- **sqlfluff 3.**

## Anti-goals

- **Any edit by sqlfluff inside a token, a default, a parameter or a list.**  That would change the SQL a user's code runs.  A templater that lints well but lets `fix` touch one has failed.
- **Requiring .NET, the `sqlsource` tool or a running process** to lint.
- **Silently linting with the wrong rules.**  Where the templater cannot read a file as SqlSource does, it says so as a sqlfluff templating error rather than guessing.

## Approaches

Three ways to keep the templater exact were considered.

| | Approach | Trade-off |
|----|----|----|
| **A (chosen)** | Port the parsing to Python, and check it against a committed corpus of expected results written by the C# parser. | Two implementations, kept honest by a test on each side.  Exactness reaches as far as the corpus covers.  The Python job needs no .NET. |
| B | Port to Python, and compare it live against the C# parser in CI, with generated inputs. | Finds differences nobody wrote a case for, but the Python job needs .NET and a hidden command, and a fuzzer is a project of its own. |
| C | Do not port: the templater calls the `sqlsource` tool for the spans. | Exact by construction, but every sqlfluff user needs the .NET SDK and the tool at the matching version, which rules out the sqlfluff Docker image and slim CI images, and each file pays a .NET process start. |

A was picked because it gives the exactness asked for, keeps the user's install pure Python, and follows how the repository already keeps recorded output honest (the golden corpus of query methods, sidecars).

## Fidelity

These cases, from the investigation, are what "exactly" must get right.  A regex or a dialect-blind scanner fails each of them.

- An unclosed `{{name:` in a comment must not swallow the SQL after it.
- Defaults belong to one query: two queries of a file can give one name different defaults, and a `-- token:` marker gives the default of its own query only.
- `-- token-ignore:` keeps a name's text as written.
- A parameter inside a default is swapped too (`{{paging:LIMIT 20 OFFSET @page}}`).
- A token name is any C# identifier that is not a keyword, non-ASCII letters included.
- Dialect rules: PostgreSQL dollar quoting and nested block comments, SQL Server `[bracketed]` names, MySQL `#` comments and backslash escapes, `E'...'`, and each dialect's options.
- A list is found by SqlSource's rule: a comment between `IN` and the parameter counts as a space, and `position(a IN @p)` is not a list.
- `@@rowcount` and an `@` inside a name are not parameters, and an `@` inside a quoted region or comment is not one.

## What the templater does

### Configuration

- Name: `sqlsource`.  Package: `sqlfluff-templater-sqlsource` (free on PyPI as of 2026-11-16).  Registered through the `sqlfluff` entry point with a `get_templaters` hook.
- The user sets `templater = sqlsource` and sqlfluff's own `dialect` in the `.sqlfluff` of the folder sqlfluff runs from (sqlfluff ignores a templater set in a subfolder's config).
- No configuration of its own is required.  *(My call.)*

### The SqlSource dialect of a file

Resolved as SqlSource resolves it ([settings](../../../site/reference/settings.md#setting-dialect), [settings files](../../../site/reference/settings-file.md#settings-files)): the `-- dialect:` marker in the file header, else the nearest `sqlsource.json` above the file that sets a valid one, else `ansi`.  Options come with the dialect.

This is part of the port and of the corpus, not only of the Python unit tests.  *(My call, from the exactness goal.)*  The port covers what `src/SqlSource/SettingsFiles/` does for the dialect: JSONC (comments, trailing commas), stopping at `"root": true`, skipping a file that is not valid JSON (SQLSRC015), a value of the wrong type (SQLSRC018) or an empty or invalid dialect, and matching the name `sqlsource.json` as SqlSource does.  A corpus case holds the chain of settings-file texts above its `.sql` file, and the C# side resolves it with the real settings reader.

### The plan

For each file the templater builds a **plan**: the list of spans of the file to replace, each with its kind.  The plan is the part that must be exact, and the part the corpus records.

- **Token**: the span of the whole `{{...}}` in the SQL outside comments, its name, and the default its query resolves for it (inline or from a `-- token:` marker), or none.  A name the query ignores is no token.  A `{{...}}` in a comment is not in the plan.
- **Parameter**: the span of each `@name` that SqlSource reads as a parameter, outside tokens, with its use: list or not.
- **Parameters inside a default**: the same, at their place inside the token's default, since the default is what sqlfluff will see.
- **Query ends**: for each query but the file's last whose SQL does not end in `;`, the place right after its last SQL lexeme.  A SqlSource query runs to the next `-- name:`, so `;` is optional, and sqlfluff's dialects other than `tsql` cannot parse two statements with nothing between them.  *(The owner's decision.)*

### Rendering

The plan is rendered into a `TemplatedFile`, each span one `templated` slice and the text between `literal` slices, as the prototype did.

- A token becomes its default, trimmed, with the parameters inside it swapped.  A token without a default becomes its name.
- A parameter becomes a stand-in that the **sqlfluff** dialect parses wherever a value can stand.  A list becomes `(<stand-in>)`.
- A query end gets a `;` that only sqlfluff sees.  The suggested way is a templated slice over the white space and comments that follow the query's last SQL, rendered as `;` followed by that same text, so that the slice has source text to map to; a zero-length source slice may confuse sqlfluff's fixes.  Tests must show that `fix` and `format` never write a `;` into the file, never move one, and that CV06 neither fires on nor adds a `;` at an inserted one.
- Starting points, from parsing `= x`, `LIKE x` and `IN (x)` with sqlfluff 4.5.0:

  | sqlfluff dialect | `@name` parses | Candidate stand-in |
  |----|----|----|
  | `tsql`, `mysql`, `mariadb`, `sqlite`, `duckdb`, `snowflake` | yes | `@name` as written |
  | `postgres`, `oracle` | no | `:name` |
  | `ansi`, `db2` | no | a literal or identifier, such as `0`, `'x'` or `p_name` (`:name` fails) |

  The stand-in must parse wherever SqlSource lets a parameter stand and must not set off a rule of sqlfluff's default set that the user's parameter would not (`= NULL` sets off CV05, so `NULL` is out).  The builder settles the table with tests; a dialect not in it gets the `ansi` stand-in.

The plan comes from the **SqlSource** dialect and the stand-ins from the **sqlfluff** dialect.  When the two are a pair that cannot both be right (SqlSource `postgres` with sqlfluff `tsql`, say), the templater reports a templating error naming both.  SqlSource's `ansi` pairs with any sqlfluff dialect.  *(My call; the builder writes the table of pairs, starting from the "Use it for" column of [Dialect names](../../../site/reference/dialects.md#choosing-a-dialect).)*

### Markers

Every line comment in marker form (`--`, optional blanks, a keyword and a colon), wherever it stands, is a templated span of the plan that renders as itself, so sqlfluff cannot move or reflow it.  *(My call.)*  Why: a fix that moved an inline `-- token: ...` after SQL onto a line of its own (LT05 with `trailing_comments = before`) would turn it into a marker, and one that moved a marker after its query's last SQL would make it SQLSRC108.  Both change how SqlSource reads the file.  The corpus records these spans with the rest of the plan.

### Errors

**Exactness is promised for files SqlSource accepts.**  *(My call.)*  SqlSource's parser gives no blocks for a file with any error, so the C# side has no plan to record for one, and matching every file error would mean porting all of SqlSource's validation, which is a non-goal.

- The errors that stop the port itself (an unterminated quoted region or block comment, an invalid `-- dialect:` marker, `SQLSRC120`) give a `SQLTemplaterError` naming the SqlSource diagnostic and its place, which sqlfluff reports as `TMP`.  The corpus records these.
- For a file with any other SqlSource error (SQL before the first `-- name:`, conflicting defaults, a duplicated name, an undeclared parameter in a default), the templater builds a best-effort plan and reports nothing: where defaults conflict, the first one renders.  The corpus holds no such files, and the user guide says to fix SqlSource's errors first.
- Errors only a method has (`SQLSRC127`) and errors of the kept form do not stop a file, so those files are in the corpus.

### Newlines and offsets

sqlfluff normalises line endings to `\n` before templating, so the templater plans the normalised text.  Offsets in the corpus count Unicode code points, not UTF-16 units, so that Python and C# agree on text outside the Basic Multilingual Plane.

## What is ported

Only what the plan needs, from `src/SqlSource/Parsing/`:

| C# | What the port needs | Size today |
|----|----|----|
| `SqlLexer`, `SqlDialectRules`, `Parsing/Quoting/` | All of it: lexemes by the rules of each dialect and its options | about 870 lines |
| `Parsing/Tokens/TokenScanner.cs`, `SqlTokenList.cs` | Finding tokens and resolving each query's defaults | about 420 lines |
| `SqlDialectMarker`, `SqlDialectName` | The `-- dialect:` marker and dialect names and options | |
| `Parsing/Markers/` | Recognising a marker, and the values of `-- name:`, `-- token:` and `-- token-ignore:` only | |
| `SqlListUses.Find` | Only whether a use is a list (the word before it is `IN`, the `position` exception, comments as spaces); not `Conditions`, `Places`, negation or null tests | |
| `SqlTextBuilder` | Only as far as a `-- token:` marker's default with a comment needs it | |

The lexer, the dialect rules and the quote readers changed in 7 commits in the three weeks before this spec; the token scanner in none.  So the templater changes whenever SqlSource changes how it reads SQL, not only when the token syntax changes.

## The corpus

- **Where**: in the Python project, `src/sqlfluff-templater-sqlsource/tests/corpus/`, so that a change to it triggers the project's CI job.
- **What a case holds**: the input `.sql` text, the settings that give its dialect, and the expected result: the resolved dialect and options, then either the plan (each span with its kind, name, resolved default and list use, the protected marker comments and the query ends, in code points) or the error that stops it.
- **Inputs**:
  - every `.sql` file in the repository outside `bin/` and `obj/` (examples, end-to-end projects, realistic cases, test fixtures);
  - the cases of the C# parser's tests that bear on the plan, at least each cell of `Lex_Construct_IsReadByTheRulesOfTheDialect` and the token, default, ignore, list and `SQLSRC120` cases;
  - the fidelity cases above.
- **The C# side**: a test in `tests/SqlSource.Tests/Parsing/` builds each case's expected result from the real parser and compares it with the committed file, and writes the file under an environment variable, as `GoldenFiles` does with `SQLSOURCE_UPDATE_GOLDEN=1`.  It fails when a file is stale, missing or orphaned, so a parser change cannot merge without regenerating the corpus.  It runs in `pre-commit-validation.sh` like every other test of `tests/SqlSource.Tests`.
- **The Python side**: a test builds the plan of each case with the port and compares it with the expected result.
- How a plan is computed from the parser's public and internal results is left to the builder; the test may need a small internal method on the C# side that returns the plan.  It must not be computed by a second implementation in the test.

## The Python project

`src/sqlfluff-templater-sqlsource/`, self-contained:

- `pyproject.toml` with the package metadata, the `sqlfluff` entry point, `requires-python = ">=3.10"` (as sqlfluff's), and `sqlfluff>=4.0,<5`.  *(My calls: the package must install into whatever Python the user's sqlfluff runs on, so 3.14.7 is the development pin only; sqlfluff 4.0 was released 2026-01-15 and its templater API is internal, so the range is capped at the next major.)*
- `.python-version` with `3.14.7`, the development and CI interpreter.
- [uv](https://docs.astral.sh/uv/) for the environment, the lock file (`uv.lock`), running tests and building.  *(My call: it is already the tool used to investigate this, and it builds and publishes.)*
- ruff for linting and formatting, configured in `pyproject.toml`.
- pytest for tests.
- Its own `.editorconfig` (with `root = true`), `.gitignore` and `.gitattributes`.
- `README.md`: the package's page on PyPI, short, with absolute links to the site's guide, like the two packed readmes.
- `AGENTS.md`: that the corpus is written by the C# test and never edited by hand, that the port mirrors the C# files it names, and anything else an agent needs that is not obvious from the code.
- Type checking (mypy or pyright) is not included.  *(My call.)*

**The project is a normal part of the repository's checks.**  *(The owner's decision: a port this size that moves in lockstep with the generator is checked like the rest of the code.)*

- `pre-commit-validation.sh` runs `ruff check`, `ruff format --check` and `pytest` for the project.  Its scoped run treats the project's files (`.py`, `pyproject.toml`, `uv.lock`, `.python-version`, `.gitattributes` and the corpus) as known kinds, and runs the Python checks when a file of the project changed, as it already runs a check only for what a change can affect; `--full` always runs them.  A change to a file of the project must not make the run `--full`.
- `format.sh` runs `ruff format` and `ruff check --fix` on the project.
- uv becomes a required tool of the repository, beside Docker and the .NET SDK, and the script fails without it, as it does without Docker.  It installs the project's Python itself from `.python-version`.
- `tools/test-scopes.txt` gets a `none src/sqlfluff-templater-sqlsource/*` line so that a change to the project alone does not run the database test projects; the corpus files are written by a test of `tests/SqlSource.Tests`, which is not a container project.

## CI and release

### CI (`ci.yml` / `build.yml`)

- A `templater` job in `build.yml` that runs on every change, like the other jobs, and starts at once as they do.  *(The owner's decision.)*  `ci.yml`'s gate job needs it through `build`.
- Steps: set up uv with the pinned Python, `ruff check`, `ruff format --check`, `pytest` with the locked sqlfluff, then `pytest` again with the lowest allowed sqlfluff (`uv run --resolution lowest-direct` or an explicit `sqlfluff==4.0.0`), then `uv build` with the run's version, uploading the wheel and sdist for `publish.yml`.
- The checks mirror those `pre-commit-validation.sh` gains, as `build.yml`'s header requires.  The run against the lowest sqlfluff is CI's alone, as the floor images of the database tests are; the root `AGENTS.md`'s push rule names it beside them.

### Nightly (`nightly.yml`)

A job runs the templater's tests against the newest sqlfluff release, unlocked.  CI runs against the locked version, so without it a sqlfluff release that breaks the internal API we build on would go unnoticed until a user reported it.

### Versions

The Python version is derived from `VersionPrefix` in `Directory.Build.props` and the build's suffix, mapped to PEP 440:

| Build | NuGet | PyPI |
|----|----|----|
| Tag `v0.2.0` | `0.2.0` | `0.2.0` |
| Manual run on `main` (beta) | `0.2.0-beta.123` | `0.2.0b123` |
| Pull request | `0.2.0-pr-42.123` | `0.2.0.dev123` (built, never published) |
| Local | `0.2.0-dev` | `0.2.0.dev0` |

How the version reaches `pyproject.toml` (a dynamic version, or a step that writes it before `uv build`) is left to the builder.

### Publishing (`publish.yml`)

- The templater is built by `build.yml` and published by `publish.yml` on the same triggers as the NuGet packages: a `v*` tag, or a manual beta run on `main`.
- PyPI **trusted publishing**: a separate job with `environment: pypi` and `permissions: id-token: write`, using `pypa/gh-action-pypi-publish` pinned by SHA, which also uploads attestations.  No token is stored.
- On a tag, the job fails unless the wheel and sdist carry exactly the tag's version, as the NuGet job does.
- The first release needs a **pending publisher** on PyPI for the project name, naming the repository, `publish.yml` and the `pypi` environment, and the `pypi` environment in the repository's settings.  `docs/publishing.md` records both, beside the nuget.org policy.
- Whether a failed PyPI push should fail the release after NuGet succeeded, and how to rerun it, is left to the builder, following how `publish.yml` handles a failed second NuGet push.

## Keeping it in sync

- `src/SqlSource/Parsing/AGENTS.md` gains a rule: a change to `SqlLexer`, `SqlDialectRules`, `Parsing/Quoting/`, `Parsing/Tokens/`, the markers the templater reads, the dialect names or the list rule changes the templater's port in the same pull request, and the corpus is regenerated with the environment variable.
- The token reference's rules are the user-facing contract; the root `AGENTS.md` names the templater among the places a change to SqlSource's syntax must update.
- The corpus test is what enforces it: the C# side fails when the corpus is stale, and the regenerated corpus fails the Python job until the port matches.

## Documentation

- **`site/guides/`**: a new guide, "Lint and format with sqlfluff": install (pip, uv, pipx, pre-commit with `additional_dependencies`), the `.sqlfluff` lines, a derived Docker image (`FROM sqlfluff/sqlfluff:<version>` plus `pip install`), matching the package version to SqlSource's, separate runs for migration folders, and what a user sees: tokens, defaults and parameters are never changed, and a default is what the query is checked with.  Effects only, per the root `AGENTS.md`.  It goes in the guides' `toc.yml`.
- **`docs/architecture/`**: a page for the templater: the plan, rendering, the corpus and how the two sides keep each other honest.
- **`CONTRIBUTING.md`**: uv as a required tool, one sentence for the project, and its commands.
- **Root `AGENTS.md`**: the toolchain rule ("It needs the full toolchain") names uv, and the push rule names the run against the lowest sqlfluff as one the script does not do.
- **`docs/publishing.md`**: the PyPI package in the release, the version mapping, and the configuration outside the repository (pending publisher, `pypi` environment).
- **`.github/workflows/README.md`**: the templater job and the nightly job.
- **`tools/README.md`**: the formatting section, for what `format.sh` now formats.
- **`README.md`**: no change; it holds no tooling beyond its three outputs.

## Left to the builder

- The final stand-in table and the table of compatible dialect pairs, settled by tests.
- The corpus's file format and layout (one file per case or one per source, JSON).
- How the C# test obtains the plan from the parser, and how the version reaches `pyproject.toml`.
- The Python module layout of the port.
- How a failed PyPI push is handled in `publish.yml`.

## Calls made without the owner

- `requires-python = ">=3.10"`, with 3.14.7 as the development pin only.
- sqlfluff `>=4.0,<5`, tested at both ends, plus a nightly run against the newest release.
- uv as the Python tool; pytest; no type checker.
- The templater has no configuration of its own.
- A mismatch between the SqlSource and sqlfluff dialects is a templating error.
- The file is named `AGENTS.md`, not `agents.md`, to match the repository and `AgentsFileTests`.
- A `none` line in `tools/test-scopes.txt` for the project.
- The `sqlsource.json` lookup is ported and recorded in the corpus.
- Exactness covers files SqlSource accepts; other files get a best-effort plan.
- Marker-form comments are protected from sqlfluff's fixes.
