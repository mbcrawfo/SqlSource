# Windows compatibility

## Intent

SqlSource ships to Windows users: the `SqlSource` package, the `sqlsource` tool and the sqlfluff templater.  Development happens on a Mac, so nothing today shows when a change breaks Windows.  A full validation run on Windows (Git Bash, 2026-11-18, `main` at `2aabd475`) found one bug a Windows user hits and several reasons a Windows developer cannot run the checks.  This work makes both hold, and keeps them holding without anyone developing on Windows.

## Goals

1. What ships works on Windows, and the nightly run proves it.
2. Every script of the repository runs under Git Bash on Windows, and `./pre-commit-validation.sh --full` passes there.
3. A change made on a Mac that would break a script on Windows fails a test on every pull request, on any system.

## Non-goals

- Running the database tests on Windows.  GitHub's Windows runners cannot start Linux containers, and a native server would break the rule that every test database is pinned to its image.  The gap is recorded as tech debt (below).
- Any Windows shell but Git Bash: PowerShell, cmd and WSL are not supported for the scripts.
- A Windows job on every pull request.  Nightly is enough.

## Anti-goals

- A script that behaves differently on Linux or macOS than it does today.  CI runs on Linux and the owner develops on macOS, whose bash is 3.2.
- Loosening a check to make it pass on Windows: a comparison, a manifest check or a test may learn Windows' spelling of the same thing, never accept a different thing.

## What fails today

| Check | Cause on Windows |
|----|----|
| `SessionTests.OpenAsync_UnreachableHost…`, `…CertificateNotTrusted…` of `tests/DatabaseTests/SqlServer`, and `check-tool-install.sh` | A real bug.  On Windows SqlClient uses its native network layer, whose errors carry no `SocketException` or `AuthenticationException`, so `ConnectionErrors.OtherAtOpen` in `src/SqlSource.Tool/Describing/SqlServer/ConnectionErrors.cs` gives the generic "the server refused to open a session" (seen: `Msg 258` for a refused port, `Msg -2146893019` for an untrusted certificate) |
| shellcheck, shfmt, actionlint | Git Bash rewrites the container path `/mnt` to `C:/Program Files/Git/mnt` before Docker sees it |
| editorconfig-checker | Every tracked file is one argument of `docker run`, past Windows' limit on a command line |
| `check-package-install.sh`, `check-tool-install.sh` | A Unix path (`/tmp/...`) is written into `nuget.config`, which NuGet reads as `C:\tmp\...` |
| `check-package-install.sh`, past that | `expected-output.txt` and the `.sql` files of `tools/package-install/` are checked out with CRLF, and the comparison fails.  Read but not run: the stand-in tool is a `.sh` that MSBuild's Exec, which runs cmd on Windows, cannot start; the manifest checks expect `/` and the Unix spelling of `pwd` |
| templater tests | `test_license_is_the_repositorys` compares `LICENSE`, checked out with CRLF, with the templater's copy, pinned to LF |
| `describe-end-to-end.sh` (read, not run) | `/opt/mssql-tools18/bin/sqlcmd`, a container path given to `docker exec`, is rewritten as `/mnt` is |
| `PostgisImageScript` of `tests/DatabaseTests/Postgres` (read, not run) | Starts `bash` by name, which outside Git Bash, as from an IDE, is WSL's `bash.exe` |

Everything else passed: format, the templater's lint and format, the site, the build, pack, the package contents and 22,681 tests.  Not run: `record-fixtures.sh`, `describe-end-to-end.sh`, `mutation-test.sh`, `.claude/skills/reviewing-test-quality/measure.sh`, and the floor images.

## Approaches

For the shipped code (chosen: A):

- **A. The package checks and the templater's tests on Windows, nightly.**  They do what a Windows user does: install the package, build, start the tool through the build, load SqlClient's Windows copy, run the templater.  No database.  It would have caught the SqlClient bug.
- B. A, and the database tests on Windows against native servers.  The harness would need a mode that connects to an existing server; installs on the runner are slow and flaky; a native server breaks image pinning.  Rejected for its cost.
- C. Only move Windows-sensitive cases into the tests that already run on Windows.  Covers only the cases someone thinks to move, and never the install or the build.

