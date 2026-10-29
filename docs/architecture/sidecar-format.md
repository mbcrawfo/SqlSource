# The sidecar format

A [sidecar](README.md#sidecar) is a contract between `sqlsource describe` and the generator, two packages versioned apart, and with every committed `<file>.sql.json`.  Two files hold it:

- [`schemas/sidecar-v1.schema.json`](../../schemas/sidecar-v1.schema.json) holds its shape, and in the `description` of each key what the key's value means.  It is strict: it says what this version of the tool writes, and `SidecarSchemaTests` validates the examples of [`tests/SqlSource.Tests/Snapshot/Examples/`](../../tests/SqlSource.Tests/Snapshot/Examples) against it.
- This page holds the rules that a schema cannot say: the text's layout, what is written when, how a file is read, and which change to the format needs a new `formatVersion`.

How the code reads, writes and compares a sidecar is [snapshot](generator.md#snapshot), and how the generator checks one against its `.sql` file is [sidecars](generator.md#sidecars).

## The text

A sidecar is one JSON object in UTF-8 without a byte order mark, with line feeds, a two-space indent, one key or array element on each line, and a trailing newline.  An empty array or object is `[]` or `{}` on the line of its key.  Its values are objects, arrays, strings, signed 32-bit integers, booleans and `null`.  A string is written with `\"` and `\\`, with `\b`, `\f`, `\n`, `\r` and `\t` for those characters, and with `\u00XX`, its hex digits in upper case, for every other character below U+0020; every other character is written as it is, `/` and characters outside ASCII included.  [`SidecarJsonBuilder`](../../src/SqlSource/Snapshot/SidecarJsonBuilder.cs) writes the layout.

Keys are written in a fixed order and never sorted: the order of the examples, which `SidecarWriterTests` holds [`SidecarWriter`](../../src/SqlSource/Snapshot/SidecarWriter.cs) to, and in which the schema lists each object's properties.  The order lets a reviewer read a diff; no reader depends on it.  The entries of `queries` are in the order of their queries in the `.sql` file.

The tool compares the text it would write with the file on the disk after turning the file's `\r\n` into `\n`, so a checkout that changes line endings does not make it write the file again (`FileOutcomes`).

## What is written

`SidecarWriter` writes a key when the format's condition for it holds, whatever the model holds, and checks nothing:

- A key that every object of its kind has is always written, with `null` when it has no value.
- `matchesTable`, `plan`, `tableMatch` and `columns` are written exactly when `resultKind` is `rows`, and a parameter's `typeSource` exactly when its `type` is not `null`.
- A PostgreSQL type writes `length`, `precision` and `scale` only when its modifier sets them, and the `element`, `base`, `labels` or `subtype` that its `kind` needs.  A SQL Server type writes its three facets always, and `userType` for an alias or a CLR type.
- `bindName` is written on a parameter's own type alone, since the tool sets it nowhere else.  Every PostgreSQL parameter's type has one, by the rules of [type names](postgres-describer.md#type-names).  No SQL Server type has one: the generated code binds a SQL Server parameter by its `SqlDbType` and facets, or a CLR type by its `userType`, as [query methods](generator.md#query-methods) describes, and needs no name to bind by.

Some things are never written.  The SQL text is beside the sidecar, and the hash proves the two agree.  A timestamp would change the file on every run.  The C# types are the generator's to choose, and change without a database.  An entry for a query with tokens says nothing of them beyond what the hash covers, so it is the entry of a plain query.

The tool never writes a sidecar with no entry.  A run over the whole run unit that has reported no error deletes the sidecar of a `.sql` file in which no query needs an entry, except one of a newer format, which it leaves, and one it cannot open, which it leaves with `SQLSRC218` (`FileOutcomes`).

## Reader rules

[`SidecarReader`](../../src/SqlSource/Snapshot/Reading/SidecarReader.cs) reads by these rules, and so accepts more than the schema: the schema says what this version of the tool writes, and the rules what a reader tolerates.  How it reads is [reading a sidecar](generator.md#reading-a-sidecar).

1. **The grammar is checked first, then `formatVersion` is read**, wherever it stands.  A text that is not one strict JSON object nested at most `SidecarTokenizer.MaxDepth` levels is malformed whatever its version.  A file of another version that passes is read as its version alone, since this reader cannot say whether such a file is well formed; only a second `formatVersion` in it is an error.
2. **An unknown key is skipped with its value**, at every level, inside a type object too.  `bindName` anywhere but on a parameter's own type is an unknown key.  This is what makes adding a key free.
3. **Key order is not significant.**  An entry's `parameters` or `columns` that comes before its `engine` is read once the entry ends.
4. **An absent key reads as `null`**, which means unknown or not applicable, and for most keys a reader never tells an absent key from a `null` one.  Two keys a reader can do without are refused as `null` all the same: `bindName`, which is a string when present, and `columns`, which is never `null` whatever the `resultKind`.  The other exceptions are the keys a reader cannot do without, which must be present and not `null`:
   - `formatVersion`, `toolVersion` and `queries` at the top;
   - `hash`, `engine`, `resultKind` and `parameters` of an entry, and `columns` when `resultKind` is `rows`;
   - `name`, `ordinal` and `nullable` of a parameter, and `ordinal`, `name`, `type` and `nullable` of a column, of which a parameter's `type` and either `nullable` may be `null`;
   - `name`, `kind`, `schema` and `internalName` of a PostgreSQL type, and the `element`, `base`, `labels` or `subtype` its `kind` needs, `null` counting as absent;
   - `name`, `maxLength`, `precision` and `scale` of a SQL Server type, and `name` of a type of an engine the reader does not know.
5. **A malformed file** is one whose text is not one JSON object nested at most `SidecarTokenizer.MaxDepth` levels, that lacks a key of rule 4, or in which: a key the reader reads has a value of another JSON type, a number that is not a 32-bit integer included; a key the reader reads, or a query's name, is written twice in one object; an `ordinal` is not its element's index; `columns` is present when `resultKind` is `none`; or `resultKind` is neither `rows` nor `none`.  The first such error ends the read, and `SidecarErrorKind` lists them.  The generator reports the file once, `SQLSRC237`, and uses none of its entries; the rest of the project is unaffected.
6. **A value of an open list is kept as written**, for a later step to judge:
   - The types of an entry whose `engine` the reader does not know are each an `OtherEngineType`, with its `name` and, on a parameter's own type, its `bindName`.  The generator reports `SQLSRC242` at each query whose entry it is, since its engine is not the file's dialect.
   - A PostgreSQL `kind` that the reader does not know is kept.  Unless an override or the type map knows the type by its name, `PostgresTypeMap.Map` fails it as `TypeMappingKind.NotWrittenByTheTool`, and the generator reports `SQLSRC246` at the query's name, with the help to describe it again.
   - A `typeSource`, `nullableSource`, `plan` or `tableMatch` that the reader does not know is kept, since the generator never reads them.
7. **Strings are compared ordinally**, a query's name included, except where the generator or the tool matches one with a name a user writes, or with a table of its own, which ignores case:
   - a parameter's `name` against the query's parameter (`QueryTyper`), and `database` against the query's (`DatabaseName.Comparer`), since two names that differ only in case read one connection variable;
   - a column's `name` against a `-- column:` marker (`ModelReader`), its `origin` against the keys of `columnTypes` (`ColumnTypeMap`), and a type's `schema` and `internalName` against the keys of `typeOverrides` (`TypeOverrideMap`), a plain part of a key ignoring case and a part in quotes exactly, as the manual's [type overrides](../manual/settings.md#type-overrides) and [column types](../manual/settings.md#column-types) say;
   - a PostgreSQL `internalName` of `pg_catalog` or of an extension the type map knows (`PostgresTypeMap`, `TypeKeyNames`), and a SQL Server type's base name in the type map and the binder's table (`SqlServerTypeMap`, `SqlServerBinding`).
8. **Everything about a query is in its entry**; only `formatVersion` and `toolVersion` come from the top.

## Compatibility

`formatVersion` is the contract with every reader of a file.  The test for each change to the format is whether a reader of the previous version, following the reader rules, still gives correct code from the new file, or fails closed on exactly the part it does not know: one diagnostic that names the unknown engine, kind or value, and no code from that query, never wrong code.  If it does, the change keeps `formatVersion`; if not, it raises it.

A change keeps the version when it adds:

- a key anywhere: at the top, on an entry, a parameter, a column, an `origin`, a `matchesTable` or a type object, which an older reader skips;
- an `engine` with its own type shape, whose entries an older generator reports with `SQLSRC242`, as long as every type object has a string `name` and no column's `type` is `null`, which an older reader needs of any engine;
- a PostgreSQL `kind`, with any keys it needs, whose queries an older generator reports with `SQLSRC246`;
- a value of `typeSource`, `nullableSource`, `plan` or `tableMatch`, which no generator reads.

So does moving a key that a reader neither requires nor refuses as `null` between "written when it has a value" and "always written", since a reader reads an absent key as `null`, and spelling the informational `serverVersion` another way.

A change raises the version when it:

- removes, renames or retypes a key, or changes what it means: ordinals counted from one, `maxLength` in characters, a PostgreSQL `name` that drops its qualification, a `nullable` with a fourth state;
- changes the algorithm or the input of `hash`, [`SqlQueryHash`](../../src/SqlSource/Parsing/SqlQueryHash.cs), since an older generator would call every entry stale and a newer one would take a stale file;
- adds a `resultKind`, which an older reader refuses as malformed;
- adds a key that a reader cannot do without, or requires `columns` when `resultKind` is `none`;
- writes `bindName` or `columns` as `null`, leaves out a parameter's or a column's `nullable`, or gives a new engine a type object without a string `name` or a column a `null` `type`, each of which an older reader refuses as malformed;
- writes a file that is not one strict JSON object nested at most `SidecarTokenizer.MaxDepth` levels, which an older reader reports as malformed instead of as newer;
- makes `queries` anything but an object keyed by the query's name;
- changes an existing engine's type object in any of these ways.

A version is the whole file's: the tool writes only `SidecarFormat.Version`, and writes a file of a lower version again in full.

Either kind of change changes the writer, the reader, the schema and the examples in one pull request.  A change that keeps the version edits `sidecar-v1.schema.json` in place, as long as every file an earlier tool wrote stays valid; one that would make such a file invalid, or that raises the version, is a new `-vN` schema with its own `SidecarFormat.SchemaId`, since files already written name the old URL, as `schemas/AGENTS.md` says.

## Versions

What the generator does with a file's versions, in `SidecarFileChecks`:

| The file has | The generator |
|----|----|
| this version's `formatVersion` | reads the file |
| a higher `formatVersion` | reports `SQLSRC238`, to update the package, and uses no entry of the file |
| a lower `formatVersion` | reports `SQLSRC239`, to run `describe`, and uses no entry of the file |
| no `formatVersion`, or one that is not an integer | reports the file malformed, `SQLSRC237` |
| this version's `formatVersion`, and a `toolVersion` whose prefix, `PackageVersion.PrefixOf`, is not `PackageVersion.Prefix` | warns `SQLSRC240`, and reads the file: the packages share a version, so the difference means that the tool and the package reference are out of step, not that the file cannot be read |

Reading several versions would be possible later, since the version is read first; nothing promises it.

What the tool does, in `RunDecisions` and `FileOutcomes`:

- An entry is current when its `hash` is the query's, its `database` is the query's ignoring case, and its file was written in this format by this tool, `PackageVersion.Prefix` (`SidecarEntry.IsCurrentFor`, `Sidecar.IsWrittenBy`).  `describe` describes every query that has no current entry, so a new version of the tool describes every query again on its first run; `--check` describes every query.
- A file is written when every query in it that needs an entry was described in this run or has a current entry, and is otherwise left as it was, so an entry from an older tool never stands beside a new one.
- A file of a higher `formatVersion` is a newer tool's: a run that writes reports `SQLSRC221` and leaves it as it is.

## What `--check` compares

`--check` compares each query's described entry with its committed one, as [a describe run](tool.md#a-describe-run) describes, through [`SidecarComparer.FindDifference`](../../src/SqlSource/Snapshot/SidecarComparer.cs): `hash`, `engine`, `database`, `resultKind`, `matchesTable`, and each parameter's and column's `name`, `ordinal`, `type` and `nullable`, in that order.  A `type` is compared whole, `bindName` included, since it changes what the generated code does.  `_WARNING`, `$schema`, `formatVersion`, `toolVersion`, `serverVersion`, `typeSource`, `nullableSource`, `plan`, `tableMatch`, `origin`, `identity` and `computed` are not compared: the versions and the server differ between a developer's machine and CI by design, provenance, `identity` and `computed` are informational, and `origin`, which decides a column's type through `columnTypes`, is not compared all the same.
