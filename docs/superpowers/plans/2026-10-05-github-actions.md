# GitHub Actions CI and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add CI for pull requests and `main`, build-time versioning, NuGet packaging, coverage reporting, and a trusted-publishing release workflow.

**Architecture:** `VersionPrefix` in `Directory.Build.props` is the hand-edited version; the workflows pass `BuildNumber` and `VersionSuffix` to MSBuild as environment variables.  A reusable workflow `build.yml` runs every check, builds, tests with coverage and packs.  `ci.yml` calls it, comments coverage on the pull request and ends with an aggregate `ci` job.  `publish.yml` calls it, pushes to nuget.org from the `nuget` environment and creates a GitHub release for a tag.

**Tech Stack:** GitHub Actions, .NET SDK 10.0.401, MSBuild, xUnit v3 on Microsoft.Testing.Platform, Microsoft.Testing.Extensions.CodeCoverage, GitHubActionsTestLogger, ReportGenerator, `gh` CLI, nuget.org trusted publishing.

**Spec:** `docs/superpowers/specs/2026-10-05-github-actions-design.md`

## Global Constraints

- Work on the branch `github-actions`.  Never merge `main` into it; rebase only.
- Every commit must pass `./pre-commit-validation.sh`.  Run it as its own command, fix what it reports, then commit in a separate command.  It needs Docker running.
- Never use `NoWarn`.  A NuGet or compiler warning is resolved within the rules in the comment in `Directory.Build.props`.
- Every action is pinned to a full commit SHA with the version in a trailing comment.  Use exactly these:
  - `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1`
  - `actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0`
  - `actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1`
  - `actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1`
  - `NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0`
- No line in a `.yml`, `.props`, `.csproj` or `.sh` file is longer than 120 characters; `tools/editorconfig-checker.sh` enforces it.  YAML indents by 2 spaces, XML and shell by 4.
- The release workflow file is named `publish.yml` and its publish job uses the environment `nuget`.  Both are fixed by the nuget.org policy.
- Version values reach MSBuild as the job-level environment variables `BuildNumber` and `VersionSuffix`, never as `-p:` flags.
- Markdown in this repository puts two spaces after a sentence and uses `|----|` table separators.
- End every commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Do not push, open a pull request, or run `publish.yml` without the user's go-ahead.

## Review Focus

1. A release tag that does not equal `VersionPrefix`, or a tag build that produced a suffixed package: `publish.yml` must fail before it logs in to nuget.org.  Pinned in Task 5, Step 3.
2. A tag build, where `CI` is `true` and `VersionSuffix` is the empty string: the package version must be `0.1.0`, not `0.1.0-dev` and not `0.1.0-`.  Pinned in Task 1, Step 4.
3. One failing check must not hide the others: a lint failure still lets the build and tests run and report.  Pinned in Task 4, Step 4.
4. A second run on the same pull request must update the coverage comment, not add another.  Pinned in Task 7, Step 4.
5. A `packages.lock.json` that no longer matches its project must fail the restore on a runner, not be rewritten.  Pinned in Task 3, Step 2.

---

### Task 1: Version properties

**Files:**
- Modify: `Directory.Build.props` (the `AssemblyVersion` and `InformationalVersion` lines at the end of the first `PropertyGroup`)

**Interfaces:**
- Consumes: nothing.
- Produces: MSBuild properties read from the environment: `BuildNumber` (integer, default `0`) and `VersionSuffix` (string, default `dev` when `CI` is not `true`).  `VersionPrefix` is `0.1.0`.  Tasks 2, 4 and 5 rely on these names.

- [ ] **Step 1: Record the current, wrong values**

Run:

```bash
dotnet msbuild src/SqlSource/SqlSource.csproj -getProperty:PackageVersion -getProperty:AssemblyVersion -getProperty:FileVersion
```

Expected: `"PackageVersion": "1.0.0"` and `"AssemblyVersion": "0.1.0.0"`.  The package version is the SDK default, which is the failure this task fixes.

- [ ] **Step 2: Replace the hardcoded versions**

In `Directory.Build.props`, replace these two lines:

```xml
        <AssemblyVersion>0.1.0.0</AssemblyVersion>
        <InformationalVersion>0.1.0.0</InformationalVersion>
```

with:

```xml
        <!--
            VersionPrefix is the only version number edited by hand.  The workflows supply BuildNumber and
            VersionSuffix as environment variables, which MSBuild reads as properties.  Away from CI the suffix is
            "dev".  The SDK composes the package and informational versions.  See "Versioning" in README.md.
        -->
        <VersionPrefix>0.1.0</VersionPrefix>
        <BuildNumber Condition="'$(BuildNumber)' == ''">0</BuildNumber>
        <VersionSuffix Condition="'$(VersionSuffix)' == '' and '$(CI)' != 'true'">dev</VersionSuffix>
        <AssemblyVersion>$(VersionPrefix).$(BuildNumber)</AssemblyVersion>
        <FileVersion>$(VersionPrefix).$(BuildNumber)</FileVersion>
        <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
```

