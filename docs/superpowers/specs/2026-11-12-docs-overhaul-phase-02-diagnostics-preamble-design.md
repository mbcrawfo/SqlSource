# Docs overhaul, phase 02: diagnostics-preamble

[The outline](2026-11-12-docs-overhaul-epic-design.md) and [the content map](2026-11-12-docs-overhaul/content-map.md) are the authority; the map's table "the preamble of `site/diagnostics.md`" (section 4) is this phase's source.  [The phase 0 brief](2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) says how the ledger is written and how the checker is run.  This brief adds what is specific to phase 2.

Done by: an Opus agent.  Needs: phases 0 and 1.  Runs alone.

## Goal

`site/diagnostics.md` becomes its title, one paragraph and the 161 sections.  Each fact of the preamble that no section and no other page states gets a home first: the new page `site/errors.md`, "How errors are reported", or the section it is about.  Every section says who reports its error, and the 18 sections about describing a query say which database gives it, so that no section depends on a sentence that is gone.

## Where things stand

- **`site/diagnostics.md`** is as at the baseline: 3,707 lines and about 47,700 words.  Lines 1 to 346 are the preamble: the title (1), one general paragraph of 11 sentences (3), 29 index tables under 29 bold names (5-309), and 18 paragraphs about ranges of ids (311-345).  `## SQLSRC001` is line 347.  Phase 1 did not touch the page.
- **The sections.**  161, in id order.  Each is `## SQLSRCnnn`, a blank line, the bold title, then its text.  None has a "Reported by" or a "Database" line.  154 end with a `See:` line.
- **The links.**  The page has 261 links to itself.  204 are in the preamble (161 in the index rows, 14 in line 3, 29 in lines 311-345) and go with it.  57 are in the sections and stay.  The preamble has no `<a id>`, and nothing in the repository links into it: every link to the page names a `#sqlsrcNNN` anchor or the page alone.
- **`site/`** is flat.  `site/errors.md` does not exist.  `toc.yml` is one flat file.  `site/README.md` is the entry page, without "What works today" since phase 1.
- **The test**, `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, has four methods: `Document_Sections_AreTheDescriptorsInOrder`, `Document_EachSection_StartsWithItsDescriptorsTitle`, `Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor` and `Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`.
- **The descriptors**: 106 in `src/SqlSource/Diagnostics/SqlDiagnostics.cs` and 55 in `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (`SQLSRC200` to `236`, `262` to `266`, `271`, `285` to `296`).
- **`site/AGENTS.md`** is about 6,870 bytes after phase 1.  Two of its bullets are about the preamble: the one that starts "**`diagnostics.md`.**" and the one after it, "Keep true the paragraphs".

## In scope / Not in scope

In scope:

- `site/errors.md`, new, at the root of `site/`, with its entry in `site/toc.yml` and on the entry page.
- In `site/diagnostics.md`: the one paragraph, the two new lines of the sections, the facts of a range paragraph that a section lacks, and the removal of lines 3 to 345.
- Three sentences placed in `site/tool.md` and `site/settings.md`.
- `DiagnosticsDocumentTests`, and the lines of `site/AGENTS.md`, `src/SqlSource/Diagnostics/AGENTS.md`, `tests/README.md` and the outline that this phase makes wrong.

Not in scope:

- The words of a section as they stand today.  Phases 21 to 24 rewrite them.  This phase adds whole lines and changes none.
- A `## SQLSRCnnn` heading, its anchor or its bold title line.
- The seven sections with no `See:` line, and the link of `site/sql-files.md` 194 to `errors.md#what-an-error-costs`, which phase 3 adds.
- A table of the 161 titles.  The outline does not keep one.

## The work

### 1. Read every section against its range paragraph

Do this first, and keep the result for the reviewers: a table in the pull request's description is enough.  For each row of [the disposition table](#the-ledger) that names sections, read each section and note which facts of the paragraph it states already: where the error is reported, how often, and what it costs.  The map's rough scan found no sentence on where in about 60 sections and none on cost in about 100, and its spot check found these missing for certain: the build alone in `SQLSRC020`; "`SQLSRC209` or `SQLSRC210` instead" in `SQLSRC241`; "parameters before columns" in `SQLSRC246` and `SQLSRC249`; "the later of two queries" in `SQLSRC257`; "one for each type, each naming the other" in `SQLSRC274`; "once for each column and parameter" in `SQLSRC283` and `SQLSRC284`; how to configure the warning in `SQLSRC026`.

