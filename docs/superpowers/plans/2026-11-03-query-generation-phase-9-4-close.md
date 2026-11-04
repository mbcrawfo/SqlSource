# Query generation 9.4: recipes, the install check, the examples and close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close phase 9: editor recipes that start `describe --watch`, what each IDE shows, the package-install check running the packed tool through the build, the examples' `.env.example` and their watcher and online-build section, and the phase's documentation read whole.

**Architecture:** Mostly documentation.  One C# documentation test in `tests/SqlSource.Tool.Tests` holds the VS Code recipe's patterns to the tool's markers and error format.  `tools/check-package-install.sh` gains a project of its own, built in its temporary folder, that installs the packed tool through a tool manifest and builds under each value of `SqlSourceOnline` with the real tool.

**Tech Stack:** bash, MSBuild, .NET 10 SDK (repository `global.json`), xUnit v3 with Shouldly, Markdown.

**Spec:** `docs/superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md`, sections "Editor recipes", "The package-install check" and "9.4"; the spike `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md`, "8 to 11. What the owner ran".

## Where merged code differs from the spec

- The spec's install check builds "the consumer" under each value of `SqlSourceOnline`.  The consumer, `tools/package-install`, has `output` `Sql` and no sidecar by design, so this plan builds a project of its own in the script's temporary folder, as the script already does for its web and multi-targeted projects.
- Its sidecar that is current is not committed under `tools/`: the script copies `examples/PostgresExamples/Models/Users.sql` and `Users.sql.json`, which `tools/describe-end-to-end.sh` keeps current, and which `docs/publishing.md` rewrites whenever `VersionPrefix` moves.  A copy under `tools/` would go stale at the next version.
- `.github/workflows/build.yml` already packs both packages into `artifacts/packages` before `Check package install`; only its comments, if any say one package, change.
- 9.2 left the stand-in checks of the script (the single-framework consumer offline, the multi-targeted run) in place; they stay.
- The VS Code and Visual Studio recipes were never run (TD-0097).  An owner question is open on whether the owner tries them.  Until it is answered they ship in the form below, marked not checked, and the answer may change Task 1's text.

## Global Constraints

- Root `AGENTS.md` rule 4: user documentation (`README.md`, `src/SqlSource.Tool/README.md`, `docs/manual/`) describes effects, never internals; never names the describer, a plan, `EXPLAIN`, a sidecar field, a tech-debt or deferred id.  Run the grep of `docs/manual/AGENTS.md` ("Write effects, never internals") on every page changed.
- `docs/manual/AGENTS.md`: recipes hold examples, not rules, each linking to the rules in `tool.md`; a heading linked from anywhere gets an `<a id>` line above it; links in the folder are relative; "One status, in one place": only the "What works today" table of `docs/manual/README.md` keeps a status.
- The markers, exactly: `sqlsource: change detected; describing` (`WatchSession.ChangeDetectedMarker`) and `sqlsource: watching for changes; press Ctrl+C to stop` (`WatchSession.WatchingMarker`), in `src/SqlSource.Tool/Watching/WatchSession.cs`.
- The tool's error lines: `<path>(<line>,<col>): error SQLSRCnnn: <message>`, `<path> : error SQLSRCnnn: <message>` with no position, `sqlsource : error SQLSRCnnn: <message>` with no path; then continuation lines indented four spaces, the last `    see: <url>` (`src/SqlSource.Tool/Reporting/Reporter.cs`).
- What the spike confirmed, and nothing more is stated as checked: Rider 2026.2.4 on macOS; a Shell Script run configuration whose text is the command, added under Settings > Tools > Startup Tasks, starts with the solution in a terminal pane where an error's path is a link; a sidecar rewritten or created by another process changed Rider's models within seconds with nothing else done; for a query edited and saved in the editor and then described by another process, the change showed after a build, and the stale-entry error did not show in the editor before it.  Visual Studio and VS Code: unverified, from their documentation ("Balanced" mode re-runs generators on a save or a build, not when another process writes a file; "Automatic" when the file is written).
- AGENTS.md files: one rule per bullet, at most 80 words, a file at most 8 KB (`src/SqlSource/build/AGENTS.md` and `src/SqlSource.Tool/AGENTS.md` are within a few bytes of it: do not add to them).
- Validation: implementers run the tests of what they changed; `./pre-commit-validation.sh` runs before each commit.  Run every command in the foreground.