- [ ] **Step 3: Verify the local case**

Run:

```bash
dotnet msbuild src/SqlSource/SqlSource.csproj -getProperty:PackageVersion -getProperty:AssemblyVersion -getProperty:FileVersion
```

Expected: `"PackageVersion": "0.1.0-dev"`, `"AssemblyVersion": "0.1.0.0"`, `"FileVersion": "0.1.0.0"`.

- [ ] **Step 4: Verify the pull request, main and tag cases**

Run each command.  The variables are set for that one command only.

```bash
CI=true BuildNumber=123 VersionSuffix=pr-42.123 dotnet msbuild src/SqlSource/SqlSource.csproj -getProperty:PackageVersion -getProperty:AssemblyVersion -getProperty:FileVersion
```

Expected: `"PackageVersion": "0.1.0-pr-42.123"`, `"AssemblyVersion": "0.1.0.123"`, `"FileVersion": "0.1.0.123"`.

```bash
CI=true BuildNumber=123 VersionSuffix=beta.123 dotnet msbuild src/SqlSource/SqlSource.csproj -getProperty:PackageVersion -getProperty:AssemblyVersion -getProperty:FileVersion
```

Expected: `"PackageVersion": "0.1.0-beta.123"`, `"AssemblyVersion": "0.1.0.123"`, `"FileVersion": "0.1.0.123"`.

```bash
CI=true BuildNumber=7 VersionSuffix= dotnet msbuild src/SqlSource/SqlSource.csproj -getProperty:PackageVersion -getProperty:AssemblyVersion -getProperty:FileVersion
```

Expected: `"PackageVersion": "0.1.0"`, `"AssemblyVersion": "0.1.0.7"`, `"FileVersion": "0.1.0.7"`.  The package version must have no suffix and no trailing hyphen.

- [ ] **Step 5: Verify the stamped attributes**

Run:

```bash
BuildNumber=123 VersionSuffix=beta.123 dotnet build SqlSource.slnx --configuration Release
```

then:

```bash
grep Version src/SqlSource/obj/Release/netstandard2.0/SqlSource.AssemblyInfo.cs
```

Expected: `AssemblyFileVersionAttribute("0.1.0.123")`, `AssemblyVersionAttribute("0.1.0.123")`, and `AssemblyInformationalVersionAttribute("0.1.0-beta.123+<40 hex characters>")`.

- [ ] **Step 6: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 7: Commit**

```bash
git add Directory.Build.props
git commit -m "Compose the version from VersionPrefix, a build number and a suffix

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Package the generator as an analyzer

**Files:**
- Modify: `src/SqlSource/SqlSource.csproj`

**Interfaces:**
- Consumes: `VersionPrefix`, `VersionSuffix` from Task 1.
- Produces: `dotnet pack SqlSource.slnx` writes one file, `SqlSource.<package version>.nupkg`.  Tasks 4 and 5 rely on that file name pattern and on the package id `SqlSource`.

- [ ] **Step 1: See the wrong layout**

Run:

```bash
dotnet pack SqlSource.slnx --output artifacts/packages-before
```

then:

```bash
unzip -l artifacts/packages-before/SqlSource.0.1.0-dev.nupkg
```

Expected: the listing contains `lib/netstandard2.0/SqlSource.dll` and nothing under `analyzers/`.  A package shaped like that adds a library reference and never runs the generator.

- [ ] **Step 2: Add the package properties and items**

Replace the whole of `src/SqlSource/SqlSource.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>netstandard2.0</TargetFramework>
        <IsRoslynComponent>true</IsRoslynComponent>
        <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    </PropertyGroup>
    <PropertyGroup>
        <PackageId>SqlSource</PackageId>
        <Description>C# SQL query source generator.</Description>
        <Authors>Michael Crawford</Authors>
        <PackageLicenseExpression>MIT</PackageLicenseExpression>
        <PackageProjectUrl>https://github.com/mbcrawfo/SqlSource</PackageProjectUrl>
        <RepositoryUrl>https://github.com/mbcrawfo/SqlSource</RepositoryUrl>
        <PackageTags>sql;source-generator;roslyn</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <!-- The compiler loads the generator from analyzers/dotnet/cs.  Nothing goes in lib/. -->
        <IncludeBuildOutput>false</IncludeBuildOutput>
        <DevelopmentDependency>true</DevelopmentDependency>
        <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
    </ItemGroup>
    <ItemGroup>
        <None Include="../../README.md" Pack="true" PackagePath="/" Visible="false" />
        <None
            Include="$(OutputPath)$(AssemblyName).dll"
            Pack="true"
            PackagePath="analyzers/dotnet/cs"
            Visible="false"
        />
    </ItemGroup>
