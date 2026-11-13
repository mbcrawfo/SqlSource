# Docs overhaul, phase 11: readmes

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both win over this brief.
Done by: an Opus agent.  Needs: phases 0 to 10.  Runs alone.

## Goal

Cut `README.md` and `src/SqlSource.Tool/README.md` to the outlines of the map's section 7.  Each becomes a brief introduction that links to the site, which now owns getting started.  Land `ReadmeTests`, the last restatement of `SupportedVersionsTests`, and the final words of the rules that say what a readme holds.  About 250 lines leave the two files, and no fact leaves the documentation.

## Where things stand

After phase 10:

- The site has its 70 pages.  `site/start/` holds the nine pages of Get started, and `site/index.md` is the finished landing page.
- `README.md` is still the long file.  By the lines of the baseline (`git show 28e20e3c:README.md`) it holds:

| Lines | What | State |
|----|----|----|
| 1-9 | Title, badges, the paragraph, "Databases" | Line 7, the status paragraph, went in phase 1 |
| 11-29 | Installation, the two bullets, "Supported environments" | Line 13 has the neutral sentence since phase 1.  Phase 8 placed copies of lines 9 and 19-29 on reference pages, and corrected the last sentence of line 26 (C-07) |
| 31-130 | The five steps | Whole.  Phase 10 restated or placed each unit on a `start/` page or on `index.md` |
| 132-288 | The two scenarios, the FAQ, the libraries, custom type mapping | Each heading holds one sentence that points to its page of the site.  Phase 8 moved the text |
| 290-305 | "Where to next", the last line | Whole, with its links retargeted |

- `src/SqlSource.Tool/README.md` is the baseline's file, with the neutral sentence of phase 1 at line 7 and its links retargeted by phases 3 to 8.
- Every link of both files names a page that exists, in the form `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>`.
- `SupportedVersionsTests` holds the table of `site/reference/databases/index.md` and the restatements of `site/index.md` and `site/start/install.md`.
- The epic's ledger has rows for `README.md` in `ledger/phase-01.tsv`, `ledger/phase-08.tsv` and `ledger/phase-10.tsv`.

## In scope / Not in scope

In scope:

- `README.md` and `src/SqlSource.Tool/README.md`.
- `tests/SqlSource.Repository.Tests/Documentation/ReadmeTests.cs`, new, and `SupportedVersionsTests`.
- Root `AGENTS.md` rule 4; `site/AGENTS.md`; `site/start/AGENTS.md`; `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`; `tests/README.md`; `docs/publishing.md`.
- `ledger/phase-11.tsv` in the epic folder.

Not in scope:

- Any page of `site/`.  If a fact that leaves a readme is on no page, stop: that is a fault of phase 8 or 10.  Put the fact on the page the map names, as a move, word for word, and say so in the pull request.
- `src/SqlSource/SqlSource.csproj` and `src/SqlSource.Tool/SqlSource.Tool.csproj`.  How the readmes are packed does not change.
- The two example readmes.  They are not packed, and what they say of `README.md` stays true.
- `src/SqlSource.Tool/AGENTS.md`.  Its bullet on the tool's readme stays true, and the file has almost no room.

## The work

How a phase is done, the ledger, the reviews and publishing are in the outline's "Delivery".  The map's section 7 gives both outlines, and the maps of `README.md` and of `src/SqlSource.Tool/README.md` in section 4 say where each unit of today goes.  As in the map, `S/` is `site/start/`, `G/` is `site/guides/`, `R/` is `site/reference/` and `D/` is `site/reference/databases/`.  Below, a link is written without its base, `https://mbcrawfo.github.io/SqlSource/`.

### 1. Check that each fact that leaves is on the site

Do this before any cut.

