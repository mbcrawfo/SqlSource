# Docs overhaul, phase 24: rewrite-diagnostics-4

Phase 24 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md).  Read the outline, the [content map](2026-11-12-docs-overhaul/content-map.md) (its entry for `diagnostics.md` in section 3, and section 10), [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md) and [`diagnostics-guide.md`](2026-11-12-docs-overhaul/diagnostics-guide.md) before this brief.  This brief holds only what is special to its range.

- Done by: an Opus agent.
- Needs: phase 11.
- Runs beside: phases 12 to 24.

## Goal

The sections `SQLSRC261` to `SQLSRC299` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide.  Every fact of today's sections is still there.  No line outside the range changes, and no other file of `site/` changes.

## The range

- **Ids:** `SQLSRC261` to `SQLSRC299`, with no gap.  The range starts at the line `## SQLSRC261` and ends at the end of the file.  The lines above that heading belong to phase 23.  A section above `299` that feature work added is yours too.  The baseline lines are 2972 to 3707 of `git show 28e20e3c:site/diagnostics.md`.  The units of the inventory in that range are yours.
- **Size:** 39 sections and 10,565 words at the baseline, counted from each heading to the next.  Phase 2 added one to three lines to each section, so count again when you start.  In the commands of the diagnostics guide, give a second heading that matches nothing, such as `## SQLSRC999`.
- **Areas**, by their names on `reference/errors.md#errors-by-area`.  The range has fifteen, more than any other:

| Area | Ids | Sections |
|----|----|----|
| An entry of the type override that maps nothing | 261 | 1 |
| The parameters of a SQL Server query | 262 | 1 |
| The columns of a SQL Server query | 263 to 266 | 4 |
| The C# types of a SQL Server query | 267 to 270 | 4 |
| The encoding of a file | 271 | 1 |
| The methods of a query | 272 to 276 | 5 |
| Converters | 277 to 280 | 4 |
| The libraries of a query | 281, 282 | 2 |
| The JSON opt-in | 283 | 1 |
| A short name of your choice | 284 | 1 |
| A `.env` file | 285 to 287 | 3 |
| A bug-report bundle | 288 to 291 | 4 |
| Making a bug report with `diagnose` | 292 to 296 | 5 |
| The collection method of a query | 297 | 1 |
| A query's synchronous method | 298, 299 | 2 |

- **Who reports them:** the tool reports 18: `SQLSRC262` to `266`, `271` and `285` to `296`.  The build reports the other 21.  The two kinds alternate through the range, so read the "Reported by" line of each section before you write of "the build" or "the tool".
- **"Database" lines:** the five sections `SQLSRC262` to `266` each have one.  All five are SQL Server's.

## Specific to this range

**The longest and hardest sections:**

| Id | Words | What makes it hard |
|----|----|----|
| `SQLSRC274` | 680 | Eight cases in which a method's name is taken, each under a member location or an output.  One exception to the cost: a clash in an extension class "leaves out its methods of that name alone" |
| `SQLSRC277` | 653 | Thirteen reasons, each a quoted text of the message.  A last paragraph on a converter in a referenced assembly, which is reported at another place |
| `SQLSRC279` | 640 | Two forms of one error, each with a `console` block.  A last paragraph on arrays, on types that the driver reads, and on NodaTime |
| `SQLSRC271` | 449 | The tool reports it, and the section says what the build reports instead: `SQLSRC241`, and on Windows `SQLSRC244`.  Three paragraphs of over 100 words |
| `SQLSRC286` | 428 | Six reasons, each a quoted text.  The value of the connection is never printed |
| `SQLSRC287` | 418 | Linux limits, with four numbers: 128, 65536, 1024 and 524288.  Two ways out, and a second form of the error when the watch polls |
| `SQLSRC283`, `284` | 408, 397 | One paragraph each of over 150 words on what is mapped and by what |
| `SQLSRC276` | 312 | Three cases, and a rule on two types that share one class |

**Several causes:** `SQLSRC274` (eight), `SQLSRC276` (three), `SQLSRC277` (thirteen), `SQLSRC286` (six), `SQLSRC292` and `SQLSRC295` (two each, each with its own fix).  Keep the order of each list: `SQLSRC274` refers to "the last case below", and `SQLSRC277` reports "the first of these that is wrong".

**Samples:** no section of the range has a table.  It has 53 fenced blocks: 41 `console`, 2 `xml`, 1 `ini`, 1 `csharp`, 4 `sql error` and 4 `sql mssql`.  Every section has at least one.  `SQLSRC263` to `266` have three each: the error, the query with the mistake as `sql error`, and the query mended as `sql mssql`.  The mended query is the sample of the fix, so it stands after `**Fix:**`.  This brief lists no block to change.

**`See:` lines that the map changed, or that this brief changes:**

