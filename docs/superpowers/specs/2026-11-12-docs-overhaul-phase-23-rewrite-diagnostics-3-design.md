# Docs overhaul, phase 23: rewrite-diagnostics-3

Phase 23 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md).  Read the outline, the [content map](2026-11-12-docs-overhaul/content-map.md) (its entry for `diagnostics.md` in section 3, and section 10), [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md) and [`diagnostics-guide.md`](2026-11-12-docs-overhaul/diagnostics-guide.md) before this brief.  This brief holds only what is special to its range.

- Done by: an Opus agent.
- Needs: phase 11.
- Runs beside: phases 12 to 24.

## Goal

The sections `SQLSRC226` to `SQLSRC260` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide.  Every fact of today's sections is still there.  No line outside the range changes, and no other file of `site/` changes.

## The range

- **Ids:** `SQLSRC226` to `SQLSRC260`, with no gap.  The range starts at the line `## SQLSRC226` and ends on the line before `## SQLSRC261`.
- **Baseline lines:** 2187 to 2971 of `git show 28e20e3c:site/diagnostics.md`.  The units of the inventory in that range are yours.
- **Size:** 35 sections and 10,668 words at the baseline, counted from each heading to the next.  Phase 2 added one to three lines to each section, so count again when you start, with the command of the diagnostics guide.
- **Areas**, by their names on `reference/errors.md#errors-by-area`:

| Area | Ids | Sections |
|----|----|----|
| Describing a query | 226 to 236, the last 11 of the area | 11 |
| The sidecar of a `.sql` file | 237 to 245 | 9 |
| The C# types of a query | 246 to 249 | 4 |
| A C# type of your choice | 250 to 252 | 3 |
| The models of a query | 253 to 260 | 8 |

- **Who reports them:** the tool reports `SQLSRC226` to `236`, when it describes a query.  The build reports `SQLSRC237` to `260`.
- **"Database" lines:** the eleven sections `SQLSRC226` to `236` each have one.  That is 11 of the 18 on the page.

## Specific to this range

**The longest and hardest sections:**

| Id | Words | What makes it hard |
|----|----|----|
| `SQLSRC231` | 1,021 | PostgreSQL, then SQL Server, each with its own `**Fix:**` today.  Four fenced blocks.  The PostgreSQL half has five cases: a misread `@`, a refusal by Npgsql itself, a `:name` that only the comparison of the texts sees, a schema that changed during the run, and an array of a domain |
| `SQLSRC246` | 868 | Ten families of types as bullets, each with a fix of its own.  PostgreSQL and SQL Server types stand in one list.  A last paragraph of 182 words on how the message writes a key with quotes and escapes |
| `SQLSRC228` | 678 | Three reasons on PostgreSQL and three on SQL Server.  Each reason is text of the message.  Two lists of types as they are written, which are code spans |
| `SQLSRC250`, `252`, `251` | 499, 442, 378 | Short names, full names and what a project can see.  Dense with "and either ...; or ...; or ..." |
| `SQLSRC232` | 457 | Five kinds of type as bullets, then one long sentence of types that Npgsql can bind, and so are not this error |
| `SQLSRC226` | 454 | PostgreSQL, then SQL Server, then a third form with no `server:` line that hands over to `SQLSRC262` |
| `SQLSRC236` | 431 | Three causes.  It is the second example of the diagnostics guide |
| `SQLSRC257` | 381 | What makes two models the same: every condition in one sentence.  Three fenced blocks |
| `SQLSRC241` | 263 | Its fix paragraph holds five cases: describe, the output `sql`, `SQLSRC271`, a sidecar that the build does not see, and the cost |
| `SQLSRC237` | 200 | The fix is a true procedure, and its order matters: bring the database to the merged schema first, then describe, then commit |

**No section of the range has a table.**

**Two databases in one section:** `SQLSRC226`, `228`, `231` and `235` cover PostgreSQL and SQL Server.  `SQLSRC246` covers both in one list of types.  Write the two databases as two causes, PostgreSQL first, as today.

**Samples:** 58 fenced blocks: 40 `console`, 2 `xml`, 8 `sql error`, 7 `sql postgres` and 1 `sql mssql`.  `SQLSRC243` has none.  `SQLSRC253` to `258` and `SQLSRC260` have three each, in one order: the error, the query with the mistake as `sql error`, and the query mended as `sql postgres`.  The mended query is the sample of the fix, so it stands after `**Fix:**`.  This brief lists no block to change.

**`**Fix:**` exists already** in `SQLSRC231`, twice, and in `SQLSRC232`.  In `SQLSRC231` each database keeps its own fix, as the last item of its cause.

**`See:` lines and links that the map changed:**