</Project>
```

The multi-line `<None>` is how CSharpier formats it.  `./format.sh --check` fails on the one-line form.

- [ ] **Step 3: Verify the layout**

Run:

```bash
dotnet pack SqlSource.slnx --output artifacts/packages-after
```

Expected: `Successfully created package '.../SqlSource.0.1.0-dev.nupkg'`, 0 warnings, and no package for `SqlSource.Tests`.

```bash
unzip -l artifacts/packages-after/SqlSource.0.1.0-dev.nupkg
```

Expected: `analyzers/dotnet/cs/SqlSource.dll` and `README.md` are listed.  Nothing is under `lib/`.

- [ ] **Step 4: Verify the metadata**

Run:

```bash
unzip -p artifacts/packages-after/SqlSource.0.1.0-dev.nupkg SqlSource.nuspec
```

Expected: `<id>SqlSource</id>`, `<version>0.1.0-dev</version>`, `<developmentDependency>true</developmentDependency>`, `<license type="expression">MIT</license>`, `<readme>README.md</readme>`, a `<repository type="git" url="https://github.com/mbcrawfo/SqlSource" ...>` element, and no `<dependencies>` element.

- [ ] **Step 5: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 6: Commit**

```bash
git add src/SqlSource/SqlSource.csproj
git commit -m "Pack the generator as an analyzer package

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Coverage and the GitHub test reporter in the test project

**Files:**
- Modify: `tests/SqlSource.Tests/SqlSource.Tests.csproj`
- Modify: `tests/SqlSource.Tests/packages.lock.json` (regenerated, not edited by hand)

**Interfaces:**
- Consumes: the versions of `GitHubActionsTestLogger` (3.0.5) and `Microsoft.Testing.Extensions.CodeCoverage` (18.11.2) already pinned in `Directory.Packages.props`.
- Produces: the test runner accepts `--coverage`, `--coverage-output-format cobertura`, `--coverage-output <file>` and `--report-github`.  Without `--coverage-output` the report is written to the results directory as `<guid>.cobertura.xml`.  Task 4 relies on these options and on that file name pattern.

- [ ] **Step 1: Add the two package references**

In `tests/SqlSource.Tests/SqlSource.Tests.csproj`, replace the `ItemGroup` that holds the package references with:

```xml
    <ItemGroup>
        <PackageReference Include="GitHubActionsTestLogger" />
        <PackageReference Include="Microsoft.CodeAnalysis.CSharp" VersionOverride="5.9.0" />
        <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="xunit.v3" />
    </ItemGroup>
```

- [ ] **Step 2: Verify that a stale lock file fails a CI restore**

Run:

```bash
CI=true dotnet restore SqlSource.slnx
```

Expected: FAIL with `error NU1004` saying the package references have changed for `SqlSource.Tests`.  This is the behaviour a runner depends on.

- [ ] **Step 3: Update the lock file**

Run:

```bash
dotnet restore SqlSource.slnx
```

then:

```bash
git status --short
```

Expected: `tests/SqlSource.Tests/SqlSource.Tests.csproj` and `tests/SqlSource.Tests/packages.lock.json` are modified.  `src/SqlSource/packages.lock.json` is not.

- [ ] **Step 4: Verify the locked restore now passes**

Run:

```bash
CI=true dotnet restore SqlSource.slnx
```

Expected: exit code 0.

- [ ] **Step 5: Verify coverage collection**

Run:

```bash
dotnet build SqlSource.slnx
```

then:

```bash
dotnet test --solution SqlSource.slnx --no-build --results-directory artifacts/test-results --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

Expected: `Test run summary: Passed!` and, under "In process file artifacts produced", the path `artifacts/test-results/coverage.cobertura.xml`.

- [ ] **Step 6: Verify the report**

Run:

```bash
dotnet reportgenerator -reports:artifacts/test-results/coverage.cobertura.xml -targetdir:artifacts/coverage '-reporttypes:Html;Cobertura;MarkdownSummaryGithub' '-assemblyfilters:+SqlSource'
```

Expected: the output names `artifacts/coverage/Cobertura.xml`, `artifacts/coverage/index.html` and `artifacts/coverage/SummaryGithub.md`.

```bash
grep -E 'Assemblies|SqlSource.SqlSourceGenerator' artifacts/coverage/SummaryGithub.md
```

Expected: `| Assemblies: | 1 |` and a row for `SqlSource.SqlSourceGenerator`.  The test assembly is not in the report.

- [ ] **Step 7: Verify the reporter option exists**

Run:

```bash
dotnet test --solution SqlSource.slnx --no-build --help | grep -- '--report-github '
```

Expected: one line, `--report-github    Enables the GitHub Actions test reporter.`

- [ ] **Step 8: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 9: Commit**

```bash
git add tests/SqlSource.Tests/SqlSource.Tests.csproj tests/SqlSource.Tests/packages.lock.json
git commit -m "Add coverage collection and the GitHub test reporter to the tests

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `build.yml` and `ci.yml`

**Files:**
- Create: `.github/workflows/build.yml`
- Create: `.github/workflows/ci.yml`
- Create: `docs/tech-debt/TD-0002-no-coverage-comment-on-fork-pull-requests.md`
- Modify: `docs/tech-debt/README.md` (`Next id` and the table)

