# Docs overhaul, phase 14: rewrite-settings-1

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 14.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The five pages of the group "Settings" are in plain language and in the shape of a reference page.  "Setting" has one meaning on all five.  MP-06 and MP-07 are merged, five terms are defined once, and the output and dialect settings carry the owner's reframings.

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 10,400 in all.  The old text is 10,500 words (section 10 of the map).

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `settings.md` | 2,600 | `precedence`, `setting-output`, `setting-database`, `setting-dialect` | MP-10: output, logical database, dialect | None |
| `settings-file.md` | 1,500 | `files-of-a-sql-file`, `which-files-are-checked`, `settings-file-in-the-build` | MP-10: settings file | `limit-symbolic-link` |
| `generator-parameters.md` | 1,500 | `gp-array-pool`, `gp-list-padding`, `gp-transaction`, `gp-method-type`, the text on places | MP-10: generator parameter | None |
| `attributes.md` | 1,600 | `attribute`, `files-of-a-type`, `setting-source`, `what-the-package-adds` | MP-07 | `limit-source-written-as-text`, `limit-embedded-attribute` |
| `msbuild.md` | 3,200 | `running-in-the-build`, `online-build`, `setting-env-file` | MP-06 | `limit-file-added-by-a-target` |

The three hardest passages:

- **`msbuild.md#running-in-the-build` with `#online-build`.**  The first is 1,440 words from `tool.md`, with ten sentences over 40 words.  The second says the same rule again with five caveats that the first lacks.
- **`settings.md#precedence`.**  Four rules, then "three exceptions", in 600 words.  The bullet on two types that claim one file holds a rule, its reason, two things that can still differ, and what to do.
- **`settings-file.md#which-files-are-checked`.**  One paragraph of ten sentences and 310 words.  It says which C# files reach a settings file, with one exception for a symbolic link, and what a broken file above the project does.

## Specific to this phase

### MP-06: running in the build

The two headings stay, each with its anchor, so no heading is lifted (section 9 of the guide).  The merge decides which heading owns each fact.

- **`#online-build` owns what turns the tool on**: the three values of `SqlSourceOnline`, what `auto` looks for, the builds in CI, and where the property is written.
- **`#running-in-the-build` owns what the build then does**: which builds, when it runs again, what it describes, the errors, a build offline, several target frameworks, what it prints, the logs.  Its bullet "What turns it on" becomes two sentences and a link to `#online-build`.
- **The conflict.**  `tool.md` said that a build runs the tool "when it finds a connection".  `settings.md` adds what that leaves out.  Each of these must be in the merged text:
  - a variable of a database's name alone, such as `SQLSOURCE_CONNECTION_BILLING`, is not found under `auto`;
  - the `.env` file turns `auto` on whatever it holds;
  - a `.env` above the project's folder turns nothing on, and the build says nothing of it;
  - the four variables of CI, and that `CI=1` is not recognised;
  - a symbolic link that leads to no file counts as there.
- **Settle it by** `src/SqlSource/build/SqlSource.targets`, the properties `SqlSourceOnlineValue` and `SqlSourceEnvFileValue` and the condition on `CI`.
- **Correction C-01** is made already, by phase 5: "only the tool checks that value", of `database`.  Keep those words.

### MP-07: the files of a type

The two headings stay, and no heading is lifted.  `#setting-source` owns the rules of `Source`: which files a type claims, and SQLSRC004, SQLSRC005 and SQLSRC013.  `#files-of-a-type` owns what happens to the files: one set of names, two types that claim one file, a file with an error.  Both say that two types can claim one file, and that a file no type claims is ignored.  Keep each once.  Settle a conflict by `src/SqlSource/Generation/TargetTypeReader.cs` and `src/SqlSource/Generation/Paths/`.

### The terms of MP-10

Phase 8 put each term of the old `concepts.md` first in its section.  Merge it into the sentence after the sample of that section.  Write in `merges/MP-10.md` under these five terms only.

| Term | Section | What the merged definition must say |
|----|----|----|
| Output | `settings.md#setting-output` | What SqlSource produces for a query, and that it decides which queries must be described.  The values in the order `repository`, `methods`, `models`, `sql` |
| Logical database | `settings.md#setting-database` | Both uses.  The tool reads it to choose the connection it describes a query through.  The build reads it to choose the connection source of a repository method.  This is correction C-01; the term's text was right where the section was wrong |
| Dialect | `settings.md#setting-dialect` | The rules by which SqlSource reads a `.sql` file.  It connects to nothing |
| Settings file | `settings-file.md#settings-files` | A file named `sqlsource.json`, in any folder, for the `.sql` files of that folder and the folders below it |
| Generator parameter | `generator-parameters.md#setting-generator-parameters` | An option that changes what SqlSource does with a query.  It is not a query parameter: `#three-kinds-of-parameter` says so in full |