- `SQLSRC243`: its `See:` line linked the bare page `dialects.md`.  It now links `reference/databases/index.md#database-support`.
- `SQLSRC245`: its text linked the known issues of `sql-files.md`.  It now links the limit `reference/sidecars.md#limit-file-in-two-projects`.  The paragraph that holds the link restates that limit.  Keep it: the reader of the warning needs it here.
- `SQLSRC235` and `SQLSRC247` state a minimum that the error enforces: PostgreSQL 14 and SQL Server 2017, and Npgsql 8.0.  Section 5 of the map says that the `See:` line of each links the table of supported versions, and the outline gives the change to this phase.  Add `reference/databases/index.md#supported-versions` to the `See:` line of each, as its first link, with the link text of the heading as it stands on that page.  Keep the links that the line has: two in `SQLSRC235`, one in `SQLSRC247`.  The numbers stay in the text of the sections.

**Lists of outputs:** twenty lines of the range name an output before `repository`, in `SQLSRC241` and in every section from `SQLSRC246` to `260`.  Nearly all say "`methods` or `repository`" or "`models`, `methods` or `repository`", where they say what the error costs.  Write "`repository` or `methods`" and "`repository`, `methods` or `models`".  A `console` block keeps what the build prints.

**Warnings and information:** three of the eight diagnostics that are not errors are here.

| Id | What its section says today | Source |
|----|----|----|
| `SQLSRC240` | "This is a warning, so that a build goes on while the sidecars are written again."  `NoWarn`, `WarningsNotAsErrors`, one `xml` block | `SqlDiagnostics.cs`, the descriptor of `SQLSRC240` |
| `SQLSRC245` | "This is a warning, since an entry that nothing uses changes no generated code."  `NoWarn`, `WarningsNotAsErrors`, no block | `SqlDiagnostics.cs`, the descriptor of `SQLSRC245` |
| `SQLSRC248` | "This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds."  `NoWarn`, one `xml` block.  Then where information is shown: not in `dotnet build`, in an IDE, in a log at `-v:detailed -tl:off`, in a binary log | `SqlDiagnostics.cs`, the descriptor of `SQLSRC248` |

`SQLSRC248` is the one full statement of where information is shown.  `reference/errors.md#severity` restates it, and `SQLSRC268` in phase 24 points to it.  Keep each of its four places.

**Sentences that phase 2 added, by the map:** "SQLSRC209 or SQLSRC210 instead" in `SQLSRC241`; "parameters before columns" in `SQLSRC246` and `249`; "an entry that no query uses is never an error" in `SQLSRC250`; "the later of two queries" in `SQLSRC257`.  They stand under the "Reported by" line, or in the text of the section.  Keep each one where phase 2 put it.

**Sources of the descriptors and the texts:**

- `src/SqlSource/Diagnostics/ToolDiagnostics.cs` holds `SQLSRC226` to `236`.  `src/SqlSource/Diagnostics/SqlDiagnostics.cs` holds `SQLSRC237` to `260`.
- The tool's lines and reasons, `SQLSRC226` to `236`: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, `ConnectionErrors.cs` and `NpgsqlCheck.cs`, `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`, and `src/SqlSource.Tool/Describing/Describers/DescribeFailures.cs`.
- The sidecar, `SQLSRC237` to `245`: `src/SqlSource/Generation/Sidecars/SidecarChecker.cs`, and `tests/SqlSource.Tests/Generator/SidecarTests.cs`.
- The types, `SQLSRC246` to `252`: `src/SqlSource/Generation/Types/TypeDiagnostics.cs` and `QueryTyper.cs`, `src/SqlSource/Generation/Types/Drivers/NpgsqlProbe.cs`, and `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs` and `NpgsqlProbeTests.cs`.
- The models, `SQLSRC253` to `260`: `src/SqlSource/Generation/Models/ModelCollector.cs` and `ModelReader.cs`, and `tests/SqlSource.Tests/Generator/ModelTests.cs`, `ModelNameTests.cs` and `SharedModelTests.cs`.
- The minimum versions: the descriptors of `SQLSRC235` and `SQLSRC247`, and `reference/databases/index.md#supported-versions`, which `SupportedVersionsTests` holds.
- For any other quoted text, `git grep -F "<the text>" -- src` finds its file.

## Done when

