# sqlfluff for the Repository's SQL: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Format and lint the repository's `.sql` files with sqlfluff and our own templater, built from source in a pinned Docker image, both locally and in CI.

**Architecture:**
- `tools/sqlfluff.sh` builds a local image from the pinned `sqlfluff/sqlfluff` image, with the templater installed from `src/sqlfluff-templater-sqlsource`.
- It runs sqlfluff on `.` from the repository root, so the folder configs (`.sqlfluff` and `.sqlfluffignore`) decide the dialect, rules and scope, exactly as a hand run would.
- Callers: `format.sh` gets a `sql` part; `pre-commit-validation.sh` and the `lint` job of `build.yml` run the check.

**Tech Stack:** bash (bash 3.2 compatible, as the other scripts are), Docker BuildKit, sqlfluff 4.5.0, the `sqlsource` templater (Python, hatchling).

**Spec:** `docs/superpowers/specs/2026-11-18-sqlfluff-repo-tooling-design.md`

## Global Constraints

- **The image:** `sqlfluff/sqlfluff:4.5.0@sha256:8a408e8f236e922fc128e06d634c74f4c683394970c8ffc5d44100a341e3adf0`, named in `tools/sqlfluff.sh` alone.  `tools/AGENTS.md` says a new linter version changes the tag and digest in the script and nothing else.
- **Width and size:** `max_line_length = 120`.  `large_file_skip_byte_limit = 50000`, so `Batches.sql` (20,438 bytes) is checked.
- **Rules relaxed for test SQL** (root `exclude_rules`): `AL01, AL03, AM03, AM04, AM05, RF04, ST06, ST09`.  The SQL Server test folder adds `CP02`.  `examples/` excludes nothing.
- **Never rewrite deliberate SQL to satisfy a rule.**
  - A finding on SQL that a test or example exists for gets `-- noqa: <rule>`, with a reason on the same or the preceding line, or an exclusion.  It is never rewritten.
  - A fix may change only a query's form: case, white space, line breaks, and keywords such as `AS` or `INNER`.  It must never change what a test covers, or a sidecar beyond its `hash` and provenance.
