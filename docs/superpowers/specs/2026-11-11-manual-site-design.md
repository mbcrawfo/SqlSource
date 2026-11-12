# The manual as a GitHub Pages site

## Intent

The user manual, today the pages of `docs/manual/`, becomes a website on GitHub Pages, built with docfx, so that a user reads the documentation of the release they installed rather than whatever `main` says.  Every link a user meets outside the repository (a build error's help link, the readmes on nuget.org, the issue template) points at the site.  The site shows the current release only.  A content revamp follows separately; this change alters the pages only where the move forces it.

## Goals

- The pages move from `docs/manual/` to `site/` at the repository root, with their docfx configuration beside them.
- docfx builds `site/` into a static site, published to `https://mbcrawfo.github.io/SqlSource/`.
- Publishing happens on its own after a stable release (a `v*` tag run of `publish.yml`), and on demand from the Actions tab or `gh workflow run`, building the branch the run is started on.
- The diagnostics' help links, the links of `README.md` and `src/SqlSource.Tool/README.md` into the manual, and the issue template point at the site.
- A pull request that breaks the site's build, such as a link docfx cannot resolve, fails CI and `pre-commit-validation.sh`.

## Non-goals

- No change to the content beyond what the move needs.
- No versioned docs: no copy per release and no version switcher.
- No custom domain.
- No API reference, and no other documents on the site: not `README.md`, `docs/architecture/`, `CONTRIBUTING.md` or the examples.
- No beta publishing: a manual run of `publish.yml` on `main` publishes the packages and not the site.

## Anti-goals

- A help link baked into a package that leads nowhere.  The address and the anchors must be ones the project never expects to change.
- Unreleased documentation published without someone asking for it.  Only a stable release or a manual run publishes.
- A toolchain other than .NET: no Node, Python or Ruby for contributors or CI.

## Constraints

- The repository is .NET only; its tools come from `dotnet-tools.json` through `dotnet tool restore`, pinned with `rollForward: false`.  SDK 10.0.401 (`global.json`).
- `publish.yml` creates the GitHub release with `github.token`, and GitHub starts no workflow from an event that token makes, so a workflow on `release: published` would never run.  The docs publish is called from `publish.yml`.
- `publish.yml`'s file name and its `nuget` environment are part of the nuget.org trusted-publishing policy; the new job must not rename or restructure the existing ones.
- Actions are pinned by commit SHA with the version in a comment, as in the existing workflows.
- The manual's rules (`docs/manual/AGENTS.md`, root `AGENTS.md` rule 4) and its tests (`tests/SqlSource.Repository.Tests/Documentation/`, `Diagnostics/DiagnosticsDocumentTests.cs`) stay in force, moved to the new paths.
- Every diagnostic's help link is `<base>#sqlsrcNNN`, the lower-case id, and the headings of `diagnostics.md` must keep producing that anchor.

## Approaches

| Approach | Trades off |
|----|----|
| **docfx (chosen)** | A .NET tool in `dotnet-tools.json`, so nothing new to install.  The modern template has search, dark mode and Mermaid.  It resolves relative `.md` links and passes `<a id>` through.  Plainer look; needs `docfx.json` and `toc.yml`, and `README.md` mapped to the site's root. |
| VitePress | The best look and speed, and relative links work as they are.  Brings Node, `package.json` and a lock file into a .NET repository. |
| MkDocs with Material | Polished, and takes `README.md` as the home page.  Brings Python, and Material has been in maintenance mode since late 2025. |
| Jekyll (GitHub Pages' built-in) | Ruby; anchors differ from GitHub's; needs a plugin for relative `.md` links.  Dropped early. |

The owner chose docfx: it fits the existing toolchain, and with a revamp coming, a plainer theme costs little now.

## Decisions made with the owner

- **Links point at the site.**  Help links, the two packed readmes and the issue template use the site's address, not `github.com/.../blob/main/docs/manual/`.
- **Current version only.**  Each publish replaces the whole site.
- **A manual publish builds the branch it runs on.**  A stable release builds its tag.
- **The manual moves to `site/`** in this change.
- **The published beta's links break, and that is accepted.**  `SqlSource 0.1.0-beta.1`, the only package on nuget.org, has help links and a readme pointing at `blob/main/docs/manual/`, which GitHub does not redirect after the move.  No stub is kept at `docs/manual/`.
- **The site goes live by one manual publish from `main` at merge**, before any release exists.  From then on a stable release publishes it.  A beta built between releases may link to an anchor, such as a new diagnostic's, that the site gains only at the next release; that gap is accepted, and betas do not publish the site.

## The design

### Layout

- `git mv docs/manual site`: the eleven pages, `README.md` and `AGENTS.md` keep their names.
- `site/docfx.json` and `site/toc.yml` are added.  The sidebar lists the pages in the order of "The pages" in `README.md`.
- `AGENTS.md` and the docfx files are excluded from the built content; only the user pages are published.
- Build output goes under `artifacts/`, which `.gitignore` already ignores (the builder picks the exact folder).
- `dotnet-tools.json` gains `docfx` 2.81.0, the current version, with `rollForward: false` like the others.

### Addresses

- Site root `https://mbcrawfo.github.io/SqlSource/` shows the entry page, `README.md`.
- A page is `https://mbcrawfo.github.io/SqlSource/<page>.html`, docfx's default; `diagnostics.md` becomes `.../diagnostics.html`, so the help link base is `https://mbcrawfo.github.io/SqlSource/diagnostics.html#`.
- The `README.md#what-works-today` link of each "Coming in a later release" callout in the packed readmes points at the entry page on the site, with the same anchor.
- Anchors on the site must equal those on GitHub for every anchor any link names: the `<a id>` anchors (passed through as HTML) and the `#sqlsrcNNN` headings of `diagnostics.md`.  The builder confirms docfx's heading ids for these, and the docfx build fails on a bookmark it cannot resolve (warnings as errors).

### Content changes the move forces

- Links from a page to something outside `site/` (`../../README.md`, the two example READMEs, `../../schemas/sidecar-v1.schema.json`) become absolute `https://github.com/mbcrawfo/SqlSource/blob/main/...` links, since those files are not on the site.
- Links between pages stay relative.
- Anything else docfx needs, such as front matter if it is needed, is added only where the build needs it.

### References to the old path

Every live reference to `docs/manual` changes, about 65 files.  `git grep docs/manual` finds them.

- **User-facing links outside the repository → the site URL:**
  - `SqlDiagnostics.HelpLinkBase`, `ToolDiagnostics`, `ToolDiagnostic`
  - the four `HelpLink`s of `src/SqlSource/build/SqlSource.targets`
  - `README.md` (46 links) and `src/SqlSource.Tool/README.md` (5)
  - `.github/ISSUE_TEMPLATE/bug-report.yml`
  - the tests that expect those URLs (`ToolDiagnosticsTests`, `WatchFailureTests`, `Expected.cs`, `RunLogDestinationTests`, and any other `git grep` finds)
- **Read inside the repository → the new path:**
  - The example READMEs keep relative links, now `../../site/...`.  They are read on GitHub beside `main`, and may show features the released site does not have yet.
  - Code comments in `examples/` and `tests/DatabaseTests/` say `site/<page>.md#...`.
  - Each `AGENTS.md` that names the path, `docs/architecture/`, `docs/tech-debt/`, `docs/deferred/`, `tests/README.md`, `docs/publishing.md` and `CONTRIBUTING.md`.
- **Left as they are:** `docs/superpowers/` specs and plans, which are history.

### Tests

- `UserDocs` reads the pages from `site/` instead of `docs/manual/`, still leaving out `AGENTS.md`.
- `UserDocsLinksTests`:
  - A link of a packed readme into the manual must be a site URL, and the page and anchor it names must exist in `site/`.
  - A link into the manual by its old GitHub address (`blob/main/docs/manual/`) fails.
  - A link from a page to a file outside `site/` must be absolute.
  - Other links of the packed readmes still check against `blob/main/` as today.
- `DiagnosticsDocumentTests` and the tests that check help links follow the new base.
- `AgentsFileTests`, `RepositoryRootTests`, `TestScopesTests` and `EditorRecipeTests` follow the new paths.

### Workflows

- **New `.github/workflows/docs.yml`**, triggered by `workflow_dispatch` and `workflow_call`.
  - **`build` job:** checks out the run's ref, sets up .NET, runs `dotnet tool restore` and docfx with warnings as errors, and uploads the output with `actions/upload-pages-artifact`.
  - **`deploy` job:** runs in environment `github-pages`, with `pages: write` and `id-token: write`, and calls `actions/deploy-pages`.
  - A `concurrency` group for Pages, so two publishes do not race.
- **`publish.yml`** gains a job after `release`:
  - Condition `if: github.ref_type == 'tag'`, calling `./.github/workflows/docs.yml` with the permissions it needs.
  - The existing jobs are untouched.
  - A failed docs publish leaves the packages and release in place, and is re-run by hand from the tag.
- **CI checks the site builds.** A docfx build with warnings as errors runs in `build.yml`, in the `Lint` job or a job of its own (builder's call).
- **`pre-commit-validation.sh` runs the same build** when a path under `site/` or `dotnet-tools.json` changes, and on `--full`.
  - It currently treats an unknown file kind as a reason to run everything; `toc.yml` and `docfx.json` must be known kinds.
  - `tools/test-scopes.txt` gets `none site/*` if `docs/*` has an equivalent line, so a manual change does not run the container tests.

### Configuration outside the repository

To be recorded in `docs/publishing.md`'s external configuration table:

- **Repository Settings → Pages:** Source "GitHub Actions".
- **Environment `github-pages`:**
  - Deployment branches and tags: branch `main` and tags `v*`.
  - A run started on any other branch builds the site and fails at deploy.
  - This is my call; the owner said a manual publish builds its branch, and in practice that is `main`.
- **Optional:** the repository's website field set to the site.

### Rollout

1. Before the merge, the owner turns on Pages with the source "GitHub Actions" and sets the `github-pages` environment's rules.
2. After the merge, someone runs `gh workflow run docs.yml --ref main` once, and checks that the site's root, a page, and a help link such as `diagnostics.html#sqlsrc024` resolve.
3. `docs/publishing.md` documents the manual run for the first publish and for re-publishing after a fix, not as the way to keep the site current between releases.

### Documentation to update

- **Root `AGENTS.md` rule 4:**
  - `docs/manual/` becomes `site/`.
  - The packed-readme rule about GitHub slugs is restated for site links.
  - The rule's list of documents gains the site's build.
- **`site/AGENTS.md`** (moved): its paths, its grep commands and the "Links" convention for links leaving `site/`.
- **`CONTRIBUTING.md`:**
  - docfx among the tools.
  - The command to build and preview the site locally (`dotnet docfx site/docfx.json --serve`, or what the builder settles on).
  - One line on `site/`.
- **`.github/workflows/README.md`:** a row for `docs.yml`, and `publish.yml`'s row and the release text.
- **`docs/publishing.md`:**
  - Releasing step 6 also publishes the site.
  - A "Docs" section on the manual run.
  - The external configuration rows.
  - Release step 3's `git grep` paths.
- **`tests/README.md` and `tools/README.md`** where they name the path or the checks.
- **`README.md`:** a link to the site where it introduces the manual.

## Left to the builder

- How `README.md` becomes the site's root:
  - docfx file mapping, a redirecting `index.md`, or another way.
  - Either way, `/` must show the entry page and `README.html#what-works-today` (or whatever address the callouts use) must resolve.
- docfx's template and its options (the modern template is expected), the site title, and whether to show a footer with a link to the repository.
- Whether the CI site build is its own job or part of `Lint`.
- The exact output folder under `artifacts/`.
- Confirming docfx 2.81.0 runs on SDK 10 (adding `rollForward` only if it must), and that the ```` ```mermaid ```` block and the `sql postgres`-style info strings render.
- The order of the commits.
