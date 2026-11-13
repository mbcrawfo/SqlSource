# Docs overhaul, phase 12: rewrite-guides

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 12.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The old text of 18 guides is in plain language, and each of the 18 pages has the fixed shape of a guide.  The merge pairs MP-12 and MP-14 to MP-22 are merged.  A reader can finish the task of each page without a click.  No fact of the old text is lost.

## The pages

All are in `site/guides/`.  "Words" is the old text on the page (section 10 of the map): 8,430 in all.  Phase 9 wrote the rest of each page, and `nullability.md` and `output-modes.md` whole, so those two are not in this phase.  One limit is in this phase, `dapper.md#limit-parameter-names`.  No page is near 3,500 words.

| Page | Words | Sections that hold old text | Merge pairs |
|----|----|----|----|
| `dapper.md` | 2,350 | `models`, `dapper-and-adonet`, `models-through-dapper`, `dapper-with-postgresql`, `dapper-with-sql-server`, `sql-only`, `limit-parameter-names` | MP-12, MP-22 |
| `report-a-bug.md` | 970 | `report-a-bug`, `raw-bundle` | MP-19 |
| `json.md` | 550 | `turn-json-on`, `class-in-a-json-column` | MP-20 |
| `troubleshooting.md` | 490 | `invalidcastexception` | |
| `several-projects.md` | 470 | `test-project-sees-internals`, `share-the-exceptions` | MP-18 |
| `describe-on-save.md` | 460 | `start-the-watcher-with-your-editor`, `vs-code`, `rider`, `visual-studio` | |
| `folders-and-files.md` | 460 | `configure-a-folder`, `leave-migration-scripts-out` | MP-15 |
| `nodatime-and-spatial.md` | 410 | All five sections | |
| `several-databases.md` | 370 | `use-several-databases`, `folder-with-another-dialect` | MP-14 |
| `custom-types.md` | 370 | `type-override`, `one-column`, `strongly-typed-id` | MP-21 |
| `repository-connection.md` | 280 | `connection-from-a-provider`, `sql-server-repository` | |
| `ci.md` | 260 | `check-sidecars-in-ci`, `ci-postgresql`, `ci-sql-server` | |
| `sync-and-streaming.md` | 260 | `run-without-awaiting` | |
| `dynamic-sql.md` | 230 | `table-name-at-run-time` | MP-16 |
| `lists.md` | 210 | `list-after-in`, `filter-by-a-list` | MP-17 |
| `many-rows.md`, `naming-and-placement.md`, `other-databases.md` | 160, 90, 40 | `insert-many-rows`; `model-namespace-task`; one bullet of `dialect-and-output` | |

The three hardest passages:

- **`dapper.md#models-through-dapper`.**  MP-12 has three texts: the paragraph of the README, the recipe, and the limit.  The recipe has a sentence of 68 words, and four samples of this page have twins in tests.
- **`troubleshooting.md#invalidcastexception`.**  The old answer holds seven quoted messages, each with its cause and its fix.  Each cause's rule has an owner on another page.  This section owns the way from the message to the cause.
- **`report-a-bug.md#raw-bundle`.**  A privacy procedure in long sentences.  It becomes numbered steps, and "do not attach" stays as strong as it is.

## Specific to this phase

### How the fixed shape meets the sections that phase 9 wrote

The shape is in section 5 of the rewrite guide, and in the brief of phase 9, which wrote its sections by it.  The shape holds for the whole page, and phase 9 wrote only parts of each page.

1. **Find what phase 9 wrote.**  `ledger/phase-09.tsv` marks it `new`.  The table "Written in: the guide pages" of the map lists the sections.  Do not reword that text.
2. **Write the opening of each page that has none.**  It is one sentence with the search words of the map's section 1, then the paragraph "Use this when".  Phase 9 wrote no opening on a page that holds old text.  These are `new` rows of your ledger.
3. **Fill `## If it does not work`.**  Phase 9 made the heading where one of its sections needed an entry.  Add the entries of the old text.  If a page has no such heading and the old text has an error or a limit to name, make the heading, last on the page, with no `<a id>` line.
4. **Put each section of old text in the order of the shape**: its sample, "What you get", then "The rules you need".  The line "Rules: ..." that ends a recipe becomes the links of the rules.  `nodatime-and-spatial.md` needs the most: phase 9 wrote nothing there.
   - **A sentence that two sections need stands once after the move.**  The third sentence of `README.md` 244, "On both, a type override gives a column a shape", stands under `nts-sql-server` alone, with the closing "See" sentence and its two links.  Restate it under `nts-postgresql`, in one sentence, with the link of that section's own database.  The sentence of `README.md` 250, on the deliveries of the two example projects, stands at the foot of the page: you can restate it on `json.md`, with its link.  Each restatement is a `new` row whose note names the unit it restates.
5. **Restate each rule that the task needs** in one or two sentences, with a link to its owner.  Do not list the values, the default or the places of a setting.
6. **Keep a fact once.**  If old text and a section of phase 9 state the same fact, keep phase 9's sentence.  The note of the old unit names that section.
7. **A false statement in phase 9's text is corrected**, with a `corrected` row.