- **Never edit by hand:** the Tool.Tests fixtures, `tests/SqlSource.Tests/Parsing/Templating/Cases/`, or the templater's `tests/corpus/`.
- **Format before you describe.**  Re-describe with `tools/describe-end-to-end.sh` after any reformat of a described query, and read the sidecar diff before committing: only `hash` (and version/provenance) may change.
- **Every commit passes `./pre-commit-validation.sh`**, run after the last edit.  Never commit past a failure.
- **`README.md` is the owner's.**  Collect the change it needs and report it; never edit it.
- **AGENTS.md rules:** one rule per bullet, at most 80 words, file at most 8 KB.  `AgentsFileTests` checks this.  A file that names files ends with the maintenance footer.
- **Commit trailer:** `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Review Focus

1. **Files rewritten by `fix` are owned by root.**  The rewrite run must use `--user "$(id -u):$(id -g)"`, as `shfmt.sh` does, so a rewritten file keeps its owner.  Task 1 Step 5 checks this.
2. **The image goes stale after a templater edit.**  A change to `sqlfluff_templater_sqlsource/*.py` must reach the next run.  Task 1 Step 6 checks this.
3. **The main checkout lints the agents' worktrees.**  A run from a checkout that has `.claude/worktrees/` must not read them.  Task 1 Step 4 checks this.
4. **A folder gets the wrong dialect, or none.**  A nested `dialect` that is not honoured shows up as `TMP` on every file of that folder.  A `.sql` file in a new folder with no dialect must fail the check with sqlfluff's "no dialect" error, not pass silently.  Task 1 Step 3 checks both.
5. **`exclude_rules` in `examples/` does not clear the root list.**  If the empty override is ignored, the examples silently get the relaxed rules.  Task 1 Step 3 checks that `AL01` fires in `examples/` and not in `tests/`.

---

### Task 1: `tools/sqlfluff.sh`, the image and the configs

The check is not wired into any caller yet: the tree still has findings until Tasks 2 and 3.

**Files:**
- Create: `tools/sqlfluff.sh`.  Usage: `tools/sqlfluff.sh [--check] [-- sqlfluff args...]`.
  - Default: `sqlfluff fix .`, rewriting files.
  - `--check`: `sqlfluff lint .`.
  - Arguments after `--` replace the subcommand and `.`, so an agent can run `tools/sqlfluff.sh -- lint examples` or `tools/sqlfluff.sh -- fix tests/DatabaseTests/Postgres/EndToEnd/Queries/Accounts.sql`.
- Create: `tools/sqlfluff.Dockerfile`, with `ARG SQLFLUFF_IMAGE`, `FROM ${SQLFLUFF_IMAGE}`, a `COPY` of the templater, and `pip install --no-cache-dir` of it.
- Create: `tools/sqlfluff.Dockerfile.dockerignore`.  BuildKit's per-Dockerfile ignore file excludes everything but `pyproject.toml`, `README.md`, `LICENSE` and `sqlfluff_templater_sqlsource/`, so `.venv` and `tests` never enter the context.
- Create `.sqlfluff`:
  - `[sqlfluff]` with `templater = sqlsource`, no `dialect`, `max_line_length = 120`, `large_file_skip_byte_limit = 50000` and `exclude_rules` (the eight rules).
  - A comment for each excluded rule giving why test SQL breaks it.
  - Set `processes = 0` in the config or on the command line; it's the implementer's choice.
- Create `.sqlfluffignore`, one line per entry, each with a `#` reason above it:
  - `.claude/`, `bin/`, `obj/`, `artifacts/` and `TestResults/`
  - `src/sqlfluff-templater-sqlsource/`
  - `tests/SqlSource.Tool.Tests/Fixtures/`
  - `tests/SqlSource.Tests/Parsing/Templating/Cases/`
  - `tests/SqlSource.Tests/EndToEnd/Dialects/`
  - `tools/package-install/Queries/AddedByATarget.sql`, `ByHeader.sql`, `ByMarker.sql` and `ByOption.sql`
  - `tests/DatabaseTests/Postgres/Realistic/Cases/table-statement/`, because sqlfluff's postgres dialect does not parse `TABLE`
  - `tests/SqlSource.Tests/EndToEnd/Tokens/Search.sql`, whose tokens have no default on purpose
- Create: `tests/DatabaseTests/SqlServer/.sqlfluff`, with `dialect = tsql` and `exclude_rules` set to the eight rules plus `CP02`, whose reason is that mixed-case names reach the describer's output.
- Create: `examples/.sqlfluff`, with `exclude_rules` emptied.
- Create: `examples/SqlServerExamples/.sqlfluff` with `dialect = tsql`.
- Create, each with `dialect = postgres`:
  - `examples/PostgresExamples/.sqlfluff`
  - `tests/DatabaseTests/Postgres/.sqlfluff`
  - `tests/SqlSource.Tests/EndToEnd/.sqlfluff`
  - `tools/package-install/.sqlfluff`

  The root sets no dialect, because other SqlSource dialects will come.
- Modify: `.gitattributes`, only if `.sqlfluff`, `.sqlfluffignore` or the Dockerfile need an `eol` entry next to the shell scripts'.
- Modify: `tools/README.md`, adding a "sqlfluff" section under "Linters" (or under "Formatting") with the usage above and "format before you describe".

**Interfaces:**
- Produces: `tools/sqlfluff.sh` (fix), `tools/sqlfluff.sh --check` (lint; exits non-zero on any finding) and `tools/sqlfluff.sh -- <args>` (pass-through).  The image tag is local, for example `sqlsource-sqlfluff:local`.  Later tasks rely only on these three call forms.

- [ ] **Step 1: Write the script, the Dockerfile and its ignore file.**  Follow `tools/shfmt.sh`:
  - `set -euo pipefail`, a header comment, and `cd` to the repository root.
  - `docker build --quiet --build-arg SQLFLUFF_IMAGE=... -f tools/sqlfluff.Dockerfile -t <tag> src/sqlfluff-templater-sqlsource`.
  - Then `docker run --rm --volume "$repo_root:/sql" --workdir /sql`, with `:ro` for `--check` and `--user` for rewrites.

- [ ] **Step 2: Build and smoke-test**

  Run: `tools/sqlfluff.sh -- --version`
  Expected: `sqlfluff, version 4.5.0`

  Run: `tools/sqlfluff.sh -- lint examples/PostgresExamples/Methods`
  Expected: findings with rule codes, and no `TMP` saying "Requested templater 'sqlsource'".

- [ ] **Step 3: Check the configs (Review Focus 4 and 5)**

  Run: `tools/sqlfluff.sh -- lint --format json --nofail .`
  Expected:
  - no `TMP` "cannot be linted with the sqlfluff dialect", and no "no dialect" error, anywhere
  - `AL01` reported in `examples/`
  - no `AL01` in `tests/DatabaseTests/SqlServer`
  - no `CP02` in `tests/DatabaseTests/SqlServer`
  - `Batches.sql` linted, with no "over the limit" warning

  Then create `docs/scratch-dialect/q.sql` holding `SELECT 1;`, run `tools/sqlfluff.sh --check`, and delete the folder.
  Expected: a non-zero exit, with sqlfluff's error that no dialect was specified for that file.

- [ ] **Step 4: Check the ignores (Review Focus 3)**

  Run, in the main checkout `/Users/michael/Projects/SqlSource`, which holds `.claude/worktrees/`: `<worktree>/tools/sqlfluff.sh -- lint --format json --nofail .`
  Expected: no path under `.claude/`, `bin/`, `obj/` or any ignored folder, and no `TMP` or `PRS`.  Run it from the main checkout by copying the configs there temporarily, or by running against the worktree's `.claude`.  If neither is practical, create a scratch `.claude/worktrees/x/q.sql` with invalid SQL in this worktree, check that it isn't reported, then delete it.

- [ ] **Step 5: Check file ownership (Review Focus 1)**

  Run: copy one formattable test query to a scratch name under `tests/DatabaseTests/Postgres/EndToEnd/Queries/`, run `tools/sqlfluff.sh -- fix <that file>`, then `ls -l` it, then delete it.
  Expected: owned by the current user, and reformatted.

- [ ] **Step 6: Check the rebuild (Review Focus 2)**

  Run: add a comment line to `src/sqlfluff-templater-sqlsource/sqlfluff_templater_sqlsource/__init__.py`, run `tools/sqlfluff.sh -- --version` with `docker build` not quiet (temporarily), check that the `pip install` layer re-ran, then revert the comment.
  Expected: the layer rebuilt; then a cached build on the next run.

- [ ] **Step 7: Run validation and commit**

  Run: `./pre-commit-validation.sh`, expecting PASS: shellcheck and shfmt on the new script, and editorconfig on the new files.
  ```bash
  git add tools/sqlfluff.sh tools/sqlfluff.Dockerfile tools/sqlfluff.Dockerfile.dockerignore .sqlfluff .sqlfluffignore tests/DatabaseTests/SqlServer/.sqlfluff tests/DatabaseTests/Postgres/.sqlfluff tests/SqlSource.Tests/EndToEnd/.sqlfluff tools/package-install/.sqlfluff examples/.sqlfluff examples/SqlServerExamples/.sqlfluff examples/PostgresExamples/.sqlfluff tools/README.md docs/superpowers
  git commit -m "build: add tools/sqlfluff.sh, running sqlfluff with the templater from source"
  ```

### Task 2: The test SQL meets the relaxed rules

**Files:**
- Modify: every `.sql` file in scope under `tests/` and `tools/package-install/Queries/`.
- Modify: the sidecars (`*.sql.json`) that `tools/describe-end-to-end.sh` rewrites.
- Modify: `src/sqlfluff-templater-sqlsource/tests/corpus/repo/**`, regenerated, never edited by hand.
- Modify: anything `./pre-commit-validation.sh` shows asserting on a query's exact text, by updating the expectation, or by excluding the file in `.sqlfluffignore` with a reason.

**Interfaces:**
- Consumes: `tools/sqlfluff.sh -- fix tests tools`, then `tools/sqlfluff.sh -- lint tests tools`.

- [ ] **Step 1: Format**

  Run: `tools/sqlfluff.sh -- fix tests tools`, then `git diff --stat`.
  Expected: only `.sql` files changed.

- [ ] **Step 2: Read the diff for deliberate SQL**

  Revert any fix that changes what a query does rather than its form. The cases known to be deliberate:
  - `right-join-orders-accounts` (`CV08`)
  - `left-joined-subquery-coalesce` (`ST05`)
  - `two-columns-named-id` and `insert-from-unnest-arrays` (`AL08`)
  - the special-character identifiers of `EndToEnd/Queries/Notes.sql` (`RF05`)
  - the quoted `"char"` of the `app` schema (`RF06`)

  Mark each with `-- noqa: <rule>` and a reason.  A `-- noqa` line is a comment, so it never changes a query's hash.

- [ ] **Step 3: Lint to zero**

  Run: `tools/sqlfluff.sh -- lint tests tools`
  Expected: exit 0.  A rule that keeps firing on purpose across many files goes into `exclude_rules`, at the root or in `tests/DatabaseTests/SqlServer/.sqlfluff`, with its reason.  An incidental finding is fixed in the SQL.

- [ ] **Step 4: Re-describe**

  Run: `tools/describe-end-to-end.sh`, then `git diff -- '*.sql.json' | grep '^[-+] ' | grep -v '"hash"'`.  Docker is needed, and the run takes a while.
  Expected: no changed line except `hash` and version or provenance members.  Anything else means a fix changed what the query does, so go back to Step 2.

- [ ] **Step 5: Regenerate the templater corpus**

  Run: the corpus tests (`TemplatingCorpusTests` in `tests/SqlSource.Tests`) with `SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1`, as "The templater's corpus" in `tests/README.md` says, then `git status src/sqlfluff-templater-sqlsource/tests/corpus`.
  Expected: only `corpus/repo/` files of the reformatted queries changed.  See "The templater's corpus" in `tests/README.md` for the exact command.

- [ ] **Step 6: Run validation and commit**

  Run: `./pre-commit-validation.sh`
  Expected: PASS, including the postgres and sqlserver tests and the templater tests.
  ```bash
  git add -A tests tools src/sqlfluff-templater-sqlsource/tests/corpus .sqlfluff .sqlfluffignore
  git commit -m "style: format the test SQL with sqlfluff and re-describe its sidecars"
  ```

### Task 3: The examples meet sqlfluff's full rules

**Files:**
- Modify: `examples/PostgresExamples/**/*.sql` and `examples/SqlServerExamples/**/*.sql`, including both `schema.sql` files.
- Modify: the examples' sidecars, the templater's `corpus/repo/examples/**`, and `src/sqlfluff-templater-sqlsource/tests/examples_unparsable.txt` if `CountQueries.sql` comes to parse.
- Modify: the site pages that quote example SQL: `site/index.md`, `site/start/first-query.md`, `site/guides/lists.md`, and every other hit of the search in Step 5.
- Modify: `examples/PostgresExamples/README.md` and `examples/SqlServerExamples/README.md` if they quote the SQL.

- [ ] **Step 1: Format**

  Run: `tools/sqlfluff.sh -- fix examples`
  Expected: the examples are rewritten, `LT09` included.

- [ ] **Step 2: Mark what an example exists to show**
  - `Models/Users.sql`, `SELECT *`: `-- noqa: AM04`, with a reason a reader can follow, for example "the table model takes every column".
  - The `name` and `location` columns of `schema.sql`: `-- noqa: RF04`, with a reason.
  - Every other finding is fixed in the SQL, provided the generated API stays the same.

- [ ] **Step 3: `CountQueries.sql`**

  Give `{{table}}` a default (for example `{{table:users}}`) if the generated `CountRows` method's signature is unchanged.  Check by comparing the generated source before and after under `examples/SqlServerExamples/obj/**/generated/`.  If the signature changes, keep `{{table}}` and add `-- noqa: PRS` with its reason instead.

- [ ] **Step 4: Lint to zero**

  Run: `tools/sqlfluff.sh -- lint examples`
  Expected: exit 0.

- [ ] **Step 5: Update the pages that quote the examples**

  Run: `grep -rn -F` for distinctive lines of each changed query, before and after the change, over `site/`, `examples/*/README.md`, `src/SqlSource.Tool/README.md` and `README.md`.  Update every hit under `site/` and `examples/` to quote the SQL as it now stands.  Record each `README.md` hit (`README.md:77` at least) with the replacement text for the owner, and don't edit it.
  Expected: no stale quote outside `README.md`.

- [ ] **Step 6: Re-describe the examples and regenerate the corpus**

  As in Task 2, Steps 4 and 5.  Expected: the sidecar diffs show only `hash` and version or provenance.

- [ ] **Step 7: Run validation and commit**

  Run: `./pre-commit-validation.sh`
  Expected: PASS, including the site build, `ReadmeTests`, the examples' build and the templater tests (`test_examples_parse.py`).
  ```bash
  git add -A examples site src/sqlfluff-templater-sqlsource/tests
  git commit -m "style: make the example projects meet sqlfluff's full rules"
  ```

### Task 4: Wire the check into format.sh, pre-commit-validation.sh and CI, and document it

**Files:**
- Modify `format.sh`:
  - Add a `sql` part.  Usage becomes `format.sh [--check] [style|analyzers|csharpier|ruff|sql ...] [-- file ...]`.
  - Without `--check`, the part runs `tools/sqlfluff.sh`; with it, `tools/sqlfluff.sh --check`.
  - It runs last.
  - Files after `--` don't apply to it; say so in the header, as for ruff.
- Modify `pre-commit-validation.sh`:
  - Add a `sql_trigger`, set off by a path matching `*.sql`, `.sqlfluff`, `*/.sqlfluff`, `.sqlfluffignore`, `tools/sqlfluff.sh`, `tools/sqlfluff.Dockerfile*`, `src/sqlfluff-templater-sqlsource/*` or `format.sh`.
  - Add `decide sql "$sql_trigger" 'no .sql file, sqlfluff config, tools/sqlfluff.sh or templater file changed'`.
  - Add `step 'sqlfluff' sql tools/sqlfluff.sh --check || true` after `actionlint`.
  - In `is_known_kind`, make `.sqlfluff`, `.sqlfluffignore` and `*.Dockerfile`/`*.dockerignore` known kinds, matched by basename as the other dotfiles are.
  - Update the script's header comment, which lists its checks.
- Modify `.github/workflows/build.yml`: in the `lint` job, add step `Lint and check the format of the SQL` with `if: ${{ !cancelled() }}`, running `tools/sqlfluff.sh --check`.
- Modify: `.github/workflows/README.md` (the lint job's checks), `CONTRIBUTING.md` (the format and check commands, and `format.sh sql`), `tools/README.md` (the "Formatting" section names the `sql` part) and `tools/AGENTS.md`, if its lists of checks or callers name them.
- Add "format before you describe" to these AGENTS.md files as one rule each:
  - `tools/AGENTS.md`
  - `examples/AGENTS.md`
  - `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md`, `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md`, `tests/DatabaseTests/Postgres/Realistic/AGENTS.md` and `tests/DatabaseTests/SqlServer/Realistic/AGENTS.md`

  The rule's text: "Run `format.sh sql` (or `tools/sqlfluff.sh -- fix <path>`) before `tools/describe-end-to-end.sh`: formatting changes a query's hash, so a sidecar described before it is stale."
- Add the same step to the procedure prose of `tests/DatabaseTests/README.md` and the two engine readmes, where they describe re-describing.
- Modify the root `AGENTS.md`'s templater rule (section 4) to name `.sqlfluff`/`.sqlfluffignore`, and only if it needs to.

- [ ] **Step 1: Prove the trigger by its plan**

  Run: `./pre-commit-validation.sh --plan tests/DatabaseTests/Postgres/EndToEnd/Queries/Accounts.sql` and `./pre-commit-validation.sh --plan src/SqlSource/Parsing/SqlQueryHash.cs`.  If `--plan` reports a full run because the script itself changed, put Task 4's change in a WIP commit first.
  Expected: `sqlfluff` listed as `run` for the `.sql` path, and as `skip` with the reason above for the `.cs` path.

- [ ] **Step 2: Run the parts**

  Run: `./format.sh --check sql` and `./format.sh sql`
  Expected: both exit 0 on the clean tree, with no file changed.

- [ ] **Step 3: Lint the workflow**

  Run: `tools/actionlint.sh`
  Expected: no output, exit 0.

- [ ] **Step 4: Run validation and commit**

  Run: `./pre-commit-validation.sh`, which runs in full because the script changed.
  Expected: PASS, with a `sqlfluff` line in the summary.
  ```bash
  git add format.sh pre-commit-validation.sh .github CONTRIBUTING.md tools AGENTS.md examples/AGENTS.md tests
  git commit -m "build: check the SQL with sqlfluff in pre-commit-validation.sh, format.sh and CI"
  ```

### Task 5: Review and hand-off

- [ ] **Step 1:** Run `branch-review:review` and address its findings until it comes back clean.  This is the root `AGENTS.md` pull request rule 2.
- [ ] **Step 2:** Report to the owner the `README.md` changes collected in Task 3, Step 5, plus anything deferred or recorded as tech debt.  Expect none: sqlfluff's inability to parse `TABLE` is a third-party limit, already noted in `.sqlfluffignore`.
