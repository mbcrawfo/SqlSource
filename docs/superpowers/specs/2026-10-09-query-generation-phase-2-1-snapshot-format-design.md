# Query generation, phase 2.1: the snapshot format - design

Date: 2026-10-09

Sub-phase 2.1 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  Phase 2 is cut into six sub-phases, each with its own spec, plan and pull request; the outline's section on phase 2 lists them.  This one delivers the code that reads and writes a sidecar.  The [sidecar format design](2026-10-07-sidecar-format-design.md) is the contract: this document says how it becomes code and adds nothing to the format.

It depends on no other sub-phase and can be built beside 2.2, 2.3 and 2.4.  Sub-phases 2.5 and 2.6 and phase 5 use it.

## Goal

The generator assembly holds a model of a sidecar, a reader, a writer and the two comparisons the format design defines, so that the tool and the generator share one implementation of the format.

```csharp
var result = SidecarReader.Read(text);
if (result.Sidecar is { } sidecar)
{
    var entry = sidecar.Find("GetUser");
    var written = SidecarWriter.Write(sidecar);
}
```

Success is:

- Both examples of the format design, sections 4.1 and 4.2, are read into the model and written back byte for byte.
- Every rule of the format design's section 1.5 has a test, and no text a user can put into a `.sql.json` file makes the reader throw.
- Every sidecar the tool can build, one whose engine is `postgres` or `mssql`, whose "always" keys have values and whose values are inside the limits the schema sets, is written as text that is valid against `schemas/sidecar-v1.schema.json`.
- Nothing in the generator's pipeline changes: no step reads a sidecar before phase 5, and the existing allocation budgets and caching tests are untouched.

## Decisions

From the epic and the format design, unchanged: the format, its reader rules, its compatibility rules, what `--check` compares, and the JSON Schema; a hand-written reader and writer in the generator assembly, since the generator can take no JSON library; the tool version compared by `VersionPrefix` alone.

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Where the code lives | A new folder, `src/SqlSource/Snapshot/`, namespace `SqlSource.Snapshot` | It is neither parsing of SQL nor a pipeline step, and the tool and the generator both use it. |
| A file of another format version | Read as its version alone: no entries, no error | A reader of format 1 cannot say whether a file of format 2 is well formed.  Phase 5 turns the version into its diagnostic. |
| A known key written twice in one object | Malformed | The format design names a duplicate query; a duplicate of any key the reader reads is the same mistake, and "the first wins" hides it. |
| Provenance values | Kept as strings | The reader must ignore a value it does not know, and the writer must write back what it read. |
| A type under an engine the reader does not know | Read as its `name` alone | The format design asks every engine for a `name`.  Phase 5 reports the engine. |
| How errors are carried | A kind, a span and one optional argument; one error for a file | Phase 5 reports one diagnostic for a malformed file.  A kind keeps the tests exact and the wording in one place. |
| The tool's version in code | The first three parts of the generator assembly's version | `AssemblyVersion` is `VersionPrefix` and the run number on every build, so no suffix reaches it. |
| The schema's test | The files the tests write as the tool would are validated with JsonSchema.Net, a validator of JSON Schema draft 2020-12 and a dependency of the test project alone | The schema is strict and the writer is hand-written.  Nothing else keeps the two in step.  Its source is MIT and its current binaries come under the Open Source Maintenance Fee; the owner chose it with that known.  The plan names the version. |
| The examples in the tests | Copies of the format design's sections 4.1 and 4.2, as files in `tests/SqlSource.Tests/Snapshot/Examples/` | The owner's choice over reading them from the document: a test does not depend on a file of `docs/superpowers`.  Nothing checks the copies against the document; a pull request that changes one changes the other. |

## Out of scope

- Any use of a sidecar by the generator: pairing a `.sql.json` file with its `.sql` file, the diagnostics for a stale, missing, orphaned or malformed sidecar, and the type maps.  They are phase 5's.
- Any diagnostic descriptor.  This sub-phase adds none.
- The `**/*.sql.json` item in the props, and the nesting under a `.sql` file in an IDE.  Phase 5.
- Writing a file to disk.  The writer gives a string; sub-phase 2.5 writes it.
- Describing a query, and the model of a description.  Sub-phase 2.5.

## The model

Records with value equality, each collection an `EquatableArray<T>`, since phase 5 caches them in the pipeline.

