# Docs overhaul, phase 15: rewrite-settings-2

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 15.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The three pages of the group "Types" are in plain language and in the shape of a reference page.  The order in which a C# type is chosen is stated once, on `type-mapping.md`, and every other statement of it on these pages is a restatement that links there.  The phase has no merge pair.

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 7,950 in all.  The old text is 7,610 words (section 10 of the map).

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `type-mapping.md` | 3,400 | `setting-type-overrides`: "The key", "The type" | None | `limit-key-in-brackets`, `limit-nullable-type-argument` |
| `library-types-and-json.md` | 2,500 | `setting-library-types`, `json-opt-in` | None | None |
| `converters.md` | 2,050 | `converters` | None | `limit-vogen-and-stronglytypedid`, `limit-one-conversion-per-type` |

The three hardest passages:

- **"The key" of `type-mapping.md#setting-type-overrides`.**  Thirteen bullets say how a database type is written: six for PostgreSQL, two for SQL Server, five for both.  The rules on quotes and case are easy to turn round: a part in quotes is compared exactly, and a plain part ignoring case.
- **`converters.md#converters`, the paragraph "Converters apply only where ..."**.  It has 217 words.  One sentence of 96 words says when a converter serves a type that a library gives, with a PostgreSQL case in three parts.
- **`library-types-and-json.md#setting-library-types`, "The default" and "Only what you did not choose".**  The default differs by database: on PostgreSQL a library is on when the project references its plugin, and on SQL Server it is off.  One sentence of 86 words gives three cases for SQL Server and PostgreSQL.

## Specific to this phase

### The order in which a C# type is chosen

`type-mapping.md#property-types` is the one statement (section 3 of the map).  Keep its numbered list of five, and the sentence for a parameter.

1. The `-- column: name -> Type` marker of the query.
2. The column types.
3. The type override.
4. A library that is on for the query, for the output `repository` or `methods` alone.
5. The type map of the database.

- **On these pages, two paragraphs say the order again**: "Below your own choices" of `#setting-library-types`, and the sentences of `#setting-type-overrides` that end "Either wins over the type override".  Each becomes a restatement of one or two sentences with a link to `#property-types`.
- **Two more are in other phases**: "Over the type override" on `model-properties.md` (phase 16), and `parameters.md#param-marker` (phase 13).  You cannot change them.  Check your list against `src/SqlSource/Generation/Types/QueryTyper.cs`, so that all three phases agree with the source.
- **This section is the owner, so it keeps every fact of the order.**  If one of the two paragraphs that you cut holds a fact that `#property-types` lacks, add the fact to `#property-types`: both are your pages.
- **The sample of `#property-types`** has four properties, each with the place that gives its type.  The sentence that explains them is 58 words.  Make it a list of four lines, one for each property.

### Rules that another section owns

| On this page | The rule | Its owner |
|----|----|----|
| `type-mapping.md#property-types` | The type of a PostgreSQL array parameter | `databases/postgresql/query-methods.md#array-parameters` |
| `type-mapping.md#property-types` | The type of a list parameter | `parameters.md#list-parameters` |
| `library-types-and-json.md#setting-library-types` | Which types each library gives | Each database's `libraries.md#nodatime`, `#nettopologysuite` and `#json` |
| `converters.md#converters`, "SqlSource's own conversions" | What the query methods convert for NodaTime on SQL Server | `databases/sqlserver/libraries.md#nodatime` |
| `converters.md#converters`, "The type the driver reads" | Which C# type the driver reads for a database type | Each database's `types.md#type-table` |

These pages own the rest: the syntax of a key and of a C# type, where each setting is written, how the entries of the places add up, what a converter is and where it is found, the JSON opt-in.

### The reframings on these pages

- **Every list of outputs** has `repository` first: "a query whose output is `repository` or `methods`" in `#setting-library-types` and in `#converters`.
- **No status language.**  `type-mapping.md` holds a pointer that phase 1 placed: a PostgreSQL type that cannot be sent as a parameter is SQLSRC232.  It is a fact.  Keep it, as a sentence and a link.
- **The three settings of these pages** each have the order of a setting's section: example, values, default, places, precedence, errors.  "Every place, merged by type" is the precedence part.  It restates `settings.md#precedence`, the third exception, and links it.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| How a key is written, quoted and compared; a domain, a range, an array, an alias type | `type-mapping.md#setting-type-overrides` | `src/SqlSource/Settings/TypeOverrides/TypeKeys.cs`, `TypeKeyNames.cs`, `TypeOverrideGrammar.cs`; `tests/SqlSource.Tests/Settings/TypeOverrides/` |
| "A name that both databases have is one key" | `type-mapping.md#setting-type-overrides` | `src/SqlSource/Settings/TypeOverrides/TypeOverrideMap.cs`; `tests/SqlSource.Tests/Settings/TypeOverrides/` |
| How a C# type is written; SQLSRC250, SQLSRC251, SQLSRC252 | `type-mapping.md#setting-type-overrides` | `src/SqlSource/Settings/ClrTypes/`; `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`, `ShortNameTests.cs` |
| The default of each library on each database | `library-types-and-json.md#setting-library-types` | `src/SqlSource/Settings/Libraries/LibrarySettings.cs`; `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`; `tests/SqlSource.Tests/Generator/LibraryTypeTests.cs` |
| What `-> json` and `"json": true` do, and for which column types | `library-types-and-json.md#json-opt-in` | `src/SqlSource/Generation/Converters/JsonConversions.cs`; `tests/SqlSource.Tests/Generator/JsonSerializerMethodTests.cs` |
| What a converter is, where it is found, which direction serves a column and which a parameter; SQLSRC277 to SQLSRC280 | `converters.md#converters` | `src/SqlSource/Generation/Converters/ConverterReader.cs`, `ConverterSet.cs`; `tests/SqlSource.Tests/Generator/ConverterDiagnosticsTests.cs`, `ConverterMethodTests.cs` |
| "Null never reaches a converter" | `converters.md#converters` | `ConverterMethodTests.cs`; `src/SqlSource/Generation/Emitting/QueryMethods/RowReaderWriter.cs` |
| Npgsql 8 and 9 read a `date` as a `System.DateTime`, Npgsql 10 as a `System.DateOnly` | `converters.md#converters` | `src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs` |

