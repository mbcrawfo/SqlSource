# Docs overhaul, phase 07: move-settings

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both are read in full before this brief, and the outline wins where they differ.
Done by: an Opus agent.  Needs: phases 0 to 6.  Runs alone: phases 0 to 11 run one after another.

## Goal

`site/settings.md` is gone from the root of `site/`.  What phase 6 left of it stands word for word on `reference/settings.md`, `settings-file.md`, `generator-parameters.md`, `msbuild.md`, `type-mapping.md`, `library-types-and-json.md`, `converters.md` and `attributes.md`, and on `guides/folders-and-files.md`.  `reference/settings.md` has the table of the five places and the matrix of the 22 settings, both written new.  "Setting" has one meaning (C-02), and the page says truly who reads the database setting (C-01).  `SettingsReferenceTests` holds the matrix and the indexes to the code.

## Where things stand

Checked at the baseline, `28e20e3c`.  Read a baseline page with `git show 28e20e3c:<path>`.

- `site/settings.md` at the baseline: 1,087 lines, 20,330 words, 31 `<a id>` lines.  Its ranges in the map's table, "Map: `site/settings.md`", match the headings of the file line for line.
- **The cut**, in lines of the baseline.  A range starts at the `<a id>` line of its first heading and ends at the blank line before the next range.  Phase 6 took 255-394, 570-620, 714-819 and 912-956: the four model settings, "Collection type", "Streaming rows", "Column types", "Where the members go", "Visibility", "Name of the nested class", "Public exceptions" and "Repository interfaces".  Phase 7 takes the rest: 1-254, 395-569, 621-713, 820-911 and 957-1087.
- So the file holds 19 `<a id>` lines when this phase starts: 18 that keep their names and `known-issues`, which retires.  It is at the root of `site/`, in the Settings group of `site/reference/toc.yml` as `../settings.md`, and in the list of `SiteStructureTests` with `concepts.md` and `recipes.md`.
- Phase 1 took out the heading of line 36 and the Status column of lines 38-58, so the table has three columns: Setting, Values, Written as.  Phase 1 placed a pointer to SQLSRC232 in `#setting-type-overrides`, and phase 2 may have placed one sentence in `#converters`.  Both move with their sections.
- `reference/msbuild.md` exists since phase 5, with `running-in-the-build`.  `reference/attributes.md` and `reference/type-mapping.md` exist since phase 6, the second with `property-types` alone.
- Links at the baseline: 344 from other pages of `site/` to `settings.md`, and 103 inside it.  Outside `site/`, after phase 6: 51 references to `site/settings.md` in 26 files, and 16 links to `settings.html` in `README.md`.
- The list of generator parameters in `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` is a local array of twelve words in `Document_UnknownGeneratorParameterSection_ListsEveryGeneratorParameter`: the eleven names and `default`.  No other test shares it.

## In scope / Not in scope

In scope:

- Every row of the map's "Map: `site/settings.md`" for the lines of phase 7, with the six known-issue bullets.
- The new text of `reference/settings.md`: the definition, the table of places, the matrix.
- Corrections C-01 (lines 153 and 1047) and C-02 (lines 3, 28, 34 and 1049), and the last target of the link of C-05.
- `SettingsReferenceTests`, and the shared list of generator parameters.
- Every link to `settings.md` and to `settings.html`.

Not in scope:

- A change of words in a moved unit, but for the corrections and for the words that the move makes wrong (step 7).  The value table of "Output" is not reordered and "Dialect" is not reworded here: the map's rows say so of phase 14.
- The merges of MP-06 and MP-07 (phase 14).
- `#build-requirements`, `#three-kinds-of-parameter`, the terms of `concepts.md` (phase 8); `#class-for-a-folder` (phase 9).
- Any change to the schema, to a setting or to a default.

## The work

How a phase is done, the ledger, the reviews and publishing are in the outline's "Delivery".

### 1. Read what changed since the baseline

Run `git diff 28e20e3c -- site/settings.md`.  Each difference is a row of an earlier ledger or text of feature work, which this phase adds to the inventory.  Line numbers below are the baseline's; in the working tree find a section by its `<a id>`.

### 2. Make the pages

The h1 of a new page is its sidebar title in the map's section 1.  A heading that the map's section 3 lists as h2 is `##` on the new page; the words of a heading do not change.  `s` is `settings.md`.

