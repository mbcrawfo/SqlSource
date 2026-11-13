# Docs overhaul, phase 18: rewrite-tool

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 18.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The five pages of the group "The sqlsource tool" are in plain language and in the shape of a reference page.  MP-08 and MP-11 are merged, and three terms are defined once.  Every fact about what the tool hides, what it writes and when it stops is as exact as it is today.

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 10,350 in all.  The old text is 10,320 words (section 10 of the map).

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `cli.md` | 2,600 | `tool`, `install-the-tool`, `logs` | MP-11; MP-10: tool | `limit-path-through-a-symbolic-link` |
| `describing.md` | 2,450 | `which-queries`, `describe-on-save` | MP-10: describe | `limit-several-target-frameworks`, `limit-saves-not-seen`, `limit-editors-not-checked`, `limit-folder-changes-not-seen` |
| `connections.md` | 1,900 | `connections`, `env-file` | None | `limit-short-password` |
| `sidecars.md` | 1,750 | `sidecars-in-git`, `describing-again` | MP-08; MP-10: sidecar | `limit-file-in-two-projects` |
| `bundles.md` | 1,650 | `bundle-contents`, `replay` | None | None |

The three hardest passages:

- **`describing.md#describe-on-save`.**  900 words.  The bullet "What a project includes may have changed" is one sentence of 118 words.  It lists the changes to what a project includes, in six groups, each with its own limit: "up to the root of your repository", "and still gone when the pass starts".
- **`cli.md#logs`.**  645 words, six sentences over 40.  It says what the tool writes as `***`: a connection value "wherever it finds it whole".  With `connections.md#limit-short-password` it is the security text of the group.
- **`sidecars.md#sidecars-in-git`.**  Three paragraphs open "After a merge", each for another case, and a fourth says what to do "before any of these runs".  They become three procedures with one first step in common.

## Specific to this phase

### The merge pairs

| Pair | Lands on | Where the texts differ | Settle it by |
|----|----|----|----|
| MP-08 | `sidecars.md#limit-file-in-two-projects` | No conflict.  The bullet of `sql-files.md` says what the two projects need.  The bullet of `tool.md` says what a run on one of them removes | `docs/tech-debt/TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md`.  The page names no id |
| MP-11 | `cli.md#install-the-tool` | No conflict.  The README adds: a project that only keeps its SQL out of code files does not need the tool | `docs/publishing.md`: the package and the tool carry one version |

- **No heading is lifted** (section 9 of the guide).  The two texts of each pair stand under one heading already: two bullets under the limit, and the README's lines below the text of `tool.md`.
- **MP-08 is a limit.**  Write the merged text in the three parts of a limit.  "What you see" has two symptoms: entries vanish from a sidecar, or the build reports SQLSRC245.
- **MP-11 holds the two install commands twice.**  Keep one block of them.  The other is the one fenced block that this phase removes.  Its row of the ledger names it, or "check rewrite 18" fails.
- **The README's sentence in MP-11 says "step 5"**, of the README's own steps.  Phase 10 pointed the link of those words at `../start/first-query.md#describe`, where the fifth step is "Describe".  Keep that target.  Settle the words: after the merge no text of a reference page names a step by its number, so say what the step does.
- **`--prerelease` stays** on the install command and on the update command.  The sentence beside it says only that the packages are published as previews.  Phase 1 wrote it.

### The terms of MP-10

| Term | Section | What the merged definition must say |
|----|----|----|
| Tool | `cli.md#tool` | The `sqlsource` command, of the package `SqlSource.Tool`.  It asks your database about your queries and writes the sidecars |
| Describe | `describing.md#describe` | To ask a database what the parameters and the columns of a query are, without running the query, and to record the answer.  Phase 5 wrote one sentence here; merge the term into it |
| Sidecar | `sidecars.md#sidecar-files` | The file `Users.sql.json` beside `Users.sql`.  The tool writes it, you commit it, and you never edit it by hand.  The build reads it in place of a database |

Write in `merges/MP-10.md` under these three terms only.

