# Query generation, phase 8.7: column types - design

Date: 2026-10-25

Sub-phase 8.7 of the [query generation epic](2026-10-07-query-generation-epic-design.md), a follow-up to [sub-phase 8.2](2026-10-24-query-generation-phase-8-2-converters-design.md) designed with the owner.  It lets a project give one column, or one parameter, a C# type of its own, where today a [type override](../../manual/settings.md#setting-type-overrides) gives every column and parameter of one database type the same C# type.  It also lets a C# type be written by its short name wherever SqlSource takes one.

## Intent

The owner's question: "Can we handle the scenario where columns of the same type need to map differently?  For example, `select id, count` where both are `int` but `id` maps to a domain type in C#.  Or `select id, count, created_by_user_id` where all are the same database type but the two id's have different domain types."

Today the only way is a change to the schema: make the column a PostgreSQL domain or a SQL Server alias type, map that type, and declare each parameter compared with it in `-- param:` ([the recipe](../../manual/recipes.md#give-an-id-a-type-of-its-own)).  A project whose schema uses plain `bigint` ids, which is most of them, cannot give `users.id` the type `UserId` and `posts.id` the type `PostId`.

The owner's direction:

- **A project-level default keyed by the column a value comes from**, `users.id` to `MyDomain.UserId`, so that every query that reads that column gets the type, with markers that set or change the mapping for one file and for one query.
- **A parameter names its C# type in its query**, on its `-- param:` marker: `-- param: @author int not null -> UserId`, and a shorthand, `-- param: @author -> UserId`.  Matching a parameter to a column by its name, or working out what a parameter is compared with, is "a very deep rabbit hole".
- **One `-- column:` marker** for columns, in the file header and inside a query.
- **Short C# names**, resolved against the project and its references, so that a full name is needed only where a short one is ambiguous; for the type override too.

## Goals

1. A setting, column types, that maps a column's source, a column of a table or view, to a C# type, at four levels: the `SqlSourceColumnTypes` property and item metadata, and the `-- column:` marker in the file header and inside a query.
2. `-- column:` inside a query gives one result column, named as the query returns it, a C# type, with or without its nullability.
3. `-- param:` gives one parameter a C# type, with or without its database type and nullability.
4. A C# type of one part, `UserId` or `List<UserId>`, resolved to the one type of that name and arity that the project can see, in the type override, the column types and `-- param:`.
5. Converters, the models' documentation, the diagnostics of a C# type and shared models working for a type chosen by any of these as they do for a type override's.

## Non-goals

- **A parameter typed by the column it is compared with**, or by its name.  The database does not report what a parameter is compared with, and a match by name guesses.  The parameter names its type.
- **A project-level or file-level mapping of a parameter.**  It has no source to key by.
- **A column mapped through a view to the table beneath it.**  A view's column has the view as its source on both engines, so a view's columns need their own entries.
- **Inferring a parameter's database type from its C# type's converters.**  The `sqlsource` tool describes a query without seeing the project's C#, and a converter's `long` fits several database types.  A parameter whose database type the database cannot infer still declares it, as today.
- **Changing how PostgreSQL types a parameter whose first use says nothing**, `@author IS NULL OR id = @author`, which stays `SQLSRC227`.  It would change describing, not typing, and a caller that sends a null without a type, Dapper for one, would then fail at run time.  The owner accepted that this pattern still needs the database type, or its two conditions the other way round.
- **A short name for a nested type.**  A name of several parts is a full name, as today.
- **Setting a source column's nullability at the project or file level.**  The header's `-- column:` takes a type alone.

## Anti-goals

- **A change to a project that uses none of this.**  No column type, no `->`, no short name: the same models and methods, byte for byte.
- **A marker that needs a describe.**  Adding, changing or removing a C# type on `-- column:` or `-- param:` leaves the query's sidecar current.
- **A silent choice between two types of one short name.**  Two types of that name and arity that the project can see, with different full names, are an error that lists them.
- **Short names costing every keystroke a walk of the framework.**

## What exists today

- **The type override**, read by `TypeOverrideGrammar`, held per engine on `SettingsLevel`, merged per key by `TypeOverrideMap`, and matched in `QueryTyper.TypeOf` through `OverrideLookup`, which gives each column and parameter a `PropertyType` with `Place`, the place that chose the type, and `Database`, the base map's type, set.  `ConversionPlanner` converts only where `Place` is set.
- **The C# names** are collected by `MappedTypeNames.Collect` and resolved by `MappedTypeResolver.Resolve` against the compilation on every run, by `GetTypesByMetadataName`.  `ClrTypeName`'s grammar requires `global::` before a name of one part, and `PropertyType.Text` is written from the name as parsed.
- **`-- column:`** is `name null|not null`, inside a query only (`SqlMarkerScope.ReadColumn`), applied by `ModelReader.IsNullable`, with `SQLSRC260` for a marker that names no column.  **`-- param:`** is `@name [type] [null|not null]` (`SqlMarkerScope.ReadParam`).
- **The query's hash** (`SqlQueryHash`) covers the SQL without comments, the tokens, and each declared parameter's name, type and nullability.  `-- column:` and `-- type-override:` are not in it.
- **Each sidecar column has its `origin`**, schema, table and column, of the table or view it is read from, on both engines; an expression, an aggregate and a column of a set operation have none.  The generator reads it today only for the documentation of a property.  A parameter has no origin.
- **Shared models** compare the properties' `Type.Text`, which `ClrTypeName.Text` writes from the name as parsed: `MyApp.Id` and `global::MyApp.Id` are one text, while a keyword and its name in `System`, `int` and `System.Int32`, are two.

## Approaches

The owner picked from these.

**Where the mappings are applied.**  One sensible way: column types become a setting beside the type override, read and merged per key the same way, and applied in `QueryTyper`, the step that picks each column's and parameter's type, where the column's origin is at hand.  A mapped column gets its `PropertyType` through the channel a type override uses, `Place` and `Database` set, so converters, documentation, diagnostics and shared models need nothing new.  Two others do not fit: writing the mappings as type-override entries cannot work, since an entry reaches every column of a type; and patching types in the model step splits typing across two steps, while `Database`, which converters need, is known in the first.

**How short names are found.**  The resolver runs on every edit, so the cost of a search is paid on every keystroke in an IDE.

1. **An index of each referenced assembly, built once and reused while that reference is unchanged; the project's own types by the compiler's lookup by name.**  One walk of each reference, on the first run and when a package changes.  More code, and a cache to keep right.
2. **A walk of every referenced assembly whenever a short name is in use.**  Simpler, nothing cached, and a large solution pays for a walk of the framework on every keystroke.

The owner chose 1: short names are meant to be the everyday way to write a type.

## Design

### Column types

```xml
<PropertyGroup>
    <SqlSourceColumnTypes>
        users.id=UserId
        posts.created_by_user_id=UserId
        billing.invoices.id=MyApp.Billing.InvoiceId
    </SqlSourceColumnTypes>
</PropertyGroup>
```

```sql postgres
-- column: posts.id -> PostId

-- name: ListPosts
-- column: total -> PostCount
-- param: @author -> UserId
SELECT p.id, p.created_by_user_id, count(*) AS total
FROM posts AS p
WHERE p.created_by_user_id = @author
GROUP BY p.id;
```

`ListPostsDto` has `Id` a `PostId`, `CreatedByUserId` a `UserId` and `Total` a `PostCount`; `ListPostsParams.Author` is a `UserId`.

**A source key** is `table.column`, which matches a column of that table or view in any schema, or `schema.table.column`, which matches that schema's alone.  Each part is plain or in double quotes, by the type override's rules for a key's parts: a plain part holds letters, digits, `_` and `$` and is compared ignoring case, a quoted part holds any character and is compared exactly, a quote in it doubled, `U&"..."` escapes read; in a project file `&` is `&amp;`.  It matches a sidecar column whose `origin` has that table and column, and that schema when the key has one.  A column with no origin is matched by no source key.  A source key names a column of either engine: a value in MSBuild serves both, and a marker its file's engine, though the key's grammar is the same for both.

**The four levels**, merged per key as the type override's are: the query's markers over the file header's, over the item metadata, over the property.  An entry for a key replaces the entries of a place below for that key alone, and `default` takes the column out of the entries below.  Of the entries that match one column, the most specific place's wins; within one place, one with a schema wins over one without, and then one spelled exactly as the origin's names over one that matches only ignoring case.

**In MSBuild**, `SqlSourceColumnTypes` is a property and item metadata, a list of `key=type` entries separated by white space, as `SqlSourceTypeOverrides` is, with its rules for white space, `<>`, `[]` and quotes; `=` and not `->`, since a `>` in a project file is `&gt;`.  An entry that is not valid is `SQLSRC014`, and the others apply.  Of two entries for one key in one value, the last wins.  The property is read and passed to the compiler as `SqlSourceTypeOverrides` is, in `SqlSource.props` and `SqlSource.targets`, and its name keeps the `SqlSource` prefix that root rule 6 requires.

**In the file header**, `-- column: <source key> -> <type>` or `-> default`, one entry for each line.  A header `-- column:` without `->`, with `null` or `not null`, or with a key of one part is `SQLSRC111`; two lines that give one key two types are `SQLSRC112`.  It is no longer `SQLSRC116` there.

### `-- column:` inside a query

```sql fragment
-- column: total -> PostCount
-- column: note null -> NoteText
-- column: display_name not null
-- column: id -> default
```

The value is a result column's name, as today, then `null` or `not null`, `-> <type>`, or both in that order: `-- column: note null -> NoteText`.  The name is one part, plain or quoted as today; a name with a `.` outside quotes is `SQLSRC111`, whose help says a source key goes in the file header.

- `-> <type>` gives the column that C# type, whatever any level's column types or type override say.  `-> default` takes it out of the column types of every level, so that the type override, or else the type map, gives its type.
- The nullability stays the column's, from the tool, a `!` or `?` mark or the marker's `null`/`not null`; `-> UserId` on a column that can be null gives `UserId?`.
- A marker that names no result column, or is on a query that returns no rows, is `SQLSRC260`, as today.  A marker that says `null` or `not null` of a column whose alias ends in `!` or `?` is still `SQLSRC260`; one that gives only a type is not, since it says the nullability once.
- Two markers for one column, compared ignoring case, conflict, `SQLSRC112`, when they give two types or two nullabilities; one may give the type and another the nullability.
- In a file with no `-- name:` line, which is one query and takes both kinds of marker, a key of two or three parts is the header's kind and a key of one part the query's.

### `-- param:`

```sql fragment
-- param: @author -> UserId
-- param: @author null -> UserId
-- param: @author int4 null -> UserId
```

`-> <type>` after the marker's other parts gives the parameter that C# type, whatever the type override says.  The type and the nullability stay optional, as today: `-- param: @author -> UserId` declares no database type, so the database infers it from the query, and says nothing of null, so the parameter is not null.  `-> default` is not valid for a parameter, `SQLSRC111`, since no level below a query maps a parameter.

Two markers for one parameter that differ in the C# type conflict, `SQLSRC112`, as two that differ in type or nullability do.  A marker with `->` and nothing else, `-- param: @author -> UserId`, still declares the parameter, so a parameter that only a token brings needs its database type as before, `SQLSRC117`.

**The hash leaves out what describes nothing.**  Today it adds the name of every parameter a `-- param:` marker names, even a marker with no type and no nullability (`SqlQueryHash`), so a new `-- param: @author -> UserId` would make the sidecar stale.  It now adds a declared parameter only when its marker gives a database type or a nullability, and never the C# type, so `->` can be added, changed or removed, with or without a marker of its own, without describing again.  A bare `-- param: @x`, which describes nothing either, leaves the hash too: a query with one gets a new hash once, and the build's check asks for a describe.  No sidecar of this repository has one, and nothing has been released.  The tool parses the new forms, through the shared parser, and ignores the C# type.

### Precedence with the type override

A column's C# type, in order: the query's `-- column: name -> type`; then the merged column types by its source; then the merged type override by its database type; then the type map.  A parameter's: its `-- param: ... -> type`; then the type override; then the type map.  So a column mapping at any level beats a type override at any level: naming a column is more specific than naming a type, and `-- column: name -> default` frees one column for the type override.

### What a mapped type does

A type chosen by a column type or by `->` is a type the project chose, as a type override's is.  Its `PropertyType` has `Place` set, worded for the place that chose it ("the property `SqlSourceColumnTypes`", "a `-- column:` marker of the file header", "the `-- param:` marker of the parameter"), and `Database`, the base map's type for the column's or parameter's database type with no override or mapping applied.  So:

- **Converters apply** under `CodeGen` exactly as for a type override: a `UserId` over a `bigint` column is read as a `long` and passed to `UserId`'s converter.  `SQLSRC279` and `SQLSRC280` apply as they do.
- **An array** of PostgreSQL mapped to an array of a foreign type, `posts.tag_ids=TagId[]`, converts element by element, and one mapped to a type that is not an array converts whole, by 8.2's rules.
- **`SQLSRC250` to `SQLSRC252`** are reported for the name as for a type override's, naming the place.
- **The documentation** of a property is unchanged; it names the database type and the origin.

### Short names

A name of one part, with or without type arguments, `UserId`, `Email`, `List<UserId>`, is a short name, valid in the type override, the column types and `-> type`.  `global::UserId` stays a name of the global namespace alone, and a name of several parts is a full name, resolved as today.  The type arguments of any name may be short: `System.Collections.Generic.List<UserId>`.

A short name is resolved to the types of that name and number of type arguments, in any namespace, not nested in another type, that the project can see: the project's own, and a reference's that is accessible to the project, `public` or under `InternalsVisibleTo`.  Their full names are the candidates.  One candidate is the type, the full name resolved as today, so the project's own wins over a reference's of that same full name.  Two candidates are `SQLSRC250`, whose message names each, up to a few and a count, and asks for the full name.  None is `SQLSRC250` as today.

The resolver's result gives each name its text with every short part expanded to the candidate's full name, `global::MyDomain.UserId` for `UserId` and `global::System.Collections.Generic.List<global::MyDomain.UserId>` for `System.Collections.Generic.List<UserId>`, and `PropertyType.Text` is that text.  A name with no short part keeps the text `ClrTypeName.Text` gives it today, so no existing project's output changes.  So the generated code compiles whatever `using` lines a file has, a short name and the full name of its type are one type to shared models and to converter matching, and a keyword and its name in `System` stay two, as today.  `ClrTypeName` keeps a short `UserId` apart from `global::UserId`, which both write `global::UserId` today.  `SQLSRC112` still compares entries by how they are written, so `UserId` and `MyDomain.UserId` in one place are two entries that conflict.

The project's types are found by `Compilation.GetSymbolsWithName` with a type filter.  Each referenced assembly's types are indexed by name and arity once, in a cache keyed by the `MetadataReference` and weakly held, and reused while the reference is the same; a run with no short name builds no index.  The index holds names and metadata names, not symbols, and the lookup re-finds a candidate's symbol in the current compilation by its metadata name.

### Diagnostics

No new id.  Changed:

| Id | Change |
|----|----|
| `SQLSRC014` | An entry of `SqlSourceColumnTypes` that is not valid |
| `SQLSRC111` | The new forms of `-- column:` and `-- param:`, and the header's `-- column:` |
| `SQLSRC112` | Two `-- column:` or `-- param:` markers that give one column or parameter two C# types; two header entries for one key |
| `SQLSRC116` | `-- column:` is no longer in the wrong place in the file header |
| `SQLSRC250` | A short name that more than one type answers, with the candidates |
| `SQLSRC250`-`SQLSRC252` | The places of a column type and of `-- param:` |
| `SQLSRC260` | A type-only `-- column:` on a column with a `!` or `?` mark is not an error |

## Testing

| Where | Cases |
|----|----|
| Parser tests | Each form of `-- column:` in a query and in the header, of `-- param:` with `->`; each invalid form, `SQLSRC111`; each conflict, `SQLSRC112`; the hash unchanged by `->`, by a marker of `->` alone and by a bare marker |
| Settings tests | `SqlSourceColumnTypes` read from the property and the metadata, invalid entries, white space and quotes; the merge per key over four levels, `default`, a key with a schema over one without, exact spelling over a match ignoring case |
| Generator tests | A column typed by each level; a result-name marker over a source key; a column mapping over a type override, and `-> default` freeing it; a view's column; a column with no origin; a parameter's `->`, with and without a database type; nullability kept; a converter applied to each; an array element by element; `SQLSRC250` to `SQLSRC252` at each new place; short names in the type override, the column types and `->`, in generic arguments, the project's own over a reference's of one full name, two candidates `SQLSRC250`, a type not accessible ignored; shared models equal across two spellings of one type; a project with none of it unchanged; the generated code compiling with no `using` lines on both Roslyn versions |
| Caching tests | An edit that changes no mapping re-types nothing; a reference unchanged reuses its index; an edit to a `->` re-runs typing, and the sidecar check, which re-runs on any edit of its file, gives an equal value |
| `BuildFileTests` | `SqlSourceColumnTypes` passed to the compiler |
| End-to-end, both engines | `Notes.sql`'s `accounts.id` and `notes.account_id` mapped to an `AccountId` in the project file, `count(...)` given a type by a query's `-- column:`, a parameter by `-- param: @id -> AccountId`, each run through the method and a converter |

## Documentation

- `docs/manual/settings.md`: a row and a section `setting-column-types` for column types; the precedence exceptions; the type override's section on short names and on a column type over it; the converters' "only where a type override chose the type" widened.
- `docs/manual/generator.md`: the `-- column:` section gains the type, and the header form.
- `docs/manual/parameters-and-tokens.md`: `-- param:` gains `->`.
- `docs/manual/sql-files.md`: the marker index's row of `-- column:`, now in the file header too.
- `docs/manual/recipes.md`: "Give an id a type of its own" leads with a column type, and keeps the domain as the schema's way.
- `docs/manual/diagnostics.md`: the sections of the ids above, and the index of markers per place.
- `docs/manual/README.md`: a row of "What works today".
- `README.md`: "Custom type mapping" and any FAQ answer that says a full name is needed.
- `docs/architecture/generator.md`: the column types in the settings and typing steps, and the short-name index.
- `src/SqlSource/AGENTS.md`, `docs/tech-debt`, `docs/deferred`: as the change touches them.
- The examples: a column type, a `-- column: ... ->` and a `-- param: ... ->` in each example project's `CodeGen` folder, by root rule 4.
- The epic outline: the Decided items that say a per-column C# type is not in the epic and that a C# type is fully qualified or a keyword, rewritten to this design; and a row for 8.7, which the owner asked for.

## Shared with phase 8.5 and 8.6

8.4 has merged.  8.5 (the realistic corpora) edits tests alone, which this change does not touch.  8.6 (examples and close) edits the example projects' `CodeGen` folders, the manual and the epic outline, all of which this change edits too; the second to merge resolves the overlap, and 8.6, which reads the manual whole, then reads this change's pages as well.  In `src/SqlSource` this change edits `QueryTyper`, `MappedTypeResolver`, `MappedTypeNames`, `ClrTypeName`, `SqlMarkerScope`, `SettingsLevel`, `SettingsLevelReader` and `QuerySettings`, none of which 8.5 or 8.6 is planned to change.

## Left to the builder

- The records and the steps, within the caching rules; whether column types share `TypeOverrideMap`'s merge or have their own.
- The text of each message, and how many candidates `SQLSRC250` lists.
- The wording of each new `Place`.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The setting's name, `SqlSourceColumnTypes`, and `=` in MSBuild where markers write `->`.
- A key of two parts matches the table in any schema; three parts match one schema; a schema wins over none in one place.
- `-> default` on a query's `-- column:` frees the column for the type override; `-> default` on `-- param:` is not valid.
- A header `-- column:` takes a type alone, never nullability; a query's takes a result name alone, never a source key.
- A type-only `-- column:` is allowed on a column whose alias has a `!` or `?`.
- A short name is one part; nested types and names of several parts are full names.
- Two candidates of a short name reuse `SQLSRC250`; the project's own type wins over a reference's only of the same full name.
- `PropertyType.Text` expands only the short parts of a name, so existing output is unchanged and `int` and `System.Int32` stay two types to shared models.
- The hash leaves out a `-- param:` marker that gives no database type and no nullability, a bare one included, which gives a query with a bare `-- param: @x` a new hash once.
- Short names do not find nested types.
- The index of a reference is cached by its `MetadataReference`, weakly.
- No new diagnostic id.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