| Type | Members |
|----|----|
| `Sidecar` | `FormatVersion`, `ToolVersion`, `Queries` in the file's order |
| `SidecarEntry` | `Name`, `NameSpan`, `Hash`, `Engine`, `Database`, `ServerVersion`, `ResultKind`, `MatchesTable`, `Plan`, `TableMatch`, `Parameters`, `Columns` |
| `SidecarParameter` | `Name`, `Ordinal`, `Type`, `Nullable`, `TypeSource` |
| `SidecarColumn` | `Ordinal`, `Name`, `Type`, `Nullable`, `NullableSource`, `Origin`, `Identity`, `Computed` |
| `SidecarOrigin` | `Schema`, `Table`, `Column` |
| `SidecarTable` | `Schema`, `Table` |
| `SidecarType` | Abstract: `Name` |
| `PostgresType` | `Name`, `Kind`, `Schema`, `InternalName`, `Length`, `Precision`, `Scale`, `Element`, `Base`, `Labels`, `Subtype` |
| `SqlServerType` | `Name`, `MaxLength`, `Precision`, `Scale`, `UserType` |
| `SqlServerUserType` | `Schema`, `Name`, `AssemblyQualifiedName` |
| `OtherEngineType` | `Name` |

- A member is nullable wherever the format design's tables let a reader find `null` or nothing: rule 2 of its section 1.5 says which keys a reader cannot do without, and every other key is nullable.
- `SidecarEntry.NameSpan` is the span of the entry's key in the text that was read, quotes included, so that phase 5 can report at the entry.  It is `default` in an entry the tool builds, and the writer does not read it.
- `ResultKind` is an enum, `SidecarResultKind`, with `Rows` and `None`: a value the reader does not know is malformed.
- `PostgresType.Kind` is the string as written.  `PostgresTypeKind`, an enum of the seven kinds, is read from it by a `TryRead` that returns false for any other value and never throws; phase 5 reports such a type as unsupported.
- `Plan`, `TableMatch`, `TypeSource` and `NullableSource` are strings.  `SidecarValues` holds a constant for each value the format design lists, which the tool writes.
- `Sidecar.Find(name)` returns the entry with that name, compared ordinally, or null.

`SidecarFormat` holds what is fixed for the format: `Version`, which is `1`; `SchemaUrl`; `Warning`, the text of `_WARNING`; and `PathFor(sqlPath)`, which appends `.json`.

`PackageVersion.Prefix` is the version of the package, `major.minor.patch`: the first three parts of `typeof(SqlSourceGenerator).Assembly.GetName().Version`.  The analyzer rule RS1035 allows the call; the review of this spec built it.

## The reader

`SidecarReader.Read(string text)` returns a `SidecarReadResult` in one of three states:

| State | `Sidecar` | `FormatVersion` | `Error` |
|----|----|----|----|
| Read | set | `1` | null |
| Another format version | null | the file's | null |
| Malformed | null | null | set |

A `SidecarError` is a `SidecarErrorKind`, a span in the text, and an argument that the kind may carry.

| Kind | When | Span | Argument |
|----|----|----|----|
| `InvalidJson` | The text is not one JSON object, or is nested deeper than 64 levels | The character that cannot be read; an empty span at the end of a text that stops early | |
| `MissingKey` | A key that a reader cannot do without is absent | The opening brace of the object that lacks it | The key |
| `WrongType` | A key the reader reads holds a value of another JSON type, a number that is not a 32-bit integer included | The value | The key |
| `DuplicateKey` | A key the reader reads, or the name of a query, is written twice in one object | The second key, quotes included | The key |
| `OrdinalMismatch` | An `ordinal` is not the index of its element | The value | The ordinal as written |
| `ColumnsWithoutRows` | `columns` is present and `resultKind` is `none` | The `columns` key, quotes included | |
| `UnknownResultKind` | `resultKind` is not `rows` or `none` | The value | The value |

The rules, which are the format design's section 1.5 made exact:

- **Two passes.**  The first walks the top-level object, skips every value, and takes `formatVersion`.  A text that is not valid JSON is malformed here, whatever its version.  A version that is not `1` ends the read in the second state.  The second pass reads the model.
- **An unknown key is skipped with its value**, at every level, and a value under such a key may be any JSON, a number with a fraction or an exponent included.
- **A missing key** that the format design marks "always" and a reader can do without is read as null.
- **A `null`** under a key that a reader cannot do without is `WrongType`, except under `nullable`, where the format design allows it.  `columns` holds an array wherever it is written: `null` is `WrongType` under either result kind.
- **The engine picks the shape of a type.**  `postgres` reads a `PostgresType` and requires `name`, `kind`, `schema` and `internalName`, and the nested type its kind names: `element` for `array`, `base` for `domain`, `labels` for `enum`, `subtype` for `range` and `multirange`.  A kind the reader does not know requires nothing more.  `mssql` reads a `SqlServerType` and requires `name`, `maxLength`, `precision` and `scale`.  Any other engine reads an `OtherEngineType` and requires `name`.
- **Strings** are read with every escape JSON has, surrogate pairs included.  An escape that JSON does not have, a control character inside a string, and a string that does not close are `InvalidJson`.
- **Nothing but white space may follow the object, or come before it.**  A comment or a trailing comma is `InvalidJson`, and so is a byte order mark: the caller removes it, as the compiler does for a file it reads.
- **The first error ends the read.**

The reader is a tokenizer over the text and a recursive reader of the model above it.  It allocates no string for a key, for a value it skips, or for a value it compares with a fixed list; a string without an escape is one `Substring`.  The depth limit is what keeps a file of ten thousand `[` from overflowing the stack: an exception in the generator costs every type its generated code.

## The writer

`SidecarWriter.Write(Sidecar sidecar)` returns the text of the file.

- The layout is the format design's section 1: two-space indent, `\n` line endings, one key or array element on a line, a trailing newline, no byte order mark, keys in the order of the format design's tables.  An empty array is `[]` on the line of its key.
- `_WARNING` and `$schema` come first, from `SidecarFormat`.
- A key marked "always" is written with `null` when it has no value.  A key marked with a condition is written when the condition holds, and a facet when it has a value.
- The condition decides, and not the value.  An entry whose `ResultKind` is `None` has no `columns`, `matchesTable`, `plan` or `tableMatch`, whatever the model holds; one whose `ResultKind` is `Rows` has all four, with `null` for one that has no value.  `typeSource` is written when `Type` is not null, with `null` when it has no value, and not otherwise.  The writer checks nothing and never throws: a model that the schema would refuse is the caller's mistake, and the reader refuses what it must.
- A string is written with `\"` and `\\`, with `\b`, `\f`, `\n`, `\r` and `\t` for those characters, and with `\u00XX` for every other character below U+0020.  Every other character is written as it is, `/` and characters outside ASCII included.
- An `OtherEngineType` is written as its `name`.

The layout of an empty array and the escapes of a string are rules of the format, and the format design's section 1 states them.

The examples of the format design are the oracle.  Where an example disagrees with the rules of its section 1, the rules win, and the pull request corrects the example.

## The two comparisons

Both are defined by the format design's section 3, and both sub-phases 2.5 and 2.6 use them.

- **Current.**  `Sidecar.IsWrittenBy(toolVersion)`: the file's `formatVersion` is `SidecarFormat.Version` and its `toolVersion` equals the argument.  `SidecarEntry.IsCurrentFor(hash, database)`: the entry's `hash` equals the argument ordinally and its `database` equals the argument ignoring case.  An entry is current when both hold.
- **What `--check` compares.**  `SidecarComparer.FindDifference(committed, described)` returns null when two entries agree on `hash`, `engine`, `database`, `resultKind`, `matchesTable`, and on each parameter's and column's `name`, `ordinal`, `type` and `nullable`, and otherwise the first difference.  `serverVersion`, `plan`, `tableMatch`, `typeSource`, `nullableSource`, `origin`, `identity` and `computed` are not compared, and neither is `NameSpan`.

A `SidecarDifference` is a path and the two values as text.

- **The order** is the order of the list above, then `parameters`, then `columns`, each array by index, and inside an element `name`, `ordinal`, `type`, `nullable`.  The first difference in that order is the one returned.
- **A path** names a leaf: `hash`, `matchesTable.table`, `parameters[0].type.name`, `columns[2].type.element.internalName`, `columns[0].type.userType.schema`.  A `type` is compared key by key, in the order of the format design's table for its engine, and a nested type is descended into.  `labels` is compared as the two arrays are: `columns[1].type.labels.length` with the two counts, and then `columns[1].type.labels[2]`.
- **Arrays of two lengths** differ at `parameters.length` or `columns.length`, with the two counts, before any element is compared.  `columns` absent on one side differs at `columns`.
- **A value as text** is its JSON: `"int4"`, `true`, `3`, `null`.  A member that is null on one side and an object on the other differs at the member, with `null` and `an object`.