### 2. Write `site/errors.md`

Written new, in plain language (the outline's "The language"), about 2,000 words.  It has no rewrite phase.  Each h2 has its `<a id>` line above it.  Links to a section are `diagnostics.md#sqlsrcNNN`; links to other pages name them as they are today (`tool.md#exit-codes`).  Say "the build" and "the tool", as the sections' new lines do.

| Heading | Anchor | Holds | From the preamble | Check against |
|----|----|----|----|----|
| `# How errors are reported` | | One or two sentences: what the page is, and a link to `diagnostics.md` for the errors themselves | | |
| `## Errors, warnings and information` | `severity` | Every problem is an error that cannot be turned off or made a warning, but for eight ids, each a link: six warnings (`026`, `031`, `121`, `240`, `245`, `284`) and two of information (`248`, `268`).  Information is not in the output of `dotnet build` | Line 3, sentences 1 and 3 | `Configurable` in `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` (19-29) |
| `## Where an error is reported` | `where-an-error-is-reported` | In the build's output.  At its line and column in the `.sql` file, the sidecar or the settings file.  Which errors have no file and line, and which are at the project file | Line 3, sentences 2, 4, 8, 9; 311, sentence 2; 319, 321, 331, 335 | `src/SqlSource/Diagnostics/AGENTS.md` 16-20; `src/SqlSource/build/SqlSource.targets` 124, 503, 749, 756, 763 |
| `## What an error costs` | `what-an-error-costs` | What a file, a type or a query loses until the error is fixed: an error in how a `.sql` file is written (`101` to `120`, `126`); in where members go (`122` to `125`); `127`; an error about describing or a sidecar; `028` to `034` but `031`, and `036`; that a query gets every error it has.  A link to `tool.md#sidecar-files` for what the build checks while a file has an error of its own | Line 3, sentences 5, 6, 7; 311, sentence 4; 329, sentence 5 | The sections named; `tests/SqlSource.Tests/Generator/` |
| `## Who reports an error: the build, the tool, or both` | `who-reports` | A table of three groups, below | 311, sentences 2, 7 (first clause), 8, 9, 10, 11-12 (one row), 13 | The lists of ids in step 3 |
| `## The tool's errors` | `the-tools-errors` | The tool prints its errors itself, in the format of a build error, each with a `see:` line that links its section.  Links to `tool.md#exit-codes` and `tool.md#running-in-the-build` | Line 3, sentence 10, first half | `tests/SqlSource.Tool.Tests/Expected.cs` 9 |
| `## Errors by area` | `errors-by-area` | A table of 29 rows: the area's name, its ids, each a link, who reports them, and where.  No titles | The 29 bold names of 5-309; 311, sentences 1, 3, 6; 313, sentence 1; 315, sentence 1; the first sentence of 317 to 335 | `git show 28e20e3c:site/diagnostics.md \| awk 'NR<=309' \| grep '^\*\*'` prints the 29 names |

The row of 311, sentences 11 and 12, in `#who-reports`: the tool reports the errors of a settings file (`015` to `019`) too, and `SQLSRC019` for `dialect`, `output` and `database` alone; the build reports it for every key but `database`.  Write "checks", not "reads", of `database`: C-01.

The page holds no reading rule for the 18 sections (C-04), no status, and no table of titles.

### 3. Add the two lines to the sections

Directly under the bold title line, each as a paragraph of its own, in this order:

```markdown
## SQLSRC227

**Parameter's first use does not fix its type**

Reported by: the tool.

Database: PostgreSQL.

PostgreSQL takes a parameter's type from its first use. ...
```

**"Reported by"**, in all 161 sections, in one of three forms, word for word:

| The line | Sections | Which |
|----|----|----|
| `Reported by: the build.` | 80 | Every descriptor of `SqlDiagnostics.cs` but those of the third row |
| `Reported by: the tool.` | 53 | Every descriptor of `ToolDiagnostics.cs` but `SQLSRC209` and `SQLSRC210` |
| `Reported by: the build and the tool.` | 28 | `SQLSRC015` to `019`; `SQLSRC101` to `120`; `SQLSRC126`; `SQLSRC209`; `SQLSRC210` |

Where the value comes from: the class that holds the descriptor, and line 311 of the preamble for the 28.  The code behind line 311: `IsReportedByTheCompiler` in `src/SqlSource.Tool/PlanSteps.cs` (281) for `209` and `210`; `SqlDiagnostics.ForParseError` (1537) and `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs` for the ids that the tool reports under the generator's; `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs` compares the two.  `SQLSRC121`, `122` to `125` and `127` are parser or placement ids that the build alone reports, and `SQLSRC020` is the build's alone.

**"Database"**, in the 18 sections `SQLSRC224` to `236` and `262` to `266`, in one of three forms: `Database: PostgreSQL.`, `Database: SQL Server.`, `Database: PostgreSQL and SQL Server.`  The line names the database that gives the error.  The descriptors do not say it; the files that make the errors do.  What they show at the baseline, for you to confirm:

| Database | Sections | Made in |
|----|----|----|
| PostgreSQL and SQL Server | `224`, `225`, `226`, `228`, `231`, `235` | Both `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs` and `src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs`; `224`, `228` and `235` through `Describing/Describers/DescribeFailures.cs`, which both call |
| PostgreSQL | `227`, `229`, `230`, `232`, `233`, `234`, `236` | `PostgresErrors.cs` alone |
| SQL Server | `262`, `263`, `264`, `265`, `266` | `SqlServerErrors.cs` alone |

Where a section's own text says otherwise, the code decides: tell the reviewers, and leave the section's text to its rewrite phase unless it is false, which is a `corrected` row.

**The facts of a range paragraph that a section lacks** (step 1) go in a paragraph of their own under the two lines, in plain language, before the section's text of today.  Add a fact only where the section does not state it.  Never put words into a line of today: the checker then shows the old line as gone.

### 4. Place three sentences on other pages

| From | The words | To | How |
|----|----|----|----|
| Line 315, sentences 5 and 6 | "A sidecar that exists is checked even when no query of its file needs an entry.  While a `.sql` file has an error of its own, ... and reports no entry of it as one that names no query." | `site/tool.md`, the section `sidecar-files` (131-168) | Word for word, as a paragraph of its own.  Its three links become `diagnostics.md#sqlsrc126`, `#sqlsrc237` and `#sqlsrc240` |
| Line 333, last sentence | "A project whose output is `models` uses no converter, and gets none of them at a query, and neither does a query whose database's driver the project does not reference, which [SQLSRC273] is about." | `site/settings.md`, the section `converters` (471-526) | Only what the section does not say.  Line 524 says that converters apply only in query methods.  It does not name the errors or the driver.  Place the sentence word for word, with "them" made "`SQLSRC277` to `SQLSRC280`", and its link as `diagnostics.md#sqlsrc273` |

Phases 5 and 7 move these on with their sections.

### 5. The paragraph, and the removal

Under `# Diagnostics` stands one paragraph, one line, of these four statements in this order:

1. Each section of the page is one `SQLSRC` diagnostic, under its id.
2. The help link of a build error opens the section of that error.
3. How errors are reported, and the list of the errors by area, are on the page "How errors are reported".  This is the paragraph's one link, `[How errors are reported](errors.md)`.
4. The samples of the tool's errors on the page leave out the `see:` line that the tool prints.

It holds no index, no range of ids, no rule about severity or about a database, and no status.  Then remove lines 3 to 345 of the baseline.  The page is then `# Diagnostics`, a blank line, the paragraph, a blank line, `## SQLSRC001`.

### 6. The sidebar and the entry page

- `site/toc.yml` gains an entry before "Diagnostics": `name: How errors are reported`, `href: errors.md`.  `SiteTocTests` fails without it.
- `site/README.md` gains a bullet under "The pages", before the bullet of Diagnostics, and one row under "Find what you need", after "Look up a build error or a tool error".  The rule of `site/AGENTS.md` on a new page asks for both, and it is in force until phase 3.  Both are `new` rows.  The table then has 50 rows: the 49 of the baseline and this one.

### 7. `DiagnosticsDocumentTests`

| Method | Change | What fails it |
|----|----|----|
| `Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor` | Goes, with its comment (44-55) | |
| `Document_Sections_AreTheDescriptorsInOrder`, `Document_EachSection_StartsWithItsDescriptorsTitle`, `Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter` | None.  The title is still the first line of a section that is not blank | |
| New: the head of the page | | The page is not `site/diagnostics.md`.  More than the title and one paragraph stands before the first `## ` heading: the lines that are not blank must be two, `# Diagnostics` and a line that is no heading, no table row and no list item |
| New: the help link | | `SqlDiagnostics.HelpLinkBase` is not `UserDocs.SiteUrl + "diagnostics.html#"` |
| New: "Reported by" | | In a section, the first line under the title that is not blank is not one of the three lines.  The line of a descriptor of `ToolDiagnostics.All` does not name the tool.  The line of a descriptor of `SqlDiagnostics.All` does not name the build |
| New: "Database" | | A section of `SQLSRC224` to `236` or `262` to `266` has no "Database" line of the three forms under its "Reported by" line.  Another section has a line that starts `Database:` |
| New: the table of areas | | An id of a descriptor is not linked, as `diagnostics.md#sqlsrcNNN`, from exactly one row of the table under `errors-by-area` in `site/errors.md`, or the links of the table are not in id order |

The class comment (15-16) says what the tests now keep.  The comment at 57-60 names `site/settings.md` and stays: phase 7 changes it.  Whether a "both" line is right is not a test's to say: the reviewer checks the 28.

### 8. The rules

| File | Bullet | Becomes |
|----|----|----|
| `site/AGENTS.md` | The one that starts "- **`diagnostics.md`.**" | "- **`diagnostics.md`.**  A new diagnostic gets a `## SQLSRCnnn` section whose first bold line is its descriptor's title, then its "Reported by" line, a "Database" line when it is an error of describing a query against its database, and, where a manual section documents the rule, a closing `See: [section](page.md#anchor)` line.  Its id joins its area's row in `errors.md#errors-by-area`." |
| `site/AGENTS.md` | The next one, "- Keep true the paragraphs ..." | "- Only the title and one paragraph stand before `## SQLSRC001`.  Never give a heading another anchor: every error's help link is the lower-case id's slug." |
| `src/SqlSource/Diagnostics/AGENTS.md` | Line 6, its third place | "`site/diagnostics.md` and its id in the table of areas of `site/errors.md`, which tests check;" |
| `tests/README.md` | Line 13 | After "the sections of `site/diagnostics.md`": " and the table of areas of `site/errors.md`" |
| The outline | The table "Tests", the row of `DiagnosticsDocumentTests` | It names the tests of the two lines and of the table of areas |

Measured on these words, `site/AGENTS.md` comes to about 7,020 bytes of 8,192, and the longer bullet has 59 words.  Measure again.  Root `AGENTS.md` does not change: its line on `site/diagnostics.md` stays true.

## The ledger

`ledger/phase-02.tsv` gives every unit of `site/diagnostics.md` a row.  Lines 3 and 311 to 345 are `move-split`, one part for each sentence or group of sentences, with the sentence's words in `words`.  Lines 5 to 309 are one `move-split` for the range, with two parts whose `words` is `*`: the names, and the rows.  A part whose fact is written new has `move` with `fact`.  "Stated by" below is the note of a `removed-preamble` row: the place that already says it, which you confirm.

| Line | Sentences | Disposition | To, or stated by |
|----|----|----|----|
| 1 | The title | `move` | `site/diagnostics.md` |
| 3 | 1 | `move`, `fact` | `site/errors.md#severity` |
| 3 | 2, 4, 8, 9 | `move`, `fact` | `site/errors.md#where-an-error-is-reported` |
| 3 | 3 | `restated-from` `site/diagnostics.md#sqlsrc248` | `site/errors.md#severity`, one sentence |
| 3 | 5, 7 | `move`, `fact` | `site/errors.md#what-an-error-costs` |
| 3 | 6 | `move`, `fact` | `site/errors.md#what-an-error-costs`, and section `SQLSRC127` |
| 3 | 10, first half | `move`, `fact` | `site/errors.md#the-tools-errors` |
| 3 | 10, second half | `move`, `fact` | `site/diagnostics.md`, the paragraph |
| 3 | 11 | `removed-preamble` | Stated by `site/tool.md#running-in-the-build`, "Errors" (232) |
| 5-309 | The 29 bold names | `move`, `fact` | `site/errors.md#errors-by-area` |
| 5-309 | The 29 header rows and the 161 rows | `removed-preamble` | Each title is the bold line of its section |
| 311 | 1, 6 | `move`, `fact` | `site/errors.md#errors-by-area` |
| 311 | 2 | `move`, `fact` | `site/errors.md#who-reports`; sections `021`, `024` to `027` |
| 311 | 3 | `move`, `fact` | `site/errors.md#errors-by-area`; sections `028` to `037` |
| 311 | 4 | `move`, `fact` | `site/errors.md#what-an-error-costs`; sections `028` to `034` but `031`, and `036` |
| 311 | 5 | `removed-preamble` | Stated by `site/settings.md#repository-interfaces` and by sections `035` and `037` |
| 311 | 7, first clause; 8; 9; 10; 13 | `move`, `fact` | `site/errors.md#who-reports`; 13 also section `020` |
| 311 | 7, the rest | `removed-preamble` | Stated by `site/errors.md#the-tools-errors` and `site/tool.md#exit-codes` |
| 311 | 11, 12 | `removed-preamble` | Stated by `site/tool.md#which-queries` (50); one row of `#who-reports` keeps it |
| 311 | 14 | `removed-preamble` | Stated by `site/tool.md#running-in-the-build` (235) |
| 313 | 1 | `move`, `fact` | `site/errors.md#errors-by-area` |
| 313 | 2 | `corrected`, `C-04` | The "Database" line of the 18 sections.  `words` is `-` |
| 313 | 3 | `removed-preamble` | Stated by `site/dialects.md#database-support` |
| 315 | 1 | `move`, `fact` | `site/errors.md#errors-by-area` |
| 315 | 2, 3 | `removed-preamble` | Stated by `site/settings.md#setting-output` |
| 315 | 4 | `move`, `fact` | Section `SQLSRC241` |
| 315 | 5, 6 | `move` | `site/tool.md#sidecar-files`, word for word |
| 317 | 1, 3 | `move`, `fact` | `site/errors.md#errors-by-area`; sections `246` and `249` |
| 317 | 2 | `removed-preamble` | Stated by `site/generator.md#property-types` |
| 319 | 1, 3 | `move`, `fact` | `site/errors.md#errors-by-area`; sections `247` and `248` |
| 319 | 2 | `removed-preamble` | Stated by `site/postgresql.md#which-npgsql` |
| 321, 323 | All | `move`, `fact` | `site/errors.md#errors-by-area`; sections `267`, `268`; `269`, `270` |
| 325 | 1, 2 | `move`, `fact` | `site/errors.md#errors-by-area`; sections `250` to `252` |
| 325 | 3 | `removed-preamble` | Stated by `site/settings.md#setting-type-overrides` |
| 327 | All | `move`, `fact` | Section `SQLSRC261` |
| 329 | 1 | `move`, `fact` | `site/errors.md#errors-by-area` |
| 329 | 2 | `removed-preamble` | Stated by `site/generator.md#property-names` |
| 329 | 3, 4 | `move`, `fact` | Sections `253` to `260`, and `251` |
| 329 | 5 | `move`, `fact` | `site/errors.md#what-an-error-costs` |
| 331 | All | `move`, `fact` | `site/errors.md#errors-by-area`; sections `272` to `276` |
| 333 | 1 to 5 | `move`, `fact` | `site/errors.md#errors-by-area`; sections `277` to `280` |
| 333 | 6 | `move` | `site/settings.md#converters`, word for word but for "them" |
| 335 | All | `move`, `fact` | `site/errors.md#errors-by-area`; sections `281` and `282` |
| 337, 339, 341, 343, 345 | All | `move`, `fact` | Sections `283`; `284`; `297`; `298`; `299` |
| 347-3707 | The 161 sections | `move` | `site/diagnostics.md`: one row for the range |

A fact that a section states already needs no new sentence: its part is then `restated-from` that section, with `to` `-`.  The `new` rows: the title and opening of `site/errors.md`; three statements of the paragraph; "the Reported by line, 161 sections"; "the Database line, 18 sections"; the bullet and the row of `site/README.md`.

The checker must show:

- **"check coverage"** passes for phases 0 to 2: every unit of `site/diagnostics.md` has a row.
- **"check move 02"**, list 1, is empty.  That proves that each of the about 1,330 units of lines 347 to 3,707 is still in the page word for word, and that the sentences of step 4 stand under their anchors.
- **List 2, left over**, holds 238 units, each with its row: line 3, the 29 bold names, the 29 header rows, the 161 rows, and the 18 paragraphs.  A unit of a section in this list is a line of today that you changed: put it back.
- **Under list 2, for lines 3 and 311 to 345**, the words that no part holds are punctuation and joining words alone.  A clause there is a sentence without a disposition.
- **List 3, added**, holds every unit of `site/errors.md`, and in `site/diagnostics.md` the paragraph, 161 "Reported by" lines, 18 "Database" lines and the paragraphs of step 3.  It holds one or two units of `site/tool.md`, at most one of `site/settings.md`, and two of `site/README.md`.
- **List 4** is empty: the preamble has no `<a id>`.
- **"check rewrite"** does not apply.

## Done when

- [ ] `sed -n '1,5p' site/diagnostics.md` prints `# Diagnostics`, a blank line, one paragraph, a blank line and `## SQLSRC001`.
- [ ] `grep -c '^## SQLSRC' site/diagnostics.md` prints 161.  `grep -c '^Reported by: ' site/diagnostics.md` prints 161, and `grep -c '^Database: ' site/diagnostics.md` prints 18.
- [ ] `grep -c '^Reported by: the build\.$'`, `'^Reported by: the tool\.$'` and `'^Reported by: the build and the tool\.$'` print 80, 53 and 28.
- [ ] `grep -c '^| \[SQLSRC' site/diagnostics.md` prints 0.
- [ ] `grep -o '](#' site/diagnostics.md | wc -l` prints 57 and the same-page links of the lines you added, and no more.
- [ ] `grep -o 'diagnostics.md#sqlsrc[0-9]*' site/errors.md | sort -u | wc -l` prints 161.
- [ ] `grep -c '<a id=' site/errors.md` prints 6, and the six are `severity`, `where-an-error-is-reported`, `what-an-error-costs`, `who-reports`, `the-tools-errors`, `errors-by-area`.
- [ ] `git diff --stat <base> -- site` names `diagnostics.md`, `errors.md`, `toc.yml`, `README.md`, `tool.md`, `settings.md` and `AGENTS.md`, and no other file.
- [ ] The grep of `site/AGENTS.md` for internals, run on `site/errors.md`, prints nothing.  `StatusLanguageTests` passes.
- [ ] `DiagnosticsDocumentTests` has no method that reads the index, and its five new tests pass.  Each was seen to fail first: on the page with its preamble, or with one line changed.
- [ ] `wc -c site/AGENTS.md` is under 8,192.
- [ ] "check coverage" and "check move 02" show what [The ledger](#the-ledger) says.
- [ ] The word count of `site/errors.md`, and of `site/diagnostics.md` before and after, are in the pull request's description.  Phases 21 to 24 recount from them.
- [ ] `tools/build-site.sh` passes.  The content reviews, a clean `branch-review:review` and `./pre-commit-validation.sh --full` are of the last commit.

## For the reviewers

**The no-loss reviewer** reads the preamble at `git show 28e20e3c:site/diagnostics.md`, lines 1 to 346, sentence by sentence, against the ledger.

- Each sentence has a part.  The checker's list of words that no part holds is the proof.
- Each `removed-preamble` row names a place.  Open it and find the fact there, in today's words.  The six that the planning found in no other place must not be among them: the general rule of severity; the reading rule of the 18 sections; that the samples leave the `see:` line out; which ids are the tool's, and that the tool reports `101` to `120` and `126` under the generator's ids; the two sentences on a sidecar that is checked; the 29 names.
- Each `move`, `fact` row that names sections: the fact is in each section, in its new paragraph or in its text of today.  Read these first: `020`, `026`, `127`, `241`, `246`, `249`, `250`, `257`, `274`, `283`, `284`.
- The 29 names of the table of areas are the 29 bold names, and each area's ids are the ids of its old table.

**The accuracy reviewer** checks the new text against the source, not against the preamble alone.

| Statement | Source |
|----|----|
| The eight ids and their severity | `SqlDiagnosticsTests.Configurable`; the `defaultSeverity` and the tags of each descriptor |
| The 55 ids of the tool | `ToolDiagnostics.All` |
| The 28 that both report | `PlanSteps.IsReportedByTheCompiler`; `SqlDiagnostics.ForParseError`; `SettingsFileDiagnostics`; `GeneratorParityTests` |
| The build alone, at the project file | The `Code="SQLSRC021"`, `024`, `025`, `026` and `027` tasks of `src/SqlSource/build/SqlSource.targets` |
| No file and line; once for the project | `src/SqlSource/Diagnostics/AGENTS.md`, "Diagnostics with no position" |
| Each "Database" line | The three files of step 3, and the section's own text |
| What an error costs | The tests of `tests/SqlSource.Tests/Generator/` for the ids named, and the sections |
| `database` is checked by the tool alone, and read by the build too | C-01: `MethodPlanner.DatabaseOf` |

The reviewer also confirms that `site/errors.md` names no internal thing: no descriptor, no class, no "generator's ids".

## Traps

- **261 is the page, not the preamble.**  The page has 261 links to itself, and the 204 of the preamble go.  The 57 of the sections stay, and a count of 0 after the phase means that you cut links out of the sections.
- **The title must stay the first line of a section.**  `Document_EachSection_StartsWithItsDescriptorsTitle` takes the first line that is not blank.  The new lines go under the title, never above it.
- **A blank line between the title and each new line.**  Without it Markdown joins them into the title's paragraph.
- **A script that adds 161 lines** must not touch another byte: no changed line ends, no trailing space.  In `git diff --numstat`, the sections gain 358 lines for the two lines with their blank lines, and the paragraphs of step 3, and lose none.
- **A link that worked on the page breaks off it.**  `(#sqlsrc237)` in a sentence that moves to `site/tool.md` or to `site/errors.md` must become `(diagnostics.md#sqlsrc237)`.  docfx and `UserDocsLinksTests` catch it.
- **"Generator" and "descriptor" are words of the code.**  The preamble says "under the generator's ids".  On the new page say what the reader sees: the same id, from the tool.
- **`SQLSRC019` and `database`.**  The tool reports it for `dialect`, `output` and `database`; the build for every key but `database`.  Do not write that only the tool reads the setting.
- **A fact stated twice.**  Where a section already says where its error is reported, do not say it again under "Reported by": phases 21 to 24 would have to choose.
- **`site/README.md` goes in phase 3.**  The bullet and the row you add move with it.  Keep each to one line.
- **Rebase before you push.**  A feature branch that adds a diagnostic adds a row to the index you remove and a section you must give its lines.  The tests of step 7 fail until it has them.

## Left to the agent

- The words of `site/errors.md`, of the paragraph and of each new paragraph of a section.
- The columns of the table of `#who-reports`, and whether the "where" of an area is a cell or a sentence under the table.
- Whether the 161 lines are added by a script or by hand.
- The names of the new test methods.
- Where a sentence of step 4 stands inside its section.

## Calls made in this brief

- **The lines are `Reported by: the build.` and `Database: PostgreSQL.`**: a plain label, a colon, the value and a full stop, each a paragraph of its own.  The map gives the two names and no form.  The diagnostics guide of phases 21 to 24 shows the same forms.  A bold label would stand beside the bold title, which a test and a rule single out.
- **Three values for "Reported by", with "the tool" and not "the `sqlsource` tool"**, so that the line reads the same in every section.
- **New facts are whole paragraphs under the two lines**, never words inside a line of today.  The checker then proves that no line of a section changed, and the rewrite finds the new text in one place.
- **A test holds the two lines and the table of areas.**  The outline names tests for the head of the page and the help link alone.  The test of the index goes, and without a test nothing keeps a new diagnostic in the table of areas or gives it its lines.
- **The table of areas links each id**, 161 links, and no range such as "`SQLSRC001` to `SQLSRC014`", so that a test can count them.
- **The "Database" values come from the describers' error files**, `PostgresErrors.cs`, `SqlServerErrors.cs` and `DescribeFailures.cs`, which the map's C-04 names as its source.  The descriptors of `ToolDiagnostics.cs` do not say which database gives an error.
- **The entry page gains a bullet and a row** for the new page, since the rule of `site/AGENTS.md` asks it until phase 3.  Phase 3 moves both with the page.
- **`site/sql-files.md` 194 keeps its link to `diagnostics.md`.**  The map's section 6.4 says so, and phase 3 adds the link to `errors.md#what-an-error-costs` when it moves the sentence.
- **`src/SqlSource/Diagnostics/AGENTS.md` names the table of areas**, since its rule lists the places a new diagnostic touches.
- **The last sentence of line 333 is placed**, with "them" made the four ids.  The section says that converters apply only in query methods, and says neither of the other two things.
