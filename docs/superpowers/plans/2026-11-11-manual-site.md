# The manual as a GitHub Pages site — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `docs/manual/` to `site/`, build it with docfx into a GitHub Pages site at `https://mbcrawfo.github.io/SqlSource/`, and point every user-facing link at it.

**Architecture:**
- `site/` holds the pages, with `docfx.json`, `toc.yml` and a redirecting `index.md` beside them.
- `tools/build-site.sh` builds the site into `artifacts/site` with warnings as errors, and fails if a guidance file was published.
- The script runs in `pre-commit-validation.sh`, in a `site` job of `build.yml`, and in a new `docs.yml`, which deploys to Pages.
- `docs.yml` runs by hand, and `publish.yml` calls it after a `v*` tag's release.

**Tech Stack:** docfx 2.81.0 (local dotnet tool), GitHub Actions (`actions/upload-pages-artifact`, `actions/deploy-pages`), xUnit + Shouldly tests in `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-11-manual-site-design.md`

## Global Constraints

- **Site address:** `https://mbcrawfo.github.io/SqlSource/`.
- **Page address:** `https://mbcrawfo.github.io/SqlSource/<page>.html`, and the help link base is `https://mbcrawfo.github.io/SqlSource/diagnostics.html#`.
- **Links into the site always name a page.** A link with an anchor must use `<page>.html#anchor`, never the bare root with an anchor: the root is a meta-refresh redirect to `README.html` and drops the anchor.
- **docfx:** version `2.81.0` in `dotnet-tools.json`, `rollForward: false`.  It ships a `net10.0` build, so SDK 10.0.401 runs it unchanged.
- **docfx excludes `**/AGENTS.md` and `**/CLAUDE*.md`.**
- **Build output:** `artifacts/site/`, which `.gitignore`'s `artifacts/` already covers.
- **Links between pages of `site/` stay relative.**  A page's link to a file outside `site/` is `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`.
- **Actions are pinned by commit SHA with `# vX.Y.Z`**, as in the existing workflows.
- **Never edit or rename** `publish.yml`'s existing jobs, its file name or its `nuget` environment.
- **Leave `docs/superpowers/` as it is.**  Every other `docs/manual` reference changes (`git grep -n docs/manual -- ':!docs/superpowers'` must print nothing at the end).
- **Every commit passes `./pre-commit-validation.sh`** (root AGENTS.md "Commits").  An AGENTS.md that names a moved path changes in the same commit as the move.
- **Commit messages** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

### What the trial run showed

docfx 2.81.0 was run once on a scratch copy of the manual:

- **Speed and exit code:** it builds in about 2 s, and exits 255 under `--warningsAsErrors` when there is any warning.
- **Heading ids** match GitHub's slugs: `diagnostics.html` has `id="sqlsrc024"`.
- **Relative `.md` links** become `.html` links, such as `README.html#what-works-today`.
- **Broken links are caught:**
  - It reports `InvalidBookmark` for a missing anchor, both in the same page and in another page.
  - It reports `InvalidFileLink` for the four links that leave the folder: `README.md`'s links to `../../README.md` and the two example READMEs, and `tool.md`'s link to `../../schemas/sidecar-v1.schema.json`.
- **The site root:** it writes `README.html`, not `index.html`.  An `index.md` holding only the front matter `redirect_url: README.html` becomes a meta-refresh `index.html`.
- **Fences:** `sql postgres` renders as `lang-sql`, and the mermaid block is kept.

## Review Focus

1. **A packed readme linking the site root with an anchor** (`…/SqlSource/#what-works-today`) loses the anchor on redirect.  A reader expects to land on the section, so the link test rejects it.  Owner: Task 1.
2. **A page added to `site/` and left out of `toc.yml`** goes live with no sidebar entry.  A reader expects to find every page in the sidebar.  Owner: Task 3.
3. **An `AGENTS.md` or `CLAUDE.md` added in a subfolder of `site/`.**  Nobody expects agent guidance on the public site.  Owner: Task 3, by the output check of `tools/build-site.sh`.
4. **A manual page linking a repository file relatively** (`../../examples/...`): docfx fails late, in CI.  A writer expects the fast tests to say so.  Owner: Task 2.
5. **A packed readme linking into `site/` by its GitHub URL** (`blob/main/site/x.md`) shows `main`'s docs, not the release's.  A user expects the release's.  Owner: Task 2.