1. List the baseline units of both readmes from `inventory.tsv`.
2. For each unit, find its row in `ledger/phase-01.tsv`, `ledger/phase-08.tsv` or `ledger/phase-10.tsv`.  A unit with no row there is this phase's: give it a row from the map (see "The ledger").
3. For each row that names a place on the site, open that place and read the fact there.  For a `restated-from` row, the place is the owner's section.
4. Read these with care, since nothing but their owner keeps them: sentence 7 of `README.md` 101 (`R/query-methods.md#method-parameters`); sentences 5 and 10 of line 126 (`R/connections.md#connections`, `R/msbuild.md#included-files`); sentence 5 of line 29 (`R/query-methods.md#limit-wrapped-connection`); the two bullets of lines 26 and 27 (`R/attributes.md#what-the-package-adds`, `R/cli.md#install-the-tool`); lines 22, 31 and 40-48 of the tool readme (`R/connections.md#env-file`, `R/settings.md#setting-database`, `R/bundles.md#diagnose`, `#replay`).

### 2. `README.md`

About 100 lines.  Six headings and no other: `# SqlSource`, `## Supported databases`, `## Install`, `## Use`, `## Other ways to use it`, `## Documentation`.

| # | Section | Holds | Links |
|----|----|----|----|
| 1 | `# SqlSource` | The badges of line 3, unchanged, with `nuget/vpre`.  One paragraph of four sentences, from line 5: what SqlSource does; why SQL in a file of its own is better; what the compiler then catches; what the tool adds with PostgreSQL or SQL Server | The site's root, with no anchor |
| 2 | (no heading) | One sample pair: the `.sql` file with one query and the C# that calls it.  It is the pair of `site/index.md`, byte for byte: the query `GetUser` of `examples/PostgresExamples/Repository/UserRepository.sql` without its line `-- generator: method-type=both`, and the call that the accuracy reviewer of phase 10 compiled.  The info strings are `sql postgres` and `csharp` | `start/first-query.html` |
| 3 | `## Supported databases` | Three lines.  PostgreSQL 14 or later and SQL Server 2017 or later get typed models and methods.  Azure SQL is accepted, and not tested.  Any other database gets its SQL as constants | `reference/databases/index.html#supported-versions`; `guides/other-databases.html` |
| 4 | `## Install` | The command that adds the package.  The two tool commands.  The sentence "The packages are published as previews, so the commands include `--prerelease`."  One line: the project targets .NET 8 or later, and builds with the .NET SDK 9.0.300 or later | `start/install.html`; `reference/msbuild.html#build-requirements` |
| 5 | `## Use` | Five numbered steps, each one or two lines, then one line for the full walkthrough | Below |
| 6 | `## Other ways to use it` | Three bullets.  Your own connection, transaction or timeout: the output `methods`.  Dapper or your own ADO.NET code: the output `models`.  Only the SQL, on any database: the output `sql` | `start/transactions.html`; `guides/dapper.html`; `guides/other-databases.html` |
| 7 | `## Documentation` | Five lines: Get started, Guides, Reference, Errors, the two example projects | `start/install.html`; `guides/lists.html`; `reference/sql-files.html`; `diagnostics.html`; `https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md` and `.../examples/SqlServerExamples/README.md` |
| 8 | (last line) | Line 305 as today: `CONTRIBUTING.md` and the licence | As today |

The five steps of `## Use`:

| Step | Says | Link |
|----|----|----|
| 1 | Write a `.sql` file with a query | `start/first-query.html#write-a-query` |
| 2 | Mark a partial class with `[SqlSourceGenerate]` | `start/first-query.html#mark-a-partial-class` |
| 3 | Name your database in `sqlsource.json`: `"dialect": "postgres"` or `"dialect": "mssql"` | `start/first-query.html#name-your-database` |
| 4 | Put the connection in a `.env` file in the folder of the project, and build.  The build describes the query and writes a sidecar file, which you commit | `start/first-query.html#connection`, `start/first-query.html#describe` |
| 5 | Call the method | `start/first-query.html#call-the-methods` |
| Then | "The full walkthrough" | `start/first-query.html` |

Rules for this file:

- **It states nothing that the site does not.**  Each sentence restates a `start/` page or `site/index.md`, in the same words where it can.
- **It follows the outline's language rule.**  No later phase rewrites the readmes, so this phase writes them in plain language.
- **The steps are in the order of `start/first-query.md`**: describe, then call.  Today's README has the call before the describe.
- **A step holds no fenced block.**  The sample pair above the headings is the only code of the walkthrough.  A fenced block inside a list item is not drawn the same way on GitHub and on nuget.org.
- **It holds no link to a heading of its own.**  The six links of today to `README.md#...` go with their sentences.
- **It states no driver version**, and never that Azure SQL is supported.
- **Step 3 does not say "choose"**, and names `postgres` and `mssql` alone.  The other dialects are behind `guides/other-databases.html`.