### Rules that another section owns, and rules owned here

| On this page | The rule | Its owner |
|----|----|----|
| `cli.md#tool`, the two paragraphs on which queries need the tool | Which queries must be described | `settings.md#setting-output`, and `describing.md#which-queries` |
| `cli.md#requirements`, the last bullet | Which versions of a database are supported | `databases/index.md#supported-versions`.  State no version here |
| `describing.md#which-queries`, its first sentences | That the output decides it | `settings.md#setting-output`.  Which files and types the tool reads stays here |
| `describing.md#check-in-ci` | The sample job | `guides/ci.md` |
| `connections.md#env-file` | The property `SqlSourceEnvFile` | `msbuild.md#setting-env-file`.  The file's format, and where the tool finds it, are owned here |
| `bundles.md`, the opening | The procedure for a report | `guides/report-a-bug.md` |

Three sections of this phase are owners that many pages restate.  Keep each whole:

- **`sidecars.md#describing-again`**: when an entry of a sidecar stops being current.
- **`connections.md#env-file`**: the tool reads the `.env` file in the folder of the project or solution, and never one above it.
- **`cli.md#install-the-tool`**: a project whose queries all have the output `sql` needs no tool.

### The reframings on these pages

- **Every list of outputs** has `repository` first: "the queries whose output is `repository`, `methods` or `models`" in `#tool` and `#which-queries`.
- **No text says "choose a dialect".**  `#which-queries` says that the tool describes the queries of `postgres` and `mssql`.
- **Four support facts stay**: "The saves of an editor were checked with Rider on macOS alone" in `#describe-on-save`; "This has not been checked" in `#what-your-editor-shows`; and the two limits `limit-saves-not-seen` and `limit-editors-not-checked`.  They are not status language.

### Write effects, never internals

This group is where the rule of `site/AGENTS.md` bites most.

- **Say what the tool asks, writes and prints**, never how.  Run the `grep` of `site/AGENTS.md` on all five pages.
- **`sidecars.md` links `schemas/sidecar-v1.schema.json` once**, for the curious.  Keep the one link, and name no field of the file.
- **`bundles.md` says what a bundle holds in the reader's words**: the query, what the database answered, what SqlSource made of the answer.  Keep those words.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| Each command, option and variable, and its default | `cli.md#command-reference` | `src/SqlSource.Tool/Cli.cs`, `SharedOptions.cs`; `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`, `UsageCheckTests.cs`; `CliReferenceTests` reads the rows |
| The exit codes `0`, `1` and `2`, and what a second Ctrl+C does | `cli.md#exit-codes` | `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs`, `TerminationTests.cs` |
| What is written as `***`, and what is not hidden: a password shorter than four characters | `cli.md#logs`, `connections.md#limit-short-password` | `src/SqlSource.Tool/Describing/Redaction.cs`, `ConnectionSecret.cs`; `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs` |
| The order of the four sources of a connection, and how a database's name becomes a variable's name | `connections.md#connections` | `src/SqlSource.Tool/Describing/ConnectionSources.cs`, `ConnectionVariables.cs`; SQLSRC213, SQLSRC214, SQLSRC215 |
| Where the `.env` file is found, its format, SQLSRC285 and SQLSRC286 | `connections.md#env-file` | `src/SqlSource.Tool/DotEnv/DotEnvSearch.cs`, `DotEnvParser.cs`; `tests/SqlSource.Tool.Tests/Commands/DotEnvRunTests.cs` |
| What makes an entry out of date, and what does not | `sidecars.md#describing-again` | `src/SqlSource/Parsing/SqlQueryHash.cs`; `src/SqlSource/Generation/Sidecars/SidecarChecker.cs`; `tests/SqlSource.Tests/Generator/SidecarTests.cs` |
| "A sidecar is written whole or not at all" | `sidecars.md#describing-again` | `src/SqlSource.Tool/Describing/SidecarStore.cs` |
| Which changes start a pass of the watch | `describing.md#describe-on-save` | `src/SqlSource.Tool/Watching/WatchSet.cs`, `RepositoryFolders.cs`; `tests/SqlSource.Tool.Tests/Watching/` |
| What `--check` reports, and that it writes nothing | `describing.md#check-in-ci` | `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs` |
| What a bundle renames; what `replay` prints for a control character: `\u001B`, `\u000A`, `\n`, `\t` | `bundles.md` | `src/SqlSource.Tool/Bundles/Renaming/`, `ReplayLines.cs`; `tests/SqlSource.Tool.Tests/Commands/ReplayTests.cs`, `DiagnoseTests.cs` |
| The lines of the summary | `cli.md#summary` | `src/SqlSource.Tool/Describing/RunSummary.cs`; `tests/SqlSource.Tool.Tests/Commands/SummaryTests.cs` |

