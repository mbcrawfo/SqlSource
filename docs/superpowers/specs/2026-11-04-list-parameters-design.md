# List parameters

## Intent

A query should be able to filter by a list of values: `WHERE id = @id AND category IN @categories`.  Today it cannot, except on PostgreSQL through an array (`category = ANY (@categories)`), and the input model's property for that array is a `T[]`, which forces a caller holding a `List<T>`, a `HashSet<T>` or a LINQ query to copy it first.

This work adds list parameters, which expand to one value parameter per element when the generated method runs, on both PostgreSQL and SQL Server.  It also makes the array parameters of PostgreSQL accept any `IEnumerable<T>` in a generated method.  SQL Server has no arrays, so expansion is its only way to send a list; PostgreSQL users get the same form when they want it, for example to write one query text that works on both engines, or so that a generic plan sees how many values there are.

Nothing about the user's SQL changes except where they wrote a list: SqlSource never rewrites `IN` into `= ANY`, and never falls back to another form of SQL on its own.

## Goals

- **`IN @name` and `NOT IN @name`, written without parentheses, make `@name` a list parameter.**  No marker is needed: outside `position(substring IN string)` (section 1), a bare parameter after `IN` is not valid SQL in any supported dialect, so the form can only mean a list.  `IN (@name)` stays one value, as SQL reads it.
- **Optional list filters work**: `(@categories IS NULL OR category IN @categories)`.  A list parameter may be used after `IN`/`NOT IN` and as the operand of `IS NULL`/`IS NOT NULL`; any other use is a build error.
- **The input model's property is `IEnumerable<T>`**, `T` the element's C# type, in both the `models` and `codegen` outputs.  It is `IEnumerable<T>?` when the list can be null.
- **The generated method expands each list when it runs**, building the SQL with `string.Create` in one allocation, for any number of lists in a query and any number of uses of one list.
- **Lists are padded to a few fixed sizes** so that a query has a bounded number of distinct SQL texts, and therefore of cached plans.  `no-list-padding` turns this off.
- **A list too large for its engine's parameter limit throws `ArgumentException`** with a message that names the query, the property, the count and the limit, before anything is sent.
- **PostgreSQL array parameters are `IEnumerable<T>` in `codegen` input models**, and the generated method sends any sequence; they stay `T[]` in the `models` output.

## Non-goals

- **Lists of rows or tuples**: `INSERT ... VALUES @rows`, `(a, b) IN @pairs`.  These are lists of objects, a different feature.
- **Lists as function arguments**, such as `COALESCE(@values)` or `GREATEST(@ids)`.
- **Table-valued parameters and `OPENJSON`/`STRING_SPLIT` on SQL Server.**  The manual names them as what a user writes for very large lists; SqlSource does not produce them.
- **Making the `sql` and `models` outputs run anywhere.**  In those outputs SqlSource's job is to put the user's query in a string constant.  A query that Dapper or another library cannot run, such as `IN @ids` through Dapper on PostgreSQL (Dapper sends a list there as an array and does not expand it), or an optional list filter through Dapper on SQL Server, is not SqlSource's concern, no more than invalid SQL is.  The owner ruled this out.
- **Caching the expanded SQL** (approach 2 below).  It was considered and not chosen; nothing a user writes or sees depends on it.

## Anti-goals

- **Rewriting the user's SQL** beyond replacing each `IN` use of a list with its expansion.  No `IN` becomes `= ANY`; no list becomes a JSON parameter.
- **A silent fallback**: a list over the limit must throw, not switch to another form of SQL.
- **Any change to a query without a list**: its constant, its method, its hash and its sidecar stay as they are.  The one exception is the PostgreSQL array change, which changes the input-model type and the binding of a `codegen` query with an array parameter, deliberately.
- **A runtime dependency**: the generated methods use no library of SqlSource's, and still don't.

## Constraints