What leaves, and where the ledger says it is:

| Leaves | Lines | Is on |
|----|----|----|
| "Databases", sentences 3 to 5 | 9 | `## Other ways to use it`; `site/index.md` |
| The two bullets on the packages; "Supported environments" but its one line | 26-29 | `S/install.md#packages`, `#requirements`, and their owners |
| The text of the five steps | 31-130 | `S/first-query.md`, `S/how-it-works.md`, `S/write-queries.md`, `S/call-queries.md`, `S/testing.md`, `S/keep-up-to-date.md` |
| The headings and pointers of the two scenarios | 132-183 | `G/dapper.md`, `G/other-databases.md` |
| The FAQ heading, its ten questions and their pointers | 185-236 | The guides, `S/testing.md` and `S/transactions.md`, by the map's rows |
| "NodaTime and other libraries", "Custom type mapping" | 238-288 | `G/nodatime-and-spatial.md`, `G/json.md`, `G/custom-types.md` |
| "Where to next" | 290-303 | `## Documentation`; `site/index.md#find-what-you-need` |

### 3. `src/SqlSource.Tool/README.md`

About 35 lines.  Four headings and no other: `# SqlSource.Tool`, `## Install`, `## Use`, `## Documentation`.

| # | Section | Holds | Links |
|----|----|----|----|
| 1 | `# SqlSource.Tool` | Three sentences: what `sqlsource describe` does; that you commit the sidecar files it writes; that it describes PostgreSQL and SQL Server queries, with no version number | `https://github.com/mbcrawfo/SqlSource/blob/main/README.md`; `reference/describing.html#describe` |
| 2 | `## Install` | One sentence: a local tool, installed from the root of the repository, at the version of the `SqlSource` package.  The sentence about previews, in the README's words.  The two commands of lines 10 and 11 | `reference/cli.html#install-the-tool` |
| 3 | `## Use` | The `.env` line of line 19.  One sentence: the file is in the folder of the project, and the tool never reads a `.env` file in a folder above it.  The four `describe` commands of lines 25-28.  Four bullets, one line each.  One sentence: a build with a connection runs `describe` itself, and a build in CI never does.  One line for `diagnose` and `replay` | `reference/connections.html#env-file`; `reference/describing.html`; `guides/describe-on-save.html`; `reference/msbuild.html#running-in-the-build`; `guides/report-a-bug.html` |
| 4 | `## Documentation` | Five links: keep your queries up to date; the command reference; connections; report a wrong model; the SqlSource readme | `start/keep-up-to-date.html`; `reference/cli.html#command-reference`; `reference/connections.html`; `guides/report-a-bug.html`; `https://github.com/mbcrawfo/SqlSource/blob/main/README.md` |

What leaves: `--env-file`, the environment that wins and `--connection` (line 22); the sentence on the name `postgres` (line 31); the two commands of `diagnose` and `replay` and their two bullets (lines 40-48).  Each is behind a link of the table.

### 4. The rules of a packed readme that stay

Root rule 4 keeps them, and a test holds most of them.  Both files must still pass each one.

| Rule | Held by |
|----|----|
| Every link is an absolute URL | `UserDocsLinksTests`: the test of the packed readmes' links |
| The site is linked by its address, never by a page on GitHub | `UserDocsLinksTests`: the test that the packed readmes reach the manual only by the site |
| A site link names `<folder>/<page>.html`, or the site's root, which is the landing page | `UserDocsLinksTests`, which follows each site URL to the file of `site/` it is built from, and to its anchor |
| A link to a file of the repository is `https://github.com/mbcrawfo/SqlSource/blob/main/<path>` | `UserDocsLinksTests` |
| No raw HTML and no `<a id>` line; a heading is linked by GitHub's slug | A reviewer.  The grep of "Done when" |
| Install and update commands carry `--prerelease`; the badge is `nuget/vpre` | A reviewer; `ReadmeTests` for the commands, by way of `start/install.md` |
| A `sql` block's info string names its dialect, or says `fragment` or `error` | `UserDocsSamplesTests` |
| No internal is named | A reviewer.  The grep of `site/AGENTS.md` |
| No status phrase | `StatusLanguageTests` |

