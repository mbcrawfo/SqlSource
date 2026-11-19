# sqlfluff for the repository's own SQL

## Intent

The repository's `.sql` files get formatted and linted by sqlfluff, run with our own templater from source, locally and in CI.  Two reasons: the queries, above all the examples that users copy, read consistently; and every run dogfoods `sqlfluff-templater-sqlsource` on a real corpus of ~200 queries in two dialects, so a templater bug that breaks a real query shows up as a `TMP` or `PRS` before a user hits it.

## Goals

- `tools/sqlfluff.sh` builds an image from `sqlfluff/sqlfluff` pinned by version and digest, with the templater installed from `src/sqlfluff-templater-sqlsource`, and runs sqlfluff in it.
- `format.sh sql` formats the SQL alone; `format.sh` with no part runs it with the rest; `format.sh --check sql` is the format check.
- A lint check: test SQL under a relaxed rule set, the example projects under sqlfluff's full rule set.
- `pre-commit-validation.sh` runs both checks when a change can affect them, and CI runs both on every pull request.
- sqlfluff never reads `.claude/` (the worktrees), the fixtures whose exact text tests depend on, or `bin/`/`obj/`.
- Agents know to format before they describe, and how to do it quickly.

## Non-goals

- No change to the templater's behaviour.  A templater bug the dogfooding finds is fixed in its own change, or recorded in `docs/tech-debt` and the file excluded.
- No sqlfluff on the Python templater's own test corpus, `src/sqlfluff-templater-sqlsource/tests/`.
- No change to the examples' generated API, and none to `site/guides/sqlfluff.md`.  The site pages that quote example SQL change only to keep quoting it as written.

## Anti-goals

- A check that a developer cannot reproduce by hand: running `sqlfluff` from the repository's root must give what CI gives.
- Editing a fixture's text, or a templating case, to satisfy a linter.
- An unpinned image.

## Constraints found

