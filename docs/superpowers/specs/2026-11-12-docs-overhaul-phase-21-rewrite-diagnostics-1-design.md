# Docs overhaul, phase 21: rewrite-diagnostics-1

Phase 21 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md).  Read the outline, the [content map](2026-11-12-docs-overhaul/content-map.md) (its entry for `diagnostics.md` in section 3, and section 10), [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md) and [`diagnostics-guide.md`](2026-11-12-docs-overhaul/diagnostics-guide.md) before this brief.  This brief holds only what is special to its range.

- Done by: an Opus agent.
- Needs: phase 11.
- Runs beside: phases 12 to 24.

## Goal

The sections `SQLSRC001` to `SQLSRC121` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide.  Every fact of today's sections is still there.  No line outside the range changes, and no other file of `site/` changes.

## The range

- **Ids:** `SQLSRC001` to `SQLSRC121`.  The range starts at the line `## SQLSRC001` and ends on the line before `## SQLSRC122`.  The ids have gaps: there is no `006`, `010`, `011` or `014`, and none from `039` to `100`.
- **Baseline lines:** 347 to 1457 of `git show 28e20e3c:site/diagnostics.md`.  The units of the inventory in that range are yours.
- **Size:** 55 sections and 10,928 words at the baseline, counted from each heading to the next.  Phase 2 added a line or two to each section, so count again when you start, with the command of the diagnostics guide.
- **Areas**, by their names on `reference/errors.md#errors-by-area`:

| Area | Ids | Sections |
|----|----|----|
| Project and attribute | 001 to 013 | 10 |
| Settings files | 015 to 020 | 6 |
| The compiler | 021 | 1 |
| Projects that share SqlSource's exceptions | 022, 023 | 2 |
| A build that runs the tool | 024 to 027 | 4 |
| Repositories | 028 to 037 | 10 |
| Projects that share SqlSource's exceptions across frameworks | 038 | 1 |
| `.sql` files | 101 to 121, the first 21 of the area | 21 |

- **Who reports them:** the build reports every one.  The tool reports `SQLSRC015` to `019` and `SQLSRC101` to `120` too.  Every other section of the range is the build's alone, `SQLSRC020` and `SQLSRC121` among them.  The "Reported by" line of each section says so already.
- **No section of the range has a "Database" line.**

## Specific to this range

**The longest and hardest sections:**

| Id | Words | What makes it hard |
|----|----|----|
| `SQLSRC111` | 1,137 | One error for every kind of marker.  Twelve bullets and five more paragraphs, each a list of the values that a marker takes.  It holds 189 code spans, and each is a fact.  A `sql error` block stands between the third and fourth bullets |
| `SQLSRC019` | 993 | The same for the keys of a settings file: 173 code spans.  It lists the names of the dialects, and `site/AGENTS.md` sends a new dialect to this list |
| `SQLSRC112` | 553 | Ten kinds of conflict as bullets.  Its last paragraph lists what is not a conflict |
| `SQLSRC120` | 397 | Three causes, each with a `sql error` block of its own.  "The second and third are errors only for a type that keeps the query's comments" refers to their order |
| `SQLSRC121` | 377 | A warning.  Four fenced blocks.  The rule on the server's collation has three cases |
| `SQLSRC028` | 343 | The fix is five bullets, each for one situation of the type |
| `SQLSRC029` | 305 | Three fenced blocks.  The second `csharp` block shows a case that is allowed, not the mistake |
| `SQLSRC022` | 273 | The fix is a true procedure: add a shared project, grant `InternalsVisibleTo`, reference it |
| `SQLSRC009` | 239 | One paragraph of 153 words on the names that a class cannot share with a query, under `method-type` and `async-method-suffix` |
| `SQLSRC013` | 242 | Two causes, each with its own fix.  The `xml` block belongs to the second |

**Lists that a test or a rule reads:**

- `SQLSRC109` lists the generator parameters.  `DiagnosticsDocumentTests.Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter` needs each of its twelve words in backticks: `keep-comments`, `token-validation`, `sort-input`, `sort-output`, `table-models`, `async-method-suffix`, `array-pool`, `list-padding`, `collection-method`, `transaction`, `method-type`, `default`.
- `SQLSRC016` lists the 18 keys of a settings file.  They are the keys of `schemas/settings-v1.schema.json`.
- `SQLSRC019` and `SQLSRC111` keep their lists of values.  The reference pages own the rules, and the sections restate the values, because the reader has only this section.