### The samples this phase can add

Some sections of old text have no sample, because they were answers of the README's FAQ.  The shape asks for a whole sample.  Add one only in these sections, and only by copying: a file of an example project, or a part of it, or a fenced block of a reference page, byte for byte.  A `sql` block needs its info string, `sql postgres` or `sql mssql`: `UserDocsSamplesTests` parses it.

| Section | Copy from |
|----|----|
| `lists.md#list-after-in` | `ListOrdersByStatuses` of `examples/PostgresExamples/Methods/OrderQueries.sql`, and its call in `OrderQueries.cs` |
| `json.md#turn-json-on` | The `libraryTypes` of `examples/SqlServerExamples/sqlsource.json` |
| `naming-and-placement.md#model-namespace-task` | The sample of `reference/models.md#setting-model-namespace` |
| `nodatime-and-spatial.md`, the four sections named for a database | PostgreSQL: `examples/PostgresExamples/Methods/DeliveryQueries.sql` and `StoreDataSource.cs`.  SQL Server: `examples/SqlServerExamples/sqlsource.json` and `Methods/DeliveryQueries.sql` |
| `nodatime-and-spatial.md#ef-core-and-dapper` | `examples/PostgresExamples/DapperSetup.cs` |

### The merge pairs

The map knows no conflict in any of the ten.  List the facts all the same (section 9 of the guide).  Where the map names a reference page as the source of truth, that page is in another phase.  Read it at the commit you start from, and settle a conflict by the source file.

| Pair | Lands on | Settle a conflict by |
|----|----|----|
| MP-12 | `dapper.md#models-through-dapper`, and `#limit-parameter-names` | `tests/DatabaseTests/Postgres/EndToEnd/DapperRecipeTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/DapperRecipeTests.cs` |
| MP-14 | `several-databases.md` | `src/SqlSource/Generation/Methods/MethodPlanner.cs`, after correction C-01: the build reads the database setting too |
| MP-15 | `folders-and-files.md#leave-migration-scripts-out` | `src/SqlSource/build/SqlSource.props`, for `SqlSourceIncludeFiles` |
| MP-16 | `dynamic-sql.md#table-name-at-run-time` | `src/SqlSource/Parsing/Tokens/`, and `tests/SqlSource.Tests/Generator/TokenValidationTests.cs` |
| MP-17 | `lists.md` | `src/SqlSource/Parsing/SqlListUses.cs`, `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs` |
| MP-18 | `several-projects.md` | `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs` |
| MP-19 | `report-a-bug.md#report-a-bug` | `src/SqlSource.Tool/DiagnoseCommand.cs` |
| MP-20 | `json.md` | `src/SqlSource/Generation/Converters/JsonConversions.cs`, `tests/DatabaseTests/Postgres/EndToEnd/Json/` |
| MP-21 | `custom-types.md` | `src/SqlSource/Settings/TypeOverrides/`, `src/SqlSource/Generation/Converters/ConverterReader.cs` |
| MP-22 | `dapper.md#models`, then `#dapper-and-adonet` | The `Models/` folders of the two example projects, which compile |

MP-14, MP-17, MP-18, MP-20 and MP-21 land on a page, not under one heading.  Their texts stand under two or three anchors.  The merge there removes what the sections say twice; every heading and anchor stays.

No heading is lifted in this phase (section 9 of the guide).  A text of the README came without its heading: it stands under the heading of the recipe, or under an anchor that phase 8 made.  The worksheet of each pair says where each text stands.

### The reframings on these pages

- **`dapper.md#models` and `#sql-only`** say what `models` and `sql` are for: a project where Dapper or your own ADO.NET code runs the SQL.  The headings of the README were questions, "Only want to generate models?".  Word them as tasks.
- **`several-databases.md#folder-with-another-dialect`** sets a dialect for a folder; it does not "choose" one.  The bullet "Any dialect works" of `other-databases.md` keeps `mysql` and `sqlite` as examples, and says "set".

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| `DefaultTypeMap.MatchNamesWithUnderscores`; Dapper matches a parameter "ignoring case but not `_`"; a `datetime2` parameter on SQL Server | `dapper.md` | The two `DapperRecipeTests.cs`; `examples/*/DapperSetup.cs` |
| What a bundle renames, and that you keep the mapping to yourself | `report-a-bug.md` | `src/SqlSource.Tool/Bundles/Renaming/`; `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs` |
| The errors of a bundle that cannot be made safe, SQLSRC293 and SQLSRC294 | `report-a-bug.md#raw-bundle` | `src/SqlSource/Diagnostics/ToolDiagnostics.cs` |
| `--check` ends with the exit code `2`; a build in CI never describes | `ci.md` | `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs`; `src/SqlSource/build/SqlSource.targets` |
| The messages "Column 'name' is null.", "holds a null element.", "holds a JSON null.", and SQL Server's "Data is Null." | `troubleshooting.md` | `src/SqlSource/Generation/Emitting/QueryMethods/RowReaderWriter.cs` |
| NetTopologySuite 2.5.0 or later "by name" on SQL Server; a library is on by reference on PostgreSQL and off on SQL Server | `nodatime-and-spatial.md` | `src/SqlSource/Settings/Libraries/`; `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`; the section SQLSRC282 |