The names of the test methods are those that phase 0 left.

### 5. `ReadmeTests`

A new class in `tests/SqlSource.Repository.Tests/Documentation/ReadmeTests.cs`.  It reads the files through `UserDocs` and `RepositoryRoot.Path`, as the other tests of the folder do.  Its comments say what fails it.

**The size of each readme.**  The test fails when a readme is over its cap, in bytes, as `AgentsFileTests` counts an `AGENTS.md`.

| File | Baseline | Expected after the cut | Cap |
|----|----|----|----|
| `README.md` | 42,304 bytes | About 6,000 bytes | 8,000 bytes |
| `src/SqlSource.Tool/README.md` | 4,069 bytes | About 2,900 bytes | 4,000 bytes |

- Measure both files after the cut, with `wc -c`.
- A cap must be between 1.15 and 1.5 times its file.  If one is not, set it to 1.25 times the file, rounded up to the next 500 bytes, and give both numbers in the pull request.
- The message of a failure says what to do: move the text to the site and link it.  It does not say to raise the cap.

**The commands.**  The test fails when an install or update command of a readme is not on `site/start/install.md`.

- A command is taken from two places of a readme: each line of a fenced block whose info string is `console`, `bash`, `sh` or `shell`, and each inline code span.
- It is normalised: a leading `$ ` is taken off, the ends are trimmed, and each run of spaces is one space.
- It is an install or update command when it then starts with `dotnet add package `, `dotnet new tool-manifest`, `dotnet tool install `, `dotnet tool update ` or `dotnet tool restore`.
- It passes when a line of a fenced block of `site/start/install.md`, or an inline code span of that page, is the same after the same normalising.  A command inside a tab counts.
- A guard fails the test when `README.md` gives fewer than three such commands, or the tool readme fewer than two, so that a reader that finds nothing cannot pass.

Check that each test can fail: add `--version 1.0.0` to one command of `README.md`, see the command test fail, and take it out.  Lower a cap below its file, see the size test fail, and restore it.

### 6. The two minimum versions, and `SupportedVersionsTests`

`## Supported databases` of `README.md` restates the two minimum server versions of `site/reference/databases/index.md#supported-versions`, and links the table.  It states nothing more of the table.

- Write them as "PostgreSQL 14 or later" and "SQL Server 2017 or later", as `site/index.md` and `site/start/install.md` do.
- `SupportedVersionsTests` gains `README.md`, in the way phases 4 and 10 gave it the other two restatements.
- The tool readme states no version, so the test does not read it.
- Check that the test can fail: change 2017 to 2016 in `README.md`, see the test fail, and change it back.

### 7. The rules that say what a readme holds

**Root `AGENTS.md`, rule 4.**  Phases 1 and 3 changed other bullets of the rule.  This phase gives three bullets their final words:

- The bullet on a change to behaviour a user can see: "A change to behaviour a user can see updates the page of the site that documents it, and `README.md` too when it changes a command, a step or a database that the README names."
- The bullet that starts "`README.md` is for the average user": "`README.md` is a brief introduction for a reader who arrives through the repository or nuget.org, and the site owns getting started.  It holds what SqlSource does and why, with one sample pair, the supported databases, the install commands, five short steps of use, three other ways to use it, and where the documentation is.  It holds no FAQ, no scenario and no option: each step and each need is a link to the site."
- A new bullet under it, after the two it has: "`ReadmeTests` in `tests/SqlSource.Repository.Tests/Documentation/` caps the size of both readmes, and fails on an install or update command of one that `site/start/install.md` does not have."

The two bullets under it stay as they are: nothing about developing or releasing the project, and how both files are packed.  The bullets under "The two packed readmes have rules of their own" stay as phase 3 left them: no raw HTML, the link form with the folder, `--prerelease` and `nuget/vpre`, the `sql` info strings, and never naming an internal.  Check that none of them still names `site/README.md`, the callout or "what works today".