### Mirrored samples and quoted words

- **The samples of `converters.md#converters`** have twins: `EmailAddress.cs`, `OrderId.cs` and `Price.cs` in `examples/PostgresExamples/Methods/` and in `examples/SqlServerExamples/Methods/`.  Byte-identical.
- **The sample of `library-types-and-json.md#json-class`** has a twin: `DeliveryDetails.cs` in the same two folders.
- **The headings "Converters" and "A class in a JSON column"** are quoted by the comments of those eight files.  Keep their words, as section 6 of the rewrite guide rules: no rewrite changes a comment.
- **The sample of `limit-vogen-and-stronglytypedid`** is shown as steps on `guides/custom-types.md#vogen`, which phase 9 wrote.  It stays here.

### Sizes

`type-mapping.md` is at 3,400 words, and "The key" grows when its bullets are split.  Do not cut a fact to hold it.  Report the count.  The page has one cut by its anchors, between `property-types` and `setting-type-overrides`, and the first part is under 600 words.  Say so for phase 25.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 15" exits with 0, so each unit has a row in `ledger/phase-15.tsv`, and it lists no fenced block as changed or gone.  The four limits have their three parts, by the loop of that section.
- The order of type choice is stated once.  `grep -c -F "Either wins over the type override" site/reference/type-mapping.md` prints 0, and `grep -c -F "type-mapping.md#property-types" site/reference/library-types-and-json.md` prints 1 or more: the two other paragraphs restate and link it.
- `git grep -n -F '"Converters"' -- examples` and `git grep -n -F '"A class in a JSON column"' -- examples` print the eight comments, and `grep -c -E "^#+ (Converters|A class in a JSON column)$" site/reference/converters.md site/reference/library-types-and-json.md` prints 1 or more for each page: the quoted headings keep their words.
- Each of the three settings has the order of a setting's section, and "The key" and "The type" have one rule in each bullet, with the PostgreSQL rules and the SQL Server rules told apart: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, with `type-mapping.md` named if it is over 3,500, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  `type-mapping.md#setting-type-overrides` is 2,600 words in one section.  Read it in its four parts: the places, the key, the type, the two forms.
- **Accuracy.**  For each rule on quotes and case, read `TypeKeys.cs` and one test of it.  For each direction of a converter, check which type is the argument and which the return type.
- **Both databases.**  "The key", "The default" and "What `on` needs" each have a PostgreSQL part and a SQL Server part.  Check each against its own source: `PostgresTypeMap.cs` and `SqlServerTypeMap.cs`.

## Traps

- **"From" and "to" turn round easily.**  A column needs the converter from the type the driver reads to the property's type.  A parameter needs the one back.
- **"Only" carries the rules of this group.**  "A library's types reach only a query whose output is ...", "converted only by a converter of the direction it needs", "only what you did not choose".  Keep each.
- **The three sections were nested** under "Type overrides" and "Column types" on the old page.  Words such as "above" and "as the type override's rules say" pointed at another page after the move, and phase 7 mended them, with the note `position`.  Check each one that is left: name the section and link it.
- **A type's key and a column's key are different things.**  `public.email` is a type.  `users.id` is a column, and its rules are on `model-properties.md`, in phase 16.
- **A short name of a C# type** is "the one type of that name" that the project can see.  Two such types are SQLSRC250.  Do not shorten this to "the type's name".
- **`default` is a value**, in `typeOverrides`, in `libraryTypes` and in a marker.  It takes an entry out.  It is not the word "default" of a sentence.

## Calls made in this brief

- **The paragraphs that say the order of type choice again are restatements.**  The map calls `#property-types` "the one statement" and marks the other paragraphs `move`.
- **The 58-word sentence of `#property-types` becomes a list of four lines.**  It explains a sample of four properties.
- **The cut of `type-mapping.md`** is named for phase 25, with its weakness.  The map lists the page as close to the limit and names no cut.