### Mirrored samples and quoted words

Section 8 of the map lists the twins.  On these pages:

- **Four blocks of `dapper.md`** have twins in `tests/DatabaseTests/*/EndToEnd/` and in `examples/`.  **The block of `several-projects.md#share-the-exceptions`** has one in `ExceptionReuseTests.cs`.  All are byte-identical.
- **The VS Code task of `describe-on-save.md#vs-code`** is read by `EditorRecipeTests`.  It stays outside any tab, under its h2.
- **The headings "Run the models through Dapper" and "With SQL Server"** were quoted by four comments in `examples/` (section 6 of the guide lists them).  The brief of phase 8 makes those comments name the anchor.  Run the `git grep` of that section: if a file still quotes a heading, keep its words.
- **Three support facts stay**: "This recipe has not been checked in VS Code", "Checked in Rider 2026.2 on macOS", "This has not been checked in Visual Studio".

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 12" exits with 0, so each unit of old text has a row in `ledger/phase-12.tsv`, and each line that the command prints has a note.
- `grep -L -F '**Use this when**' site/guides/*.md` prints `site/guides/AGENTS.md` and `site/guides/troubleshooting.md` alone: each of the 18 task pages has its opening.  The other parts of the shape, in the form of phase 9, are the reviewers' to read.
- Each new part of the shape and each copied sample has a `new` row, and the row of a sample names the file it was copied from.  "check rewrite 12" lists no fenced block as changed or gone.  The count of fences of each page, by the loop of section 10 of the guide, grew only by the copies of "The samples this phase can add".
- `grep -c '^## The merged text' docs/superpowers/specs/2026-11-12-docs-overhaul/merges/MP-{12,14,15,16,17,18,19,20,21,22}.md` prints 1 for each of the ten worksheets, and each merged text holds every fact of its list.
- `grep -c -F '**What you see.**' site/guides/dapper.md` prints 1: the limit has its three parts.
- The description of the pull request holds what section 10 of the rewrite guide asks, with "Facts with no owner" written as its section 8 says.

## For the reviewers

- **No-loss.**  The old text is 8,430 words in 18 pages.  Read it by page, old against new.  For a merge pair, read the worksheet's list of facts first.
- **Accuracy.**  Check each added sample against the file it was copied from.  Check each new part of the shape as new text.  Check the steps of `report-a-bug.md#raw-bundle` one by one against the old paragraph.
- **Both databases.**  `dapper.md`, `ci.md` and `nodatime-and-spatial.md` have a section for each database.  Check each against its own tests and example.

## Traps

- **`troubleshooting.md` is not a task.**  It gets no "Use this when" and no `## If it does not work`.  Its entries for the limits are new text of phases 3 to 9.  Rewrite `invalidcastexception` alone, as entries by the message the reader sees.
- **A guide must not grow into reference.**  When a rule needs more than two sentences, state its common form and link.
- **`dapper.md` owns a limit**, so it keeps `## Limits`.  Write the limit in three parts.  No other page of this phase gets the heading.  On this page `## If it does not work` stands before `## Limits`, which ends the page.
- **The two database sections of `dapper.md` are h2 now.**  They were h3 under the recipe.  "Each database needs more, below" is still true: check that "below" still points down the page.
- **The tool says "will" in its own output.**  The console blocks of `report-a-bug.md` are byte-identical, so the word stays there.
- **Rider's field is "Working directory".**  The name of a control stays.

## Calls made in this brief

- **A sentence of the README that two sections need is restated in the second**, as a `new` row.  The move places a unit once: the map and the brief of phase 8 leave the second place to this phase.
- **The form of the shape is phase 9's**: three bold labels, and `## If it does not work` as an h2 with no `<a id>` line.  The map lists the parts and no form.  The rule "no anchor is added" is about `<a id>` lines and the anchors of the map.
- **Phase 12 writes the opening of a page and the parts of the shape that the old text lacks.**  The brief of phase 9 leaves them to this phase.  The map says that part of a guide is "the text of the fixed shape that phase 12 adds around the moved sections".
- **A sample that this phase adds is a copy**, from an example project or a reference page.  The examples compile, so the accuracy review is a comparison.
- **A page-level merge pair keeps its sections.**  The map lands five pairs on a page, and gives that page two or three anchors.
- **On `dapper.md`, `## If it does not work` stands before `## Limits`.**  The map ends that page with its limits, and names no place for the other heading.
- **A quoted heading keeps its words only while a file quotes it.**  The brief of phase 8 makes the four comments name the anchor, and the `git grep` of the rewrite guide decides.