**Lists of outputs:** three lines of the range name an output before `repository`.  `SQLSRC009` says "`sql` or `models`" twice: write "`models` or `sql`".  `SQLSRC019` and `SQLSRC111` each say "`output` takes `sql`, `models`, `methods` or `repository`".  By the diagnostics guide, `repository` comes first unless the message prints the values in that order: read what each of the two messages ends with for `output`, in `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs` and `SqlDiagnostics.ForParseError`, before you change it.  The other lists of values of those two sections keep their order.

**The table:** `SQLSRC116` has the one table of the range, 17 rows of where a marker can stand.  It is the third example of the diagnostics guide.  It must agree with the marker index of `reference/sql-files.md#marker-index`.

**Samples:** 71 fenced blocks: 20 `console`, 15 `csharp`, 6 `jsonc`, 4 `xml`, 1 `ini`, 1 `text`, 22 `sql error` and 2 `sql`.  Five sections have none: `SQLSRC003`, `008`, `021`, `104` and `110`.  This brief lists no block to change.

**`See:` lines:**

- Three sections have none: `SQLSRC003`, `SQLSRC012` and `SQLSRC021`.  Give each `See: [What the build needs](reference/msbuild.md#build-requirements)`, with the link text of the heading as it stands on that page.  That section owns .NET 8, C# 12, the .NET SDK 9.0.300 and Visual Studio 2022 17.14.
- The map changes no other `See:` line of the range beyond the page of each link, which the move phases did.
- `SQLSRC022` and `SQLSRC038` link the guide `guides/several-projects.md#share-the-exceptions` in their text.  That is right: the `See:` line of each goes to `reference/exceptions.md#projects-that-share-internals`.

**Warnings:** three of the eight diagnostics that are not errors are here.

| Id | What its section says today | Source |
|----|----|----|
| `SQLSRC026` | It is a warning.  It stays one under `-warnaserror`, and `TreatWarningsAsErrors` does not reach it.  `SqlSourceOnline` `false` stops it.  Phase 2 added how a project configures it | `SqlDiagnostics.OnlineToolCannotRunOffline` and its comment; `MSBuildWarningsNotAsErrors` in `src/SqlSource/build/SqlSource.targets` |
| `SQLSRC031` | "This is a warning, since ...", `NoWarn`, `WarningsNotAsErrors`, one `xml` block | `SqlDiagnostics.cs`, the descriptor of `SQLSRC031` |
| `SQLSRC121` | The same, with an `xml` block and an `ini` block for `.globalconfig` | `SqlDiagnostics.cs`, the descriptor of `SQLSRC121` |

**Sources of the descriptors and the texts:**

