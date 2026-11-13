# Docs overhaul, phase 16: rewrite-generated-code-1

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 16.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

Four pages of the group "Generated code" are in plain language and in the shape of a reference page: what is generated and where it goes, the models, their properties, and what a method returns.  The four merge pairs of `generator.md` with `settings.md`, MP-01 to MP-04, are merged.  Phase 17 takes the other five pages of the group.

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 11,650 in all.  The old text is 11,750 words (section 10 of the map), close to the limit of one rewrite.

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `members.md` | 3,850 | `generator`, `locations` with `setting-member-location`, `setting-visibility` | MP-01; MP-10: generator, token method | None |
| `models.md` | 3,350 | `model-names`, `model-namespace`, `model-shape`, `shared-models`, `table-models` | MP-02, MP-03, MP-04 | None |
| `model-properties.md` | 2,700 | `column-marker`, `setting-column-types` | None | None |
| `return-types.md` | 1,750 | `streaming-rows` | None | None |

The three hardest passages:

- **`members.md#setting-visibility`.**  The bullet "What does not fit" is one sentence of 161 words, and "What fits, with no diagnostic" one of 117.  Section 3.6 of the rewrite guide shows one clause of the first, before and after.
- **`model-properties.md#column-marker`.**  650 words in bullets that hold four to six rules each: nullability and type in one marker, the alias mark, what a marker inside a query names, and its errors.
- **`return-types.md#streaming-rows`.**  Six bullets, eight sentences over 40 words.  One sentence of 79 words says what an error in the loop does to a transaction, and it differs by database.

## Specific to this phase

### The merge pairs

Phase 6 put the texts of each pair side by side, untouched.  The text of `generator.md` stands under the heading of the pair.  Each section of `settings.md` keeps its own heading, one level down, with its own `setting-` anchor above that heading.  The worksheet of the pair says which headings to lift.  After the merge, each pair is one heading with every `<a id>` line of the pair above it (section 9 of the guide): five headings go, one on `members.md` and four on `models.md`, and no `<a id>` line goes.

| Pair | The merged heading and its `<a id>` lines | Where the texts differ | Settle it by |
|----|----|----|----|
| MP-01 | "Where the members go": `locations`, `setting-member-location`.  The h3 `placement-for-a-file` stays under it | No conflict known.  Both give the default by output | `src/SqlSource/Generation/Placement/QueryPlacement.cs`, `PlacementChecks.cs` |
| MP-02 | "Names": `model-names`, `setting-model-suffix`, `setting-model-name` | Each has rules the other lacks.  `settings.md`: two equal suffixes are SQLSRC257; the suffix ends a table model's name.  `generator.md`: `UserDto` and `UserDTO` are two models | `src/SqlSource/Generation/Models/ModelCollector.cs`, `ModelNameChecker.cs`, `TypeModels.cs` |
| MP-03 | "Namespace": `model-namespace`, `setting-model-namespace` | `settings.md` has "two types in two namespaces that claim one file, or read one table", and the four words `file`, `required`, `scoped`, `extension`.  `generator.md` has neither, and has the global namespace | `src/SqlSource/Generation/Models/ModelPlace.cs`, `TypeModels.cs`; `src/SqlSource/Settings/SettingValue.cs` |
| MP-04 | "Shape": `model-shape`, `setting-model-type` | No conflict known | `src/SqlSource/Settings/ModelKind.cs`; `src/SqlSource/Generation/Models/ModelKinds.cs` |

- **Each merged section has the order of a setting's section**: example, values, default, places, precedence, errors.  Then come the facts about the generated code.
- **"Names" holds two settings.**  Mark each with a bold lead-in, "The suffix" and "The name of one query's model", so that a reader who arrives by `#setting-model-name` finds its part.
- **Each pair has two or three samples.**  All stay, byte-identical.  The section opens with the sample of the setting.  The other sample stands lower, with the sentence that explains it.
- **Each section of `settings.md` ended with a pointer**: "The rules of the names are on the generator page."  Phase 6 mended the words that name a page of today, with the note `position`.  After the merge the pointer points at its own section.  It goes, as a fact said twice.
- **"Or read one table" in MP-03** is a second case, not a wording.  Two types in two namespaces that read one table get a table model each.  Check it in `TypeModels.cs` before you write it.