---

### Task 1: Read site URLs in the link tests

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocs.cs`
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`
- Test: `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs`

**Interfaces:**
- Produces:
  - `UserDocs.SiteUrl` (`public const string = "https://mbcrawfo.github.io/SqlSource/"`)
  - `UserDocs.Site` (`internal const string = "site"`, the folder)
  - `bool UserDocs.IsSiteLink(string target)`
  - `string? UserDocs.SiteLinkProblem(string target)`
  - `UserDocs.Resolve` maps a site link to `site/<page>.md`.

`Site` is the folder the pages move to in Task 2.  Until then nothing links to the site, so the new code is exercised only by its own unit tests.

- [ ] **Step 1: Write the failing tests** in `UserDocsReadingTests`:

```csharp
[Theory]
[InlineData("https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc024", "site/diagnostics.md", "sqlsrc024")]
[InlineData("https://mbcrawfo.github.io/SqlSource/README.html#what-works-today", "site/README.md", "what-works-today")]
[InlineData("https://mbcrawfo.github.io/SqlSource/", "site/README.md", "")]
public void Resolve_SiteUrl_IsThePageOfSite(string target, string path, string anchor) =>
    UserDocs.Resolve("README.md", target).ShouldBe((path, anchor));

[Theory]
[InlineData("https://mbcrawfo.github.io/SqlSource/#what-works-today")]
[InlineData("https://mbcrawfo.github.io/SqlSource/tool")]
[InlineData("https://mbcrawfo.github.io/SqlSource/tool.md")]
[InlineData("https://mbcrawfo.github.io/SqlSource/sub/tool.html")]
public void SiteLinkProblem_NotAPageOfTheSite_IsReported(string target) =>
    UserDocs.SiteLinkProblem(target).ShouldNotBeNull();

[Theory]
[InlineData("https://mbcrawfo.github.io/SqlSource/")]
[InlineData("https://mbcrawfo.github.io/SqlSource/tool.html#exit-codes")]
[InlineData("https://github.com/mbcrawfo/SqlSource/blob/main/README.md")]
public void SiteLinkProblem_APageOrAnotherSite_IsNone(string target) =>
    UserDocs.SiteLinkProblem(target).ShouldBeNull();
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-method "*Site*"`

Expected: a build failure, because `SiteLinkProblem` is not defined.

- [ ] **Step 3: Implement**
  - In `UserDocs.cs`, add `SiteUrl`, `Site`, `IsSiteLink` (the target starts with `SiteUrl`, ordinal and case-insensitive) and `SiteLinkProblem`.
  - `SiteLinkProblem` returns null for any link that is not a site link, and for the bare root with no anchor.  Otherwise it returns a message unless the path after `SiteUrl` is a single segment `<name>.html`.
  - In `Resolve`, a site link goes before the scheme check.  The bare root gives `("site/README.md", anchor)`, and `<name>.html` gives `($"{Site}/{name}.md", anchor)`.
  - Change `Follow` in `UserDocsLinksTests` to return `SiteLinkProblem(target)` first when it is not null.
  - Add the fact `SiteUrls_OfTheUserDocs_ReachAPageAndAnchorThatExist` there, the twin of `RepositoryUrls_…`, filtered on `UserDocs.IsSiteLink`.  Exclude site links from `RelativeLinks_…`.

- [ ] **Step 4: Run the project's tests**

Run: `dotnet test --project tests/SqlSource.Repository.Tests`

Expected: PASS.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh` (expect it to pass), then commit `test: follow links to the manual's site`.

---

### Task 2: Move the manual to `site/` and point every link at it

One commit: half a move breaks the link tests.

