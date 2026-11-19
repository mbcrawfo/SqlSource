# Windows Compatibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** What ships works on Windows and the nightly run proves it; every script runs under Git Bash on Windows; a test on every pull request catches script patterns that break Windows.

**Architecture:** One sourced bash helper, `tools/platform.sh`, carries the two Windows conversions (Docker arguments left unconverted, host paths converted to Windows spelling), and every script uses it.  `ConnectionErrors` of the SQL Server describer maps the errors of SqlClient's native network layer to the texts Linux already gets.  A repository test reads every `*.sh` and fails on a bare `docker` call or an unconverted host path in a generated file or a value; a new nightly job runs the package checks and the templater's tests on Windows and macOS.

**Tech Stack:** bash (3.2-compatible), Git Bash on Windows, C# / xUnit / Shouldly, GitHub Actions, uv.

**Spec:** `docs/superpowers/specs/2026-11-18-windows-compatibility-design.md`

## Global Constraints

- Every script behaves on Linux and macOS exactly as today; macOS bash is 3.2 (no `declare -A`, no `${var,,}`, no `mapfile`).
- Git Bash is the only Windows shell supported for the scripts.
- A check may learn Windows' spelling of the same thing, never accept a different thing; each platform's test expects its one text.
- On Windows, SqlClient error 258 gives `the connection to the server timed out` (the owner's decision).
- The helper's conversion of Docker arguments is never exported to a whole script: `dotnet` must still get converted paths.
- `AGENTS.md` rules apply: `./pre-commit-validation.sh` passes before every commit; docs updated in the same PR (`CONTRIBUTING.md`, `tools/README.md`, `.github/workflows/README.md`, `tools/AGENTS.md`, `site/diagnostics.md`); new tech debt in `docs/tech-debt`; `branch-review:review` before offering a PR; version check before the PR.
- Validate on Windows in Git Bash (this machine) and in Linux CI; the owner runs the final `--full` on macOS.

## Review Focus

1. A path with a space (`C:/Program Files/...`, a user folder with a space): `native_path` output is always quoted where used; the stand-in `.cmd` quotes both paths.  Task 4 Step 5's run repeats the package checks with `TMPDIR` set to a folder with a space.
2. A partial pre-commit run on Windows that runs one database pair: the solution filter must name the solution in Windows spelling.  Task 5 Step 2 runs it.
3. A refused port on Windows that is actually a firewall drop reads the same; the diagnostics row says so (Task 2).
4. A script run from a folder reached through a symlink or a different drive letter case: `cygpath -m` normalizes; the manifest comparison in Task 4 compares Windows spellings case-insensitively only on Windows.
5. editorconfig-checker on a tree with more files than one batch: Task 3 Step 3 checks a run splits into several batches and one failing batch fails the run.

---

### Task 1: The shared helper

**Files:**
- Create: `tools/platform.sh`
- Modify: `pre-commit-validation.sh` (the full-run trigger near `script_trigger`, lines ~199 and ~216)

**Interfaces:**
- Produces, for every later task, sourced as `source "$repo_root/tools/platform.sh"` (with a `# shellcheck source=` directive that shellcheck in `tools/shellcheck.sh` accepts):
  - `on_windows` — exit status 0 when `$OSTYPE` is `msys*` or `cygwin*`.
  - `native_path <path>` — prints `cygpath -m -- "$1"` on Windows (`C:/Users/...`), else `$1` unchanged.
  - `windows_path <path>` — prints `cygpath -w -- "$1"` on Windows (`C:\Users\...`), else `$1` unchanged; for comparing with what a Windows program prints.
  - `run_docker <args...>` — `MSYS_NO_PATHCONV=1 docker "$@"`, the variable set for that one command.

- [ ] **Step 1: Write `tools/platform.sh`** with the four functions above, a header comment saying why each exists, and nothing that runs on source.

- [ ] **Step 2: Check it on this machine**

Run: `bash -c 'source tools/platform.sh; on_windows && echo win; native_path /tmp; windows_path /tmp; run_docker run --rm alpine:3.20 ls -d /mnt'`
Expected: `win`, `C:/Users/<user>/AppData/Local/Temp`, `C:\Users\<user>\AppData\Local\Temp`, `/mnt`.

- [ ] **Step 3: Make a change to the helper a full run** in `pre-commit-validation.sh`: the condition that sets `script_trigger` also matches `tools/platform.sh`, and the full reason reads `pre-commit-validation.sh or tools/platform.sh changed`.

Run: `./pre-commit-validation.sh --plan tools/platform.sh | head -1`
Expected: `Full run: pre-commit-validation.sh or tools/platform.sh changed.`

- [ ] **Step 4: Commit**

```bash
git add tools/platform.sh pre-commit-validation.sh
git commit -m "build: add tools/platform.sh, the scripts' Windows conversions"
```

### Task 2: SQL Server's errors at open on Windows

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs` (`AtOpen`, `OtherAtOpen`)
- Modify: `tests/SqlSource.Tool.Tests/Describing/SqlServer/ConnectionErrorsTests.cs`
- Modify: `tests/DatabaseTests/SqlServer/Describer/SessionTests.cs:69-84`
- Modify: `tools/check-tool-install.sh:130-176`
- Modify: `site/diagnostics.md` (the `SQLSRC224` table of SQL Server, the timeout row near line 2885)

**Interfaces:**
- Consumes: `on_windows` (Task 1) in `check-tool-install.sh`.
- Produces: `internal static string? NativeLayerText(int errorNumber, int? win32Code)` on `ConnectionErrors`.  Null when the pair is not one the native layer gives for a failed open.

- [ ] **Step 1: Write the failing unit tests** in `ConnectionErrorsTests`:

```csharp
[Theory]
[InlineData(258, 258, "the connection to the server timed out")]
[InlineData(11001, 11001, "SqlClient could not reach the server: HostNotFound")]
[InlineData(10061, 10061, "SqlClient could not reach the server: ConnectionRefused")]
[InlineData(-2146893019, null, "the server's certificate is not trusted, or the TLS handshake failed")]
public void NativeLayerText_NativeError_IsTheTextLinuxGives(int number, int? win32, string text) =>
    ConnectionErrors.NativeLayerText(number, win32).ShouldBe(text);

[Theory]
[InlineData(18456, null)]
[InlineData(4060, null)]
[InlineData(0, 5)]
public void NativeLayerText_OtherError_IsNull(int number, int? win32) =>
    ConnectionErrors.NativeLayerText(number, win32).ShouldBeNull();
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class SqlSource.Tool.Tests.Describing.SqlServer.ConnectionErrorsTests`
Expected: build error, `NativeLayerText` not defined.

- [ ] **Step 3: Implement `NativeLayerText`**: code 258 (`WAIT_TIMEOUT`, in the number or the Win32 code) gives `ConnectionTexts.ConnectTimedOut`; a Win32 code from 10000 to 11999 that is a defined `SocketError` gives `UnreachablePrefix + ": " + name`; the SSPI codes of a failed handshake give `CertificateRefused`.  `-2146893019` (`SEC_E_UNTRUSTED_ROOT`) is the one seen; add the others of a certificate that fails (`SEC_E_WRONG_PRINCIPAL`, `CERT_E_UNTRUSTEDROOT`, `CERT_E_EXPIRED`, `CERT_E_CN_NO_MATCH`) after checking each against Microsoft's list, and cite the list and the probe of the spec in a comment.  `OtherAtOpen` calls it only when `OperatingSystem.IsWindows()`, with the first error's number and the `NativeErrorCode` of the first `Win32Exception` in the chain, before its current logic; `AtOpen` uses its result for a 258, which today falls to `other`.

- [ ] **Step 4: Run the unit tests**

Run: the command of Step 2.  Expected: PASS.

- [ ] **Step 5: Give `SessionTests.OpenAsync_UnreachableHost_IsSqlsrc224WithNoPartOfTheValue` its Windows text**: on Windows the single line matches `^Msg 258, Level 20, State 0: the connection to the server timed out$`, elsewhere the current pattern.  Its comment says that SqlClient's native layer on Windows retries a refused port until the connect timeout.

Run: `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class SqlSource.DatabaseTests.SqlServer.Describer.SessionTests`
Expected: PASS on Windows, every test, the certificate test included.

- [ ] **Step 6: Give `check-tool-install.sh` its Windows text**: under `on_windows` the unreachable check greps `the connection to the server timed out`, else `SqlClient could not reach the server`; the message on failure names the text it looked for.  Its comment says either text shows SqlClient's copy for the platform loaded, since the stub gives another.

- [ ] **Step 7: Update `site/diagnostics.md`**: the timeout row of SQL Server's table adds that on Windows a port where nothing listens gives this text too, after the connect timeout.  Read the rest of the `SQLSRC224` section and change only what is now untrue on Windows.  If `docs/architecture/` describes `ConnectionErrors`' mapping (grep `OtherAtOpen`, `AuthenticationException`), update it.

- [ ] **Step 8: Validate and commit**

Run: `./pre-commit-validation.sh` (the package checks of `check-tool-install.sh` pass only after Task 4 on Windows; until then that step's failure must be the `nuget.config` path, nothing else).

```bash
git add src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs tests/SqlSource.Tool.Tests/Describing/SqlServer/ConnectionErrorsTests.cs tests/DatabaseTests/SqlServer/Describer/SessionTests.cs tools/check-tool-install.sh site/diagnostics.md
git commit -m "fix: give SQLSRC224 its texts for SqlClient's native network layer on Windows"
```

### Task 3: The linters

**Files:**
- Modify: `tools/shellcheck.sh`, `tools/shfmt.sh`, `tools/actionlint.sh`, `tools/editorconfig-checker.sh`

**Interfaces:**
- Consumes: `run_docker`, `native_path` (Task 1).

- [ ] **Step 1: Route every `docker` call of the four scripts through `run_docker`.**  The host side of each `--volume` is `$(native_path "$repo_root")`.

- [ ] **Step 2: Batch editorconfig-checker**: add files to a batch until its paths total 24,000 characters, run one `run_docker ... editorconfig-checker <batch>` per batch, and exit non-zero when any batch failed, after running all of them.  One file never makes a batch of its own unless it alone passes the limit.

- [ ] **Step 3: Run each linter on Windows**

Run: `tools/shellcheck.sh && tools/shfmt.sh --check && tools/actionlint.sh && tools/editorconfig-checker.sh; echo $?`
Expected: `0`.  Then add a trailing space to a file near the end of `git ls-files`, run `tools/editorconfig-checker.sh`, expect a failure naming that file, and undo the edit.

- [ ] **Step 4: Validate and commit**

Run: `./pre-commit-validation.sh` — the four linter steps pass.

```bash
git add tools/shellcheck.sh tools/shfmt.sh tools/actionlint.sh tools/editorconfig-checker.sh
git commit -m "build: run the linters under Git Bash on Windows"
```

### Task 4: Line endings and the package checks

**Files:**
- Modify: `.gitattributes`
- Modify: `tools/check-package.sh`, `tools/check-package-install.sh`, `tools/check-tool-install.sh`
- Renormalize: `LICENSE`, `tools/package-install/**`

**Interfaces:**
- Consumes: `on_windows`, `native_path`, `windows_path` (Task 1).

- [ ] **Step 1: Pin the line endings** in `.gitattributes`, each with a one-line reason as its neighbours have: `LICENSE text eol=lf` (the templater's copy is compared byte for byte with it) and `tools/package-install/** text eol=lf` (its output is compared with `expected-output.txt`).  Then `git add --renormalize .gitattributes LICENSE tools/package-install` and check the working files out again (`git checkout -- LICENSE tools/package-install`) so they are LF on this machine.

Run: `git ls-files --eol LICENSE tools/package-install/expected-output.txt`
Expected: `i/lf w/lf` for both.

Run: `uv run --locked --directory src/sqlfluff-templater-sqlsource pytest tests/test_license.py`
Expected: PASS.

- [ ] **Step 2: Compare the consumer's output without regard to `\r`**: `diff --strip-trailing-cr` in `check-package-install.sh`.

- [ ] **Step 3: Convert every host path the three scripts write or pass as a value**: the `globalPackagesFolder` and package source of each `nuget.config`, `-p:SqlSourceManifestFile=`, `-p:SqlSourceToolCommand=`, `DOTNET_CLI_HOME`, `--add-source`, and any other found while running them.  Each converted value is held in a variable whose name ends `_native` (Task 6 relies on the suffix).

- [ ] **Step 4: Make the stand-in tool runnable by MSBuild's Exec on Windows**: under `on_windows` also write `stand-in.cmd` holding `@"<native_path of $BASH>" "<native_path of stand-in.sh>" %*` and pass its native path as `SqlSourceToolCommand`; elsewhere pass `stand-in.sh` as today.

- [ ] **Step 5: Make the manifest checks read Windows' spelling of the same path**: the `File=` check accepts `/` or `\` between `Queries` and `Users.sql`; the `Settings=` check compares with `windows_path "$(pwd -P)"`'s `\sqlsource.json` on Windows, case-insensitively there only, and with today's value elsewhere; the `manifest_argument` grep of the multi-target build does the same.

Run: `dotnet pack SqlSource.slnx --output artifacts/packages && tools/check-package.sh artifacts/packages && tools/check-package-install.sh artifacts/packages && tools/check-tool-install.sh artifacts/packages; echo $?`
Expected: `0`.  Run the two install checks again with `TMPDIR="$(mktemp -d)/with space"` (created first) and expect `0`.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh` — `package`, `package-install` and `tool-install` pass.

```bash
git add .gitattributes LICENSE tools/package-install tools/check-package.sh tools/check-package-install.sh tools/check-tool-install.sh
git commit -m "build: run the package checks under Git Bash on Windows"
```

### Task 5: The other scripts and the PostGIS test runner

**Files:**
- Modify: `pre-commit-validation.sh` (the solution filter, line ~378)
- Modify: `tools/describe-end-to-end.sh`, `tools/record-fixtures.sh`, `tools/build-postgis-image.sh`, `tools/pull-test-images.sh`, `tools/mutation-test.sh`, `.claude/skills/reviewing-test-quality/measure.sh`
- Modify: `tests/DatabaseTests/Postgres/PostgisImageScript.cs`

**Interfaces:**
- Consumes: `run_docker`, `native_path` (Task 1).

- [ ] **Step 1: Every `docker` call of these scripts goes through `run_docker`**; the build context of `build-postgis-image.sh` and any other host path given to `docker` goes through `native_path`; a host path written into a file or passed as a value is held in a `*_native` variable.

- [ ] **Step 2: The solution filter names the solution in Windows spelling**: `pre-commit-validation.sh` writes `$(native_path "$repo_root")/$SOLUTION` into `tests.slnf`.

Run: add a comment line to `tools/build-postgis-image.sh`, which `tools/test-scopes.txt` gives the `postgres` scope alone, check with `./pre-commit-validation.sh --plan` that the SQL Server pair is skipped, run `./pre-commit-validation.sh`, and undo the edit.
Expected: the `test` step runs through the filter and passes; before this step it failed with `D:\d\Projects\...\SqlSource.slnx ... does not exist`.

- [ ] **Step 3: `PostgisImageScript` starts Git's bash on Windows**: on Windows, the `bash.exe` of Git's `bin` folder, found from the folder `git --exec-path` prints (its install root is two folders above `mingw64/libexec/git-core`); elsewhere `bash` as today.  A private static `BashPath` in the same file.

Run: `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class SqlSource.DatabaseTests.Postgres.PostgisImageScriptTests`
Expected: PASS, also when started from PowerShell rather than Git Bash.

- [ ] **Step 4: Run the recording scripts on Windows**

Run: `tools/record-fixtures.sh && tools/describe-end-to-end.sh && git status --short`
Expected: both exit 0 and `git status` shows nothing but this task's own edits.

- [ ] **Step 5: Run the mutation scripts on one small target**

Run: `tools/mutation-test.sh tool --tests 'FullyQualifiedName~ConnectionErrorsTests' 'src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs'`, then `measure.sh` on its smallest scope as its header describes.
Expected: each writes its `reports/mutation-report.json` and exits 0.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`

```bash
git add pre-commit-validation.sh tools/describe-end-to-end.sh tools/record-fixtures.sh tools/build-postgis-image.sh tools/pull-test-images.sh tools/mutation-test.sh .claude/skills/reviewing-test-quality/measure.sh tests/DatabaseTests/Postgres/PostgisImageScript.cs
git commit -m "build: run the recording, image and mutation scripts under Git Bash on Windows"
```

### Task 6: The static test

**Files:**
- Create: `tests/SqlSource.Repository.Tests/Validation/ScriptPortability.cs`
- Create: `tests/SqlSource.Repository.Tests/Validation/ScriptPortabilityTests.cs`
- Modify: `tools/AGENTS.md`

**Interfaces:**
- Produces: `internal static class ScriptPortability` with `public static IReadOnlyList<string> Violations(string path, string text)`; each violation is `<path>:<line>: <rule>: <the line>`.

Rules, with `tools/platform.sh` exempt and comment lines (`#` after optional space) skipped:

1. **Bare docker**: a command word `docker` (at the start of a line or after `;`, `&&`, `||`, `|`, `(`, `$(`, `!`, `then`, `do`) is a violation.
2. **Unconverted host path**: a variable is a host path when the script assigns it from `mktemp`, `pwd`, `cd ... && pwd`, `$repo_root`, `$BASH`, or a string starting with another host-path variable.  A host-path variable whose name does not end `_native`, used inside a heredoc whose target file ends `.config`, `.csproj`, `.props`, `.targets`, `.json`, `.slnf` or `.env`, inside a `-p:Name=` or `--name=` argument, in a `printf` whose format holds `{"`, or as the value of an `export` or a command-prefixed assignment, is a violation.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("docker run --rm -v \"$repo_root:/mnt\" img")]
[InlineData("container=\"$(docker run -d img)\"")]
[InlineData("if docker exec \"$c\" pg_isready; then")]
public void Violations_BareDocker_IsOne(string line) =>
    ScriptPortability.Violations("tools/x.sh", line).ShouldHaveSingleItem().ShouldContain("bare docker");

[Fact]
public void Violations_RunDocker_IsNone() =>
    ScriptPortability.Violations("tools/x.sh", "run_docker run --rm img").ShouldBeEmpty();

[Theory]
[InlineData("work=\"$(mktemp -d)\"\ncat >\"$c/nuget.config\" <<EOF\n<add key=\"k\" value=\"$work/feed\" />\nEOF")]
[InlineData("work=\"$(mktemp -d)\"\ndotnet build -p:SqlSourceToolCommand=$work/stand-in.sh")]
[InlineData("work=\"$(mktemp -d)\"\nexport DOTNET_CLI_HOME=\"$work/home\"")]
[InlineData("repo_root=\"$(pwd)\"\nprintf '{\"solution\":{\"path\":%s}}' \"$repo_root/x.slnx\"")]
public void Violations_UnconvertedHostPath_IsOne(string script) =>
    ScriptPortability.Violations("tools/x.sh", script).ShouldHaveSingleItem().ShouldContain("unconverted host path");

[Fact]
public void Violations_ConvertedHostPath_IsNone() =>
    ScriptPortability
        .Violations("tools/x.sh",
            "work=\"$(mktemp -d)\"\nwork_native=\"$(native_path \"$work\")\"\ncat >\"$c/nuget.config\" <<EOF\n<add value=\"$work_native/feed\" />\nEOF")
        .ShouldBeEmpty();

[Fact]
public void Violations_EveryScriptOfTheRepository_IsNone()
```

The last test reads every `*.sh` under the repository root (skipping `.git`, `bin`, `obj`, `artifacts`, `TestResults` and `.claude/worktrees`, as `TestScopesFileTests` does) through `RepositoryRoot`, and asserts the joined violations are empty.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class SqlSource.Repository.Tests.Validation.ScriptPortabilityTests`
Expected: build error, `ScriptPortability` not defined.

- [ ] **Step 3: Implement `ScriptPortability.Violations`** line by line, following assignments in file order for rule 2.

- [ ] **Step 4: Run the tests**

Run: the command of Step 2.  Expected: PASS.  Then check the rules against the scripts before this branch: write `git show 2aabd475:tools/check-package-install.sh`, `…:tools/shellcheck.sh` and `…:pre-commit-validation.sh` to the scratchpad, call `Violations` on each from a temporary test, and confirm it reports the `nuget.config` and `-p:` lines, the `docker` call and the solution filter's `printf`; fix a rule that misses one, then delete the temporary test.

- [ ] **Step 5: Add one rule to `tools/AGENTS.md`**: a script calls `docker` through `run_docker` and converts a host path it writes into a file or passes as a value with `native_path` into a `*_native` variable, both from `tools/platform.sh`, and `ScriptPortabilityTests` of `tests/SqlSource.Repository.Tests/Validation/` fails otherwise; one sentence of reason (Git Bash on Windows), within 80 words.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`

```bash
git add tests/SqlSource.Repository.Tests/Validation/ScriptPortability.cs tests/SqlSource.Repository.Tests/Validation/ScriptPortabilityTests.cs tools/AGENTS.md
git commit -m "test: fail on script patterns that break under Git Bash on Windows"
```

### Task 7: The nightly job, the docs and the debt

**Files:**
- Modify: `.github/workflows/nightly.yml` (a new job after `test`)
- Modify: `.github/workflows/README.md` (`## Nightly`), `CONTRIBUTING.md`, `tools/README.md`, `tests/README.md` (if it lists the repository tests by folder)
- Create: `docs/tech-debt/TD-0120-database-tests-do-not-run-on-windows.md` (the next free id; check the folder's index, if it has one, and add the entry there)

- [ ] **Step 1: Add the job `packages`** to `nightly.yml`: `runs-on: ${{ matrix.os }}`, matrix `windows-latest` and `macos-latest`, `fail-fast: false`, `timeout-minutes: 30`, `defaults.run.shell: bash`, the checkout and `setup-dotnet` steps of the `test` job, `astral-sh/setup-uv` at the pin `build.yml` uses, then the steps `dotnet pack SqlSource.slnx --output artifacts/packages`, `tools/check-package-install.sh artifacts/packages`, `tools/check-tool-install.sh artifacts/packages` and `uv run --locked --directory src/sqlfluff-templater-sqlsource pytest`.  A comment says why it is its own job (the `test` job's time budget).

Run: `tools/actionlint.sh`
Expected: PASS.

- [ ] **Step 2: Write the docs**: `CONTRIBUTING.md` says Windows is supported for development under Git Bash, with Docker Desktop and uv, and adds the job to the nightly paragraph; `tools/README.md` gets a section on `tools/platform.sh` and what the scripts need on Windows; `.github/workflows/README.md` describes the job; `tests/README.md` names `ScriptPortabilityTests` if it lists the repository tests.

- [ ] **Step 3: Write the tech-debt entry**, in the form of its neighbours: the database tests do not run on Windows because GitHub's Windows runners cannot start Linux containers and a native server breaks image pinning; untested on Windows: SqlClient's and Npgsql's native behaviour against a real server, the certificate case of `SQLSRC224` among them; it is checked by hand with `tests/DatabaseTests/SqlServer` on a Windows machine with Docker Desktop.

- [ ] **Step 4: Validate on Windows, commit, and run the full check**

Run: `./pre-commit-validation.sh --full`
Expected: every step passes in Git Bash on this machine.

```bash
git add .github/workflows/nightly.yml .github/workflows/README.md CONTRIBUTING.md tools/README.md tests/README.md docs/tech-debt
git commit -m "ci: run the package checks and the templater's tests nightly on Windows and macOS"
```

- [ ] **Step 5: Before the pull request**: run `branch-review:review` until clean, the version check of `AGENTS.md`, and (after the owner agrees to push) `gh workflow run nightly.yml --ref <branch>`; the `packages` job passes on both systems.  Ask the owner to run `./pre-commit-validation.sh --full` on macOS.