The paragraph "The two answer different questions" stands under `#setting-database`.  It is the reader's statement of dialect against logical database.  Keep both of its sentences and its example.

### The reframings on these pages

- **The table of `settings.md#setting-output`** has its rows in the order `repository`, `methods`, `models`, `sql`.  Today each row builds on the row above: "Its SQL", then "Its SQL, and its models".  After the change each row must stand alone.
- **The text of `#setting-output`** says when to set each of the other three, in the owner's words (section 7 of the guide).  Its sentences on `methods` and `models` already do; put them beside the table.
- **`#setting-output` owns "which queries must be described"**, and SQLSRC209 for a project that sets neither the dialect nor the output.  Keep these whole.
- **`settings.md#setting-dialect`** opens, after its sample: "Set it to your database: `postgres` or `mssql`."  It sends every other name to `dialects.md`.  The sample keeps its `mysql` block.

### One meaning of "setting"

Phase 6 and phase 7 made correction C-02, each at the sentences it carried.  This phase corrects nothing: it checks that the five pages agree with the one definition (section 5 of the map, "One definition"), and keeps them so.  Three sentences of `settings-file.md` arrive corrected by phase 7.  Check each against its source.

- **`#settings-file-in-the-build`, "The project file holds no setting"** (`settings.md` 1049, a place of correction C-02).  By the one definition that was false: an MSBuild property is one of the five places.  The sentence arrives as "The project file holds no key of a settings file: an MSBuild property or item metadata named after one is not read".  The rest of it stays: nothing reports such a property.
- **The same paragraph, the MSBuild properties that a project sets** (C-02 too).  The old list had four, and lacked `SqlSourceRepositoryInterfaces`.  It arrives with the five.  Check `src/SqlSource/build/SqlSource.props` and `SqlSource.targets`.  The table of `msbuild.md#properties` owns the list, so the rewrite cuts the list here to a restatement that links it.
- **`#which-files-are-checked`, its last sentence** (`settings.md` 1047, a place of correction C-01): "`database`, which only the tool reads".  The build reads the setting too.  The sentence arrives as "which only the tool checks".  Check `MethodPlanner.cs`, `DatabaseOf`.
- "Not a key of a settings file", of `SqlSourceOnline` and `SqlSourceEnvFile`, is true and stays.
- If one of the three did not arrive corrected, correct it here with a `corrected` row, as the outline's "Reviews" asks of any false statement, and say so in the first lines of the pull request.

### What the package adds

`attributes.md#what-the-package-adds` holds a bullet of the README.  `attributes.md#attribute` holds a sentence of `generator.md` that lists the same things.  The map asks this phase to join them, and gives the pair no id.

- **They disagreed.**  The README said "their enums and the exception its methods throw" (`README.md` 26).  The package adds no enum, and three exceptions.  It is correction C-07 of the map, and the bullet arrives corrected by phase 8, which carries it.
- **Check it against** `src/SqlSource/Generation/AttributeSource.cs` and `src/SqlSource/Generation/Exceptions/ExceptionSource.cs`: three attributes, three exceptions and `Microsoft.CodeAnalysis.EmbeddedAttribute`.
- **`#what-the-package-adds` owns the list.**  `#attribute` restates it in one sentence and links.  Record the join in the notes of both units: the unit of `#attribute` is `restated-from`, with the owner in `arg`.

### Two changes of structure that phase 7 prepared