- `src/SqlSource` targets netstandard2.0, runs on the Roslyn 4.14 floor, and caches the incremental pipeline by value equality.  Anything added to a pipeline value, such as a flag on `SqlQueryParameter`, must compare by value.  The parser is pure, and a parse must not get slower for queries without lists; `SqlDeclaredVariables`' cheap pre-search (an `IndexOf` of the keyword before any walk) is the model.
- `SqlParameterList` alone builds a query's parameter list (`src/SqlSource/Parsing/AGENTS.md`).
- The hash (`SqlQueryHash`) must not change for any query without a list.  A change to how segments are split, or to what the hash reads from them, would otherwise make every stored hash stale.
- The sidecar format stays at version 1.  No new key is needed (see the tool section).
- Generated methods must compile in the user's project as it is: .NET 8 or later, C# 12 or later, Npgsql 8, 9 and 10, Microsoft.Data.SqlClient as today.
- Npgsql, on 8, 9 and 10 alike, sends an array parameter only from a `T[]` or an `IList<T>`, and resolves the converter from `NpgsqlParameter<T>`'s `T`, not from the value: an `NpgsqlParameter<IEnumerable<int>>` fails even when it holds an `int[]` or null.  A probe in this session established this (the full results are summarised under PostgreSQL arrays).
- The repository's rules apply in full: docs change in the same PR as the code; a new diagnostic updates `docs/manual/diagnostics.md`; examples and end-to-end sidecars are regenerated with `tools/describe-end-to-end.sh`; a change to a describer runs that describer's tests on its floor image.

## Approaches

The approaches differ in when the expanded SQL is built in a generated method; the SQL produced is the same.

1. **Build the SQL on each call with `string.Create` (chosen).**  The generator splits the query at each list use and each token.  At run time the method counts each list, pads it, computes the exact length, and writes the text in one allocation.  It extends what token methods already do (`MethodWriter.AppendCreate`), so a query with tokens and lists is one `string.Create`.  The cost is one string per call, small next to the round trip.  The owner chose it because it builds on existing code and keeps generated methods free of static state.
2. **Cache the expanded SQL per padded size.**  With padding, a query with one list has at most 13 texts, so each could be built once.  It saves the allocation but adds per-query static state, thread-safe lazy creation, a combined key for several lists, and it does not help a query with tokens or one with padding turned off.  It could be added later on top of approach 1.

Also considered and dropped, because they rewrite the user's SQL, which the owner ruled out: a single JSON parameter unpacked with `OPENJSON` on SQL Server; rewriting `IN @list` into `= ANY(@list)` on PostgreSQL.

## The design

### 1. What a user writes

```sql
-- name: ListProducts
SELECT id, name FROM products
WHERE (@categories IS NULL OR category IN @categories)
  AND id NOT IN @excluded;
```

- **A list use** is a parameter whose nearest preceding word, outside strings, quoted identifiers and comments, is `IN` (any case), with nothing but white space and comments between.  `NOT IN` is the same use; the `NOT` is ordinary SQL before it.  `IN` must be a whole word: `JOIN @x` is not one.  Whole-word rules follow the dialect's name characters (`QuoteReader.IsNameCharacterOfSqlServer` or `...OfPostgreSql`), as `SqlDeclaredVariables` and `UncastParameters` already do.
- **`position(a IN @p)` is not a list use.**  PostgreSQL, ANSI SQL and MySQL write `position(substring IN string)`, where a bare parameter after `IN` is valid: `WHERE position('@' IN @email) > 0`.  An `IN` directly inside the parentheses of a call to `position` (any case, nesting tracked by parentheses outside strings and comments) is skipped, so such a query builds as it does today.  *(My call.)*  No other construct of the supported dialects puts a bare value after `IN` outside a quoted body (a PL/pgSQL `FOR r IN` sits inside a dollar-quoted string, which the lexer already skips); the manual's known issues name the rule so a user of another construct can find it.
- **A null test** is a use followed by `IS NULL` or `IS NOT NULL`, the same way.
- **A parameter is a list** when at least one of its uses is a list use.  Every other use of it must then be a list use or a null test; any other use is the new parse error (section 5), at that use.
- **Lists and token defaults do not mix.**  A token's text is the caller's at run time, so the method cannot expand a list inside it, nor send a null test's parameter for it.  So a bare `IN @x` inside a token's default, and any use of a list parameter inside one, is the new parse error.  Detection reads only the query's SQL outside tokens, and the describe-time sample never has to find a list inside a default.  *(My call.)*
- **A `-- param:` marker on a list parameter describes its element**: `-- param: @categories text` gives the element's database type, `null` makes the list nullable, and `-> Category` gives the element's C# type.  `-> json` on a list parameter is the new parse error.  *(My call: JSON elements are out of scope; erroring keeps the door open.)*
- **Nullability.**  A list is nullable when the query has a null test of it, or when its marker says `null`.  *(My call, since the owner prefers inference.)*  A null list where the property is not nullable is treated as an empty list.
- **Elements are never nullable.**  A list of a value type is `IEnumerable<int>`, never `IEnumerable<int?>`: no marker or override gives a nullable element.  A `NULL` in an `IN` list never matches, and in a `NOT IN` list it makes the condition match no row, so a nullable element offers nothing but that trap.  A reference-type element that is null at run time is sent as `NULL`, as any reference-type parameter is today, and the manual says what that does in `NOT IN`.  *(My call.)*
- **Every dialect parses lists**, since detection is part of the parse.  In the `sql` output nothing changes: the constant holds the SQL as written.