**Interfaces:**
- Consumes: `BuildNumber` and `VersionSuffix` from Task 1; the package from Task 2; the test options and coverage file pattern from Task 3.
- Produces: the reusable workflow `./.github/workflows/build.yml` with one optional string input, `version-suffix`.  It uploads the artifact `packages` (the `.nupkg` files at the artifact root) and the artifact `coverage` (with `SummaryGithub.md` at the artifact root).  `ci.yml` has a job whose id and name are both `ci`.  Task 5 calls `build.yml` and downloads `packages`.

- [ ] **Step 1: Create `build.yml`**

Create `.github/workflows/build.yml`:

```yaml
# Runs every check, builds, tests, packs and reports coverage.  Called by ci.yml and publish.yml.
# The check steps mirror pre-commit-validation.sh: a check added to one is added to the other in the same commit.
name: Build

on:
  workflow_call:
    inputs:
      version-suffix:
        description: Prerelease suffix of the package version.  Empty for a release build.
        type: string
        required: false
        default: ''

permissions:
  contents: read

env:
  SOLUTION: SqlSource.slnx
  CONFIGURATION: Release
  DOTNET_NOLOGO: 'true'

jobs:
  build:
    name: Check, build, test and pack
    runs-on: ubuntu-latest
    timeout-minutes: 15
    env:
      # MSBuild reads environment variables as properties, so every dotnet command in this job sees the same version.
      BuildNumber: ${{ github.run_number }}
      VersionSuffix: ${{ inputs.version-suffix }}
    steps:
      - name: Check out
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - name: Set up .NET
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore packages
        run: dotnet restore "$SOLUTION"

      # Every check runs even when an earlier one failed, so that one run reports every problem.
      - name: Check formatting
        if: ${{ !cancelled() }}
        run: ./format.sh --check

      - name: Check .editorconfig
        if: ${{ !cancelled() }}
        run: tools/editorconfig-checker.sh

      - name: Lint shell scripts
        if: ${{ !cancelled() }}
        run: tools/shellcheck.sh

      - name: Check shell script formatting
        if: ${{ !cancelled() }}
        run: tools/shfmt.sh --check

      - name: Lint workflows
        if: ${{ !cancelled() }}
        run: tools/actionlint.sh

      - name: Build
        id: build
        if: ${{ !cancelled() }}
        run: dotnet build "$SOLUTION" --configuration "$CONFIGURATION" --no-restore

      - name: Test
        if: ${{ !cancelled() && steps.build.outcome == 'success' }}
        run: >-
          dotnet test --solution "$SOLUTION" --configuration "$CONFIGURATION" --no-build
          --results-directory artifacts/test-results
          --coverage --coverage-output-format cobertura
          --report-github

      - name: Report coverage
        id: coverage
        if: ${{ !cancelled() && hashFiles('artifacts/test-results/*.cobertura.xml') != '' }}
        run: |
          dotnet reportgenerator \
              '-reports:artifacts/test-results/*.cobertura.xml' \
              -targetdir:artifacts/coverage \
              '-reporttypes:Html;Cobertura;MarkdownSummaryGithub' \
              '-assemblyfilters:+SqlSource'
          cat artifacts/coverage/SummaryGithub.md >> "$GITHUB_STEP_SUMMARY"

      - name: Upload coverage
        if: ${{ !cancelled() && steps.coverage.outcome == 'success' }}
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: coverage
          path: artifacts/coverage
          if-no-files-found: error

      - name: Pack
        run: dotnet pack "$SOLUTION" --configuration "$CONFIGURATION" --no-build --output artifacts/packages

      - name: Upload packages
        uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        with:
          name: packages
          path: artifacts/packages/*.nupkg
          if-no-files-found: error
```

`Pack` and `Upload packages` have no `if`, so they run only when every earlier step passed.  A package is never uploaded from a run with a failed check.

- [ ] **Step 2: Create `ci.yml`**

Create `.github/workflows/ci.yml`:

```yaml
# Checks, builds, tests and packs every pull request to main and every push to main.
name: CI

on:
  pull_request:
    branches:
      - main
  push:
    branches:
      - main

permissions:
  contents: read

# A newer run for the same pull request cancels the older one.  Runs on main are never cancelled.
concurrency:
  group: ci-${{ github.event.pull_request.number || github.run_id }}
  cancel-in-progress: true

jobs:
  build:
    name: Build
    uses: ./.github/workflows/build.yml
    with:
      version-suffix: >-
        ${{ github.event_name == 'pull_request'
        && format('pr-{0}.{1}', github.event.pull_request.number, github.run_number)
        || format('beta.{0}', github.run_number) }}

  # Pull requests from forks and from Dependabot get a read-only token, so they get no comment: TD-0002.
  coverage-comment:
    name: Coverage comment
    needs: build
    if: >-
      ${{ github.event_name == 'pull_request'
      && github.event.pull_request.head.repo.full_name == github.repository
      && github.actor != 'dependabot[bot]' }}
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      pull-requests: write
    steps:
      - name: Download the coverage report
        uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          name: coverage
          path: coverage

      - name: Create or update the comment
        env:
          GH_TOKEN: ${{ github.token }}
          REPO: ${{ github.repository }}
          PR_NUMBER: ${{ github.event.pull_request.number }}
          RUN_URL: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
        run: |
          # The marker on the first line identifies the comment on later runs.
          {
              echo '<!-- sqlsource-coverage -->'
              cat coverage/SummaryGithub.md
              printf '\nThe full report is the coverage artifact of [this run](%s).\n' "$RUN_URL"
          } > comment.md

          comment_id="$(
              gh api "repos/$REPO/issues/$PR_NUMBER/comments" --paginate \
                  --jq '.[] | select(.body | startswith("<!-- sqlsource-coverage -->")) | .id' | head -n 1
          )"

          if [[ -n "$comment_id" ]]; then
              gh api --method PATCH "repos/$REPO/issues/comments/$comment_id" --field body=@comment.md > /dev/null
          else
              gh api --method POST "repos/$REPO/issues/$PR_NUMBER/comments" --field body=@comment.md > /dev/null
          fi

  # The one job to require in branch protection.  It runs even when a job it needs failed, because a skipped
  # required check counts as passing.  A skipped job is not a failure: coverage-comment is skipped on pushes.
  ci:
    name: ci
    needs:
      - build
      - coverage-comment
    if: ${{ always() }}
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions: {}
    steps:
      - name: Fail unless every job succeeded or was skipped
        if: ${{ contains(needs.*.result, 'failure') || contains(needs.*.result, 'cancelled') }}
        run: exit 1
```

- [ ] **Step 3: Lint the workflows**

Run: `tools/actionlint.sh`
Expected: no output, exit code 0.  actionlint also runs ShellCheck on every `run:` block.

Run: `tools/editorconfig-checker.sh`
Expected: exit code 0.  A failure here is usually a line longer than 120 characters.

- [ ] **Step 4: Verify that no check can hide another**

Run:

```bash
grep -c '!cancelled()' .github/workflows/build.yml
```

Expected: `9`.  The nine are the five checks, `Build`, `Test`, `Report coverage` and `Upload coverage`.  If the number is lower, a step is missing its `if` and will be skipped when an earlier check fails.

- [ ] **Step 5: Record the tech debt**

Create `docs/tech-debt/TD-0002-no-coverage-comment-on-fork-pull-requests.md`:

```markdown
# TD-0002: No coverage comment on fork and Dependabot pull requests

## Problem

The `coverage-comment` job in [`ci.yml`](../../.github/workflows/ci.yml) is skipped when a pull request comes from a fork or from Dependabot.  Those pull requests get no coverage comment.

## Why it exists

GitHub gives the `pull_request` runs of forks and of Dependabot a read-only `GITHUB_TOKEN`, so the job could not create the comment.  Skipping it keeps those runs green.  The alternative, a privileged workflow that comments on their behalf, was judged not worth its risk while the project has no outside contributors.

## Impact

Low.  The coverage report is still in the run's job summary and in its `coverage` artifact.  Only the comment on the pull request is missing, so a reviewer has to open the run to see coverage.

## Proposed fix

Add a second workflow triggered by `workflow_run` on completion of `CI`.  It runs with `pull-requests: write`, downloads the `coverage` artifact of the triggering run and creates or updates the comment.  It must treat the artifact as untrusted input: never check out or execute the pull request's code, and post the markdown only after checking its size and that it is the expected file.  Then remove the fork and Dependabot conditions from `coverage-comment`, or remove that job entirely.

## Trigger

The first pull request from an outside contributor, or enabling Dependabot.
```

In `docs/tech-debt/README.md`, change `Next id: \`TD-0002\`` to `Next id: \`TD-0003\`` and add this row to the end of the "Active items" table:

```markdown
| [TD-0002](TD-0002-no-coverage-comment-on-fork-pull-requests.md) | Open | 2026-10-05 | Low | Fork and Dependabot pull requests get no coverage comment |
```

- [ ] **Step 6: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`, including `actionlint`.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/build.yml .github/workflows/ci.yml docs/tech-debt
git commit -m "Add the CI workflow

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `publish.yml` and `.github/AGENTS.md`

**Files:**
- Create: `.github/workflows/publish.yml`
- Create: `.github/AGENTS.md`

**Interfaces:**
- Consumes: `./.github/workflows/build.yml` and its `version-suffix` input, and the `packages` artifact, from Task 4.  The GitHub environment `nuget` and its variable `NUGET_USER`, which already exist.
- Produces: nothing that a later task consumes.

- [ ] **Step 1: Create `publish.yml`**

Create `.github/workflows/publish.yml`:

```yaml
# Publishes to nuget.org through trusted publishing: a release from a v* tag, or a beta from a manual run on main.
# The file name and the nuget environment are part of the nuget.org policy.  Do not rename either.
name: Publish

on:
  push:
    tags:
      - v*
  workflow_dispatch:

permissions:
  contents: read