Database names are compared ignoring case here, as the format design's reader rule 6 says and sub-phase 2.4 does; every other string is compared ordinally.

## The schema

`schemas/sidecar-v1.schema.json` is the schema of the format design's section 5, as a file.  Its `$id` is `SidecarFormat.SchemaUrl`.  The test project copies it to its output folder and validates with it.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests.

1. The epic outline: the row of 2.1 says In progress.
2. The model, `SidecarFormat`, `SidecarValues` and `PackageVersion`.
3. The tokenizer, with its tests.
4. The reader.
5. The writer, the copies of the two examples and their round trip, and the round trip of the sidecars Bogus builds.  The test project gains its reference to Bogus here, which `Directory.Packages.props` pins and nothing references yet.
6. The two comparisons.
7. The schema file, the validator in `Directory.Packages.props` and the test project, and the validation tests.
8. `src/SqlSource/AGENTS.md`, and the outline's row set to Done.

## Testing

In `tests/SqlSource.Tests/Snapshot/`.  The folder is not compiled into `tests/SqlSource.Tests.RoslynFloor`: nothing here depends on the compiler's version.

| Where | Cases |
|----|----|
| `SidecarTokenizerTests` | Each token; every escape, a surrogate pair, an escape JSON lacks, a control character in a string, a string left open; an integer at each end of the 32-bit range and one past it; a fraction and an exponent, skipped and read; depth 64 and 65; text after the object |
| `SidecarReaderTests` | The two examples, member by member.  For each row of the error table, the kind, the span and the argument.  A `null` under each key a reader cannot do without, and `columns` as `null` under each result kind.  Each key a reader cannot do without, removed.  Each other "always" key, removed and read as null.  An unknown key at each level, with a nested value.  Keys in another order.  A format version of `2`, of `0`, absent and a string.  An unknown engine, an unknown kind, an unknown provenance value.  A text that starts with a byte order mark, an empty text, white space alone: each `InvalidJson` |
| `SidecarWriterTests` | The two examples, byte for byte.  Each escape.  A name outside ASCII.  An entry with no rows has no `columns`, `matchesTable`, `plan` or `tableMatch`, also when the model holds them.  An entry with rows and none of the four has each as `null`.  `typeSource` with and without a `Type`.  An empty `parameters`.  Each facet written only when set |
| `SidecarRoundTripTests` | Sidecars built by Bogus from a fixed seed, in two sets.  As the tool builds them: each of the two engines, each kind, every "always" key with a value, and every value inside the schema's limits, which ask for a column at least, names that are not empty, a hash of 64 hex digits and a scale between -1000 and 1000.  As a reader may find them: another engine, an unknown kind, nulls where the reader allows them, and nothing the writer's conditions leave out.  Both sets: written, read and equal apart from `NameSpan`; written again and the same text |
| `SidecarComparerTests` | For each compared member, a difference is found, with its path and its two values.  `labels` of two lengths, and of one length with a label that differs; a `userType` that differs.  Two differences: the first in the order.  Arrays of two lengths; `columns` on one side; `matchesTable` null on one side; a nested type that differs three levels down.  For each member that is not compared, none is.  Database names that differ in case.  `IsWrittenBy` and `IsCurrentFor` for each of their inputs |
| `SidecarSchemaTests` | The two examples and every sidecar of the round trip's first set are valid; the second set is not validated.  The schema's `$id` is `SidecarFormat.SchemaUrl`.  A file with an extra key is not valid, though the reader reads it |
| `SidecarReaderAllocationTests` | A budget in bytes for each character of the first example.  The implementer measures it, pins it, and states it in the pull request |
| `PackageVersionTests` | The value is three numbers and is the start of the assembly's version |

## Documentation

- `src/SqlSource/AGENTS.md`: a section on `Snapshot/`.  The format is a contract with committed files and with the schema; a change to what the writer gives is a change to the format design, to the schema, and to the examples in the document and in `tests/SqlSource.Tests/Snapshot/Examples/`, in one pull request; the reader must never throw; `Snapshot/` uses `EquatableArray<T>` and `TextSpan` and nothing of `Parsing/` or `Generation/`.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md` and `docs/diagnostics.md`: nothing in them changes.  A user can do nothing with a sidecar before phase 5.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The sidecar format design: an example that the round trip shows to break its own rules, corrected, with its copy in the tests.  Nothing else: the commit that added this spec gave its section 1 the layout of an empty array and the escapes, and its reader rule 6 the comparison of database names.
- The epic outline: in steps 1 and 8.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