### 2. The parse

- **`SqlQueryParameter` gains `IsList`** (and, if the builder finds it useful, `HasNullTest`), with value equality.  `SqlParameterList.Create` sets it while it walks each use, where it already has each use's span in the stripped text and can report at `sql.ToSourceSpan(span)`.
- **Detection must cost nothing for a query without the word `IN`**: a case-insensitive `IndexOf` over the query first, then the look-back only for parameters.  The builder chooses between looking back in the stripped text, where a block comment is already one space and a line comment is gone, and walking lexemes as `SqlDeclaredVariables` does.  A hint between `IN` and the parameter is the one case where the two differ; treating it as white space is fine.
- **The generator must know where each list use is.**  The builder chooses between a new `SqlSegmentKind` for a list use, split out of literal segments in both `Segments` and `KeptSegments`, and a list of list-use positions kept beside the segments.  Either way, `SqlQueryHash` must read exactly the text it reads today, so that the `@name` of a list segment is hashed as the literal text it was and no stored hash changes.
- **The hash needs nothing new.**  Whether a parameter is a list, and whether it is nullable by inference, both follow from the SQL text, which is already hashed.  The marker's `null` is hashed today.

### 3. The tool: describing

- **`SampleSql.Build` makes the stand-in** (`src/SqlSource.Tool/Describing/SampleSql.cs`), the one place both describers get their SQL:
  - each list use `@ids` becomes `(@ids)`, so the database types `@ids` as one value of the element type;
  - each null test's `@ids` becomes `NULL`, so `@ids IS NULL` is described as `NULL IS NULL`.
- **Why the null tests are replaced:** on PostgreSQL, `(@ids IS NULL OR id IN (@ids))` is SQLSRC227, because the first use fixes no type.  At run time the null test reads its own `boolean`/`bit` parameter (section 4), not the list's, so describing it as a constant is faithful.  Folding the condition to true loses nothing for nullability: a condition on a parameter alone can never prove a column not null.  *(My call.)*
- **The sidecar entry is unchanged in shape**: the list's entry has the element's type, as the database inferred it from `(@ids)`, and `nullable` as the marker says.  The generator knows a parameter is a list from the parse, so the sidecar needs no `list` key and the format stays at version 1.
- **An older generator reading a newer tool's entry for a list query** would treat the list as one value and send `IN @ids` unexpanded, which [the compatibility rules](../../architecture/sidecar-format.md#compatibility) count as wrong code.  The owner ruled that this needs no measure (no hash line, no version raise), because SqlSource has not been released, so no older generator exists to read such an entry.
- **Both describers need no change** beyond what `SampleSql` gives them.  A list used twice is `(@ids)` twice, which SQL Server's `ParameterCopies` already handles for any parameter used more than once.  The builder confirms both describers' behaviour with tests of their own, including on each engine's floor image.