For keeping the scripts working on Windows (chosen: static test):

- **A repository test, on every pull request on any system, that fails on the script patterns that break on Windows.**  Cheap and immediate; covers the patterns it knows.
- A stand-in `docker` on the nightly Windows runner that records what the scripts pass.  Exercises the real scripts on Windows, but is more machinery and still runs nothing in a container.  Rejected.

For the SqlClient bug:

- **Map the native layer's errors to the texts the managed layer already gets.**  Chosen.
- Turn on SqlClient's `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows`, so Windows behaves as Linux.  Rejected: the managed layer has no LocalDB, which the tool supports on Windows.

## Design

### 1. SQL Server's errors at open on Windows

`ConnectionErrors.AtOpen` gives the same texts on Windows as on Linux and macOS: `UnreachablePrefix` (with the socket error's name when there is one) for a server that cannot be reached, and `CertificateRefused` for a failed TLS handshake.  It recognises the native layer by what it puts in the `SqlException`: error numbers, and a `Win32Exception` in the chain where there is one.  The new mapping applies only to what the native layer throws, so the texts on Linux and macOS do not change (my call).

What a probe of SqlClient 7.1.1 on Windows showed (2026-11-18, `Connect Timeout=3`):

| Case | `SqlError.Number` | Chain | Time |
|----|----|----|----|
| Nothing listens at the port (`127.0.0.1,1`) | 258 | `Win32Exception` 258, `WAIT_TIMEOUT` | the whole timeout |
| An address that drops what is sent (`10.255.255.1`) | 258 | `Win32Exception` 258 | the whole timeout |
| A host name that is not found | 11001 | `Win32Exception` 11001, `WSAHOST_NOT_FOUND` | about 2.5 s |
| An untrusted certificate (from the test run) | -2146893019, `SEC_E_UNTRUSTED_ROOT` | not probed | |

The native layer retries a refused port until the timeout ends, so on Windows a refused port and a dropped connection cannot be told apart.  A `Win32Exception` whose code is a Winsock error (10000 to 11999, as 11001) is the socket error of the same number, and gives `UnreachablePrefix` with its `SocketError` name, as on Linux (`HostNotFound`).  Code 258 gives `ConnectionTexts.ConnectTimedOut`, "the connection to the server timed out", since that is what happened (the owner's decision).  So on Windows `OpenAsync_UnreachableHost` and the unreachable check of `check-tool-install.sh` expect that text, and the `SQLSRC224` table of `site/diagnostics.md` says, on its timeout row, that on Windows a port where nothing listens gives it too, after the connect timeout, as the row below already notes what differs on Linux.  The expectation of each test is the one text of its platform, never either text on any platform.  The SSPI errors of a failed handshake give `CertificateRefused`; which codes those are, beyond the one seen, is the builder's to establish and cite in a comment.  The existing `SessionTests`, with the Windows expectation above, are the acceptance, run on Windows; a unit test of the mapping in `tests/SqlSource.Tool.Tests` is the builder's call, if a `SqlException` of those numbers can be built without reflection into SqlClient.  The rest of `site/diagnostics.md`'s `SQLSRC224` section is checked and changed only if it says something now untrue on Windows.

### 2. A shared helper for the scripts

One file under `tools/`, sourced by every script that needs it, `pre-commit-validation.sh` and `.claude/skills/reviewing-test-quality/measure.sh` included, and valid in bash 3.2.  It gives:

- A way to run `docker` with Git Bash's argument conversion off (`MSYS_NO_PATHCONV=1`), so a container path such as `/mnt` or `/opt/.../sqlcmd` reaches Docker as written.  Every `docker` call of every script goes through it.  It is never exported to a whole script, since `dotnet` must still get Windows paths.
- `native_path`: a host path in the spelling a Windows program reads (`cygpath -m` under Git Bash), and the path unchanged elsewhere.  Every host path written into a file another program reads (`nuget.config`, a project, a `.env`) or passed as a value inside an argument (`-p:Name=path`, `--add-source`) goes through it.

On Linux and macOS both are no-ops.  With conversion off, a host path given to `docker` on its own, such as the build context of `build-postgis-image.sh`, also goes through `native_path`.  A change to the helper makes a run of `pre-commit-validation.sh` full, as a change to that script does, since it sources it (my call).

### 3. The scripts

- The four linters call `docker` through the helper.  editorconfig-checker checks the same files as today without putting them all on one command line; how (batches, or a list the container reads) is the builder's, as long as one run gives one verdict.
- `check-package-install.sh` and `check-tool-install.sh` use `native_path` for every path they write or pass as a value.  The stand-in tool is started in a way MSBuild's Exec can run on Windows and on Unix.  The manifest checks accept the path MSBuild writes on Windows (`\`, a drive letter) as the same path, not any path.
- `describe-end-to-end.sh`, `record-fixtures.sh`, `build-postgis-image.sh`, `pull-test-images.sh`, `mutation-test.sh` and `measure.sh` get the same treatment wherever they hit either pattern.
- `PostgisImageScript` starts Git's bash on Windows, found from `git`'s own install, not whatever `bash` the PATH holds first (my call: tests are often run from an IDE).

### 4. Line endings

- `LICENSE` and `src/sqlfluff-templater-sqlsource/LICENSE` are both pinned to LF in `.gitattributes`.
- The comparison of `check-package-install.sh` ignores the difference between `\r\n` and `\n`, since the consumer prints with the platform's line ending, and the files of `tools/package-install/` are pinned to LF.  Any line that still differs is a real difference, to fix at its cause.

### 5. The static test

A test class in `tests/SqlSource.Repository.Tests` reads every tracked `*.sh` and fails, naming the file and line, when a script:

- calls `docker` other than through the helper; or
- writes a host path into a generated file, passes one as a value inside an argument, or gives one to `docker`, without `native_path`.

How the second is detected (a naming convention for converted variables, or recognising the heredocs and `-p:` arguments) is the builder's call; each rule has a case it fails on, taken from today's scripts before the fix.  `tools/AGENTS.md` gains one rule pointing at the helper and the test.

### 6. The nightly run

A new job of `.github/workflows/nightly.yml` on `windows-latest` and `macos-latest`, in Git Bash on Windows, runs `dotnet pack`, `tools/check-package-install.sh`, `tools/check-tool-install.sh` and the templater's `pytest` through `uv`.  A job of its own (my call), so that the existing test job keeps its time budget: it already takes 15 to 17 minutes of its 30.

### 7. Documentation and debt

- `CONTRIBUTING.md`: Windows is supported for development under Git Bash, with Docker Desktop and uv; anything else a Windows developer needs that the run surfaced.
- `tools/README.md`: the helper, and what the scripts need on Windows.
- `.github/workflows/README.md`: the new nightly job.
- `tests/README.md`: the static test, if its conventions describe the repository tests by folder.
- `docs/tech-debt`: a new entry for the database tests not running on Windows, with what that leaves untested (SqlClient's native layer against a real server, the certificate case among them).

## Done

- `./pre-commit-validation.sh --full` passes in Git Bash on Windows, and on Linux in CI.
- `tools/record-fixtures.sh` and `tools/describe-end-to-end.sh` run on Windows and leave no diff.
- `tools/mutation-test.sh` and `measure.sh` run on one small target on Windows.
- `gh workflow run nightly.yml --ref <branch>` passes the new job on both systems.
- The static test fails on today's scripts and passes on the fixed ones.

## Left to the builder

- The native error numbers and how they are recognised (section 1).
- The helper's file name and function names.
- How editorconfig-checker avoids the long command line, and how the stand-in is started.
- How the static test detects an unconverted path.
- Whether this lands as one pull request or several; the version check of `AGENTS.md` applies to each.