- `SQLSRC294`, correction C-03 of the map.  Its `See:` line had a second link, to the known issues of `tool.md`, and no bullet there was about a bundle.  Phase 5 dropped that link.  Do not put it back, and do not replace it with a link to a limit.
- `SQLSRC293`, `294`, `295` and `296` link the guide `guides/report-a-bug.md#raw-bundle`, which holds the procedure.  The first link of a `See:` line is a reference section.  So give each of the four `reference/bundles.md#diagnose` as its first link, and keep the guide as the second.  `SQLSRC296` keeps `reference/bundles.md#replay` as its third.
- `SQLSRC267` states a minimum that the error enforces: Microsoft.Data.SqlClient 6.1.  Section 5 of the map says that its `See:` line links the table of supported versions, and the outline gives the change to this phase.  Add `reference/databases/index.md#supported-versions` to the line, as its first link, with the link text of the heading as it stands on that page.  Keep the link that the line has.  The number stays in the text of the section.

**Lists of outputs:** twenty lines of the range name an output before `repository`, in 18 sections: `SQLSRC261`, `267` to `270`, `272` to `275`, `277` to `284` and `298`.  Nearly all say "`methods` or `repository`" where they say what the error costs.  Write "`repository` or `methods`".  One text keeps its order: the message of `SQLSRC273` says "has the output methods or repository", and the `console` block of that section shows it as the build prints it.  The sentences around the block follow the rule.

**Warnings and information:** two of the eight diagnostics that are not errors are here.

| Id | What its section says today | Source |
|----|----|----|
| `SQLSRC268` | "This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds."  `NoWarn`, one `xml` block.  For where information is shown, it points to `SQLSRC248` | `SqlDiagnostics.cs`, the descriptor of `SQLSRC268` |
| `SQLSRC284` | "This is a warning, so that a build goes on when an update of a package brings a new type of a name you use."  `NoWarn`, `WarningsNotAsErrors`, one `xml` block, and one `ini` block for `.globalconfig`.  The query keeps the type, its models and its method | `SqlDiagnostics.cs`, the descriptor of `SQLSRC284` |

`SQLSRC268` points to `SQLSRC248`, which phase 23 rewrites.  Keep the pointer, and keep the sentence that it is not in the output of `dotnet build`, so that the section stands alone.

**What an error costs differs most in this range.**  Check each against its section, and do not make them alike:

- `SQLSRC272` to `275`, `279`, `280`, `282` and `283`: the query gets no method, and its models are still generated.  `SQLSRC298` adds the collection method to that.
- `SQLSRC277` and `278`: the error is at the converter.  For a converter in a referenced assembly it is at each query that would use it, and that query gets no method.
- `SQLSRC276`: the type gets none of its methods.  `SQLSRC281` and `284`: the query keeps its models and its method.
- `SQLSRC297`: only the collection method is not generated.  `SQLSRC299`: only the synchronous methods under `method-type=both`, and every method under `sync`.

**Sources of the descriptors and the texts:**

- `src/SqlSource/Diagnostics/ToolDiagnostics.cs` holds the tool's 18.  `src/SqlSource/Diagnostics/SqlDiagnostics.cs` holds the other 21.
- SQL Server, `SQLSRC262` to `266`: `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`.
- The encoding, `SQLSRC271`: `src/SqlSource.Tool/Planning/RunPlanner.cs`.
- The methods, `SQLSRC272` to `276`, `298` and `299`: `src/SqlSource/Generation/Methods/MethodConflicts.cs` and `MethodPlanner.cs`, and `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs`.
- The thirteen reasons of `SQLSRC277`: `src/SqlSource/Generation/Converters/ConverterReader.cs`, and `tests/SqlSource.Tests/Generator/ConverterDiagnosticsTests.cs`.
- The libraries, `SQLSRC281` and `282`: `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`, and `tests/SqlSource.Tests/Generator/LibraryDiagnosticsTests.cs`.
- The six reasons of `SQLSRC286`: `src/SqlSource.Tool/DotEnv/DotEnvParser.cs`.  The watcher, `SQLSRC287`: `src/SqlSource.Tool/Watching/WatchSession.cs`.
- The bundles, `SQLSRC288` to `296`: `src/SqlSource.Tool/Bundles/BundleErrors.cs` and `DiagnoseRun.cs`.
- For any other quoted text, `git grep -F "<the text>" -- src` finds its file.

## Done when