### `members.md` is over 3,500 words

It is 3,850 words today.  The map expects this phase to shorten it in two ways, with no fact lost:

- **MP-01** removes about 500 words that the two sections "Where the members go" have in common.
- **The overview under `#generator`** says several rules that other sections own.  Cut each to a restatement and a link (below).

Report the count.  If the page is still over 3,500, say so.  The map names the split for phase 25: the placement sections go to `member-placement.md`.  Do not split.

### The terms of MP-10

| Term | Section | What the merged definition must say |
|----|----|----|
| Generator | `members.md#generator` | The part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files and writes C# beside your code.  It needs no database |
| Token method | `members.md#token-methods` | The method that a query with tokens gets in place of a constant.  Its signature and where it goes are owned here.  What a token is, is owned by `tokens.md` |

Write in `merges/MP-10.md` under these two terms only.

### Rules that another section owns

| On this page | The rule | Its owner |
|----|----|----|
| `members.md#generator` | What each output gives a query, and which queries must be described | `settings.md#setting-output` |
| `members.md#generator` | .NET 8 or later, and C# 12 or later | `msbuild.md#build-requirements`.  That section does not name SQLSRC003 and SQLSRC012, so the two ids stay here, and each is reported as a fact with no owner (section 8 of the guide) |
| `members.md#token-methods` | The rules of a token | `tokens.md#tokens` |
| `model-properties.md#model-nullability` | Why a database reports that a column can be null | Each database's `nullability.md#nullability` |
| `model-properties.md#setting-column-types`, "Over the type override" | The order in which a C# type is chosen | `type-mapping.md#property-types` |
| `model-properties.md#column-marker`, last bullet | A `-- column:` line in the file header | `#setting-column-types`, on the same page |
| `return-types.md#method-shapes` | The five words of a result shape, and their errors | `sql-files.md#result-shapes`.  What each shape returns is owned here |

### The reframings on these pages

- **`members.md#generator`** names the outputs with `repository` first, and says what `methods` is for: a connection, a transaction or a timeout that your own code manages.  The old text does both.  Keep it so when you shorten it.
- **The table of member locations** has three rows.  Put the row `class` first: it is the default of `repository`.  Then `extension-class`, then `nested-class`.
- **Every list of outputs** has `repository` first: in `models.md#which-models`, in `return-types.md#method-shapes`, in the defaults of `#setting-visibility`.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| The default member location of each output; SQLSRC122 | `members.md#locations` | `QueryPlacement.cs`; `tests/SqlSource.Tests/Generator/PlacementTests.cs` |
| Which visibility fits which place; SQLSRC123 and SQLSRC124 | `members.md#setting-visibility` | `PlacementChecks.cs`; `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs` |
| A placement error "leaves its file out"; a visibility that does not fit the models "leaves out that method alone" | `members.md#locations` | `src/SqlSource/Generation/Placement/TypePlacement.cs` |
| The names a member cannot have; SQLSRC007, SQLSRC009, SQLSRC125 | `members.md`, `setting-nested-class` | `tests/SqlSource.Tests/Generator/NestedClassTests.cs` |
| When two queries share a model, and SQLSRC257 | `models.md#shared-models` | `tests/SqlSource.Tests/Generator/SharedModelTests.cs` |
| When a query gets a table model, and its name | `models.md#table-models` | `tests/SqlSource.Tests/Generator/TableModelTests.cs` |
| How a column's name becomes a property's name; SQLSRC254 | `model-properties.md#property-names` | `src/SqlSource/Generation/Models/PropertyNames.cs` |
| What `-- column:` takes inside a query and in the file header | `model-properties.md#column-marker` | `src/SqlSource/Parsing/Markers/SqlColumnDeclaration.cs`; `tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs` |
| How a key of `columnTypes` is written and matched | `model-properties.md#setting-column-types` | `src/SqlSource/Settings/ColumnTypes/ColumnTypeKey.cs`; `tests/SqlSource.Tests/Generator/ColumnTypeTests.cs` |
| The values of the collection type, and the default `Array` | `return-types.md#setting-collection-type` | `src/SqlSource/Settings/CollectionKind.cs` |
| What each bullet of streaming rows says of the connection, a `break` and an error | `return-types.md#streaming-rows` | `tests/DatabaseTests/Postgres/EndToEnd/StreamingQueryTests.cs`, and the same file of `SqlServer` |