The root file is 14,925 bytes at the baseline, and its limit is 16,384.  After the edit, `wc -c AGENTS.md` must print less than 16,384, and no bullet may pass 80 words: `AgentsFileTests` holds both.

**`site/AGENTS.md`.**  Its opening bullet on `README.md` lists the scenarios and the FAQ.  Replace the bullet with: "**The two packed readmes** hold the outlines of root rule 4, and state nothing that a page of this folder does not own."  Take out any other line that says the README holds a FAQ, a scenario or getting started.

**`site/start/AGENTS.md`.**  Add to its last bullet, on `SupportedVersionsTests`: "`ReadmeTests` holds the install and update commands of the two readmes to `install.md`, so a command changes in all three files in one commit."

**`tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`.**  Its first bullet lists what each test checks: add `ReadmeTests`, with the size of the two readmes and their install commands.  Its bullet on the packed readmes must give the link form with the folder.

**`tests/README.md`.**  The row of `tests/SqlSource.Repository.Tests` lists the checks of the repository's files: add the size and the commands of the two readmes.

### 8. `docs/publishing.md`

- **Release step 3** takes `--prerelease` and its sentence out of the readmes and `site/` for the first stable release, and finds them with `git grep -- --prerelease`.  Add one sentence: `ReadmeTests` holds the commands of the two readmes to `site/start/install.md`, so the flag leaves all three in one commit.
- Check that the step's list of files is still right: `README.md`, `src/SqlSource.Tool/README.md`, the two example readmes and `site/`.  Run the `git grep` and compare.
- **The section "Site"** says where the links of the two readmes go.  It stays true.  Change nothing else there: the exception for this epic is phase 3's, and the close takes it out.

### 9. The packed files and nuget.org

- `src/SqlSource/SqlSource.csproj` packs the root file: `<PackageReadmeFile>README.md</PackageReadmeFile>` and `<None Include="../../README.md" Pack="true" PackagePath="/" Visible="false" />`.
- `src/SqlSource.Tool/SqlSource.Tool.csproj` packs its own: `<PackageReadmeFile>README.md</PackageReadmeFile>` and `<None Include="README.md" Pack="true" PackagePath="/" />`.
- `tools/check-package.sh` fails when a package has no `README.md`.
- nuget.org draws the file as Markdown with HTML off, and has no preview before a package is pushed.  So the check is of the file: no HTML, no relative link, no link to a heading of its own, no table, and no fenced block inside a list.  The new outlines need none of them.
- Pack, and see that each package holds the cut file (see "Done when").

## The ledger

`ledger/phase-11.tsv`, in the format of phase 0.  A unit of `README.md` that phase 1, 8 or 10 placed on the site keeps that row, and gets no second one.  A unit that phase 8 or 10 gave a `move` row to `README.md`, with the note "phase 11", gets its last row here, since "check rewrite 11" asks for a row for each unit that this phase changes.  This phase's rows:

- **The units that stay in `README.md`**: line 3 (`move`); the four sentences of line 5 (`move`, rewritten here, since the readmes have no rewrite phase); the commands of lines 15-17 and 21-24 (`move`); line 305 (`move`).
- **The units that the map sends to "its owner only"**: sentence 7 of line 101, sentences 5 and 10 of line 126.  Each is `restated-from` its owner, with no place in the README.
- **The navigation**: sentences 3 to 5 of line 9; the "More:" lines; the ten rows of lines 292-303.  Each is `move` with the note "navigation", and names what takes its place.
- **Each heading that goes**, as `move-split`, heading only.
- **Each pointer that phase 8 wrote**, removed.  It is text of phase 8 and has no unit of the baseline: list each with its phase 8 row.
- **Every row of the map of `src/SqlSource.Tool/README.md`**, but line 7's second sentence, which is phase 1's.
- **One `new` row for each section of both new files**, with the `start/` page or the owner that it restates.
- **Each fenced block that changes**: the sample of lines 35-51 becomes the sample pair, and the blocks of lines 62-70, 76-85, 93-99 and 112-124 go.

## Done when