- Each of the 39 sections has the one shape.  Each has its fix and ends with a `See:` line: the two counts of the diagnostics guide, of headings and of `See:` lines over the range, both print 39, or one more for each section that feature work added.
- The check of the diagnostics guide, with `## SQLSRC261` and a second heading that matches nothing, prints nothing: no line above the range changed.  The file still ends with one line break.
- `git diff --stat` against the merge base names `site/diagnostics.md`, the phase's ledger file, and the outline if the plan changed.  It names no other file.
- "check rewrite 24" exits with 0 on the last commit.  Every section of the range has a row in `ledger/phase-24.tsv`, and each difference that the command prints has a true note.  "check coverage" still passes.
- `awk '/^## SQLSRC/ { id = $2 } /^See:/ && /index\.md#supported-versions/ { print id }' site/diagnostics.md` prints `SQLSRC267` among its lines, and the same command with `bundles\.md#diagnose` prints `SQLSRC293` to `296`.  The five links are `new` rows of the ledger: four to `reference/bundles.md#diagnose`, and one to the table of supported versions.
- Each of the five "Database" lines is as phase 2 wrote it.
- The tests that read this page pass: `DiagnosticsDocumentTests`, `UserDocsSamplesTests`, `UserDocsLinksTests`, `StatusLanguageTests`, `SiteStructureTests`, `SupportedVersionsTests` and `InboundSiteReferenceTests`.  `dotnet test --project tests/SqlSource.Repository.Tests` runs those of that project, and `./pre-commit-validation.sh --full` runs them all.
- `tools/build-site.sh` gives no warning.
- The grep of `site/AGENTS.md` for internals, run over the range, has no hit that is not text of a message or a title.
- The word count of the range, before and after, is in the pull request's description and in a `#` comment line of `ledger/phase-24.tsv`.
- The search for the order of the outputs, run over the range as the diagnostics guide says, prints only text that the build or the tool prints.
- The description of the pull request holds what section 10 of the rewrite guide asks.  "Facts with no owner" is written as its section 8 says, in the pull request and in `ledger/phase-24.tsv`.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.

## For the reviewers

**The no-loss reviewer** reads each section old against new, with the checker's output for that section beside it.

- Start with `SQLSRC274`, `277` and `279`.  Count the cases: eight, thirteen, and two forms.
- Compare the quoted reasons of `SQLSRC277`, `286`, `292` and `295` byte for byte.  A reason is text of the message.
- Compare the four numbers of `SQLSRC287`, and the versions `1.4.0` and `1.6.0` in `SQLSRC290` and `296`.  The command with `--version 1.4.0` is "for example": those words stay.
- Read `SQLSRC293` for its warning to the reader: "do not paste this error into an issue".  It must stay, as a sentence of its own, before the fix.
- Read `SQLSRC297`, `298` and `299` for "only": each costs only a part of what the others cost.
- Run the check for lines outside the range yourself.

**The accuracy reviewer** checks the reworded text against the source.

- The two paragraphs on severity against `SqlDiagnosticsTests.Configurable`: one information and one warning, and no other section of the range.
- The versions: Microsoft.Data.SqlClient 6.1 in `SQLSRC267` and `270`; 8 and 7 in `SQLSRC268`; 5.1 with EF Core 8 and 9 in `SQLSRC267`; the library versions of `SQLSRC282`, against `LibraryProbe.cs`.
- What each error costs, by the list above, against the tests named.
- The "Reported by" line of each section against the text under it.  A section of the build must not say "the tool reports", and the reverse.
- The new first links of `SQLSRC293` to `296`: the anchor exists, and that section documents `--no-obfuscate` and `--scrub`.

## Traps

- **`captureVersion`, `bundleVersion` and `formatVersion` stand in `SQLSRC289`.**  The grep for internals hits "capture".  They are words of the message, so they stay as code spans.  The text around them does not explain them.
- **The title of `SQLSRC261` says "catalog", and the title of `SQLSRC291` says "engine".**  A title stays.
- **Do not turn the six reasons of `SQLSRC286` into a table.**  The fifth and sixth hold a fix of their own, which a cell does not hold well.
- **`SQLSRC293` and `SQLSRC294` have the same fix, word for word, today.**  Keep them the same after the rewrite.
- **`SQLSRC266` is to SQL Server what `SQLSRC253` is to PostgreSQL**, and the tool reports the one and the build the other.  The section says so.  `SQLSRC253` is in phase 23: do not edit it.
- **Do not copy the paragraph on severity of `SQLSRC284` into `SQLSRC268`.**  `SQLSRC268` is information: `WarningsNotAsErrors` and `.globalconfig` are not in its text.

## Calls made in this brief

- **`SQLSRC293` to `296` gain `reference/bundles.md#diagnose` as the first link of their `See:` line.**  The map leaves `SQLSRC294` with a guide as its only link (C-03).  The guide owns the procedure, and `#diagnose` owns the options `--no-obfuscate` and `--scrub`.
- **`SQLSRC267` gains the link to `reference/databases/index.md#supported-versions` as the first link** of its `See:` line.  The outline gives the link to this phase; the place in the line is this brief's call.
- **The range does not move after the recount.**  The lines of phase 2 are new text, which no reviewer reads old against new, so they do not count against the 12,000 words of the outline.
- **This phase does not edit `site/AGENTS.md`.**  Four phases would collide on it.  Phase 26 lands the rule on the shape of a diagnostic section there, after the four have merged.