- **`settings-file.md`: five bold parts become headings.**  Phase 7 put the anchors `file-name`, `file-contents`, `files-of-a-sql-file`, `which-files-are-checked` and `settings-file-in-the-build` above five bold lead-ins.  Make each lead-in an h3 under its anchor.  These are the only headings that the phase adds, and it adds no `<a id>` line.
- **`generator-parameters.md`: the eleven parameters.**  Phase 7 put each Effect cell under an h3 of its own, word for word.  Give each `gp-` section the order of a setting: an example where one helps, the values, the default, the effect, its errors.  No fact of a cell is cut.  The text of `gp-array-pool` is the only home of its facts.  Where a section names outputs, as `gp-collection-method` and `gp-method-type` do, put `repository` first.  `SettingsReferenceTests` and the list in `DiagnosticsDocumentTests.cs` hold the eleven names.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| The order of the places, and the three exceptions | `settings.md#precedence` | `src/SqlSource/Settings/SettingsLevelReader.cs`, `QuerySettings.cs`; `tests/SqlSource.Tests/Generator/SettingLevelTests.cs`, `TypeOverrideLevelTests.cs`, `GeneratorParameterLevelTests.cs` |
| "Of a key, or an entry, written twice, the first applies" | `settings.md#a-value-that-is-wrong` | `src/SqlSource/SettingsFiles/SettingsFileReader.cs` |
| The characters of a database's name; SQLSRC211 | `settings.md#setting-database` | `src/SqlSource/DatabaseName.cs`; `tests/SqlSource.Tests/DatabaseNameTests.cs` |
| The build reads `database` | `settings.md#setting-database` | `src/SqlSource/Generation/Methods/MethodPlanner.cs`, `DatabaseOf` |
| The names, values and defaults of the eleven parameters | `generator-parameters.md` | `src/SqlSource/Settings/GeneratorParameters.cs`, `GeneratorParameterList.cs`; `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs` |
| The keys and the JSON type of each value | `settings-file.md#file-contents` | `schemas/settings-v1.schema.json`; `src/SqlSource/SettingsFiles/SettingsFileKeys.cs` |
| The file's name in lower case; macOS and Windows against Linux | `settings-file.md#file-name` | `tests/SqlSource.Tests/Generator/SettingsFileTests.cs` |
| `CI` is `true`, `TF_BUILD` is `True`, `TEAMCITY_VERSION` or `JENKINS_URL` is set | `msbuild.md#online-build` | `SqlSource.targets`, the condition on `CI` |
| .NET 8, C# 12, the .NET SDK 9.0.300, Visual Studio 2022 17.14; SQLSRC021 | `msbuild.md#build-requirements` | `SqlSource.targets`, the check of `CompilerApiVersion` |
| "This was checked in Rider on macOS", and the three that were not checked | `msbuild.md#running-in-the-build` | A support fact.  It stays |

### Samples and sizes

- **No sample of these pages has a twin in a test.**  The console block of `#running-in-the-build` holds the tool's real output: byte-identical.
- **The matrix and the table of places** on `settings.md`, and the table of `msbuild.md#properties`, are new text of phase 7.  `SettingsReferenceTests` reads them.  Leave them.
- **`msbuild.md` is at 3,200 words.**  Report its count.  If it passes 3,500, its clean cut is `running-in-the-build` with `online-build` and `setting-env-file`.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 14" exits with 0, so each unit has a row in `ledger/phase-14.tsv`, and it lists no fenced block as changed or gone.  The four limits have their three parts, by the loop of that section.
- The worksheets `MP-06.md` and `MP-07.md` are finished, and the command of that section for `merges/MP-10.md` prints the h2 of Output, Logical database, Dialect, Settings file and Generator parameter.
- `grep -c '^## ' site/reference/msbuild.md site/reference/attributes.md` prints the counts of the commit you started from: MP-06 and MP-07 keep their two headings.
- `grep -c '^### ' site/reference/settings-file.md` prints five more than at that commit.
- `awk '/<a id="setting-output">/ { on = 1 } on && /^\|/ { n++ } n == 3 { print; exit }' site/reference/settings.md` prints the row of `repository`: it is the first row of the table under its header.
- `git grep -n -i -E "holds no setting|only the tool reads" -- site/reference/settings.md site/reference/settings-file.md` prints nothing.
- `SettingsReferenceTests` passes.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, with `msbuild.md` named if it is over 3,500, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  Read MP-06 with its worksheet: the five caveats above are the facts most likely to go.
- **Accuracy.**  Check the merged definition of the logical database against `MethodPlanner.cs`.  Check the list of what the package adds against the two source files.  Check each `gp-` section against `GeneratorParameterList.cs`.
- **Each row of `#setting-output`** must be true alone: read it without the rows around it.

## Traps

- **`settings.md#precedence` said "its row of the tables".**  The two tables are one matrix now, and phase 7 mended the words, with the note `position`.  Check that the sentence names one table, and keep its link.
- **A setting is documented where it changes something.**  The pages of this group hold eight of the 22 settings.  Do not copy the values of the other fourteen here.
- **"The build" and "the tool" are two actors.**  A wrong `database` in a settings file is reported by the tool alone.  A wrong `dialect` or `output` is reported by both.
- **`SqlSourceOnline` `true` in your own environment**, and not in the project, is a piece of advice with a reason.  Keep both.

## Calls made in this brief

- **Who owns what in MP-06 and MP-07.**  The map keeps two headings for each pair and does not divide the facts.
- **No sentence is corrected here by plan.**  "The project file holds no setting" and its list of four properties (C-02), the last sentence of "Which files are checked" (C-01) and the README's "their enums and the exception" (C-07) are false today, so the moves that carry them, phases 7 and 8, correct them.  This phase checks each, and corrects only what did not arrive corrected.
- **The join of the README's bullet and `generator.md`'s sentence is a merge without an id.**  The map says "the rewrite of phase 14 joins them".  `#what-the-package-adds` owns the list, and `#attribute` restates it.
- **The clean cut of `msbuild.md`** is named for phase 25.  The map lists the page as close to the limit and names no cut.