- **A query's hash covers its SQL as written**, case and white space included (`SqlQueryHash`), so formatting a described query makes its sidecar stale.  The first reformat re-describes every sidecar it touches with `tools/describe-end-to-end.sh`, in the same PR; afterwards each SQL edit is formatted before it is described.
- **sqlfluff 4.5.0 is the current release**, the templater's floor and `uv.lock`'s version.  The pinned image is `sqlfluff/sqlfluff:4.5.0` by digest.
- **The templater must be set in the `.sqlfluff` of the folder sqlfluff runs from**; a nested `.sqlfluff` can set the dialect and rule options.
- **Default lint findings** (sqlfluff 4.5.0, default rules, 216 in-scope files):
  - Layout and capitalisation (`LT`, `CP`): ~2,000, nearly all fixed by `sqlfluff format`.  `LT05` falls to zero on the Postgres files at `max_line_length = 120`, the `.editorconfig` width.
  - Rules test SQL breaks on purpose: `AL01` 167, `ST09` 108, `AL03` 104, `AM05` 72, `RF04` 50, `AM04` 20, `AM03` 14, `ST06` 16.
  - A handful of others: `CV08`, `ST05`, `ST11`, `AL05`, `AL02`, `AL08`, `RF01`–`RF03`, `RF05`, `RF06`, `ST02`, `CP05`, `LT06`.
  - Cannot be checked: the mixed-dialect files of `tools/package-install/Queries` (`AddedByATarget.sql`, `ByHeader.sql`, `ByMarker.sql`, `ByOption.sql`) and `tests/SqlSource.Tests/EndToEnd/Dialects/`; `ByOption.sql` uses `no-backslash-escapes`, which no sqlfluff dialect reads.
  - `PRS`: `tests/DatabaseTests/Postgres/Realistic/Cases/table-statement/query.sql` (`TABLE x`, which sqlfluff's postgres dialect does not parse); `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql` (tokens without defaults, on purpose); `examples/SqlServerExamples/Sql/CountQueries.sql` (`{{table}}` without a default).
  - Skipped for size: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Batches.sql`, 20,438 bytes, over the 20,000-byte `large_file_skip_byte_limit`.

## Approaches

- **A. Config files in the folders (chosen).**  The root `.sqlfluff` sets the templater, `max_line_length = 120`, the size limit and the `exclude_rules` for test SQL; a `.sqlfluff` in each dialect folder sets its dialect; `examples/.sqlfluff` clears `exclude_rules`; `.sqlfluffignore` holds the exclusions.  It is the setup `site/guides/sqlfluff.md` tells users to build, so the docs are dogfooded too, and a hand run of `sqlfluff` matches CI.  Cost: about six small config files.
- **B. Everything in the script.**  File lists per dialect and `--dialect`/`--rules` flags in `tools/sqlfluff.sh`.  One place for scope, but a hand or editor run gets none of it, and it exercises no setup a user would build.
- **C. A `-- sqlfluff:` directive per file.**  Handles mixed-dialect folders, at the cost of a line in ~200 files, including the examples users read.

The owner chose A.

For the examples, two ways were weighed: the full rule set with deliberate cases marked `-- noqa`, the pages that quote them updated (chosen); or rules that change an example's purpose or layout (`AM04`, `RF04`, `LT09`) turned off in `examples/.sqlfluff`, which keeps the pages as they are but gives up the full rule set.

## Decided

- **The image.**  `tools/sqlfluff.sh` builds `FROM sqlfluff/sqlfluff:4.5.0@sha256:…` and installs the templater from its source folder, which is the build context, so Docker's cache rebuilds only when the templater changes.  The run then mounts the repository and runs sqlfluff from its root, as `shfmt.sh` and `actionlint.sh` run their images.
- **The configs.**
  - `.sqlfluff` at the root: `templater = sqlsource`, `max_line_length = 120`, a `large_file_skip_byte_limit` above `Batches.sql`, and `exclude_rules` with the rules that test SQL breaks on purpose, each with a comment saying why.
  - The root sets no dialect (owner's call: more SqlSource dialects, such as MySQL and SQLite, will come).  Each folder of one dialect has a `.sqlfluff` that sets it: `dialect = postgres` in `examples/PostgresExamples`, `tests/DatabaseTests/Postgres`, `tests/SqlSource.Tests/EndToEnd` and `tools/package-install`; `dialect = tsql` in `examples/SqlServerExamples` and `tests/DatabaseTests/SqlServer`.  A `.sql` file in a folder that no `.sqlfluff` gives a dialect fails the check, so a new folder must choose one.
  - `examples/.sqlfluff`: no excluded rules, so the examples meet sqlfluff's full rule set (owner's choice, option A).  Their findings are fixed in the SQL, `LT09`'s one target per line included.  What an example exists to show stays, marked `-- noqa: <rule>` with a reason a reader can follow: the `SELECT *` of `Models/Users.sql` that shows the table model (`AM04`), and the keyword column names `name` and `location` of `schema.sql` (`RF04`).  Each fix leaves the generated API as it is.
- **Pages that quote example SQL** are updated in the same PR to quote it as it now stands: `site/index.md`, `site/start/first-query.md`, `site/guides/lists.md`, and any other a search for the examples' queries finds.  `README.md:77` quotes it too; the owner maintains `README.md`, so the change it needs is brought to the owner, not made.
  - `.sqlfluffignore`: `.claude/`, `bin/`, `obj/`, `src/sqlfluff-templater-sqlsource/`, `tests/SqlSource.Tool.Tests/Fixtures/`, `tests/SqlSource.Tests/Parsing/Templating/Cases/`, `tests/SqlSource.Tests/EndToEnd/Dialects/`, the four mixed-dialect files of `tools/package-install/Queries`, `Realistic/Cases/table-statement/` and `EndToEnd/Tokens/Search.sql`, each with a comment saying why.
- **`format.sh` gets a `sql` part**, beside `style`, `analyzers`, `csharpier` and `ruff`; `format.sh sql` runs `tools/sqlfluff.sh fix` (or `format`), and `--check sql` the format check.  Agents may also call `tools/sqlfluff.sh` directly.
- **pre-commit-validation.sh** gets a SQL trigger, set off by a `.sql` file, a `.sqlfluff` or `.sqlfluffignore`, `tools/sqlfluff.sh`, or anything under `src/sqlfluff-templater-sqlsource/` (a templater change can change what sqlfluff reports), with steps for the format check and the lint.  `.sqlfluff` and `.sqlfluffignore` become known kinds in `is_known_kind`.
- **CI**: steps in `build.yml`'s `lint` job, beside shfmt and actionlint, each with `if: ${{ !cancelled() }}`.
- **Test SQL: suppress what is deliberate, fix what is incidental** (my call, from the anti-goal).  A finding on SQL that a test exists for, such as `right-join-orders-accounts` (`CV08`), `left-joined-subquery-coalesce` (`ST05`), `two-columns-named-id` (`AL08`), the special-character identifiers of `EndToEnd/Queries/Notes.sql` (`RF05`) or the quoted `"char"` (`RF06`), is never rewritten.  A rule that test SQL breaks on purpose across many files goes in the root `exclude_rules`; one broken on purpose in a few places stays on, with `-- noqa: <rule>` and a reason on those lines.  `CP02` is excluded in the SQL Server test folders, whose mixed-case names, such as `dbo.Email`, reach the describer's output.  Fixing a finding changes nothing but the query's form: a fix that would change a test's coverage or a sidecar's content beyond its hash is a suppression instead.
- **The first reformat** runs `format.sh sql`, fixes or suppresses the remaining lint as above, then re-describes the touched sidecars with `tools/describe-end-to-end.sh`, all in the same PR.  It also regenerates the templater's copy of the repository's `.sql` files, `src/sqlfluff-templater-sqlsource/tests/corpus/repo/`, through `TemplatingCorpusTests`, and drops `CountQueries.sql` from `tests/examples_unparsable.txt` if that file comes to parse.
- **"Format before you describe"** goes in the AGENTS.md files beside the describe workflow (`tools/AGENTS.md`, `tests/DatabaseTests/` and its engine folders, `examples/`), and the procedure in `tools/README.md` and the DatabaseTests readmes.
- **Docs**: `CONTRIBUTING.md` (the new check and command), `tools/README.md`, `.github/workflows/README.md`, and the root `AGENTS.md`'s templater rule if it needs to name the new files.

## Left to the builder

- `fix` versus `format` for `format.sh sql`, and whether `fix` changes anything in test SQL that `format` would not.
- Whether `tools/sqlfluff.sh` takes `git ls-files` paths, as `shfmt.sh` does, or `.`; `.sqlfluffignore` must hold the exclusions either way, so a hand run matches.
- Whether the tool scripts' `--check` convention or sqlfluff's own subcommands make the clearer interface.
- `CountQueries.sql`: give `{{table}}` a default when that leaves the generated API as it is; otherwise mark the line `-- noqa: PRS` with a reason.
- Any test that asserts on a query's exact text and breaks under the reformat: update the expectation, or exclude the file with a reason.
- Whether `.gitattributes` needs entries for the new config files.