### Samples

No sample of these four pages has a twin in a test.  The comments of `examples/*/Methods/OrderQueries.cs` link `#setting-member-location` by its anchor, which stays.  The samples of `#streaming-rows` are shown again on `guides/sync-and-streaming.md#stream-rows`, which phase 9 wrote.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 16" exits with 0, so each unit has a row in `ledger/phase-16.tsv`, and it lists no fenced block as changed or gone.
- The four worksheets `MP-01.md` to `MP-04.md` are finished, and the command of that section for `merges/MP-10.md` prints the h2 of Generator and Token method.
- The loop of that section for the anchors prints nothing: the seven anchors of MP-02, MP-03 and MP-04 and the two of MP-01 are on their pages.
- Each pair is one heading.  `grep -c -E "^#+ Where the members go$" site/reference/members.md` prints 1, and `grep -c -E "^#{2,} " site/reference/models.md` prints four fewer than at the commit you started from.
- `SettingsReferenceTests` passes.
- "What does not fit" and "What fits" are lists, one case in each item, with "all of these" or "one of these" said: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, whether `members.md` and `models.md` are over 3,500, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  Start with the four worksheets.  For MP-02 and MP-03, find each of the rules in the column "Where the texts differ" in the merged text.
- **Accuracy.**  Read the two lists of `#setting-visibility` against `PlacementChecks.cs`, case by case.  This is the place of the phase where a split sentence most easily turns a rule round.
- **Both databases.**  `return-types.md#streaming-rows` and `#method-shapes` say different things for PostgreSQL and SQL Server.  Check each against its own tests.

## Traps

- **"Type" has three meanings here.**  The type that claims the file; a model's shape, which the keys call `inputModelType`; the C# type of a property.  Say "shape" for the second, as the headings do.
- **Two costs of one error.**  SQLSRC123 leaves a whole file without members and models for the type.  When only the models of one method do not fit, it leaves out that method alone.  Keep "alone".
- **"The models follow the type, and the members follow `visibility`."**  A `private` method of a `public` type still has public models.  Do not merge the two rules into one.
- **`UserDto` and `UserDTO` are two models**, and a query's name is case-sensitive.  A setting's value is matched ignoring case.  The page has both rules.
- **"The rows still to come"** in `#streaming-rows` is not status language.  It means the rows that the loop has not read.
- **A sentence of `#streaming-rows` is true of one database only.**  When you split it, each half names its database.

## Calls made in this brief

- **Every `<a id>` line of a pair stands above the one merged heading**, also for "Names", which holds two settings.  The map says the three headings keep seven anchors.  Bold lead-ins mark the two settings inside the section.
- **The row `class` comes first in the table of member locations.**  The map asks for `repository` first where the outputs are listed; this table lists the defaults of the outputs.
- **SQLSRC003 and SQLSRC012 stay on `members.md#generator`**, and are reported with the note `no owner:`.  The owner of the requirements does not name them, and phase 25 gives them to it.
- **A pointer from a setting's section to its own merged section goes**, as a fact said twice.