| Page of `site/` | Sections in order: anchor (lines) |
|----|----|
| `reference/settings.md`, new | Under the h1: the definition (s 3, C-02) and one new sentence that links `settings-file.md`.  New h2 "The places a setting is written", `places`: the three samples (s 5-26), a fourth sample, the table of places, and what is not a setting (s 28, C-02).  `all-settings`: s 32-34 with C-02, the matrix, s 60.  `precedence` (s 64-89) with `a-value-that-is-wrong` (s 93-98).  `setting-output` (s 102-130).  `setting-database` (s 134-155, C-01).  `setting-dialect` (s 159-189) |
| `reference/settings-file.md`, new | Two new sentences under the h1, which link `settings.md#all-settings` and `settings.md#precedence`.  `settings-files` (s 1011-1051), with five new `<a id>` lines, each above its bold paragraph: `file-name` (s 1033), `file-contents` (s 1035), `files-of-a-sql-file` (s 1042), `which-files-are-checked` (s 1047), `settings-file-in-the-build` (s 1049).  `limits` with `limit-symbolic-link` (s 1057) |
| `reference/generator-parameters.md`, new | `setting-generator-parameters` (s 193-253), cut as step 4 says |
| `reference/type-mapping.md`, exists | After `property-types`: `setting-type-overrides` (s 397-467); `limits` with `limit-key-in-brackets` (s 1069) and `limit-nullable-type-argument` (s 1070) |
| `reference/converters.md`, new | `converters` (s 471-526); `limits` with `limit-vogen-and-stronglytypedid` (s 1071-1086) and `limit-one-conversion-per-type` (s 1087) |
| `reference/library-types-and-json.md`, new | `setting-library-types` (s 661-712); `json-class` (s 530-568); `json-opt-in` (s 623-657) |
| `reference/attributes.md`, exists | `setting-source` (s 822-847), an h2 after `files-of-a-type` and before `limits` |
| `reference/msbuild.md`, exists | New h2 "The properties", `properties`, first on the page; `included-files` (s 884-910); `running-in-the-build`, as phase 5 left it; `online-build` (s 959-980); `setting-env-file` (s 984-1007); `limits` with `limit-file-added-by-a-target` (s 1058-1068) |
| `guides/folders-and-files.md`, new | `configure-a-folder` (s 851-880), and nothing else.  Phases 8 and 9 fill the page |

Totals: 18 anchors keep their names; 29 `<a id>` lines are new (`places`, the five of the settings file, twelve `gp-` lines, `properties`, four `limits`, six `limit-` lines).  The words moved are in the map's section 10, "Phase 7": about 14,900.

### 3. Write the table of places and the matrix

Both are new text, in plain language, and both get the accuracy review.  The definition is the map's, under "One definition of \"setting\"": a setting is a named choice that changes what SqlSource generates or does, and that you write in one of five places.

**The table of places** has three columns: place, written as, applies to.

| Place | Written as | Applies to | Read from |
|----|----|----|----|
| A marker inside a query | A `-- word:` line under a `-- name:` line | That query | `SettingPlaceKind.Query` in `src/SqlSource/Settings/SettingPlace.cs` |
| A marker in the file header | A `-- word:` line before the first `-- name:` line | Every query of the file | `SettingPlaceKind.FileHeader` |
| A key of a settings file | A member of `sqlsource.json` | The `.sql` files of its folder and of every folder below | `SettingPlaceKind.SettingsFile` |
| A property of an attribute | `[SqlSourceGenerate(Source = "...")]` | The type, or the member, that carries the attribute | `src/SqlSource/Generation/AttributeSource.cs`, lines 68 to 127 |
| An MSBuild property of the project | An element of the project file, or of a `Directory.Build.props` above it, or `-p:` on the command line | The whole project | `src/SqlSource/build/SqlSource.props` and `SqlSource.targets` |

The fourth sample shows a property of the project.  Take it from s 916-920, the sample of "Public exceptions", which is valid as it stands.

**The matrix** stands under `all-settings`, as the first table after the anchor.  Its columns, in this order: Setting, `sqlsource.json` key, Marker, In the file header, Inside a query, Attribute property, MSBuild property, Default.  It has no Values column.  All 22 rows were checked against the source for this brief, and the map has none of them wrong.