### Samples and tables

- **No sample of these pages has a twin in a test**, but every console block is the tool's real output.  Each is byte-identical, also where the tool says "will".
- **The tables of arguments, options, variables and exit codes are tables of facts.**  `CliReferenceTests` reads the first column.  Leave the rows.  Six rows of options are new text of phase 5, with the sentence that introduces them: `--help`, `--version`, `--query`, `--out`, `--no-obfuscate` and `--scrub`.  Keep them as they are.
- **`src/SqlSource.Tool/AGENTS.md` links `cli.md#exit-codes`.**  The anchor stays, as every anchor does.

No page of the phase is near 3,500 words.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 18" exits with 0, so each unit has a row in `ledger/phase-18.tsv`.  The one fenced block that it lists as gone is the second pair of install commands, and none is listed as changed.  The seven limits have their three parts, by the loop of that section.
- The worksheets `MP-08.md` and `MP-11.md` are finished, and the command of that section for `merges/MP-10.md` prints the h2 of Tool, Describe and Sidecar.
- `grep -c -- "--prerelease" site/reference/cli.md` prints 2 or more: the install command and the update command keep the flag.  `grep -c -i "step 5" site/reference/cli.md` prints 0.
- `CliReferenceTests` passes.
- The procedures of `#sidecars-in-git` and `#describe-on-save` are numbered steps, and each list of what the tool sees or hides is a vertical list: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  Read `cli.md#logs` and `connections.md#limit-short-password` first, word by word.  A lost "whole", "alone" or "shorter than four" there tells a reader that a secret is hidden when it is not.
- **Accuracy.**  Check the list of what the watch sees against `WatchSet.cs`, group by group.  Check each of the three merge procedures of `#sidecars-in-git` against the old paragraph: the commands, and their order.
- **The limits.**  For each of the seven, "what to do" must be something a reader can do today.

## Traps

- **"The tool" is one actor and "the build" another.**  A build can run the tool.  Then the tool describes, and the build reports the tool's errors.  Say which does what in each sentence.
- **`describe`, `describe --force`, `describe --check` and `describe --watch` are four things.**  Do not shorten a command in a code span.
- **Some options are for the build's use**, as `--manifest` and `--online`.  The old text says so.  Keep that they are not for a reader to type.
- **"Never edit a sidecar by hand"** and "a connection never goes in the project file or a settings file" are rules with a reason.  Keep each "never" and each reason.
- **A variable's name is made from the database's name**: upper case, and every character that is not a letter or a digit written another way.  Copy the rule; do not round it to "upper case".
- **`SQLSOURCE_CONNECTION` alone** is used only when the run has exactly one database.  "Only" and "exactly one" are the rule.

## Calls made in this brief

- **One fenced block goes in MP-11**: the README's copy of the two install commands, which stands beside the same commands of `tool.md`.  The map says the pair has no conflict; a merge keeps a fact once.
- **The limit of MP-08 names no tech-debt id.**  The map names the open item as the source of truth; `site/AGENTS.md` forbids the id on a page.
- **The term "describe" merges into the sentence that phase 5 wrote.**  That sentence is new text, and the merge is the reason to change it.
- **No text of `cli.md` names a step by its number.**  The briefs of phases 8 and 10 keep the words "step 5" true by their link, and leave the words to this merge.