- Each of the 35 sections has the one shape.  Each ends with a `See:` line, and each but `SQLSRC243` has its fix: the two counts of the diagnostics guide, of headings and of `See:` lines over the range, both print 35.
- The check of the diagnostics guide, with `## SQLSRC226` and `## SQLSRC261`, prints nothing: no line outside the range changed.
- `git diff --stat` against the merge base names `site/diagnostics.md`, the phase's ledger file, and the outline if the plan changed.  It names no other file.
- "check rewrite 23" exits with 0 on the last commit.  Every section of the range has a row in `ledger/phase-23.tsv`, and each difference that the command prints has a true note.  "check coverage" still passes.
- `awk '/^## SQLSRC/ { id = $2 } /^See:/ && /index\.md#supported-versions/ { print id }' site/diagnostics.md` prints `SQLSRC235` and `SQLSRC247` among its lines.  The two links are `new` rows of the ledger.
- Each of the eleven "Database" lines is as phase 2 wrote it.
- The tests that read this page pass: `DiagnosticsDocumentTests`, `UserDocsSamplesTests`, `UserDocsLinksTests`, `StatusLanguageTests`, `SiteStructureTests`, `SupportedVersionsTests` and `InboundSiteReferenceTests`.  `dotnet test --project tests/SqlSource.Repository.Tests` runs those of that project, and `./pre-commit-validation.sh --full` runs them all.
- `tools/build-site.sh` gives no warning.
- The grep of `site/AGENTS.md` for internals, run over the range, has no hit that is not text of a message or SQL that a user writes.
- The word count of the range, before and after, is in the pull request's description and in a `#` comment line of `ledger/phase-23.tsv`.
- The search for the order of the outputs, run over the range as the diagnostics guide says, prints only text that the build or the tool prints.
- The description of the pull request holds what section 10 of the rewrite guide asks.  "Facts with no owner" is written as its section 8 says, in the pull request and in `ledger/phase-23.tsv`.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.

## For the reviewers

**The no-loss reviewer** reads each section old against new, with the checker's output for that section beside it.

- Start with `SQLSRC231` and `SQLSRC246`.  In `SQLSRC246`, check that each of the ten families of types still has its own fix and its own example of a key.
- In `SQLSRC228` and `232`, compare the types that are written as code spans, as sets.
- In `SQLSRC257`, check each condition under which two models are the same: the names, the C# types, the nullability, the order, the model type, and whether the properties are sorted.
- Read `SQLSRC243`, `244` and `245` for "not" and "only": "A change to a comment alone does not count", "The entry is not used".
- Check each `sql postgres` and `sql mssql` block: the info string and the bytes.  A block that changed fails no test if it still parses.
- Run the check for lines outside the range yourself.

**The accuracy reviewer** checks the reworded text against the source.

- The three paragraphs on severity against `SqlDiagnosticsTests.Configurable`: two warnings and one information, and no other section of the range.
- The versions: PostgreSQL 14 and SQL Server 2017 in `SQLSRC235`; Npgsql 8.0 in `SQLSRC247`; Npgsql 11 and 10 in `SQLSRC248`; Npgsql 8, 9 and 10 in `SQLSRC246`.
- What each error costs, section by section: under `repository` or `methods` the query gets no method, and under `models` the constant or token method is still generated.  `SQLSRC257` differs: the earlier query's model is still generated.
- Each sentence on one database on its own: a sentence true of PostgreSQL must not now read as true of both.
- The eleven "Database" lines against the text under them.

## Traps

- **`SQLSRC243` has no cause that a user can meet.**  It says that no dialect gets this error.  By the diagnostics guide it has no "Fix" part.  Keep what it says, as phase 1 left it.
- **`EXPLAIN` stands in the list of `SQLSRC233`.**  The grep for internals hits it.  It is SQL that a user writes, so it stays.  "Plan" and "describer" stay out of the text.
- **Do not copy the paragraph on severity from one section to another.**  `SQLSRC245` names no `.globalconfig` and has no sample.  `SQLSRC248` is information, and `WarningsNotAsErrors` does not apply to it.
- **Do not shorten the paragraph of `SQLSRC245` on a file that two projects include to a link.**  The reader has the warning and no other page.
- **The mended query of `SQLSRC253` to `258` and `260` is the sample of the fix.**  Do not move it before `**Fix:**`, and do not drop the `sql error` block before it.
- **`SQLSRC237` has an order.**  "First bring your database to the schema of the merged code" is step 1, not a note at the end.
- **`SQLSRC226` hands over to `SQLSRC262`**, which is in phase 24.  Keep what `SQLSRC226` says of it, and do not edit `SQLSRC262`.
- **`SQLSRC226` is the first section of the range and `SQLSRC260` the last.**  The line `## SQLSRC261` and all below it belong to phase 24.
- **A step that says "run `dotnet sqlsource describe` and commit the sidecar" is two steps.**  One instruction in a step.

## Calls made in this brief

- **`SQLSRC235` and `SQLSRC247` gain the link to `reference/databases/index.md#supported-versions` as the first link** of their `See:` lines.  The outline gives the link to this phase; the place in the line is this brief's call, since the table owns the number that the error enforces.
- **`SQLSRC243` has no "Fix" part.**  The outline's shape has a fix in every section, and this section says that no project gets the error.
- **The paragraph of `SQLSRC245` that restates a limit stays whole.**  A section stands alone.
- **The range does not move after the recount.**  The lines of phase 2 are new text, which no reviewer reads old against new, so they do not count against the 12,000 words of the outline.
- **This phase does not edit `site/AGENTS.md`.**  Four phases would collide on it.  Phase 26 lands the rule on the shape of a diagnostic section there, after the four have merged.