| # | Setting, and its link | Key | Marker | Header | Query | Attribute property | MSBuild property |
|----|----|----|----|----|----|----|----|
| 1 | Output, `#setting-output` | `output` | `-- output:` | yes | yes | | |
| 2 | Database, `#setting-database` | `database` | `-- database:` | yes | yes | | |
| 3 | Dialect, `#setting-dialect` | `dialect` | `-- dialect:` | yes | no | | |
| 4 | Generator parameters, `generator-parameters.md#setting-generator-parameters` | `generatorParameters` | `-- generator:` | yes | yes | | |
| 5 | Input and output model suffix, `models.md#setting-model-suffix` | `inputModelSuffix`, `outputModelSuffix` | `-- input-model-suffix:`, `-- output-model-suffix:` | yes | no | | |
| 6 | Model namespace, `models.md#setting-model-namespace` | `modelNamespace` | `-- model-namespace:` | yes | no | | |
| 7 | Input and output model type, `models.md#setting-model-type` | `inputModelType`, `outputModelType` | `-- input-model-type:`, `-- output-model-type:` | yes | yes | | |
| 8 | One query's model name, `models.md#setting-model-name` | | `-- input-model:`, `-- output-model:` | no | yes | | |
| 9 | Collection type, `return-types.md#setting-collection-type` | `collectionType` | `-- collection-type:` | yes | yes | | |
| 10 | Type overrides, `type-mapping.md#setting-type-overrides` | `typeOverrides` | `-- type-override:` | yes | yes | | |
| 11 | Column types, `model-properties.md#setting-column-types` | `columnTypes` | `-- column:` | yes | yes | | |
| 12 | Library types, `library-types-and-json.md#setting-library-types` | `libraryTypes` | `-- library-types:` | yes | yes | | |
| 13 | Member location, `members.md#setting-member-location` | `memberLocation` | `-- member-location:` | yes | yes | | |
| 14 | Visibility, `members.md#setting-visibility` | `visibility` | `-- visibility:` | yes | yes | | |
| 15 | Nested class, `members.md#setting-nested-class` | `nestedClass` | `-- nested-class:` | yes | yes | | |
| 16 | `Source`, `attributes.md#setting-source` | | | no | no | `Source` | |
| 17 | Repository interfaces, `repositories.md#repository-interfaces` | | | no | no | `GenerateInterface`, `InterfaceName` | `SqlSourceRepositoryInterfaces` |
| 18 | A repository's connection, `repositories.md#repository-connection` | | | no | no | `[SqlSourceConnection]`, `Database` | |
| 19 | Running the tool in the build, `msbuild.md#online-build` | | | no | no | | `SqlSourceOnline` |
| 20 | The build's `.env` file, `msbuild.md#setting-env-file` | | | no | no | | `SqlSourceEnvFile` |
| 21 | Which `.sql` files are included, `msbuild.md#included-files` | | | no | no | | `SqlSourceIncludeFiles` |
| 22 | Public exceptions, `exceptions.md#public-exceptions` | | | no | no | | `SqlSourcePublicExceptions` |

Where each cell is read from:

- **Key**: the names under `properties` of `schemas/settings-v1.schema.json`, but `$schema` and `root`: 16 keys, in rows 1 to 7 and 9 to 15.  `src/SqlSource/Settings/SettingId.cs` has 15 of them and no `Dialect`, which `SqlDialectName` reads, so the schema is the list and not the enum.
- **Marker**: the words of `Keywords` in `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs`.  `SqlMarkerKind` has 23 members: 18 are in rows 1 to 15, and `Name`, `Summary`, `Param`, `Token` and `TokenIgnore` are not settings.
- **Header and Query**: `IsAllowedInPreamble` and `IsAllowedInQuery` of `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs`, lines 245 to 281.  The parser reads `-- dialect:` itself (`SqlFileParser.cs`), so neither method names it: header yes, query no, as the marker index says.  The cell of row 11 may add "for a column of the query's result".
- **Attribute property**: `AttributeSource.cs`, lines 95, 115, 121 and 127.
- **MSBuild property**: `SqlSource.props` holds `SqlSourceIncludeFiles` (line 33), `SqlSourcePublicExceptions` (54) and `SqlSourceRepositoryInterfaces` (55).  `SqlSourceOnline` and `SqlSourceEnvFile` are read in `SqlSource.targets`, lines 422 and 438, not in the props file.
- **Default**: the "Default" sentence of the old Values cell (s 40-58), checked against the `description` of the key in the schema (12 keys state one), `DefaultInputModelSuffix`, `DefaultOutputModelSuffix`, `DefaultNestedClass` and `MemberLocation` in `src/SqlSource/Settings/QuerySettings.cs`, `PublicExceptions.Read` (off), `RepositoryInterfaces.Read` (on), and `SqlSource.targets` lines 423 to 425 (`auto`) and 439 (the `.env` of the project's folder).  Rows 8, 18, 21 and 22 have no cell today: read the default from the section the row links.

**The Values column goes.**  Before it goes, read each of the 19 cells of s 40-58 against the section its row links.  A fact that is in the cell and not in the section is placed in the section, word for word, and the ledger says so.  The cell of the generator parameters (s 43) is held by the table and the sections of `generator-parameters.md`.

### 4. Cut the table of generator parameters

The map asks for an h3 for each parameter, so that no Effect cell is shortened.  The table of s 212-224 has ten rows for eleven parameters, and a row for `default`.

- The order under the h2: the sample, s 210, the table, s 226-253, then the h3 sections.
- The table keeps the columns Parameter and Default.  Each name becomes a link to its h3.
- Each Effect cell goes under its h3, word for word.  The h3's words are the name in code font.  The anchors: `gp-keep-comments`, `gp-token-validation`, `gp-sort-input`, `gp-sort-output`, `gp-table-models`, `gp-async-method-suffix`, `gp-array-pool`, `gp-list-padding`, `gp-collection-method`, `gp-transaction`, `gp-method-type`.
- `sort-input` and `sort-output` share one row and one cell: one h3, with both `<a id>` lines above it.
- The row of `default` gets an h3 of its own, `gp-default`, last, for its cell.  The map's line of `R/generator-parameters.md` lists the anchor.
- So the page has eleven h3 headings and twelve `gp-` anchors.

### 5. Make the corrections

| Id | At | Becomes | Source |
|----|----|----|----|
| C-01 | s 153, the fifth bullet of "Database" | "The tool reads the setting to choose the connection it describes a query through.  The build reads it to choose the connection source of a repository method.  A `database` value that is not valid in a settings file is reported by the tool alone."  The second sentence links `repositories.md#repository-connection` | `DatabaseOf` in `src/SqlSource/Generation/Methods/MethodPlanner.cs`, lines 319 and 320, calls `QuerySettings.ResolveDatabase`, and lines 268 and 300 use it; `src/SqlSource.Tool/Planning/RunPlanner.cs` 260; SQLSRC030 and SQLSRC031 in `src/SqlSource/Diagnostics/SqlDiagnostics.cs` |
| C-02 | s 3 | The one definition, then what the page holds: the places, every setting, which place wins.  It no longer says "Every setting is defined here": each setting is defined on the page of the thing it changes | The map, "One definition of \"setting\"" |
| C-02 | s 28 | Five places, as the table.  It no longer says "Which of the three" | The table above |
| C-02 | s 34 | It explains the matrix's columns.  "for the two settings of the build and the repository interfaces, a property of the project" becomes the five properties of rows 17 and 19 to 22 | `SqlSource.props`, `SqlSource.targets` |
| C-01 | s 1047, the last sentence of "Which files are checked", now under `settings-file.md#which-files-are-checked` | "for every key but `database`, which only the tool reads" becomes "for every key but `database`, which only the tool checks".  No other word of the paragraph changes | As for line 153; `tool.md` 50 of the baseline says the same, in true words |
| C-02 | s 1049, the last two sentences of "In the build", now under `settings-file.md#settings-file-in-the-build` | "The project file holds no setting: an MSBuild property or item metadata named after a setting is not read" becomes "The project file holds no key of a settings file: an MSBuild property or item metadata named after one is not read".  The list of the properties that a project sets has four and gains the fifth, last: `SqlSourceRepositoryInterfaces`, "which says whether a repository gets its interface", as a link to `repositories.md#repository-interfaces` | `SqlSource.props` 54 and 55; `SqlSource.targets` 422 and 438; `RepositoryInterfaces.Read` |
| C-05 | `docs/tech-debt/TD-0067-nullability-inside-a-mapped-type-argument-is-not-checked.md` 13 | The link names `site/reference/type-mapping.md#limit-nullable-type-argument` | s 1070 |

True and left as they are: "not a key of a settings file" (s 980, 1007), and "`Source` is a property of the attribute and nothing else" (s 838).  The list of what is not a setting, from the map, stands under `places` in at most five bullets.

### 6. Place the merge pairs and the limits

- **MP-06**: s 959-980 goes under `online-build`, an h2 after `running-in-the-build`.  The two stay two headings.  Add the text to `merges/MP-06.md`, which phase 5 started, with the conflicts of the map's row.
- **MP-07**: s 822-847 goes under `setting-source`.  Add it to `merges/MP-07.md`, which phase 6 started.
- **MP-10**: add to `merges/MP-10.md` the openings that phase 8 pairs with a term: s 102-130 (output), s 134-155 (logical database), s 159-189 with line 183 (dialect), s 1031 (settings file), s 210 (generator parameter).

Each bullet of "Known issues" goes under `## Limits` of its page as an h3, in the form of phases 3 to 6, with one entry in `guides/troubleshooting.md`.  The heading (s 1055) goes.

| Limit | Bullet | Entry under |
|----|----|----|
| `reference/settings-file.md#limit-symbolic-link` | s 1057 | `not-described` |
| `reference/msbuild.md#limit-file-added-by-a-target` | s 1058-1068 | `not-described` |
| `reference/type-mapping.md#limit-key-in-brackets` | s 1069 | `wrong-model` |
| `reference/type-mapping.md#limit-nullable-type-argument` | s 1070 | `wrong-model` |
| `reference/converters.md#limit-vogen-and-stronglytypedid` | s 1071-1086 | `at-run-time` |
| `reference/converters.md#limit-one-conversion-per-type` | s 1087 | `wrong-model` |

### 7. Retarget the links

An anchor keeps its name and only the page changes; the table of step 2 gives the page of each of the 18.  Retarget by anchor: a replace of the page alone sends 12 of them to the wrong page.

| Link | New target |
|----|----|
| s 1047, `#known-issues` | `#limit-symbolic-link`, on `reference/settings-file.md` |
| `generator.md` 352, now in `reference/models.md` | `type-mapping.md#limit-nullable-type-argument` |
| `sql-files.md` 200, now in `reference/sidecars.md` | `msbuild.md#limit-file-added-by-a-target` |
| Bare `settings.md`: `sql-files.md` 47, now in `reference/sql-files.md` | `settings.md#all-settings` |
| s 155, to the command reference | `connections.md#connections` |
| `[the tables](#all-settings)`, s 82, 880 and 1037 | `settings.md#all-settings`, from each new page |

**Words that the move makes wrong.**  A link's words can name "the settings page" where its section is now on another page: `parameters-and-tokens.md` 252, on `reference/tokens.md`, says it of the generator parameters.  "The tables" (s 28, 82, 880 and 1037) names one matrix now.  A section that leaves its parent can say "above" or "below" of text that is on another page: `#json-opt-in` and `#json-class` are two.  Find them with `git grep -n -i -E "settings page|the tables" -- site` and `grep -n -i -E "this page|above|below" site/reference/{settings,settings-file,generator-parameters,type-mapping,converters,library-types-and-json,attributes,msbuild}.md site/guides/folders-and-files.md`.  Change the fewest words that make each phrase true, and give the unit's row the note `position`.  Change no other word.

**Interim links this phase retargets:**

| Link | Left by | Parked at | Becomes |
|----|----|----|----|
| `sql-files.md` 47, on `reference/sql-files.md` | Phase 3 | `../settings.md#all-settings` | `settings.md#all-settings` |
| `sql-files.md` 134, on `reference/sql-files.md` | Phase 3 | `../settings.md#setting-dialect` | `settings.md#setting-dialect` |
| `sql-files.md` 200, on `reference/sidecars.md` | Phase 3 | `../settings.md#known-issues` | `msbuild.md#limit-file-added-by-a-target` |
| `postgresql.md` 3 and `sqlserver.md` 3, the first link of each, on each database's `index.md` | Phases 3 and 4 | `../../../settings.md#setting-dialect` | `../../settings.md#setting-dialect` |
| `generator.md` 352, on `reference/models.md` | Phase 6 | `../settings.md#known-issues` | `type-mapping.md#limit-nullable-type-argument` |
| Two links of the table of `reference/attributes.md#attributes` | Phase 6 | `../settings.md#setting-source`, `../settings.md#converters` | `#setting-source`, on its own page; `converters.md#converters` |
| The one sentence of `guides/troubleshooting.md` that links known issues | Phase 3 | `../settings.md#known-issues`, its last link | The sentence goes: each of the 36 limits has its entry |
| `docs/tech-debt/TD-0067-...md` 13 | Phase 4 | `site/settings.md#known-issues` | C-05, in step 5 |
| `docs/tech-debt/TD-0025-...md` 25 | Phase 5 | `site/settings.md#known-issues` | `site/reference/msbuild.md#limit-file-added-by-a-target` |
| `src/SqlSource.Tool/AGENTS.md`, "The logical database setting is in" | Phase 5 | `site/settings.md` | `site/reference/settings.md` |
| `docs/architecture/sidecar-format.md` 52, the slug `#type-overrides` | Phase 6 | `site/settings.md#type-overrides` | `site/reference/type-mapping.md#setting-type-overrides` |

**Interim links this phase leaves:**

| Link | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| Each link of the moved text to a term of `concepts.md` (s 210 is one) | The pages of step 2 | `../concepts.md#<term>` | The term's section: the map's 6.3 | 8 |
| Each link of the moved text to a recipe | The pages of step 2 | `../recipes.md#<anchor>` | The recipe's guide, with the same anchor | 8 |

Find the rest by search:

- Inside `site/`: `git grep -n -E "settings\.md" -- site`.
- `README.md`: 16 links to `settings.html`.  By anchor: `all-settings` and `setting-output` to `reference/settings.html`; `settings-files` to `reference/settings-file.html`; `setting-generator-parameters` to `reference/generator-parameters.html`; `included-files` to `reference/msbuild.html`; `setting-type-overrides` to `reference/type-mapping.html`; `converters` to `reference/converters.html`; `json-class`, `json-opt-in` and `setting-library-types` to `reference/library-types-and-json.html`; `configure-a-folder` to `guides/folders-and-files.html`.
- Elsewhere: `git grep -n -E "site/settings\.md" -- ':!site' ':!docs/superpowers'`.

| File | What changes |
|----|----|
| `docs/architecture/generator.md` 242, 300, 382 | `site/reference/attributes.md#setting-source`; `converters.md#converters`; line 382 uses the slug `#which-place-wins`, which becomes `site/reference/settings.md#precedence` |
| `docs/architecture/msbuild-and-manifest.md` 3 and 70 | `site/reference/msbuild.md#included-files`; `site/reference/settings-file.md#settings-files`, twice |
| `docs/architecture/sidecar-format.md` 52 | The slug `#type-overrides` becomes `site/reference/type-mapping.md#setting-type-overrides` |
| `docs/deferred/D-0001-dialects-not-delivered.md` 17 | "a value in the Dialect row of `site/settings.md`": the row holds no value now.  It becomes the section `site/reference/settings.md#setting-dialect` |
| `docs/deferred/D-0003-custom-type-conversion.md` 9; `D-0007-converters-by-convention.md` 9 and 20; `D-0008-converter-per-file-or-query.md` 9 and 20 | `site/reference/converters.md#converters`; line 9 of D-0007 `#limit-vogen-and-stronglytypedid`; line 9 of D-0008 `#limit-one-conversion-per-type`; `site/reference/type-mapping.md#setting-type-overrides` |
| `docs/tech-debt/TD-0016-settings-file-beside-a-late-targets-file-is-missed.md` 19; `TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md` 25 | `site/reference/msbuild.md#limit-file-added-by-a-target`, in the words and in the link.  In TD-0025 the words "and those of `site/tool.md` point there" go, if phase 5 left them |
| `examples/*/Methods/EmailAddress.cs`, `OrderId.cs`, `Price.cs` 8 | `site/reference/converters.md, "Converters"` |
| `examples/*/Methods/DeliveryDetails.cs` 6 | `site/reference/library-types-and-json.md, "A class in a JSON column"` |
| `examples/*/Methods/OrderQueries.cs` 13; `examples/*/Repository/UserRepository.cs` 12 | `site/reference/generator-parameters.md#setting-generator-parameters`; `site/reference/settings.md#setting-output` |
| `examples/PostgresExamples/README.md`, `examples/SqlServerExamples/README.md` | Nine links each, by anchor |

### 8. Tests

**`SettingsReferenceTests`**, new, in `tests/SqlSource.Repository.Tests/Documentation/`.  It reads the pages as text and the code through `InternalsVisibleTo`.  Each check fails with the name that has no row.

| It reads | What must hold |
|----|----|
| The names under `properties` of `schemas/settings-v1.schema.json`, but `$schema` and `root` | Each key is in the Key cell of exactly one row of the matrix, and that column holds no other key |
| Every member of `SqlMarkerKind`, with its word from `SqlMarkerReader.WordOf` | `-- word:` is in a row of the marker index of `site/reference/sql-files.md`.  For every kind but `Name`, `Summary`, `Param`, `Token` and `TokenIgnore`, a list in the test with its reason, it is in the Marker cell of exactly one row of the matrix |
| `SqlMarkerScope.IsAllowedInPreamble` and `IsAllowedInQuery` | For each kind of a matrix row but `Dialect`, the cells "In the file header" and "Inside a query" start with "yes" or "no" as the methods say |
| The list of generator parameters | One constant that `DiagnosticsDocumentTests` and this test share: the eleven names.  The diagnostics test adds `default` itself.  The constant equals the names of `GeneratorParameterList.Definitions`.  Each name has a row in the table of `site/reference/generator-parameters.md` and an `<a id="gp-<name>">` there |
| A list in the test of five names: `SqlSourceOnline`, `SqlSourceEnvFile`, `SqlSourceIncludeFiles`, `SqlSourcePublicExceptions`, `SqlSourceRepositoryInterfaces` | Each is in a row of the table of `site/reference/msbuild.md#properties`, and in the MSBuild property cell of exactly one row of the matrix.  A comment says that the properties the package sets for itself are left out on purpose |
| Every `<a id="setting-...">` of every page under `site/` | The Setting cell of exactly one row of the matrix links it.  None and two both fail.  Today that is 17 anchors: rows 1 to 16 and 20.  Rows 17, 18, 19, 21 and 22 link anchors with other names |

Other tests:

- **`DiagnosticsDocumentTests`**: the array becomes the shared constant.  The comment above the test, lines 57 to 60, reads: the section of SQLSRC109 tells a user what the generator parameters are; a parameter that is added is added to the shared list and to the section together; it also goes in `site/reference/generator-parameters.md`, in the table and in a section of its own, which `SettingsReferenceTests` checks.
- **`SiteStructureTests`**: `settings.md` leaves the list of the pages not yet moved, which is then `concepts.md` and `recipes.md`.  Phase 8 empties it.
- **What else fails a mistake**: `SiteTocTests`, `UserDocsLinksTests`, `InboundSiteReferenceTests`, `UserDocsSamplesTests` and the docfx build, as in phase 6.

### 9. `AGENTS.md`, the other documents and the sidebars

- **`site/AGENTS.md`**: four rows of "Where a new thing goes" take the words of the outline's table: "Setting, as a settings-file key or a marker", "Marker", "Generator parameter" and "MSBuild property", each with `SettingsReferenceTests` in its last cell.  The bullet "Pages not yet moved" loses `settings.md`, and its sentence "A limit of one is a bullet of its "Known issues"" goes: no page has known issues now.  Run `grep -n "settings\.md" site/AGENTS.md site/*/AGENTS.md` and rewrite each hit that means the old page.
- **`docs/deferred/AGENTS.md`** and **`docs/tech-debt/AGENTS.md`**, line 40: the clause that phase 3 wrote, ", and the "Known issues" of a page not yet moved,", goes.  The sentence reads "A limit names no id, so search the `## Limits` sections for the effect."
- **`src/SqlSource/Settings/AGENTS.md`**, "Documenting a setting", lines 16 and 17.  The first bullet: "**A setting a user can write is documented on the page of `site/reference/` for the thing it changes**, in a section anchored `setting-<name>`, with one row in the matrix of `site/reference/settings.md` that names every place it can be written.  A new marker also gets a row in the marker index of `site/reference/sql-files.md`.  `SettingsReferenceTests` fails without the rows."  In the second bullet, the sentence on `settings.md` becomes "In `site/reference/generator-parameters.md`, a row of the table and a section anchored `gp-<name>`.", and "the list in `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs`" becomes "the list that `DiagnosticsDocumentTests` and `SettingsReferenceTests` share".  The section has two bullets: phase 1 took out the third, on a status.
- **`schemas/AGENTS.md`** 3: `site/settings.md` becomes `site/reference/settings-file.md`.
- **`src/SqlSource.Tool/AGENTS.md`**: the last sentence of the bullet that phase 5 wrote, "The logical database setting is in `site/settings.md`.", names `site/reference/settings.md`.  That is 10 bytes more.  Phase 5 left the file at 8,174 bytes, so it comes to 8,184 of 8,192 and needs no cut.  Measure it: if another phase changed the file, cut words of that bullet, never a rule about the code.
- **`tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`**: the first bullet names `SettingsReferenceTests` and what it holds.  **`tests/README.md`** 13: the row of `tests/SqlSource.Repository.Tests` names the settings reference among its checks.
- **`site/reference/toc.yml`**: `../settings.md` goes.  The Settings group is `settings.md`, `settings-file.md`, `generator-parameters.md`, `attributes.md`, `msbuild.md`.  The Types group is `type-mapping.md`, `library-types-and-json.md`, `converters.md`.  **`site/guides/toc.yml`**: `folders-and-files.md`, under "Projects".

## The ledger

`ledger/phase-07.tsv` has a row for every unit of the baseline `settings.md` in the five ranges of phase 7.  With `ledger/phase-01.tsv` and `ledger/phase-06.tsv`, every unit of the page then has a disposition, and "check coverage" passes.

- `move`: every unit that no line below names.
- `corrected`: s 3, 28, 34 and 1049 (C-02); the bullet of s 153 and the paragraph of s 1047 (C-01).  Each row holds both texts.
- `move-split`: the h1; the 19 rows of s 40-58, each with its matrix row and the section that holds its values; the 11 rows of s 214-224, each with its table row and its h3; s 1055, the heading "Known issues".
- `merge-pair`: s 959-980 (MP-06); s 822-847 (MP-07).
- `new`: the h1 of six pages; `places`, the fourth sample, the table of places, the list of what is not a setting; the matrix; the sentences that open `settings.md` and `settings-file.md`; the five anchors of the settings file; eleven `gp-` headings with their twelve anchors; `properties` and its table; four `## Limits` headings, six limit headings, six entries of `troubleshooting.md`.
- `move`, with the note `position` and the old and the new words, as in phase 6: each unit whose words step 7 mended.
- A row by `@site/settings.md:<line>` for each unit that is not in the inventory: the pointer of phase 1 in `#setting-type-overrides`, and the sentence of phase 2 in `#converters`.

"check move 07" must leave over exactly the `corrected` and `move-split` rows and the rows with the note `position`.

## Done when

- [ ] `ls site/*.md` prints `site/AGENTS.md`, `site/concepts.md`, `site/diagnostics.md`, `site/index.md` and `site/recipes.md`.
- [ ] `git grep -n -E "site/settings\.md|SqlSource/settings\.html" -- ':!docs/superpowers'` prints nothing.
- [ ] `git grep -n "known-issues" -- ':!docs/superpowers' ':!tests'` prints nothing.  A hit is a link that an earlier phase missed: retarget it by the map's 6.1 and 6.2, and say so in the pull request.
- [ ] `grep -c "Only the tool reads this setting" site/reference/settings.md` prints 0, and `grep -c "The build reads it to choose the connection source" site/reference/settings.md` prints 1.
- [ ] `grep -c -E "Which of the three|Every setting is defined here" site/reference/settings.md` prints 0.
- [ ] `git grep -n -E "only the tool reads|holds no setting" -- site` prints nothing, and `grep -c "SqlSourceRepositoryInterfaces" site/reference/settings-file.md` prints 1.
- [ ] The matrix has 22 rows, and the page has no column named Values or Status.
- [ ] `grep -c '<a id="gp-' site/reference/generator-parameters.md` prints 12.
- [ ] `grep -o '<a id="setting-[a-z-]*"' -r site | wc -l` prints 17.
- [ ] `SettingsReferenceTests` passes.  Each of these, tried once and undone, fails it with a message that names the thing: the row of "Collection type" removed; a second row that links `#setting-output`; `SqlSourceIncludeFiles` removed from `msbuild.md#properties`; the heading of `gp-array-pool` removed; "Inside a query" of "Model namespace" set to yes.
- [ ] `AgentsFileTests` passes: `src/SqlSource/Settings/AGENTS.md` and `src/SqlSource.Tool/AGENTS.md` are under 8,192 bytes and their bullets under 80 words.
- [ ] `merges/MP-06.md` and `merges/MP-07.md` hold both texts, and `merges/MP-10.md` the five openings.
- [ ] "check coverage" passes, and "check move 07" leaves over only the rows the ledger names.
- [ ] `tools/build-site.sh` ends with no docfx warning.
- [ ] `./pre-commit-validation.sh --full` passes on the tree as pushed, after a clean `branch-review:review`.
- [ ] The pull request says that the site is published by hand after the merge.

## For the reviewers

The no-loss reviewer, beyond the general kit:

- **The 19 Values cells.**  Read each cell of s 40-58 against the section of its row, fact by fact: the list of values, the default, and the rule beside it.  The long ones are s 43 (generator parameters), s 49 (column types), s 50 (library types), s 51 and 52 (the defaults by output).
- **The Effect cells**: each of the eleven cells of s 214-224 is whole under its h3, the cell of `default` under `gp-default`.
- **The table of s 246-251** and the list of s 234-236 stand whole under the h2, before the first h3.
- **Bullets that hold a fenced block**: s 1058-1068 and s 1071-1086, with fences at lines 1060 and 1073.  Follow what phase 4 did with the block of `limit-hidden-rollback`; if the indent of a block changes, the ledger lists the block.
- **`#json-opt-in`** leaves its parent "Column types", which phase 6 moved, and `#json-class` leaves "Converters".  Read each for a sentence that points up or down the old page.
- **The pointers of phases 1 and 2** are in `type-mapping.md#setting-type-overrides` and `converters.md#converters`.

The accuracy reviewer:

- **The matrix**, all 22 rows and every cell, against the sources of step 3.
- **The table of places** against `SettingPlace.cs`, `AttributeSource.cs` and s 980 and 1007, which say where an MSBuild property is written.
- **C-01**: the three sentences against `MethodPlanner.cs`, `RunPlanner.cs`, and `tool.md` 50 of the baseline, which says "`database` by the tool alone".  Check that `reference/msbuild.md#running-in-the-build`, where phase 5 made C-01 at `tool.md` 235, says the same.
- **The table of `msbuild.md#properties`** against `SqlSource.props` and `SqlSource.targets`: five properties, each default.
- **The corrected sentences of s 1047 and s 1049**: the five properties of the paragraph are the five of the table, and no sentence of `reference/settings-file.md` says that only the tool reads `database`.
- **Each unit with the note `position`**: the fewest words changed, and the phrase is now true.
- **The fourth sample** is valid MSBuild.
- **`SettingsReferenceTests`**: that each check reads the code and not a copy of the page, and that the five trials of "Done when" fail.

## Traps

- **Two files named `settings.md`.**  The old page leaves the root and a new page of that name appears in `reference/`.  A link `../settings.md#x` in a page of `reference/` that is turned into `settings.md#x` is right for six anchors and dead for twelve.
- **`../../settings.md` is a right link** from a page of `reference/databases/postgresql/`, so no search for `../` proves anything.  The docfx build and `UserDocsLinksTests` do.
- **Three links outside `site/` use a heading's slug**: `#which-place-wins`, `#type-overrides`, and `#column-types`, which phase 6 took.  They pass the tests today.  Use the `<a id>`.
- **Decisions 4 and 5 do not reorder moved text.**  The new text lists the outputs as `repository`, `methods`, `models`, `sql`, and never says "choose a dialect".  The moved value table of "Output" stays in today's order until phase 14.
- **`SettingPlaceKind` has three members, not five.**  The attribute and the project are places for the reader, not for the code.  Do not add a member.
- **YAML.**  The title "Settings: every setting, and where you write it" holds a colon and a space, so its `name:` is quoted in `toc.yml`.
- **Azure and status words.**  The new text states facts.  `StatusLanguageTests` fails on "works today".
- **`docs/architecture/generator.md#settings`** is linked from `src/SqlSource/Settings/AGENTS.md` 3.  It is not the site's page and does not change.

## Left to the agent

- The words of the new text, within the columns and the facts fixed above.
- The name and the file of the shared constant, the names of the test methods, and how the test finds a table and a column.
- The words of the six limit headings and of the six entries of `troubleshooting.md`.
- Whether `reference/settings.md` is made by `git mv`.

## Calls made in this brief

- **`sort-input` and `sort-output` get one h3 with two anchors**, since they share one row and one Effect cell today.
- **`default` gets an h3 and the anchor `gp-default`.**  Its Effect cell has no subject without a heading, and the table links every name.
- **Two more sentences are corrected than the first map listed**: the last sentence of s 1047 (C-01) and the last two of s 1049 (C-02).  Each is false by the same source as its correction, and a correction lands in the move that carries the sentence.
- **The 19 table rows are `move-split`, not `move`.**  The map calls the columns Setting and Written as a move, but a row of the matrix cannot match the old row word for word, so the checker leaves them over and the reviewer reads them.
- **`SettingsReferenceTests` also holds the header and query cells to the code**, as the map's "Where a marker may stand" asks, and holds the shared list to `GeneratorParameterList.Definitions`, so that a new parameter fails a test.
- **What is not a setting is stated on the page**, under `places`, since the reader of the matrix asks why `-- param:` has no row.
- **Words that the move makes wrong are mended**, in the fewest words, each with the note `position`, as in phase 6.
- **`D-0001` 17 names the Dialect section**, since the row of the matrix holds no value.