### 4. The generator

#### Models

- **A list parameter's property is `IEnumerable<E>`**, in `codegen` and `models` alike, where `E` is the C# type its element would have as an ordinary parameter of the described type: the marker's `->`, the type override, a library type in `codegen`, the type map.  *(My call for `models`: Dapper expands any `IEnumerable` on SQL Server, so the property type works there.)*
- **Nullable**: `IEnumerable<E>?` by the rule in section 1.  `ModelReader.ReadParameters` takes nullability from the sidecar today, so it must also take the parse's inferred nullability for a list.
- **`ConversionPlanner` and the textual array checks** (`ElementOf`, `EdgeElementOf`, `PostgresTypeMap.ArrayOf`/`Gives`) recognise arrays by a `[]` suffix.  A list's element converter must be found from `E`, not from the property's `IEnumerable<E>` text; the builder decides whether `PropertyType` gains an element or whether the list wrapper is applied only when the property is declared.

#### Expansion in the generated method

- **The SQL** is built with `string.Create` when the query has a list or a token, extending `MethodWriter.AppendCreate`.  The state carries each token's text and each list's padded count.  Each list use writes `(@ids0, @ids1, …, @idsN)`; every use of one list writes the same names.  The method keeps the `CA2100` suppression, with a reason that fits a list (the text is built from the query's own SQL).
- **Each list is read once.**  If the value is an `ICollection<E>` or an `IReadOnlyCollection<E>`, its `Count` is taken and it is enumerated once to bind; otherwise it is copied with `Enumerable.ToArray` first.  *(The owner's rule, widened to `IReadOnlyCollection<E>`.)*
- **Padding** rounds each list's count up to the next of 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, then the next multiple of 512 (1,024, 1,536, 2,048, …), repeating the last element to fill.  Duplicates change no result of `IN` or `NOT IN`.  On SQL Server that is 13 sizes up to its limit; on PostgreSQL the steps of 512 go on to its limit.  *(Sizes are the owner's; continuing in 512s on PostgreSQL is my call.)*
- **Padding never causes the limit to be hit.**  If the padded total would exceed the engine's limit and the exact total would not, the method sends the exact counts.  *(My call.)*
- **An empty list, or a null one,** writes `(SELECT @ids0 WHERE 1 = 0)` and sends `@ids0` as a `NULL` of the element's type.  `IN` then matches nothing and `NOT IN` matches everything, as for an empty list; a typed parameter keeps PostgreSQL from refusing to compare the column with an untyped `NULL`.  This is the form Dapper uses.
- **A null test** reads a separate parameter named as the list, `@ids`, sent only when the query has a null test of that list: a `bit` on SQL Server and a `boolean` on PostgreSQL, `NULL` when the list is null and true otherwise.  So a null list passes every row through `(@ids IS NULL OR …)`, and an empty list matches none.
- **Binding each element** uses the same binder as an ordinary parameter of the element's type: `DataTypeName` on PostgreSQL, `SqlDbType` with its facets and the over-long check on SQL Server, and the element's converter when it has one.  The binders take a value expression today fixed to `parameters.X`; the builder generalises that to the loop's element.
- **Names**: `@ids0` could collide with another parameter of the query, such as one named `ids0`.  The generator picks, at generation time, a form for each list's names that matches no other parameter of the query (for example `ids_0`, then `ids__0`).  On SQL Server a name may hold at most 128 characters: a list name that leaves no room for its suffix is a generation error, or the builder picks a shorter form.  The builder settles the exact scheme and its tests.
- **The limit**: before building the SQL, the method adds up every parameter it will send (the query's ordinary parameters, the null-test parameters, and each list's count after padding) and throws `ArgumentException` (`paramName: "parameters"`) when the total exceeds the engine's limit.  For example: "The query 'ListOrders' would send 2,412 parameters; SQL Server accepts 2,100.  Pass fewer values in 'Ids', or use a table-valued parameter."  The method's documentation gains an `<exception cref="ArgumentException">`.
  - The limits are about 2,100 on SQL Server and about 65,535 on PostgreSQL.  The exact numbers must be confirmed with a test against each engine: SQL Server's `sp_executesql` takes two parameters of its own, which may leave 2,098 or 2,099 for a query.
  - Why `ArgumentException`: the cause is the argument the caller passed, it is the type token checks already throw, it needs nothing generated, and any assembly can catch it.  A type of SqlSource's own would be `internal` unless the project sets `SqlSourcePublicExceptions`.  The owner asked whether a derived `InvalidOperationException` would be better; `InvalidOperationException` is for an object in the wrong state, which this is not.

#### `no-list-padding`

A generator parameter, defined beside `no-array-pool` (`GeneratorParameters`, `GeneratorParameterList`, `QuerySettings`, `QueryMethod`, `MethodPlanner`), that sends exact counts.  It touches the settings schema, the settings and diagnostics pages, and the tests that `no-array-pool` touches.

#### PostgreSQL arrays

- **In `codegen` input models, an array parameter is `IEnumerable<E>`** in place of `E[]`, and `IEnumerable<E>?` when nullable.  This applies when the property's type is `E[]` as the type map, a library or the type override gives it (so `int4[]=int?[]` gives `IEnumerable<int?>`).  It does not apply to a C# type the `-- param:` marker names after `->`, which is taken as written, or to types that are not arrays of elements: `bytea` (`byte[]`), multidimensional types such as `int[,]`, `int2vector` and `oidvector`, and multiranges.  *(My call on what counts.)*
- **It is bound as `NpgsqlParameter<IList<E>>`**, with the same `DataTypeName`, and `TypedValue = value is null ? null : value as IList<E> ?? Enumerable.ToArray(value)`.  The probe showed this works on Npgsql 8.0.9, 9.0.5 and 10.0.3, including null, an empty list, after `PrepareAsync`, and for nullable elements.  Arrays, `List<T>`, `ReadOnlyCollection<T>`, `ImmutableArray<T>`, `ImmutableList<T>`, `ArraySegment<T>` and any custom `IList<T>` pass through; a `HashSet<T>`, an `ICollection<T>`- or `IReadOnlyList<T>`-only type, and a lazy sequence are copied once.
- **An element converter** (`Array.ConvertAll` today, as for a JSON class array) applies after the value is an array or a list.
- **Every kind of array must be checked, not only the probed ones.**  The probe covered `integer[]` and `text[]`, with nullable elements.  The builder first checks `NpgsqlParameter<IList<E>>` on each major for the other arrays the type map gives: enum arrays sent as `string[]` under the enum's `DataTypeName`, domains over arrays, NodaTime's and NetTopologySuite's arrays, `JsonElement[]`, and JSON class arrays converted element by element.  Npgsql registers the `IList<E>` mapping beside `E[]` for each element type it maps, so all are expected to work.  A kind that fails keeps `E[]` and is named in the manual beside `bytea` and the multiranges.  *(My call.)*
- **The `models` output keeps `E[]`**, because there a library such as Dapper hands the value to Npgsql as it is, and Npgsql refuses a set or a lazy sequence.  The owner chose this.  It follows the existing rule by which library types reach `codegen` models only (`TypeModels.Request`'s `WithoutLibrary` path), and a model shared by a `codegen` and a `models` query that then differs is SQLSRC257, which already compares input models.
- **This is a breaking change** for a `codegen` caller that reads the array back from its input model; constructing the model with an array still compiles.  SqlSource is pre-release, so it needs no migration, only the manual's account.

### 5. Diagnostics

- **One new parse error**: a list parameter used anywhere but after `IN`/`NOT IN` or in a null test, a list use or a list parameter inside a token's default, or a list parameter whose `-- param:` marker says `-> json`.  The message names the parameter and what is wrong at that use.  Title, for example: "List parameter is used outside IN".
- **Its id.**  `SqlParseErrorKind`'s members map to `SQLSRC101 + index`, which a test enforces (`ForParseError_EveryKind_HasItsOwnDescriptorInTheFileRange`), but SQLSRC122 to SQLSRC125 are already placement errors.  The new error takes SQLSRC126, the next free id in the file range, and the test changes to allow the parse errors' ids to continue after the placement errors.  Nothing has shipped (`AnalyzerReleases.Shipped.md` is empty), but renumbering the placement errors would move four documented ids for no gain.  *(My call.)*
- **The manual's diagnostics page** gets the section and the table row, and its intro text, which says file errors run from 101 to 120, is corrected.

### 6. Documentation and examples

- **`docs/manual/parameters-and-tokens.md`**: a section on list parameters (writing them, the null test, markers, padding, empty lists, the limit and what to write instead, a null element of a reference type in `NOT IN`, which matches no row as SQL says, `position(... IN @p)`, and lists in token defaults), and a known issue for whatever detection leaves out.
- **`docs/manual/generator.md`**: property types, nullability, how a parameter is sent, and the `ArgumentException`.
- **`docs/manual/postgresql.md`**: array parameters as `IEnumerable<T>` in `codegen`, what is copied and what passes through, `= ANY` versus `IN @list`.
- **`docs/manual/sqlserver.md`**: lists, the parameter limit, and table-valued parameters or `OPENJSON` for large lists.
- **`docs/manual/settings.md`**: `no-list-padding`.
- **`docs/manual/README.md`**: a row in "What works today"; `README.md` if its FAQ or common path mentions filtering by a list.
- **`docs/manual/recipes.md`**: an optional list filter. *(My call.)*
- **`docs/architecture/generator.md`**: Parameters (detection), Token methods and Query methods (the SQL built at run time, binding a list, the limit), Models (the element type), and the PostgreSQL binding of arrays.  `docs/architecture/tool.md` for the stand-in in `SampleSql`.
- **Examples**: a list query in each example project's `CodeGen/` (an optional list filter on one of them), with each README's table, and the sidecars from `tools/describe-end-to-end.sh`.
- **AGENTS.md files** that name what changes, such as `src/SqlSource/Parsing/AGENTS.md` for detection, stay within their size limits.

### 7. Tests

- **Parsing**: detection in every dialect, `JOIN @x`, `position('@' IN @email)` and other near misses, comments and hints between, null tests, every invalid use, markers on lists, lists in token defaults, and that hashes of queries without lists are unchanged (the pinned goldens in `SqlQueryHashTests`).
- **Generator**: golden cases in `QueryMethodCorpus` for both engines: one list, two lists, one list used twice, a list with tokens, a null test, `no-list-padding`, converters on elements, and the PostgreSQL array binding.  Model tests for `IEnumerable<E>`/`IEnumerable<E>?` in both outputs and SQLSRC257 for a shared input model.
- **Tool**: `SampleSql` stand-ins, and describing a list query on each engine, including on the floor images.
- **End to end, on each engine**: a `Queries/Lists.sql` with its claiming class and tests that run the methods with each kind of collection (array, `List<T>`, `HashSet<T>`, lazy LINQ), counts at and across each padding step, empty and null lists with `IN` and `NOT IN`, the null test, a list used twice, two lists, and the limit (the exact count that passes and the first that throws).  PostgreSQL array parameters with the same kinds of collection, including on the Npgsql 8 project, which links `AllTypes.sql`.
- **Realistic case** `any-ids-and-enum-array`: its input check (`RealisticInputs`) adjusted for `IEnumerable<T>`.

## Left to the builder

- Look-back in the stripped text or a walk of the lexemes; a new segment kind or a list of positions (section 2).
- Whether `PropertyType` carries the element or the wrapper is applied at declaration (section 4).
- The exact name scheme for expanded parameters, and the handling of a name too long for SQL Server.
- How the length and digits are computed in the generated code, and whether helpers are written inline per method or into one generated file.
- Whether this lands as one pull request or two (the PostgreSQL array change can go first on its own).