## Review Focus

1. **A VS Code problem matcher that reads a continuation line as a problem**, such as `    server: ERROR: ...` or `    see: https://...`: the documentation test asserts the recipe's regexp matches no line but a first line.
2. **An error with no position or no file** (`<path> : error`, `sqlsource : error`): the matcher must read the first and must not read the second as a file named `sqlsource`; the test covers both.
3. **A summary line or a marker read as a problem**: the test asserts the regexp matches neither marker nor a summary line `postgres (postgres): 1 described, 0 skipped, 0 failed`.
4. **The install check passing because the tool never ran**: each online case asserts a line only the real tool writes (`sqlsource: summary:`, `sqlsource: offline:`, `SQLSRC213`) and the `false` case asserts the tool's command line is absent from a `-v:d` log.
5. **The install check reading a developer's or CI's environment**: every online build clears `CI`, `TF_BUILD`, `TEAMCITY_VERSION`, `JENKINS_URL`, `GITHUB_ACTIONS`, `SQLSOURCE_LOG` and `SQLSOURCE_CONNECTION*` (the script already unsets them before the stand-in builds; the new section runs after that).

---

### Task 1: Editor recipes, what each IDE shows, and the documentation test

**Files:**
- Modify: `docs/manual/recipes.md` (new recipe, its list entry)
- Modify: `docs/manual/tool.md` (`#describe-on-save`'s "Editors" paragraph becomes a subsection on what each editor shows; Known issues)
- Modify: `docs/manual/README.md` ("Find what you need" row, "What works today" row)
- Create: `tests/SqlSource.Tool.Tests/Watching/EditorRecipeTests.cs`
- Modify: `src/SqlSource.Tool/Watching/AGENTS.md` (name the test in the marker rule)
- Modify: `docs/tech-debt/TD-0097-ide-checks-of-phase-9-ran-in-rider-on-macos-alone.md` (its VS Code check runs the shipped recipe)

**Interfaces:**
- Consumes: `WatchSession.ChangeDetectedMarker`, `WatchSession.WatchingMarker` (internal consts); `Reporter` (`new Reporter(TextWriter error)`, `Report(ToolDiagnostic)`), the way existing tests build a `ToolDiagnostic`; `RepositoryRoot.Path("docs", "manual", "recipes.md")` from `tests/SqlSource.Testing`.
- Produces: the anchor `recipes.md#start-the-watcher-with-your-editor` and `tool.md#what-your-editor-shows`, which Tasks 3 and 4 link.

- [ ] **Step 1: Write the recipe** in `docs/manual/recipes.md`, after "Describe again after a schema change", anchored `<a id="start-the-watcher-with-your-editor"></a>`, heading `## Start the watcher with your editor`, and its entry in the list at the top.  A sentence linking [describe on save](tool.md#describe-on-save) for the rules and [what your editor shows](tool.md#what-your-editor-shows), then one `###` per editor:
  - **VS Code**: a ` ```jsonc ` block, `.vscode/tasks.json`, exactly:

    ```jsonc
    {
      "version": "2.0.0",
      "tasks": [
        {
          "label": "sqlsource watch",
          "type": "shell",
          "command": "dotnet sqlsource describe --watch",
          "isBackground": true,
          "runOptions": { "runOn": "folderOpen" },
          "presentation": { "reveal": "silent", "panel": "dedicated" },
          "problemMatcher": {
            "owner": "sqlsource",
            "source": "sqlsource",
            "fileLocation": ["autoDetect", "${workspaceFolder}"],
            "pattern": {
              "regexp": "^(?!sqlsource )(\\S.*?)(?:\\((\\d+),(\\d+)\\))?\\s?: (error|warning) (SQLSRC\\d+): (.*)$",
              "file": 1, "line": 2, "column": 3, "severity": 4, "code": 5, "message": 6
            },
            "background": {
              "activeBegin": true,
              "beginsPattern": "^sqlsource: change detected; describing$",
              "endsPattern": "^sqlsource: watching for changes; press Ctrl\\+C to stop$"
            }
          }
        }
      ]
    }
    ```

    Then, in the user's words: the task starts when the folder opens; VS Code asks once to allow automatic tasks in the folder, and never runs one in a folder it does not trust; each error's first line is read into the Problems panel; the folder that opens must hold the solution or project, or set `"options": { "cwd": "..." }`.  Then the sentence that this recipe has not been checked in VS Code (pending the owner's answer).
  - **Rider**: Run > Edit Configurations, add a Shell Script configuration whose script text is `dotnet sqlsource describe --watch`, in the solution's folder; Settings > Tools > Startup Tasks, add it.  It starts when the solution opens, in a terminal pane, where an error's path is a link to the file and line.  Checked in Rider 2026.2 on macOS.
  - **Visual Studio**: View > Terminal (the Developer PowerShell), `dotnet sqlsource describe --watch` in the solution's folder; it runs until you stop it or close the terminal.  Not checked in Visual Studio (pending the owner's answer).

- [ ] **Step 2: Write `### What your editor shows`** in `tool.md`, under `## Describe on save`, anchored `<a id="what-your-editor-shows"></a>`, replacing the paragraph "**Editors.**".  Example first (a short timeline or the Rider case), then: Rider shows models from a sidecar the watch rewrote or created within seconds, with nothing else done; a query you edit and save shows the error for its out-of-date entry, and its new models, only after a build, since the editor's own run comes before the watch writes the sidecar (state it as Rider's observed effect, no internals).  Visual Studio and VS Code, by their documentation: in their default mode a sidecar the watch writes changes the models at your next save of any file or your next build, in "Automatic" mode when it is written; say where each IDE's setting is (Visual Studio: Tools > Options, search "source generator"; VS Code: settings, search "source generator") and that this has not been checked.  Keep the saves sentence that is there (Rider on macOS alone checked) and its link to known issues.  Add a Known issues bullet: in Visual Studio and VS Code the watch's recipes and what the editor shows have not been checked; if the models do not change after the watch writes a sidecar, save any file or build.

- [ ] **Step 3: Update `docs/manual/README.md`**: a "What works today" row after "Describing on save with `describe --watch`": `| Starting the watcher with your editor: VS Code, Rider and Visual Studio | Available | [The recipe](recipes.md#start-the-watcher-with-your-editor) |`; a "Find what you need" row: `| Refresh the sidecars as I save, from my editor | [Describe on save](tool.md#describe-on-save), [what your editor shows](tool.md#what-your-editor-shows), or the [recipe](recipes.md#start-the-watcher-with-your-editor) |`.

- [ ] **Step 4: Write the failing test** `tests/SqlSource.Tool.Tests/Watching/EditorRecipeTests.cs`.  A helper reads `recipes.md`, takes the one fenced block whose info string is `jsonc` and whose text holds `"beginsPattern"` (assert exactly one), and parses it with `JsonDocument` (`CommentHandling = Skip`, `AllowTrailingCommas = true`); take `tasks[0]`.  Tests:
  - `VsCodeTask_RunsTheWatch`: `command` is `dotnet sqlsource describe --watch`, `isBackground` is `true`, `runOptions.runOn` is `folderOpen`.
  - `VsCodeTask_BeginsPattern_MatchesTheChangeMarkerAlone`: `Regex.IsMatch(WatchSession.ChangeDetectedMarker, begins)` true, `Regex.IsMatch(WatchSession.WatchingMarker, begins)` false.
  - `VsCodeTask_EndsPattern_MatchesTheWatchingMarkerAlone`: the converse.
  - `VsCodeTask_Pattern_ReadsTheFirstLineOfAnError`: write two errors through a real `Reporter` (not one-line) to a `StringWriter`, one `ToolDiagnostic` with a path and position (e.g. `/work/App/Queries/Users.sql`, line 4, column 10 as written) and one with a path and no position; split the output into lines.  Exactly two lines match; for the first, groups 1, 2, 3, 5 are `/work/App/Queries/Users.sql`, `4`, `10`, the diagnostic's id; for the second group 1 is the path and group 2 is empty.
  - `VsCodeTask_Pattern_PassesOverEveryOtherLine`: no match for a continuation line from that output, `    see: https://...`, a `sqlsource : error SQLSRC201: ...` line, either marker, and `postgres (postgres): 1 described, 0 skipped, 0 failed`.
  The comment at the top says what fails it: a change to the markers, to the error format, or to the recipe without the other.

- [ ] **Step 5: Run it** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*EditorRecipeTests"` (or the repository's filter form): before Step 1 it fails on the missing block; after, PASS.  Also run `tests/SqlSource.Tests` filtered to `UserDocsLinksTests` and `UserDocsSamplesTests`.

- [ ] **Step 6: Update `src/SqlSource.Tool/Watching/AGENTS.md`**'s marker rule: "the VS Code recipe in `docs/manual/recipes.md` reads them in its problem matcher, and `EditorRecipeTests` of `tests/SqlSource.Tool.Tests/Watching/` holds the two together", dropping "of phase 9.4".  Update TD-0097's VS Code section: the check now runs the recipe of `docs/manual/recipes.md#start-the-watcher-with-your-editor` (its sample's `tasks.json` keeps the stand-in command and takes the recipe's matcher).

- [ ] **Step 7: Run the internals grep of `docs/manual/AGENTS.md`** on the changed pages, `./pre-commit-validation.sh`, and commit: `docs(manual): recipes that start the watcher with each editor, and what each shows`.

### Task 2: The package-install check runs the packed tool through the build

**Files:**
- Modify: `tools/check-package-install.sh`
- Modify: `tools/README.md` (`#check-package-installsh`), `tools/AGENTS.md`, `CONTRIBUTING.md` (its line on the check), `pre-commit-validation.sh` (`needs_packages`), `.github/workflows/build.yml` and `.github/workflows/README.md` only where they say the check takes one package.

**Interfaces:**
- Consumes: `examples/PostgresExamples/Models/Users.sql` and `Users.sql.json` (query `GetUser`, dialect `postgres`, database `postgres`, current for the tool of `VersionPrefix`).
- Produces: nothing other tasks use.

- [ ] **Step 1: Packages.**  The directory argument must hold one `SqlSource.<version>.nupkg` and one `SqlSource.Tool.<version>.nupkg` of the same version (fail naming what is missing); without an argument, pack both projects into the temporary feed.  Update the header comment and usage text.

- [ ] **Step 2: The online project**, after the multi-targeted section (the variables are already cleared there).  In `$work/online`: `global.json`, `nuget.config` (copied), `Online.csproj` (`net10.0` library, `TreatWarningsAsErrors`, `PackageReference` to `SqlSource` with `PrivateAssets="all"`), `sqlsource.json` `{ "root": true, "dialect": "postgres" }`, `Queries/Users.sql` and `Queries/Users.sql.json` copied from the example, and a class `[SqlSourceGenerate(Source = "Queries", Output = GeneratorOutput.Models)] internal static partial class Queries;` (or the form the example's `StoreQueries` needs to compile; it must compile with the models).  Then `dotnet new tool-manifest` and `dotnet tool install SqlSource.Tool --version "$version"` in it.  Each failure prints its log and a `check-package-install:` line saying what failed.

- [ ] **Step 3: The checks, in this order**, each failing with its own `check-package-install:` line:
  1. `dotnet sqlsource --version` prints `$version` (compare as `check-tool-install.sh` does).
  2. `-p:SqlSourceOnline=false`, `dotnet build -tl:off -v:d`: passes, and the log holds no `describe --manifest`.
  3. A `.env` holding `APP_SETTING=1` alone, `SqlSourceOnline` unset (`auto`), `dotnet build -tl:off -v:d`: passes, and the log holds a line with `sqlsource: summary: `.
  4. Change the SQL of `GetUser` in `Queries/Users.sql` (e.g. `SELECT id, name FROM users WHERE id = @userId;` in place of `SELECT *`), so its entry is stale.
  5. `dotnet sqlsource describe` in the project's folder: exits `1` and prints a line matching `Users\.sql\([0-9]+,[0-9]+\): error SQLSRC213: `.
  6. `auto` with the same `.env`, `dotnet build -tl:off -v:d`: fails; the log holds `sqlsource: offline: no connection for postgres; the build uses the committed sidecars` and a `error SQLSRC244` at `Queries/Users.sql`.
  7. `-p:SqlSourceOnline=true`, `dotnet build -tl:off`: fails; the output holds a line matching `Users\.sql\([0-9]+,[0-9]+\): error SQLSRC213: .*; see: https://` (the whole error on one line), and no `MSB3073`.
  Remove `.env` where a case needs none.  Every build passes `--nologo`.  Add the closing `echo` line: `and runs the packed tool through the build of a project, under each value of SqlSourceOnline`.

- [ ] **Step 4: Run** `tools/check-package-install.sh` (no argument, packs both) and see it pass; then break it on purpose once (e.g. point the copy at a missing file) to see the failure message, and restore.  Run `tools/shellcheck.sh` and `tools/shfmt.sh --check`.

- [ ] **Step 5: Callers and docs.**  `needs_packages` in `pre-commit-validation.sh` gains `examples/PostgresExamples/Models/Users.sql*`; run any test of `tests/SqlSource.Tests/Validation/` that reads the script.  `tools/AGENTS.md` gains one rule: the script copies the example's `Users.sql` and its sidecar, so moving, renaming or changing that query changes the script in the same commit.  `tools/README.md`'s section says what the new part does, in the style of the section.  `CONTRIBUTING.md`'s line names the packed tool.  Check `build.yml`'s step and `.github/workflows/README.md` say nothing wrong; change only what does.

- [ ] **Step 6: Commit**: `test(tools): the package-install check runs the packed tool through the build`.

### Task 3: The examples' `.env.example` and the watcher and online build

**Files:**
- Create: `examples/PostgresExamples/.env.example`, `examples/SqlServerExamples/.env.example`
- Modify: `examples/PostgresExamples/README.md`, `examples/SqlServerExamples/README.md`; `examples/AGENTS.md` if a rule changes

- [ ] **Step 1: `.env.example`** in each: a comment (copy to `.env`, which git ignores; the tool reads it) and the one `SQLSOURCE_CONNECTION="..."` line the README's "Describe the queries" uses, the same value.  Check `git check-ignore` passes over `.env.example` and catches `.env`.

- [ ] **Step 2: README section** `## Describe as you save, and in the build`, after "Describe the queries", in each README: in a project of your own, copy `.env.example` to `.env`, run `dotnet sqlsource describe --watch` (or start it with your editor: link `../../docs/manual/recipes.md#start-the-watcher-with-your-editor`), and a build describes before it compiles ([running in the build](../../docs/manual/tool.md#running-in-the-build)).  How this example differs: the repository pins no published tool and sets `SqlSourceOnline` to `false`, so here the watch runs from the source (`dotnet run --project src/SqlSource.Tool -- describe --watch examples/<Project>/<Project>.csproj` from the repository's root, with the connection set) and its build stays offline.  Reword "Describe the queries" where it now repeats this.  Relative links only.

- [ ] **Step 3: `examples/AGENTS.md`**: rule 3 names the README's `SQLSOURCE_CONNECTION` and the script's connection; add `.env.example` to it.  Build the examples (`dotnet build examples/PostgresExamples examples/SqlServerExamples` or the solution) to see nothing changed in them; `./pre-commit-validation.sh`; commit `docs(examples): a .env.example, and describing on save and in the build`.

### Task 4: Close phase 9

**Files:** `README.md`, `src/SqlSource.Tool/README.md`, `docs/manual/*.md` as reading finds, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, `CONTRIBUTING.md` if a command changed.

- [ ] **Step 1: Read whole** `README.md` (step 5, the FAQ, the status line), `src/SqlSource.Tool/README.md`, `docs/manual/tool.md` (connections, `.env`, running in the build, describe on save, the command reference, exit codes, known issues), `docs/manual/settings.md`'s `SqlSourceOnline`, `docs/manual/README.md`, the new recipe, and `docs/manual/diagnostics.md`'s SQLSRC024 to 027 and 285 to 287.  Fix what is wrong, repeated or contradictory, by root rule 4 and `docs/manual/AGENTS.md`; run its two greps ("When a phase lands", and the internals one).  Packed readmes keep absolute links and the callout rule.
- [ ] **Step 2: Epic outline**: rows 9.0 to 9.4 `Done`; the paragraph after the table says phase 9 is closed, as it says of phases 7 and 8.
- [ ] **Step 3**: `./pre-commit-validation.sh`; commit `docs: close phase 9`.

### After the tasks

`./pre-commit-validation.sh --full`, then `/branch-review:review --base origin/main`.
