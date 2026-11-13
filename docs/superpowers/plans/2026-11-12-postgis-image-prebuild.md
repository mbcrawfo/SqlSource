# Building the PostGIS image before the tests: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every CI job that runs PostGIS tests builds the PostGIS image in a step of its own that fails the job visibly, and the tests reuse that image instead of building it again.

**Architecture:** The harness's `PostgisContainer` names the image by the postgres tag, the PostGIS version and a hash of the Dockerfile, and builds it only when no image of that name exists.  A new script, `tools/build-postgis-image.sh`, computes the same name and builds the image with Docker's plain output; four workflow jobs and `tools/describe-end-to-end.sh` call it.

**Tech Stack:** C# (.NET 10, xunit v3, Shouldly, Testcontainers 4.16.0), bash, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-11-12-postgis-image-prebuild-design.md`

## Global Constraints

- Image name: `sqlsource-postgis:<postgres tag>-<PostGIS version>-<hash>`, e.g. `sqlsource-postgis:18.6-3.6.4-<12 hex>`.
- Hash: the first 12 lowercase hex characters of the SHA-256 of `tests/DatabaseTests/TestHarness/Postgis/Dockerfile`'s bytes with every `\r` (0x0D) removed.  In bash: `tr -d '\r' <Dockerfile | shasum -a 256 | cut -c1-12`.
- The harness builds with `.WithImageBuildPolicy(PullPolicy.Missing)`; the script always runs `docker build`.
- A failed build step fails its job and no test step runs after it.
- No workflow names the default image (`tools/pull-test-images.sh`'s rule); the script reads it from `DefaultImages.cs` as that script does.
- Root `AGENTS.md`: every commit passes `./pre-commit-validation.sh`; before any push, `./pre-commit-validation.sh --full` passes on the exact tree.  Comments and docs follow the repo's existing prose style.
- Non-goals: apt retries, `examples/PostgresExamples/Postgis/Dockerfile`, the Windows/macOS nightly jobs.

## Review Focus

- **The harness reuses an image built by the script.**  Build with the script, then run a PostGIS test: the image ID must not change.  Task 1 step 5 and Task 2 step 6 check it by hand, since no unit test can.
- **A Windows checkout of the Dockerfile (CRLF) gives the same name.**  Task 1's hash test pins it.
- **The script and the harness disagree on the name.**  CI would then build twice and hide failures again; Task 2's script test pins it for the default and floor images.
- **The build step fails but the Test step still runs in `build.yml`'s `build` job**, whose Test step has its own `if:`.  Task 3 gives the step an `id` and adds its outcome to that condition.
- **Two test projects build the same missing image at once.**  The existing lock still serializes them; the second must find the image and skip its build.  Covered by the existing lock tests plus the build policy; no new test.

---

### Task 1: The harness names the image by the Dockerfile's hash and reuses it

**Files:**
- Modify: `tests/DatabaseTests/TestHarness/PostgisContainer.cs`
- Modify: `tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs`
- Modify: `tests/DatabaseTests/Postgres/README.md` (section "The PostGIS image": the name now carries the hash, the image is reused when it exists, an edit to the Dockerfile renames it)
- Modify: comments that give the old name or say the image is always built: `PostgisContainer.cs`, `tests/DatabaseTests/README.md:29` if it no longer holds

**Interfaces:**
- Produces: `public static string ImageOf(string postgresImage)` (unchanged signature, new value), `internal static string DockerfileHash(ReadOnlySpan<byte> dockerfile)`.

- [ ] **Step 1: Write the failing tests** in `PostgisContainerTests`, replacing `ImageOf_PostgresImage_NamesItsTagAndPostgisVersion`:

```csharp
[Theory]
[InlineData("postgres:18.6", @"^sqlsource-postgis:18\.6-3\.6\.4-[0-9a-f]{12}$")]
[InlineData("postgres:14.24", @"^sqlsource-postgis:14\.24-3\.6\.4-[0-9a-f]{12}$")]
public void ImageOf_PostgresImage_NamesItsTagPostgisVersionAndDockerfileHash(string postgres, string pattern) =>
    PostgisContainer.ImageOf(postgres).ShouldMatch(pattern);

[Fact]
public void ImageOf_PostgresImage_EndsWithTheDockerfileHash() =>
    PostgisContainer.ImageOf("postgres:18.6").ShouldEndWith(
        "-" + PostgisContainer.DockerfileHash(
            File.ReadAllBytes(RepositoryRoot.Path("tests", "DatabaseTests", "TestHarness", "Postgis", "Dockerfile"))));

[Theory]
[InlineData("a\nb")]
[InlineData("a\r\nb")]
public void DockerfileHash_LineEndings_AreIgnored(string text) =>
    PostgisContainer.DockerfileHash(Encoding.UTF8.GetBytes(text)).ShouldBe("7e18f737311b");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*PostgisContainerTests'`
Expected: build error, `DockerfileHash` not defined.

- [ ] **Step 3: Implement** in `PostgisContainer.cs`: `DockerfileHash` drops every 0x0D byte, hashes with `SHA256.HashData`, returns `Convert.ToHexStringLower(...)[..12]`.  `ImageOf` appends `-{hash}` of the repository's Dockerfile, read once (a static `Lazy<string>` or a static readonly field).  `PrepareImageAsync` adds `.WithImageBuildPolicy(PullPolicy.Missing)` (`DotNet.Testcontainers.Images`).  Update the class and method comments: the image is built only when missing, and the hash renames it when the Dockerfile changes.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*PostgisContainerTests'`
Expected: PASS.

- [ ] **Step 5: Check by hand that the harness reuses its image**

```bash
dotnet build tests/DatabaseTests/Postgres --configuration Release
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres --configuration Release --no-build --filter-class '*PostgisParameterTests'
docker images --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep 'sqlsource-postgis:14.24'
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres --configuration Release --no-build --filter-class '*PostgisParameterTests'
docker images --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep 'sqlsource-postgis:14.24'
```

Expected: both runs pass; the hashed image's ID is the same after the second run, and the second run's duration is a few seconds, not 40 or more.  If the ID changes, `PullPolicy.Missing` does not skip the build: stop and report.

- [ ] **Step 6: Update the docs listed under Files**, run `./pre-commit-validation.sh`, and commit.

```bash
git add docs/superpowers/specs/2026-11-12-postgis-image-prebuild-design.md docs/superpowers/plans/2026-11-12-postgis-image-prebuild.md
git add tests/DatabaseTests/TestHarness/PostgisContainer.cs tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs tests/DatabaseTests/Postgres/README.md tests/DatabaseTests/README.md
git commit -m "test(harness): name the PostGIS image by its Dockerfile's hash and reuse it when it exists"
```

### Task 2: `tools/build-postgis-image.sh`, used by `describe-end-to-end.sh`

**Files:**
- Create: `tools/build-postgis-image.sh` (executable, LF)
- Modify: `tools/describe-end-to-end.sh:159-167` and its `postgis_package` helper, which moves to the new script
- Create: `tests/DatabaseTests/Postgres/PostgisImageScriptTests.cs`
- Modify: `tools/test-scopes.txt`: `postgres tools/build-postgis-image.sh` before `none tools/*`, since the new test runs it, so a change to it reaches the PostgreSQL projects in `pre-commit-validation.sh`
- Modify: `tools/README.md` (a section for the script; the describe script's paragraph), `tools/AGENTS.md:16` (the describe script now calls the new script; a rule that the script and `PostgisContainer.ImageOf` compute one name, kept by the test), `tests/DatabaseTests/Postgres/README.md` (the test in the list of tests that need no server; "The PostGIS image" names the script)

**Interfaces:**
- Consumes: `PostgisContainer.ImageOf(string)` from Task 1.
- Produces: `tools/build-postgis-image.sh [--name] [postgres-image]`.  With no image it uses `DefaultImages.Postgres`, read by the same `sed` as `pull-test-images.sh`.  `--name` prints the image name and exits 0, building nothing.  Otherwise it runs `docker build --progress=plain --build-arg "PG=<tag>" --build-arg "POSTGIS=<package>" -t <name> tests/DatabaseTests/TestHarness/Postgis` from the repository root, so Docker's whole output is in the log, and exits with the build's status.  The package is read from `PostgisContainer.cs`'s `Package` line as `describe-end-to-end.sh` reads it today.

- [ ] **Step 1: Write the failing test** `PostgisImageScriptTests`, not in any collection, since it starts no container:

```csharp
[Theory]
[InlineData("postgres:18.6")]
[InlineData("postgres:14.24")]
public async Task Name_PostgresImage_IsTheHarnesss(string postgres) =>
    (await RunScriptAsync("--name", postgres)).ShouldBe(PostgisContainer.ImageOf(postgres));

[Fact]
public async Task Name_NoImage_IsTheHarnesssOnTheDefaultImage() =>
    (await RunScriptAsync("--name")).ShouldBe(PostgisContainer.ImageOf(DefaultImages.Postgres));
```

`RunScriptAsync` starts `bash tools/build-postgis-image.sh <args>` from `RepositoryRoot`, asserts exit code 0, and returns stdout trimmed.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test --project tests/DatabaseTests/Postgres --filter-class '*PostgisImageScriptTests'`
Expected: FAIL, the script does not exist.

- [ ] **Step 3: Write `tools/build-postgis-image.sh`** to the interface above, with a header comment in the style of `pull-test-images.sh` (what it does, who calls it, that it always builds and the harness then reuses the image, usage).  Use `set -euo pipefail`.

- [ ] **Step 4: Run the test to verify it passes**, plus `tools/shellcheck.sh` and `tools/shfmt.sh --check`.
Expected: PASS, and no findings.

- [ ] **Step 5: Make `describe-end-to-end.sh` call the script**: `postgis_image="$(tools/build-postgis-image.sh --name "$image")"`, then `tools/build-postgis-image.sh "$image"` in place of the `docker build --quiet` and its `Building ... done` lines.  Remove `postgis_package` and `POSTGIS_CONTAINER` if nothing else uses them.

- [ ] **Step 6: Check by hand that the script's image is reused**

```bash
tools/build-postgis-image.sh postgres:14.24
docker images --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep 'sqlsource-postgis:14.24'
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres --configuration Release --no-build --filter-class '*PostgisParameterTests'
docker images --format '{{.Repository}}:{{.Tag}} {{.ID}}' | grep 'sqlsource-postgis:14.24'
```

Expected: the build log shows apt's lines (or cached steps); the test passes; the image ID is unchanged.

- [ ] **Step 7: Update the docs listed under Files**, run `./pre-commit-validation.sh`, and commit.

```bash
git add tools/build-postgis-image.sh tools/describe-end-to-end.sh tools/test-scopes.txt tools/README.md tools/AGENTS.md tests/DatabaseTests/Postgres/PostgisImageScriptTests.cs tests/DatabaseTests/Postgres/README.md
git commit -m "build: build the PostGIS image with tools/build-postgis-image.sh"
```

### Task 3: The CI and nightly jobs build the image in a step of their own

**Files:**
- Modify: `.github/workflows/build.yml` (jobs `build`, `test-postgres-floor`)
- Modify: `.github/workflows/nightly.yml` (jobs `test-postgres`, `sync-async-postgres`)
- Modify: `.github/workflows/README.md:18` (the build step beside the pull step), `tools/AGENTS.md` (the rule at line 12 gains the new script as a CI step, not a check, so it is not in `pre-commit-validation.sh`)

**Interfaces:**
- Consumes: `tools/build-postgis-image.sh [postgres-image]` from Task 2.

- [ ] **Step 1: Add a step "Build PostGIS image" after each job's pull step and before its Test step**, with a comment saying a failed build fails the job on this step, with Docker's output, instead of inside the tests:
  - `build.yml` `build`: `id: postgis`, `if: ${{ !cancelled() && steps.build.outcome == 'success' }}`, `run: tools/build-postgis-image.sh`; the Test step's `if:` becomes `${{ !cancelled() && steps.build.outcome == 'success' && steps.postgis.outcome == 'success' }}`.
  - `build.yml` `test-postgres-floor`: `run: tools/build-postgis-image.sh "$POSTGRES_FLOOR_IMAGE"`.
  - `nightly.yml` `test-postgres`: `env: IMAGE: ${{ matrix.image }}`, `run: tools/build-postgis-image.sh "$IMAGE"`.
  - `nightly.yml` `sync-async-postgres`: `run: tools/build-postgis-image.sh`.

- [ ] **Step 2: Lint the workflows**

Run: `tools/actionlint.sh`
Expected: no findings.

- [ ] **Step 3: Update the docs listed under Files**, run `./pre-commit-validation.sh`, and commit.

```bash
git add .github/workflows/build.yml .github/workflows/nightly.yml .github/workflows/README.md tools/AGENTS.md
git commit -m "ci: build the PostGIS image in a step of its own before the tests"
```

### Task 4: Full validation and review

- [ ] **Step 1:** Run `./pre-commit-validation.sh --full`.  Expected: pass.
- [ ] **Step 2:** Run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres --configuration Release`, since the harness changed.  Expected: pass.
- [ ] **Step 3:** Run the `branch-review:review` skill, address its findings, and run it again until clean.
