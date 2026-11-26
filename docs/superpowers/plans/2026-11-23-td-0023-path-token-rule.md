# TD-0023: a secret typed where a path goes - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A token typed where a path goes is printed and logged only when it reads as a path, so `sqlsource describe S3cret` no longer repeats `S3cret`, and a mistyped real path is still named.

**Architecture:** One rule, `PathToken.MayBePrinted`, is decided once for each path value, right after the command line is read and before the log file is made, and stored on the `UsageValue`.  Everything that prints or logs a path already asks the value, so it follows.  The refusal of `=` and `;` keeps its own test, `HoldsConnectionText`.

**Tech Stack:** C#, .NET 8, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0023-path-without-an-equals-sign-is-printed-as-given.md`.  Its proposed rule, a path that exists or is relative, does not hold back its own example, so this plan replaces it, as the unit's brief asks.

## Global Constraints

- The `--online` paths of `--manifest`, `--log` and `--env-file` stay printed and logged in full: the owner's ruling.
- A path from `SQLSOURCE_LOG` is no token of the command line, and stays printed.
- A value that holds `=` or `;` is refused or held back exactly as on `main`.
- `UsageValue.MayBePrintedAsPath` defaults to false, so a value nothing judged is held back.
- Resolving the item deletes its file, its row, every reference to `TD-0023`, and the limit lines in `site/`.

## The rule

A token may be printed when it holds neither `=` nor `;` and any of these is true:

1. It is empty, which is what an unset shell variable gives.
2. Its extension is one the tool's paths have, ignoring case: `.sln`, `.slnx`, `.csproj`, `.sql`, `.env`, `.log`, `.json`, `.jsonl`, `.zip`.
3. It names a file or a folder that exists.
4. It holds a separator, `/` or `\`, and the first folder it names exists.  A bare separator would print a base64 password such as `aB3/xY9+Qz`.

`--env-file none` is a keyword and always printable.  A `--query` value is judged by its file, before the last colon, and the whole value must still hold no `=` or `;`.

## Review Focus

- A password holding `/`, such as `aB3/xY9+Qz`: held back, since no folder `aB3` exists.
- `--log S3cret`, whose file the run itself makes: the verdict is taken before the file exists, so `arguments` holds `***`.
- `--env-file none`: stays `none` in `arguments`.
- A mistyped real path, `src/Typo/App` or `Missing.csproj`: still named in full.
- A system message that quotes a held-back path, as a failed `--log` or `--out` gives: the path appears nowhere in the error.

---

### Task 1: The rule and where it is decided

**Files:**
- Create: `src/SqlSource.Tool/PathToken.cs`
- Modify: `src/SqlSource.Tool/UsageValue.cs`, `src/SqlSource.Tool/Usage.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/SharedOptions.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Test: `tests/SqlSource.Tool.Tests/PathTokenTests.cs`, `tests/SqlSource.Tool.Tests/UsageValueTests.cs`

**Interfaces:**
- Produces: `PathToken.MayBePrinted(string text, string workingDirectory) : bool`; `UsageValue.HoldsConnectionText : bool`; `UsageValue.MayBePrintedAsPath { get; init; }`; `Usage.WithPathsJudgedFrom(string workingDirectory) : Usage`.

- [ ] **Step 1:** Write `PathTokenTests` over a temporary folder: `S3cret`, `aB3/xY9+Qz` and `/aB3` are false; `Missing.csproj`, `q.SQL`, an existing folder, an existing file, `./Typo/App`, `<existing>/Typo/App` and the empty token are true; `a=b.sql` and `a;b/c` are false.
- [ ] **Step 2:** Run them and see them fail to compile.
- [ ] **Step 3:** Implement the rule, the property and `WithPathsJudgedFrom`, which judges only arguments and the values of `--project`, `--log`, `--env-file`, `--manifest`, `--query`, `--out` and `--scrub`, and prints the first three build paths under `--online`.  Call it in `Cli.ReadCommandLineAsync`.  Move the refusals of `ProblemOfPath` and `ProblemOfManifestPath` to `HoldsConnectionText`.
- [ ] **Step 4:** Run `PathTokenTests` and `UsageCheckTests`; both pass.

### Task 2: What is printed and logged

**Files:**
- Modify: `src/SqlSource.Tool/Logging/LoggedArguments.cs`, `src/SqlSource.Tool/Logging/RunLogFile.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/PlanSteps.cs`, `src/SqlSource.Tool/Describing/ConnectionSources.cs`, `src/SqlSource.Tool/Describing/ConnectionEnvironment.cs`, `src/SqlSource.Tool/Describing/DescribeErrors.cs`, `src/SqlSource.Tool/Projects/RunProjects.cs`, `src/SqlSource.Tool/Bundles/DiagnoseRun.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs`, and the existing tests whose paths the rule now holds back

**Interfaces:**
- Consumes: `UsageValue.MayBePrintedAsPath`, `UsageValue.NamedAsPath`, `ReplayCommand.AsNamed`.

- [ ] **Step 1:** Add tests that `S3cret` in each path place (unit, `--project`, `--log`, `--env-file`, `--manifest`, `--out`) reaches neither the output nor `arguments`, and that `--online` paths and `--env-file none` still do.
- [ ] **Step 2:** Run them and see them fail.
- [ ] **Step 3:** `LoggedArguments` replaces every judged value that may not be printed.  A failed `--log`, `--manifest` or `--out` is reported through the position, with no system text that quotes the path.  `SQLSRC213` names a held-back `--env-file` by its position.
- [ ] **Step 4:** Update the existing tests that relied on a bare missing name being printed, and run `SqlSource.Tool.Tests`.

### Task 3: Documents and the item

**Files:**
- Delete: `docs/tech-debt/TD-0023-path-without-an-equals-sign-is-printed-as-given.md`
- Modify: `docs/tech-debt/README.md`, `site/reference/cli.md`, `site/diagnostics.md`, `docs/architecture/tool.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/UsageCheck.cs`

- [ ] **Step 1:** State the rule once in `site/reference/cli.md`, link it from each `SQLSRC` section that names a path by position, and remove the limit line.
- [ ] **Step 2:** Update `docs/architecture/tool.md` and the `AGENTS.md` rule, and remove every `TD-0023` reference.
- [ ] **Step 3:** Run `./format.sh`, then `./pre-commit-validation.sh --full`; it passes.  Commit.