**Files:**
- Move: `docs/manual/` → `site/` (`git mv docs/manual site`)
- Modify, user-facing URLs to `UserDocs.SiteUrl` + `<page>.html#anchor`:
  - `README.md`, `src/SqlSource.Tool/README.md`
  - `.github/ISSUE_TEMPLATE/bug-report.yml`
  - `src/SqlSource/Diagnostics/SqlDiagnostics.cs:20` (`HelpLinkBase`)
  - `src/SqlSource/build/SqlSource.targets:505,751,758,765`
  - root `AGENTS.md:17` (the callout's link becomes `https://mbcrawfo.github.io/SqlSource/README.html#what-works-today`)
- Modify, tests that expect the URL:
  - `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs:66`
  - `tests/SqlSource.Tool.Tests/Commands/Watch/WatchFailureTests.cs:49,74`
  - `tests/SqlSource.Tool.Tests/Expected.cs:9`
  - `tests/SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs:224`
- Modify, repository paths to `site/`.  Every remaining `git grep -n docs/manual -- ':!docs/superpowers'` hit, including:
  - the `Manual` constants of `UserDocs.cs` and `UserDocsLinksTests.cs`
  - `RepositoryRoot.Path("docs", "manual", …)` in `DiagnosticsDocumentTests.cs:27`, `EditorRecipeTests.cs:140` and `RepositoryRootTests.cs:101-102`
  - the sample paths of `UserDocsReadingTests.cs:132-137`, `RepositoryRootTests.cs:54,75` and `TestScopesTests.cs:30`
  - code comments in `src/`, `examples/` and `tests/DatabaseTests/`
  - the example READMEs' relative links (`../../docs/manual/` → `../../site/`)
  - every `AGENTS.md`, `docs/architecture/`, `docs/tech-debt/`, `docs/deferred/`, `docs/publishing.md:28`, `tests/README.md`, `CONTRIBUTING.md`
- Modify, the four links leaving `site/` to `https://github.com/mbcrawfo/SqlSource/blob/main/…`:
  - `site/README.md:3` and `site/README.md:55` (×2)
  - `site/tool.md:168`
  - Check `git grep -n '](\.\./' -- site` for any others.
- Test: `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`

**Interfaces:**
- Consumes: `UserDocs.SiteUrl`, `UserDocs.Site`, `UserDocs.IsSiteLink` from Task 1.
- Produces: `SqlDiagnostics.HelpLinkBase == "https://mbcrawfo.github.io/SqlSource/diagnostics.html#"`.

- [ ] **Step 1: Write the failing tests** in `UserDocsLinksTests`:

```csharp
// A packed readme is read on nuget.org beside a release, so it sends the reader to the release's site, not to main.
[Fact]
public void Links_OfThePackedReadmes_ReachTheManualOnlyBySite() =>
    UserDocs.Links()
        .Where(link => PackedReadmes.Contains(link.File) && UserDocs.IsRepositoryLink(link.Target))
        .Where(link => UserDocs.Resolve(link.File, link.Target) is { } r
            && (r.Path + "/").StartsWith(UserDocs.Site + "/", StringComparison.Ordinal))
        .Select(link => $"{link.File}:{link.Line}: {link.Target}")
        .ShouldBeEmpty();

// docfx publishes site/ alone, so a relative link out of it is broken on the site.
[Fact]
public void RelativeLinks_OfTheSite_StayInsideIt() =>
    UserDocs.Links()
        .Where(link => link.File.StartsWith(UserDocs.Site + "/", StringComparison.Ordinal))
        .Where(link => !UserDocs.IsRepositoryLink(link.Target) && !UserDocs.IsSiteLink(link.Target))
        .Where(link => UserDocs.Resolve(link.File, link.Target) is { } r
            && !r.Path.StartsWith(UserDocs.Site + "/", StringComparison.Ordinal))
        .Select(link => $"{link.File}:{link.Line}: {link.Target}")
        .ShouldBeEmpty();
```

- [ ] **Step 2: Move the folder**
  - `git mv docs/manual site`
  - Change the two `Manual` constants and the `RepositoryRoot.Path("docs", "manual", …)` calls.
  - Run `dotnet test --project tests/SqlSource.Repository.Tests`.  Expected: FAIL.
    - `RelativeLinks_OfTheSite_StayInsideIt` names the four outbound links.
    - `RepositoryUrls_…` names every `blob/main/docs/manual/` link: "no file or folder".

- [ ] **Step 3: Rewrite the links and paths**
  - Rewrite every file listed above.
  - A readme link `blob/main/docs/manual/<page>.md#a` becomes `https://mbcrawfo.github.io/SqlSource/<page>.html#a`.
  - `…/docs/manual/README.md` with no anchor becomes `https://mbcrawfo.github.io/SqlSource/`.
  - Keep each anchor as it is.

- [ ] **Step 4: Run the tests that the URLs and paths reach**

Run: `dotnet test --solution SqlSource.slnx` (Docker running).

Expected: PASS.  `BuildFileTests.Online` ties the targets' `HelpLink`s to `HelpLinkUri`, and `DiagnosticsDocumentTests` ties each id to a section.

- [ ] **Step 5: Check nothing is left**

Run: `git grep -n "docs/manual" -- ':!docs/superpowers'`

Expected: no output.

- [ ] **Step 6: Restate the documentation rules for the new home**
  - In root `AGENTS.md` rule 4, `docs/manual/` becomes `site/`.
  - The packed-readme sub-rule ("their headings are linked by GitHub's slug") becomes: they link the manual by its site URL, `https://mbcrawfo.github.io/SqlSource/<page>.html#<anchor>`, and the anchor is the heading's GitHub slug or its `<a id>`.
  - In `site/AGENTS.md`, change the paths and the two grep commands, and extend "**Links.**" with: a link to a file outside `site/` is an absolute `blob/main/` URL.
  - In `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, name `site/`, and say the packed readmes link the manual by site URL.
  - Root `AGENTS.md` must stay ≤ 16 KB and each bullet ≤ 80 words (`AgentsFileTests`).

- [ ] **Step 7: Validate and commit**

Run `./pre-commit-validation.sh` (expect it to pass), then commit `docs: move the manual to site/ and link it at its site`.

---

### Task 3: Build the site with docfx, locally and in CI

**Files:**
- Modify: `dotnet-tools.json` (add `docfx` `2.81.0`, command `docfx`, `rollForward: false`)
- Create:
  - `site/docfx.json`
  - `site/toc.yml`
  - `site/index.md`
  - `tools/build-site.sh` (executable)
- Modify:
  - `pre-commit-validation.sh`
  - `tools/test-scopes.txt`
  - `.github/workflows/build.yml`
  - `tests/SqlSource.Repository.Tests/Documentation/UserDocs.cs`
  - `CONTRIBUTING.md`, `tools/README.md`, `.github/workflows/README.md`
  - root `AGENTS.md` rule 4 (`site/` "builds with docfx; `tools/build-site.sh`")
- Test: `tests/SqlSource.Repository.Tests/Documentation/SiteTocTests.cs`

**Interfaces:**
- Consumes: `UserDocs.Site`.
- Produces:
  - `tools/build-site.sh`: no arguments, builds into `artifacts/site`, exits non-zero on any docfx warning or on a guidance file in the output.
  - `UserDocs.SitePages`: `IReadOnlyList<string>`, the user pages of `site/`, without `AGENTS.md` or `index.md`.

**Settled values:**
- **`docfx.json`:**
  - content `files: ["*.md", "toc.yml"]`, `exclude: ["**/AGENTS.md", "**/CLAUDE*.md"]`
  - `output: "../artifacts/site"`, `template: ["default", "modern"]`
  - `globalMetadata`: `_appTitle: "SqlSource"`, `_enableSearch: true`
- **`toc.yml`** has one `- name: … / href: <page>.md` per page, in the order of "The pages" in `site/README.md`.  The entry page `README.md` comes first, as "Manual".
- **`index.md`** is exactly:

  ```
  ---
  redirect_url: README.html
  ---
  ```

  `UserDocs.Files` must leave it out: its `---` reads as a setext heading.

- [ ] **Step 1: Write the failing test** `SiteTocTests`:

```csharp
// docfx publishes a page that toc.yml leaves out, with no warning and no entry in the sidebar.
[Fact]
public void Toc_ListsEveryPageOnce_AndNothingElse()
{
    var hrefs = File.ReadAllLines(RepositoryRoot.Path(UserDocs.Site, "toc.yml"))
        .Select(line => HrefPattern().Match(line))
        .Where(match => match.Success)
        .Select(match => $"{UserDocs.Site}/{match.Groups[1].Value}")
        .ToList();
    hrefs.ShouldBeUnique();
    hrefs.Order(StringComparer.Ordinal).ShouldBe(UserDocs.SitePages.Order(StringComparer.Ordinal));
}
// HrefPattern: ^\s*-?\s*href:\s*(\S+)\s*$
```

- [ ] **Step 2: Run it and see it fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class "*SiteTocTests"`

Expected: FAIL, because `toc.yml` is missing.

- [ ] **Step 3: Add the config and the tool**
  - Add `docfx` to `dotnet-tools.json`, then run `dotnet tool restore`.
  - Write `docfx.json`, `toc.yml` and `index.md`.
  - Add `SitePages`, and leave `index.md` out of `Files`.
  - Write `tools/build-site.sh` in the style of the other `tools/*.sh`: `set -euo pipefail`, run from the repository root.
    - It runs `dotnet docfx site/docfx.json --warningsAsErrors`.
    - Then it fails with a message naming each file when `find artifacts/site -iname 'agents.html' -o -iname 'claude*.html'` finds any.

- [ ] **Step 4: Check the build and the guard**
  - Run `tools/build-site.sh`.  Expected: exit 0, `artifacts/site/README.html`, `artifacts/site/index.html`, `artifacts/site/diagnostics.html` with `id="sqlsrc024"`, and no `AGENTS.html`.
  - Then prove the guard and drop the change:
    - Temporarily remove `**/AGENTS.md` from the exclude, rerun, and see it fail naming `AGENTS.html`.  Restore the exclude.
    - Temporarily add `[x](generator.md#nope)` to a page, rerun, and see it fail with `InvalidBookmark`.  Remove the link.
  - Run the toc test again.  Expected: PASS.

- [ ] **Step 5: Wire it into the validation script**

In `pre-commit-validation.sh`:
- `is_known_kind` accepts `site/*.yml`.
- A new trigger `site_trigger` is set by a path under `site/`, by `dotnet-tools.json` and by `tools/build-site.sh`.
- Add `decide site "$site_trigger" 'nothing under site/ changed'`.
- Add `step 'site' site tools/build-site.sh || true` after `actionlint`.

In `tools/test-scopes.txt`, add `none site/*` beside `none docs/*`.

Check: `./pre-commit-validation.sh --plan site/tool.md` shows `run site`, and both container pairs skipped.  `--plan src/SqlSource/x.cs` shows `skip site`.

- [ ] **Step 6: Run it in CI**

In `build.yml`, add a job `site`, named "Site", in the style of `format-style`:
- checkout, then `setup-dotnet` with `global-json-file`, `cache: true`, and `cache-dependency-path: dotnet-tools.json`
- `dotnet tool restore`
- `tools/build-site.sh`
- `timeout-minutes: 10`

Update the file's header comment, which names the steps that mirror the script.

Run `tools/actionlint.sh`.  Expected: no findings.

- [ ] **Step 7: Document**
  - `CONTRIBUTING.md`: docfx among the tools (restored by `dotnet tool restore`), one sentence on `site/`, and the preview command `dotnet docfx site/docfx.json --serve`.
  - `tools/README.md`: `build-site.sh`.
  - `.github/workflows/README.md`: `build.yml`'s row says eight jobs, with `Site`.

- [ ] **Step 8: Validate and commit**

Run `./pre-commit-validation.sh` (expect it to pass), then commit `build: build the manual's site with docfx`.

---

### Task 4: Publish the site to GitHub Pages

**Files:**
- Create: `.github/workflows/docs.yml`
- Modify:
  - `.github/workflows/publish.yml` (new job only)
  - `.github/workflows/README.md`
  - `docs/publishing.md`

**Interfaces:**
- Consumes: `tools/build-site.sh` and the output folder `artifacts/site` from Task 3.

**Settled values for `docs.yml`:**
- **Header comment:** publishes the manual to GitHub Pages, run by hand on the branch to build or called by `publish.yml` for a `v*` tag.  The `github-pages` environment allows `main` and `v*` alone.
- **Triggers:** `on: workflow_dispatch` and `workflow_call`.  `permissions: contents: read` at the top.
- **Concurrency:** `concurrency: { group: pages, cancel-in-progress: false }`.
- **Job `build`, "Build the site":**
  - checkout with `persist-credentials: false`, setup-dotnet as in Task 3, and `dotnet tool restore`
  - `tools/build-site.sh`
  - `actions/upload-pages-artifact` with `path: artifacts/site`
- **Job `deploy`, "Deploy to GitHub Pages":**
  - `needs: build`, `environment: { name: github-pages, url: ${{ steps.deployment.outputs.page_url }} }`
  - `permissions: { pages: write, id-token: write }`
  - `actions/deploy-pages` with `id: deployment`
- **Pinning:** use each action's latest release, pinned by SHA: `gh api repos/actions/upload-pages-artifact/releases/latest --jq .tag_name`, then `gh api repos/actions/upload-pages-artifact/commits/<tag> --jq .sha`, and the same for `deploy-pages`.

**Settled values for `publish.yml`:**
- A job `docs`, "Publish the site", with `needs: release` and `if: ${{ github.ref_type == 'tag' }}`.
- It calls `uses: ./.github/workflows/docs.yml` with `permissions: { contents: read, pages: write, id-token: write }`.
- Extend the header comment: after a tag's release, the manual's site is published.

- [ ] **Step 1: Write `docs.yml` and the `publish.yml` job**

- [ ] **Step 2: Lint**

Run: `tools/actionlint.sh`

Expected: no findings.

- [ ] **Step 3: Document**
  - `.github/workflows/README.md`:
    - a row for `docs.yml`: manual runs and calls from `publish.yml`; builds `site/` and deploys it to Pages
    - `publish.yml`'s row: "…, creates a GitHub release and publishes the site for a tag"
    - a sentence that a manual run from a branch other than `main` builds but fails at deploy
  - `docs/publishing.md`:
    - release step 6 also publishes the site
    - a "### Site" section: `gh workflow run docs.yml --ref main` for the first publish and to publish again after a fix, never as the way to keep the site current between releases
    - external configuration rows:
      - Pages source "GitHub Actions"
      - environment `github-pages`, deployment branches and tags: branch `main`, tags `v*`

- [ ] **Step 4: Validate and commit**

Run `./pre-commit-validation.sh` (expect it to pass), then commit `ci: publish the manual to GitHub Pages`.

---

### Task 5: Finish the branch

- [ ] **Step 1:** Run `./pre-commit-validation.sh --full`.  Expected: every step passed.
- [ ] **Step 2:** Run the `branch-review:review` skill, address its findings, and repeat until it is clean (root AGENTS.md "Pull requests" 2).
- [ ] **Step 3:** Run `git fetch --tags origin && git tag --list 'v*' --sort=-v:refname | head -n 1`.  With no tag, there is nothing to compare.  Otherwise compare with `VersionPrefix` and ask the owner if it is not greater.
- [ ] **Step 4:** Offer the pull request.  Its description lists the owner's steps:
  - before merging, set Pages' source to "GitHub Actions" and add tag `v*` to `github-pages`
  - after merging, run `gh workflow run docs.yml --ref main` and open `https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc024`
  - say plainly that `SqlSource 0.1.0-beta.1`'s help links and readme links break, as accepted