- [ ] `grep -n '^#' README.md` prints six lines: `# SqlSource`, `## Supported databases`, `## Install`, `## Use`, `## Other ways to use it`, `## Documentation`.
- [ ] `grep -n '^#' src/SqlSource.Tool/README.md` prints four lines: `# SqlSource.Tool`, `## Install`, `## Use`, `## Documentation`.
- [ ] `wc -c README.md src/SqlSource.Tool/README.md` prints two numbers under the caps of `ReadmeTests`, and the pull request gives them with the caps.
- [ ] `grep -o 'https://mbcrawfo.github.io/SqlSource/[^) ]*' README.md src/SqlSource.Tool/README.md | sort -u` prints the site links of the tables of steps 2 and 3, and no other.
- [ ] `grep -n -E '\]\((#|\.|/|[A-Za-z0-9_-]+\.md)' README.md src/SqlSource.Tool/README.md` finds nothing.
- [ ] `grep -n -E '<(a|br|p|div|img|details|summary|sub|sup|kbd)[ />]' README.md src/SqlSource.Tool/README.md` finds nothing.
- [ ] `grep -n -i -E 'FAQ|Where to next|Only want|Just want|step [0-9]|Getting started' README.md` finds nothing.
- [ ] `grep -c 'nuget/vpre' README.md` prints 1.  `grep -c -e '--prerelease' README.md` prints 3, and the same for the tool readme prints 2: the commands and the one sentence.
- [ ] `git grep -n -E 'README\.md#(installation|getting-started|[1-5]-|only-want|just-want|faq|how-do-i|i-got-an|my-|can-i|nodatime|custom-type|where-to-next)' -- . ':!docs/superpowers'` finds nothing.
- [ ] `diff` of the two fenced blocks of the sample pair in `README.md` against those of `site/index.md` shows no difference.
- [ ] `dotnet test tests/SqlSource.Repository.Tests` passes, with `ReadmeTests`, `SupportedVersionsTests`, `UserDocsLinksTests`, `UserDocsSamplesTests`, `StatusLanguageTests`, `InboundSiteReferenceTests` and `AgentsFileTests` in the run.  Each new test was seen to fail, as steps 5 and 6 say.
- [ ] `wc -c AGENTS.md` prints less than 16,384, and `wc -c site/AGENTS.md site/start/AGENTS.md tests/SqlSource.Repository.Tests/Documentation/AGENTS.md` prints three numbers under 8,192.
- [ ] `git grep -n -E 'short FAQ|FAQ of five|FAQ answer|Where to next|average user' -- AGENTS.md site/AGENTS.md` finds nothing.
- [ ] `dotnet pack SqlSource.slnx --output artifacts/packages`, then `tools/check-package.sh artifacts/packages` passes.  `unzip -p artifacts/packages/SqlSource.[0-9]*.nupkg README.md | diff - README.md` and `unzip -p artifacts/packages/SqlSource.Tool.*.nupkg README.md | diff - src/SqlSource.Tool/README.md` show no difference.
- [ ] "check coverage" reports no unit of either readme without a disposition.  "check rewrite 11" has been run on both files, and each code span, id, numeral and sentence that it lists as gone has a place on the site named in a ledger.
- [ ] `tools/build-site.sh` ends with no docfx warning.
- [ ] The content reviews, `branch-review:review` and `./pre-commit-validation.sh --full` have passed, in the outline's order, on the last commit.
- [ ] The pull request says two things about publishing.  The site must have been published from `main` after phase 10 merged, before this phase merges, or the README on `main` links pages that the published site lacks.  And the site is published by hand again after this merge.

## For the reviewers

**The no-loss reviewer** has the larger part in this phase.  Beyond the outline's kit:

- Take each unit of the baseline `README.md` and of the baseline tool readme, in order.  Find its one row, in the ledger of phase 1, 8, 10 or 11.  Open the place the row names, in the built site, and read the fact there.
- Read the three "owner only" sentences and the tool readme's lines 22, 31 and 40-48 in their owners' sections, word against word.
- Confirm that each of the ten rows of "Where to next" has a target that a reader still reaches: from `## Documentation`, from `site/index.md#find-what-you-need`, or from a sidebar.
- Confirm that no pointer of phase 8 is left, and that none pointed at a page that the new README no longer reaches through the site.

**The accuracy reviewer** checks:

- Each sentence of both new files against the `start/` page or the owner that its ledger row names.  A readme says no more than that page, and nothing other.
- The steps of `## Use` against `site/start/first-query.md`: the same order, the same names, the same file names.
- The three commands, the sentence about previews, and the line of requirements against `site/start/install.md` and `site/reference/msbuild.md#build-requirements`.
- The three bullets of `## Other ways to use it` against the outline's decision 4: `methods` for your own connection, transaction or timeout; `models` and `sql` as fallbacks.  No bullet presents the outputs as a choice of equal weight.
- "Azure SQL is accepted, and not tested": those words, and never "supported".
- Both files as GitHub draws them, and as plain text with HTML off.
- The final words of the three `AGENTS.md` rules against what the files now hold.

## Traps

- **The site must be ahead of the README.**  Every link of the cut README goes to a page that phase 10 wrote.  See the last item of "Done when".
- **A link to the site's root may carry an anchor** since phase 3, which made the root the landing page: `https://mbcrawfo.github.io/SqlSource/#find-what-you-need` resolves to `site/index.md`.  At the baseline the test refused it.  The README's one link to the root has no anchor.
- **The new headings have new slugs**: `supported-databases`, `install`, `use`, `other-ways-to-use-it`, `documentation`.  Nothing in the repository links a heading of the README after this phase.  A page outside the repository that links an old heading still reaches the file.
- **The sample pair is read by `UserDocsSamplesTests`** as `sql postgres`, and a warning fails it.
- **A command in prose counts.**  `dotnet add package Npgsql` in an inline code span is an install command to `ReadmeTests`, and `start/install.md` has it in a tab.  Write a command as that page writes it.
- **`ReadmeTests` must not grow into a test of words.**  It holds a size and a set of commands.  The outline's anti-goals rule out a test that an agent learns to weaken.
- **The README is read in two places.**  On GitHub it stands beside `main`; on nuget.org it stands beside a release, and the site shows that release.  So it never links a page of `site/` on GitHub.
- **The tool readme is under `src/`**, which `UserDocsLinksTests` treats as an internal folder for every file but this one.  A link from `README.md` to the tool readme is allowed; a link to any other file of `src/` is not.
- **The readmes are the last files with the old shape.**  After the cut, grep the repository, but for `docs/superpowers`, for words that describe the old README: "FAQ", "five steps", "Getting started", "Where to next".  Each hit is true of the new file or is rewritten.

## Left to the agent

- The words of both files, within the tables and the rules above.
- The name and the count of the test methods of `ReadmeTests`, and how it parses a readme.
- Whether the five lines of `## Documentation` are a list or five short lines.
- The numbers of the caps, by the rule of step 5.

## Calls made in this brief

- **The caps are in bytes**, 8,000 for `README.md` and 4,000 for the tool readme, with a rule that resets a cap that is too tight or too loose.  The outline says "a readme over its size" and gives no unit; `AgentsFileTests` counts bytes.
- **A command is recognised by how it starts**, in fenced blocks of a shell and in inline code spans, after normalising.  The outline says only "an install or update command".
- **`dotnet tool restore` counts as an install command**, since `start/install.md` has it for a colleague who clones the repository.
- **The readmes are written in plain language in this phase.**  No rewrite phase names them, and the map's rows say "stays" of text that is far over the language rule's 25 words.
- **The sample pair of the README is the landing page's**, byte for byte, so that one check of accuracy covers both: the reviewer of phase 10 compiled the form without `-- generator: method-type=both`, which no example file compiles.  No test holds the two pairs equal.
- **A step of `## Use` holds no fenced block**, for nuget.org.
- **Step 4 of `## Use` links two anchors**, `#connection` and `#describe`, as the map's outline joins the two steps of the site in one.
- **The tool readme's "Documentation" has five links, four of them to the site.**  The fifth is the SqlSource readme, as the map's section 7 lists them.
- **`site/AGENTS.md` keeps one line on the readmes** in place of its opening bullet, since the file says it holds rules for them.  Root rule 4 holds the outline.
- **Each README unit has one ledger row**, in the phase that placed it on the site.  Phase 11's ledger holds what stays, what has an owner alone, the navigation and the tool readme.
- **`src/SqlSource.Tool/AGENTS.md` is not edited.**  Its bullet on the readme is true after the cut.