- `src/SqlSource/Diagnostics/SqlDiagnostics.cs` holds all 55 descriptors.  None of the range is in `ToolDiagnostics.cs`.
- `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs` writes the ends of the messages of `SQLSRC015` to `020`.
- `src/SqlSource/build/SqlSource.targets` raises `SQLSRC021` and `SQLSRC024` to `027`.  Search it for `Code="SQLSRC`.
- `SqlDiagnostics.ForParseError` and `src/SqlSource/Parsing/SqlParseErrorKind.cs` give `SQLSRC101` to `121`.
- The values that `SQLSRC019` and `SQLSRC111` list: `src/SqlSource/Settings/SettingValue.cs`, `src/SqlSource/Settings/GeneratorParameters.cs`, `src/SqlSource/Parsing/SqlDialectName.cs` and `src/SqlSource/Parsing/Markers/`.
- What an error costs and where it points: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`, `RepositoryDiagnosticsTests.cs` and `FileDiagnosticsTests.cs`, and `tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs`.

## Done when

- Each of the 55 sections has the one shape.  Each has its fix and ends with a `See:` line: the two counts of the diagnostics guide, of headings and of `See:` lines over the range, both print 55.
- The check of the diagnostics guide, with `## SQLSRC001` and `## SQLSRC122`, prints nothing: no line outside the range changed.
- `git diff --stat` against the merge base names `site/diagnostics.md`, the phase's ledger file, and the outline if the plan changed.  It names no other file.
- "check rewrite 21" exits with 0 on the last commit.  Every section of the range has a row in `ledger/phase-21.tsv`, and each difference that the command prints has a true note.  "check coverage" still passes.
- The three new `See:` lines are `new` rows of the ledger.
- The tests that read this page pass: `DiagnosticsDocumentTests`, `UserDocsSamplesTests`, `UserDocsLinksTests`, `StatusLanguageTests`, `SiteStructureTests` and `InboundSiteReferenceTests`.  `dotnet test --project tests/SqlSource.Repository.Tests` runs those of that project, and `./pre-commit-validation.sh --full` runs them all.
- `tools/build-site.sh` gives no warning.
- The grep of `site/AGENTS.md` for internals, run over the range, has no hit that is not text of a message.
- The word count of the range, before and after, is in the pull request's description and in a `#` comment line of `ledger/phase-21.tsv`.
- The search for the order of the outputs, run over the range as the diagnostics guide says, prints only text that the build or the tool prints.
- The description of the pull request holds what section 10 of the rewrite guide asks.  "Facts with no owner" is written as its section 8 says, in the pull request and in `ledger/phase-21.tsv`.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.

## For the reviewers

**The no-loss reviewer** reads each section old against new, with the checker's output for that section beside it.

- Start with `SQLSRC019`, `111` and `112`.  Compare the code spans as sets: a value lost from a list is the likely loss.
- Compare the 17 rows of the table of `SQLSRC116` cell by cell.
- Read the last paragraph of `SQLSRC112`, and `SQLSRC114` and `120`, for "not" and "only".
- Check that each `sql error` block still has that info string and its bytes.
- Run the check for lines outside the range yourself.

**The accuracy reviewer** checks the reworded text against the source.

- Each list of values of `SQLSRC016`, `019`, `109` and `111` against the source files named above.
- The three paragraphs on severity against `SqlDiagnosticsTests.Configurable`.  No other section of the range can say "warning" of itself.
- `SQLSRC024` to `027` against the texts in `SqlSource.targets`.
- The costs in `SQLSRC028` to `037`: which leave the repository queries without a method, and that `SQLSRC035` and `037` cost only the interface.  Check `RepositoryDiagnosticsTests.cs`.
- The three new `See:` lines: the anchor exists, and the section owns the numbers.

## Traps

- **Do not turn `SQLSRC019` or `SQLSRC111` into a link to `reference/`.**  The reader has the error and no other page.  Rewrite the sentences around the lists, and keep the lists.
- **Do not copy the `NoWarn` paragraph of `SQLSRC031` into `SQLSRC026`.**  The build raises `SQLSRC026`, not the compiler, and it is configured another way.  Keep what phase 2 wrote.
- **Do not change an info string.**  A `sql error` block marked `sql` is parsed and fails the test.  A `sql` block marked `sql error` is no longer parsed, and no test tells you.
- **`SQLSRC008` and `SQLSRC104` look alike and differ.**  One is a name used in two files of one type, and the other is a name used twice in one file.  Do not make their texts the same.
- **Do not fill the gaps in the ids.**  A section exists only for a descriptor.
- **`SQLSRC121` is the last section of the range.**  The line `## SQLSRC122` and all below it belong to phase 22.
- **A fix that is a choice is not a numbered list.**  `SQLSRC007` gives several ways out: write them as bullets under "do one of these".
- **The order of the three causes of `SQLSRC120` is a fact**, since the text refers to "the second and third".

## Calls made in this brief

- **The `See:` line of `SQLSRC003`, `012` and `021`** goes to `reference/msbuild.md#build-requirements`.  The map says that the rewrite gives each of the seven sections a `See:` line and names no target.  Section 5 of the map names that section as the owner of these numbers.
- **The range does not move after the recount.**  The lines of phase 2 are new text, which no reviewer reads old against new, so they do not count against the 12,000 words of the outline.
- **This phase does not edit `site/AGENTS.md`.**  Four phases would collide on it.  Phase 26 lands the rule on the shape of a diagnostic section there, after the four have merged.