jobs:
  build:
    name: Build
    if: ${{ github.ref_type == 'tag' || github.ref == 'refs/heads/main' }}
    uses: ./.github/workflows/build.yml
    with:
      # The empty string must be the last operand: `cond && '' || x` always yields x.
      version-suffix: ${{ github.ref_type != 'tag' && format('beta.{0}', github.run_number) || '' }}

  publish:
    name: Push to nuget.org
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    environment: nuget
    permissions:
      id-token: write
    steps:
      - name: Download the package
        uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          name: packages
          path: packages

      - name: Check that the package matches the tag
        if: ${{ github.ref_type == 'tag' }}
        env:
          TAG: ${{ github.ref_name }}
        run: |
          shopt -s nullglob
          expected="packages/SqlSource.${TAG#v}.nupkg"
          found=(packages/*.nupkg)
          if [[ ${#found[@]} -ne 1 || "${found[0]}" != "$expected" ]]; then
              echo "::error::Tag $TAG needs exactly $expected, but the build produced: ${found[*]:-nothing}."
              echo 'Set VersionPrefix in Directory.Build.props to the version in the tag.'
              exit 1
          fi

      - name: Log in to nuget.org
        id: login
        uses: NuGet/login@8d196754b4036150537f80ac539e15c2f1028841 # v1.2.0
        with:
          user: ${{ vars.NUGET_USER }}

      # --skip-duplicate makes a re-run after a partial failure safe.
      - name: Push
        env:
          NUGET_API_KEY: ${{ steps.login.outputs.NUGET_API_KEY }}
        run: >-
          dotnet nuget push packages/*.nupkg
          --api-key "$NUGET_API_KEY"
          --source https://api.nuget.org/v3/index.json
          --skip-duplicate

  release:
    name: Create the GitHub release
    needs: publish
    if: ${{ github.ref_type == 'tag' }}
    runs-on: ubuntu-latest
    timeout-minutes: 5
    permissions:
      contents: write
    steps:
      - name: Download the package
        uses: actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c # v8.0.1
        with:
          name: packages
          path: packages

      - name: Create the release
        env:
          GH_TOKEN: ${{ github.token }}
          REPO: ${{ github.repository }}
          TAG: ${{ github.ref_name }}
        run: gh release create "$TAG" packages/*.nupkg --repo "$REPO" --title "$TAG" --generate-notes --verify-tag
```

When `build` is skipped because the ref is neither a tag nor `main`, `publish` and `release` are skipped with it.

- [ ] **Step 2: Lint**

Run: `tools/actionlint.sh`
Expected: no output, exit code 0.

Run: `tools/editorconfig-checker.sh`
Expected: exit code 0.

- [ ] **Step 3: Test the tag check**

This workflow cannot run before it is on `main`, so test the script of the "Check that the package matches the tag" step by hand.  Make a scratch directory outside the repository and save the script there, copied verbatim from the step:

```bash
scratch="$(mktemp -d)" && echo "$scratch"
```

Create `$scratch/tag-check.sh`:

```bash
#!/usr/bin/env bash
set -eo pipefail
shopt -s nullglob
expected="packages/SqlSource.${TAG#v}.nupkg"
found=(packages/*.nupkg)
if [[ ${#found[@]} -ne 1 || "${found[0]}" != "$expected" ]]; then
    echo "::error::Tag $TAG needs exactly $expected, but the build produced: ${found[*]:-nothing}."
    echo 'Set VersionPrefix in Directory.Build.props to the version in the tag.'
    exit 1
fi
```

Create the four cases:

```bash
mkdir -p "$scratch/empty/packages" "$scratch/match/packages" "$scratch/two/packages" "$scratch/suffixed/packages"
touch "$scratch/match/packages/SqlSource.0.1.0.nupkg"
touch "$scratch/two/packages/SqlSource.0.1.0.nupkg" "$scratch/two/packages/Other.0.1.0.nupkg"
touch "$scratch/suffixed/packages/SqlSource.0.1.0-beta.5.nupkg"
```

Run each line and compare the exit code:

| Command | Expected |
|----|----|
| `(cd "$scratch/match" && TAG=v0.1.0 bash ../tag-check.sh); echo $?` | `0`, no output |
| `(cd "$scratch/match" && TAG=v0.2.0 bash ../tag-check.sh); echo $?` | `1`, error naming `SqlSource.0.2.0.nupkg` |
| `(cd "$scratch/empty" && TAG=v0.1.0 bash ../tag-check.sh); echo $?` | `1`, "the build produced: nothing" |
| `(cd "$scratch/two" && TAG=v0.1.0 bash ../tag-check.sh); echo $?` | `1`, both files named |
| `(cd "$scratch/suffixed" && TAG=v0.1.0 bash ../tag-check.sh); echo $?` | `1`, error naming the `-beta.5` file |

The last case is the safety net for the `version-suffix` expression: if a tag build were ever given a suffix, the publish stops here.

- [ ] **Step 4: Create `.github/AGENTS.md`**

Create `.github/AGENTS.md`:

```markdown
# AGENTS.md - .github

GitHub Actions workflows.  `workflows/ci.yml` and `workflows/publish.yml` both call the reusable `workflows/build.yml`.

## Actions

- **Pin every action to a full commit SHA**, with the version in a trailing comment: `uses: actions/checkout@<40-character sha> # v7.0.1`.  Never use a tag or a branch as the ref.
- **When adding an action, pin its latest release.**  Look it up; do not copy a SHA from another repository or from memory:

  ```bash
  gh api repos/<owner>/<repo>/releases/latest --jq .tag_name
  ```

  ```bash
  gh api repos/<owner>/<repo>/commits/<tag> --jq .sha
  ```

- When changing a pin, change the SHA and the version comment together, everywhere the action is used.

## Workflows

- **`workflows/publish.yml` keeps its file name, and its `publish` job keeps `environment: nuget`.**  The nuget.org trusted publishing policy names both; renaming either stops publishing.  The `NuGet/login` and push steps stay in that file, not in a reusable workflow.
- **The check steps in `workflows/build.yml` mirror `pre-commit-validation.sh`.**  A check added to or removed from one is added to or removed from the other in the same commit.
- **The `ci` job in `workflows/ci.yml` is the required status check.**  Keep its name.  Add every new job in `ci.yml` to its `needs`.  It must keep `if: ${{ always() }}`: a skipped required check counts as passing.
- **Version values reach MSBuild as the job-level environment variables `BuildNumber` and `VersionSuffix`**, not as `-p:` flags, so that the format check, build, test and pack steps cannot disagree.
- **An expression cannot yield an empty string from the middle of `&&`/`||`.**  `cond && '' || x` always yields `x`.  Put the empty string last, as `publish.yml` does.
- Files here are limited to 120 characters per line.  Fold long commands with `>-` or `\`.
- Run `tools/actionlint.sh` after any change.  `publish.yml` cannot run from a branch: it is first exercised on `main`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
```

- [ ] **Step 5: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/publish.yml .github/AGENTS.md
git commit -m "Add the publish workflow and workflow guidance

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `README.md` and the root `AGENTS.md`

**Files:**
- Modify: `README.md`
- Modify: `AGENTS.md` (the "Pull requests" section)

**Interfaces:**
- Consumes: the commands verified in Tasks 1 to 3 and the workflow names from Tasks 4 and 5.
- Produces: nothing that a later task consumes.

- [ ] **Step 1: Add coverage and packaging to "Build and test"**

In `README.md`, directly after the `dotnet test --solution SqlSource.slnx` code block and before `## Checks`, insert:

````markdown
### Coverage

```bash
dotnet test --solution SqlSource.slnx --results-directory artifacts/test-results --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

```bash
dotnet reportgenerator -reports:artifacts/test-results/coverage.cobertura.xml -targetdir:artifacts/coverage -reporttypes:Html -assemblyfilters:+SqlSource
```

The report is `artifacts/coverage/index.html`.

### Package

```bash
dotnet pack SqlSource.slnx --output artifacts/packages
```

This builds in `Release` and writes `artifacts/packages/SqlSource.<version>-dev.nupkg`.
````

- [ ] **Step 2: Add the versioning, CI and release sections**

At the end of `README.md`, after the table of `tools/` scripts, append:

````markdown
## Versioning

`VersionPrefix` in `Directory.Build.props` is the `major.minor.patch` of the next release, and the only version number edited by hand.  The workflows add the rest.  With `VersionPrefix` 0.1.0, pull request 42 and run 123:

| Build | Package version | Assembly and file version |
|----|----|----|
| Pull request | `0.1.0-pr-42.123` | `0.1.0.123` |
| `main` | `0.1.0-beta.123` | `0.1.0.123` |
| Tag `v0.1.0` | `0.1.0` | `0.1.0.<run>` |
| Local | `0.1.0-dev` | `0.1.0.0` |

The informational version is the package version followed by `+<commit sha>`.  Run numbers are counted per workflow, so a beta published by `publish.yml` does not share a number with the `ci.yml` artifact for the same commit.

## Continuous integration

| Workflow | Runs on | Does |
|----|----|----|
| `ci.yml` | Pull requests to `main`, pushes to `main` | Calls `build.yml`, posts the coverage report as a pull request comment, and ends with the `ci` job |
| `build.yml` | Called by the other two | Every check in `pre-commit-validation.sh`, a `Release` build, the tests with coverage, and `dotnet pack` |
| `publish.yml` | `v*` tags, manual runs on `main` | Calls `build.yml`, pushes the package to nuget.org, and creates a GitHub release for a tag |

Each run uploads two artifacts: `packages`, the `.nupkg`, and `coverage`, the HTML, Cobertura and markdown reports.  The test results and the coverage summary are also in the run's job summary.  Pull requests from forks and from Dependabot get no coverage comment; see `docs/tech-debt/TD-0002-no-coverage-comment-on-fork-pull-requests.md`.

The `ci` job succeeds only when every other job in `ci.yml` succeeded or was skipped.  It is the one status check to require in the `main` ruleset.

## Releasing

Packages go to nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).  The repository holds no API key.

### Release

1. Set `VersionPrefix` in `Directory.Build.props` to the version being released, and merge that to `main`.
2. Tag the merged commit and push the tag:

   ```bash
   git tag v0.2.0
   ```

   ```bash
   git push origin v0.2.0
   ```

3. `publish.yml` builds the tag, fails if the tag is not `v<VersionPrefix>`, pushes `SqlSource.0.2.0.nupkg` to nuget.org and creates the GitHub release.
4. Raise `VersionPrefix` in the next pull request.  Until then, betas built from `main` sort below the release just published.

### Beta

Run `publish.yml` by hand on `main`, from the Actions tab or with:

```bash
gh workflow run publish.yml --ref main
```

It publishes `<VersionPrefix>-beta.<run>`.  A run started from any other branch builds and publishes nothing.

### External configuration

| Where | Setting | Value |
|----|----|----|
| nuget.org trusted publishing policy | Repository owner and repository | `mbcrawfo`, `SqlSource` |
| | Workflow file | `publish.yml` |
| | Environment | `nuget` |
| GitHub environment `nuget` | Deployment branches and tags | Branch `main`, tags `v*` |
| | Required reviewers | None |
| | Allow administrators to bypass | Off |
| | Variable `NUGET_USER` | The nuget.org profile name, not the email address |
| GitHub ruleset `main` | Required status check | `ci` |
````

- [ ] **Step 3: Add the version rule to `AGENTS.md`**

In the root `AGENTS.md`, in the "Pull requests" section, add this item after item 1:

```markdown
2. **Check the version before you open a pull request.**  Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with the highest release tag, `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If `VersionPrefix` is not greater than that tag, ask the user what the new version should be and set it in the same pull request.  When the repository has no `v*` tags there is nothing to compare.
```

- [ ] **Step 4: Check the commands in the README**

Run the three commands added in Step 1 exactly as written.
Expected: the tests pass, `artifacts/coverage/index.html` exists, and `artifacts/packages/SqlSource.0.1.0-dev.nupkg` exists.

- [ ] **Step 5: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 6: Commit**

```bash
git add README.md AGENTS.md
git commit -m "Document versioning, CI and releasing

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Pull request and verification on GitHub

**Files:**
- Commit: `docs/superpowers/plans/2026-10-05-github-actions.md` and the spec, if not already committed.

**Interfaces:**
- Consumes: everything above.
- Produces: a pull request whose `ci` check is green.

- [ ] **Step 1: Check the version rule**

Run:

```bash
git fetch --tags origin
```

then:

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output.  The repository has no release tags, so `VersionPrefix` 0.1.0 stands.  If a tag is printed and `VersionPrefix` is not greater, ask the user for the new version.

- [ ] **Step 2: Push and open the pull request**

Ask the user before pushing.  Then:

```bash
git push -u origin github-actions
```

```bash
gh pr create --base main --title "Add GitHub Actions CI and release workflows" --body "Adds build-time versioning, analyzer packaging, coverage reporting, the CI workflow and the trusted-publishing release workflow.

Spec: docs/superpowers/specs/2026-10-05-github-actions-design.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 3: Verify the CI run**

When the run finishes, confirm each of these in the run:

- The jobs `Build / Check, build, test and pack`, `Coverage comment` and `ci` all succeeded.
- The job summary shows the test results table and the coverage summary.
- The run has two artifacts, `packages` and `coverage`.
- The package is named for the pull request:

  ```bash
  gh run download <run-id> --name packages --dir artifacts/ci-packages
  ```

  Expected: one file, `SqlSource.0.1.0-pr-<pull request number>.<run number>.nupkg`.

- The pull request has one comment that starts with the coverage summary.

If a step fails on the runner but passed locally, fix it in a new commit on the branch.  Do not weaken a check to get the run green.

- [ ] **Step 4: Verify that a second run updates the comment**

Re-run the workflow, or push the next commit if a fix was needed:

```bash
gh run rerun <run-id>
```

When it finishes, run:

```bash
gh api repos/mbcrawfo/SqlSource/issues/<pull request number>/comments --jq '[.[] | select(.body | startswith("<!-- sqlsource-coverage -->"))] | length'
```

Expected: `1`.

- [ ] **Step 5: Reply to review findings**

Reply in the thread of every CodeRabbit finding, as the root `AGENTS.md` requires: what was changed, or why the finding does not apply.

- [ ] **Step 6: Hand the remaining checks to the user**

These need `main` or publish to nuget.org, so the user does them.  Report this list:

1. Add `ci` as a required status check in the `main` ruleset.  The check can be selected once this pull request's run has reported it.
2. After the merge, the `CI` run on `main` uploads `SqlSource.0.1.0-beta.<run>.nupkg` in its `packages` artifact.
3. First test of `publish.yml`: run it by hand on `main`.  It publishes `0.1.0-beta.<run>` to nuget.org, which cannot be undone, only unlisted.
4. The tag path is first exercised by the first release: `git tag v0.1.0` on `main`, then `git push origin v0.1.0`.
