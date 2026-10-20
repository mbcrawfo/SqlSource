# Quoted, case-exact type-override keys Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0057 and TD-0058: a key of the type override can quote a part, `"my schema"."order-status"`, a quoted part is compared exactly, and two keys that differ only by case are two keys, the one that equals a type's key exactly winning over one that equals it ignoring case.

**Architecture:** A key stays a `string` everywhere (`TypeOverride.Key`, `TypeMapping.Key`, `PropertyType.Key`), held in a *written* form: each part plain or in double quotes, as a user writes it.  `TypeKeys` owns the grammar: it reads a key, splits a written key into its parts, and writes a type's part.  `TypeOverrideMap` compares keys by the text of their parts without quotes: a key's identity is that text compared ordinally, the order is ignoring case first and ordinal second, and `Find` takes the entry equal ordinally, else the first equal ignoring case whose quoted parts are equal ordinally.  `PostgresTypeMap.KeyOf` writes a type's key in the same form, so the help of `SQLSRC246` quotes a part that needs it.

**Tech Stack:** C# (netstandard2.0 generator, `System.Memory` spans), xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0057-type-override-keys-that-differ-by-case.md` and `docs/tech-debt/TD-0058-type-override-keys-cannot-be-quoted.md` (Problem and Proposed fix), against the grammar of `docs/superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md` ("The grammar", "The keys").

## Where the code differs from the TD files

- TD-0057 says "keep the keys sorted ignoring case".  They stay sorted ignoring case first, with an ordinal tie-break so that keys equal ignoring case sit together in a fixed order; the comparison is of parts, not of the whole string, so that `"a.b".c` and `a."b.c"` are two keys.
- TD-0058 says a quoted part is "matched by its text without the quotes".  So `public."status"` and `public.status` are one key (one identity): in one MSBuild value the last wins, and in one marker scope two types for them are `SQLSRC112`.  The entry kept carries its own quotes, which decide what it matches ignoring case.
- An unquoted, unqualified key is a known name; a quoted one, `"char"` or `"int4"`, is accepted when its text is exactly a known name or `_` and one, and normalized to the plain name.  An alias in quotes, `"integer"`, is not a key, since it is not a type's name.
- SQL Server's bracket form, `[dbo].[Name]`, is not accepted.  The epic's phase 6 section records that phase 6 decides brackets against this grammar.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run after the last edit (root `AGENTS.md`, "Commits").
- User docs describe effects, never internals; never name a TD id in `docs/manual/` or the readmes (`docs/manual/AGENTS.md`).
- Resolving TD-0057 and TD-0058 deletes both files, their rows in `docs/tech-debt/README.md`, every reference to their ids, and their known issues in `docs/manual/settings.md`.
- TD-0059 (an array outside `pg_catalog` has two keys) is out of scope: `public._tag` stays a key of its own.
- Comments and docs follow the house style: plain sentences, two spaces after a full stop.

## Review Focus

- **A quoted part holding `.`, `[]`, `=`, white space, `<` or `"`** — `"a.b".c`, `"x[]".y`, `"a=b".c`, `"a""b".c` read as one key with the part's text intact, and an MSBuild value keeps the entry whole.  Tests in Tasks 1 and 3.
- **An unclosed quote in an MSBuild value** — the rest of the value is one invalid entry, as an unclosed `<` is today.  Test in Task 3.
- **A key that matches several entries ignoring case and none exactly** — `public.STATUS` against `public.Status` and `public.status` — gives one fixed entry, the first in ordinal order.  Test in Task 2.
- **A type with a capital letter or a space in its name** — the help of `SQLSRC246` writes a key that, pasted back, is valid and maps that type alone.  Test in Task 4.
- **A run of white space inside quotes in an MSBuild value** — the package collapses it to one space before the generator reads it, so such a name is named only by a marker.  Documented in Task 5; no code can change it.

---

### Task 1: The key grammar in `TypeKeys`

**Files:**
- Modify: `src/SqlSource/Settings/TypeKeys.cs`
- Test: `tests/SqlSource.Tests/Settings/TypeKeysTests.cs`

**Interfaces:**
- Produces:
  - `TypeKeys.TryNormalize(ReadOnlySpan<char> key, out string normalized)` — as today, plus quoted parts; `normalized` is the written form.
  - `TypeKeys.TrySplit(ReadOnlySpan<char> key, out ReadOnlySpan<char> schema, out ReadOnlySpan<char> name, out bool isArray)` — the parts of a written key as written, quotes included; `schema` empty for an unqualified key.  False for text that is not `[part "."] part ["[]"]`.
  - `TypeKeys.TextOf(ReadOnlySpan<char> part)` → `ReadOnlySpan<char>` — a part's text without its quotes and with `""` undoubled (allocates only when it holds `""`).
  - `TypeKeys.IsQuoted(ReadOnlySpan<char> part)` → `bool`.
  - `TypeKeys.WritePart(string text)` → `string` — `text` plain when it is not empty and every character is a letter, a digit, `_` or `$` and none is upper case; else in double quotes with each `"` doubled.

Grammar: a part is plain, letters, digits, `_` and `$` (today's `IsPart`), or quoted, `"` then one or more characters with `""` for a quote, then `"`.  At most two parts, `.` between, `[]` once at the end, nothing else.  Parse left to right; do not use `EndsWith("[]")`, since `"x[]"` is a name.

Normalization:
- Plain unqualified: unchanged rules (known name, alias, `_` + known name).
- Quoted unqualified: its text must equal a known name ordinally, or `_` and one; gives that plain name, or its array key (`"_int4"` → `int4[]`).  Anything else is false.
- Schema `pg_catalog`, plain ignoring case or quoted exactly: taken off.  A plain name follows today's rules.  A quoted name whose text is a known name is that plain name, whose text is `_` and a name is that name's key with `[]` (a known name plain, another quoted again), and otherwise stays quoted as written.
- Any other qualified key: kept as written.

- [ ] **Step 1: Write the failing tests** in `TypeKeysTests`:

```csharp
[Theory]
[InlineData("public.\"Status\"", "public.\"Status\"")]
[InlineData("\"my schema\".\"order-status\"", "\"my schema\".\"order-status\"")]
[InlineData("\"my schema\".status[]", "\"my schema\".status[]")]
[InlineData("\"a.b\".c", "\"a.b\".c")]
[InlineData("a.\"b.c\"", "a.\"b.c\"")]
[InlineData("s.\"x[]\"", "s.\"x[]\"")]
[InlineData("s.\"a=b\"", "s.\"a=b\"")]
[InlineData("s.\"say \"\"hi\"\"\"", "s.\"say \"\"hi\"\"\"")]
[InlineData("\"PG_CATALOG\".int4", "\"PG_CATALOG\".int4")]
public void TryNormalize_QuotedPart_IsKeptAsWritten(string key, string expected) => ShouldNormalize(key, expected);

[Theory]
[InlineData("\"char\"", "char")]
[InlineData("\"int4\"[]", "int4[]")]
[InlineData("\"_int4\"", "int4[]")]
[InlineData("\"pg_catalog\".int4", "int4")]
[InlineData("pg_catalog.\"int4\"", "int4")]
[InlineData("pg_catalog.\"_int4\"", "int4[]")]
[InlineData("pg_catalog.\"New Type\"", "\"New Type\"")]
[InlineData("pg_catalog.\"_Odd\"", "\"Odd\"[]")]
public void TryNormalize_QuotedCatalogName_IsItsKey(string key, string expected) => ShouldNormalize(key, expected);
```

Add to `TryNormalize_AnythingElse_IsNotAKey` (keep `"\"quoted\""`): `"\"\""`, `"public.\"\""`, `"\"unclosed"`, `"public.\"a\"b"`, `"\"a\".\"b\".\"c\""`, `"public. \"x\""`, `"\"Int4\""`, `"\"integer\""`, `"\"int4\"[][]"`.

Add `TrySplit_WrittenKey_GivesItsParts` (`"\"a.b\".c[]"` → schema `"\"a.b\""`, name `"c"`, array true; `"int4"` → schema empty, name `"int4"`, false), `TextOf_QuotedPart_IsItsTextUndoubled` (`"\"say \"\"hi\"\"\""` → `say "hi"`; `"plain"` → `plain`), and `WritePart_Text_IsQuotedWhenItNeedsIt` (`status`→`status`, `user_status$1`→ same, `Status`→`"Status"`, `my schema`→`"my schema"`, `a"b`→`"a""b"`).

- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~TypeKeysTests` — the new cases fail.
- [ ] **Step 3: Implement** the grammar and the four members above in `TypeKeys.cs`; replace the `IsPart` comment that cites TD-0058, and update `TryNormalize`'s doc comment for quoted parts.
- [ ] **Step 4: Run** the same filter — all pass.
- [ ] **Step 5: Run `./pre-commit-validation.sh`, then commit** `Read a quoted part in a type-override key`.

---

### Task 2: Keys compared by their parts in `TypeOverrideMap`

**Files:**
- Modify: `src/SqlSource/Settings/TypeOverrideMap.cs`
- Test: `tests/SqlSource.Tests/Settings/TypeOverrideMapTests.cs`

**Interfaces:**
- Consumes: `TypeKeys.TrySplit`, `TypeKeys.TextOf`, `TypeKeys.IsQuoted` (Task 1).
- Produces (signatures unchanged, meaning changed):
  - `TypeOverrideMap.Over(higher, lower)` — one key for keys equal ordinally by their text.
  - `TypeOverrideMap.IndexOf(List<TypeOverride>, string key)` — the same identity.
  - `TypeOverrideMap.Find(EquatableArray<TypeOverride> map, string key)` — `key` is a type's key in written form; returns the entry equal ordinally, else the first, in the map's order, equal ignoring case whose quoted parts equal the key's ordinally, else null.

Order (private `Compare(string a, string b, StringComparison comparison)`): the parts as a list, `[name]` or `[schema, name]`, compared lexicographically by `TextOf` with `MemoryExtensions.CompareTo(..., comparison)`, a shorter list first, then not-an-array before an array.  The map's order is `OrdinalIgnoreCase`, then `Ordinal` to break a tie.  `Find` binary-searches the first entry not less than `key` ignoring case and walks the entries equal to it ignoring case.  Keep that walk a small private method of its own, so TD-0059's later change to which keys a type has stays local.

- [ ] **Step 1: Write the failing tests.**  Change `Over_KeysOfBoth_AreMergedInTheOrderOfTheKeysIgnoringCase` so that `public.Status` (lower) and `PUBLIC.status` (higher) are both kept, in the order bool, int4, PUBLIC.status, public.Status, text, and rename it `Over_KeysThatDifferByCase_AreTwoKeys`.  Add:

```csharp
[Fact]
public void Over_QuotedAndPlainSpellingOfOneText_AreOneKey() =>
    TypeOverrideMap
        .Over(Map(Entry("public.\"status\"", "long", SettingPlace.Query)), Map(Entry("public.status", "int", SettingPlace.Property)))
        .ShouldBe(Map(Entry("public.\"status\"", "long", SettingPlace.Query)));

[Theory]
[InlineData("public.Status", "global::App.A")]
[InlineData("public.status", "global::App.B")]
[InlineData("public.\"Status\"", "global::App.A")]
[InlineData("public.STATUS", "global::App.A")]   // neither exact: the first in ordinal order
public void Find_KeysThatDifferByCase_PreferTheExactOne(string key, string type) { /* map: public.Status=App.A, public.status=App.B */ }

[Theory]
[InlineData("public.Status", "global::App.A")]
[InlineData("public.status", null)]
[InlineData("Public.Status", "global::App.A")]   // the schema is plain
public void Find_QuotedPart_MatchesItsCaseAlone(string key, string? type) { /* map: public."Status"=App.A */ }

[Fact]
public void Find_QuotedAndPlainEntriesEqualIgnoringCase_TakesTheOneThatMatches()
    // map: public."Status"=App.A, public.status=App.B; Find("public.STATUS") is App.B

[Theory]
[InlineData("\"a.b\".c", "global::App.A")]
[InlineData("a.\"b.c\"", "global::App.B")]
public void Find_DotInsideQuotes_IsPartOfThePart(string key, string type)

[Fact]
public void Find_QuotedSchemaAndName_IsFound()
    // map: "my schema"."order-status"=App.A; Find of it is App.A, Find("\"My Schema\".\"order-status\"") is null
```

Keep `Find_Key_IsFoundIgnoringCase` as it is.

- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter FullyQualifiedName~TypeOverrideMapTests` — the new and changed cases fail.
- [ ] **Step 3: Implement** the comparison, `Find` and the walk; rewrite the class's summary ("sorted by key, ignoring case, with each key once") and the `Keys` comment that cites TD-0057.
- [ ] **Step 4: Run** the same filter, then all of `tests/SqlSource.Tests/Settings` — all pass.
- [ ] **Step 5: Run `./pre-commit-validation.sh`, then commit** `Keep type-override keys that differ by case apart`.

---

### Task 3: Quotes in an entry and in an MSBuild value

**Files:**
- Modify: `src/SqlSource/Settings/TypeOverrideGrammar.cs`
- Test: `tests/SqlSource.Tests/Settings/TypeOverrideGrammarTests.cs`, `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`

**Interfaces:**
- Consumes: `TypeKeys.TryNormalize` (Task 1), `TypeOverrideMap.IndexOf` (Task 2).
- Produces: `TryReadEntry` splits at the first `=` outside double quotes; `NextEntry` keeps white space inside quotes in the entry, and does not count `<`, `>`, `[` or `]` inside quotes.  A `"` toggles "inside quotes", which handles `""` with no special case.

- [ ] **Step 1: Write the failing tests** in `TypeOverrideGrammarTests`:
  - `ReadList_QuotedKeyWithSpaces_IsOneEntry`: `"\"my schema\".\"order status\"=MyApp.A int4=long"` → `[("int4","long"), ("\"my schema\".\"order status\"","global::MyApp.A")]` in the map's order, nothing invalid.
  - `ReadList_BracketOrEqualsInsideQuotes_IsPartOfTheKey`: `"s.\"a[b\"=int s.\"a=b\"=long int8=long"` → three entries.
  - `ReadList_UnclosedQuote_MakesTheRestOneInvalidEntry`: `"int4=long \"my schema.x=int int8=long"` → `[int4]`, invalid `["\"my schema.x=int int8=long"]`.
  - `ReadList_KeysThatDifferByCase_AreTwoEntries`: `"public.Status=MyApp.A public.status=MyApp.B"`.
  - `ReadList_QuotedAndPlainSpellingOfOneText_TheLastWins`: `"public.status=MyApp.A public.\"status\"=MyApp.B"` → one entry, key `public."status"`, type `global::MyApp.B`.
  - `TryReadEntry_QuotedKeyInAMarker_AllowsSpacesAroundEquals`: `"\"my schema\".\"x y\" = int"` with `spacesAroundEquals: true`.

  In `SqlMarkerScopeTests`: `Read_TypeOverrideKeysThatDifferByCase_AreTwoEntries` (`public.Status = App.A`, `public.status = App.B`, no error, two entries) and add `"-- type-override: INT4 = int"` is unchanged; add a row to `Read_TwoValuesForOneKeyInOneScope_...`-style test `Read_QuotedAndPlainSpellingOfOneKeyWithTwoValues_ConflictAtTheSecond` (`public.status = App.A`, `public."status" = App.B` → `ConflictingSettings`, the first stands).

- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TypeOverrideGrammarTests|FullyQualifiedName~SqlMarkerScopeTests"` — the new cases fail.
- [ ] **Step 3: Implement** the two changes in `TypeOverrideGrammar.cs` and update its remarks ("outside `<>` and `[]`" gains "and double quotes").
- [ ] **Step 4: Run** the same filter — all pass.
- [ ] **Step 5: Run `./pre-commit-validation.sh`, then commit** `Read quoted keys in an MSBuild value and a marker`.

---

### Task 4: A type's key written with quotes, end to end

**Files:**
- Modify: `src/SqlSource/Generation/Types/PostgresTypeMap.cs` (`KeyOf`), `src/SqlSource/Generation/Types/QueryTyper.cs` (`AsAKey`, `OverrideLookup.EntryOf`)
- Test: `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`, `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`

**Interfaces:**
- Consumes: `TypeKeys.WritePart`, `TypeKeys.TrySplit` (Task 1); `TypeOverrideMap.Find` (Task 2).
- Produces: `PostgresTypeMap.KeyOf(type)` = `WritePart(InternalName)` for a type of `pg_catalog`, else `WritePart(Schema) + "." + WritePart(InternalName)`.  `AsAKey` decides "qualified" by `TrySplit`'s schema, not by `IndexOf('.')`.  The extension's bare-name lookup uses `WritePart(type.InternalName)`.

- [ ] **Step 1: Write the failing tests** in `TypeOverrideLevelTests` (types from `TestSidecars.Postgres(name:, kind:, schema:, internalName:)`):
  - `Run_KeysThatDifferOnlyByCase_EachMapsItsOwnType`: enums `public.Status` and `public.status`; property `public.Status=System.Guid public.status=System.Uri` → columns `Guid` (key `public.Status`) and `Uri` (key `public.status`), no diagnostics.
  - `Run_QuotedKey_DoesNotMapATypeThatDiffersByCase`: composites `public.Status` and `public.status`; property `public."Status"=System.Guid` → `SQLSRC246` for the column of `public.status`, whose help is `public.status=<a C# type>`.
  - `Run_QuotedSchemaAndName_MapsTheType` (theory over the four levels, as `Places`): a composite of schema `my schema` named `order-status`; entry `"my schema"."order-status"=System.Guid` → `Guid`.  For the metadata and property levels, the value also holds `int4=long` after it, to show the split.
  - `Run_KeysThatDifferByCaseInOneScope_AreNoConflict`: header markers `-- type-override: public.Status = System.Guid` and `-- type-override: public.status = System.Uri` → no diagnostics.

  In `QueryTypesTests`, `Run_CompositeWhoseNameNeedsQuotes_HelpQuotesIt`: a composite of schema `my schema` named `Order Item` → help `"my schema"."Order Item"=<a C# type>`.

- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TypeOverrideLevelTests|FullyQualifiedName~QueryTypesTests"` — the new cases fail.
- [ ] **Step 3: Implement** `KeyOf`, `AsAKey` and `EntryOf` as above; update `AsAKey`'s comment.
- [ ] **Step 4: Run** all of `tests/SqlSource.Tests` — all pass; a test that pinned a key of a type with a capital letter changes only in its quotes.
- [ ] **Step 5: Run `./pre-commit-validation.sh`, then commit** `Write a type's key with the quotes it needs`.

---

### Task 5: Documentation and the tech debt

**Files:**
- Modify: `docs/manual/settings.md` (the key bullets of "Type overrides", "In a property or metadata", "Values written in MSBuild", "Known issues"), `docs/manual/postgresql.md` (the paragraph under "The types"), `docs/manual/diagnostics.md` (`SQLSRC014`, `SQLSRC111`, `SQLSRC112`, `SQLSRC246`), `docs/architecture/generator.md` ("The type override", the `OverrideLookup` step of "Types"), `src/SqlSource/AGENTS.md` (the type-override rule of `Settings/`), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (phase 6, Decided), `docs/tech-debt/README.md`
- Delete: `docs/tech-debt/TD-0057-type-override-keys-that-differ-by-case.md`, `docs/tech-debt/TD-0058-type-override-keys-cannot-be-quoted.md`

What each says, in effects:
- **settings.md**: a key's part may be written in double quotes, a quote inside doubled, `"my schema"."order-status"`; a part in quotes is compared exactly, case included, and a part without quotes ignoring case, so `public."Status"` maps `Status` alone; of entries for one type, one whose key is spelled exactly as the type's name wins over one that matches it only ignoring case, so `public.Status` and `public.status` map two types; `public.status` and `public."status"` are one key.  In an MSBuild value white space inside quotes is part of the entry; an unclosed quote makes the rest one invalid entry; a run of white space inside quotes becomes one space, so a name with two spaces in a row, a tab or a line break is named by a marker; in an item's attribute write `&quot;` or put the attribute in single quotes.  `"char"` is a key.  Remove the two known issues (case, quotes); keep the array one.
- **postgresql.md**: a type created with a quoted name is written with its quotes in a key, `public."Order Status"`.
- **diagnostics.md**: `SQLSRC014` — white space inside quotes too, and an unclosed quote.  `SQLSRC111` — a part may be quoted.  `SQLSRC112` — `public.Status` and `public.status` are two types, `public.status` and `public."status"` one.  `SQLSRC246` — the key in the help quotes a part that needs quotes, one with a capital letter included.
- **generator.md**: `TypeKeys` reads plain and quoted parts and writes a type's part; keys are strings in written form; `TypeOverrideMap` orders by part text ignoring case then ordinally, one key per ordinal text, `Find` exact first, then ignoring case with quoted parts exact; `KeyOf` writes quotes.
- **src/SqlSource/AGENTS.md**: "sorted by key, ignoring case" becomes the order above, linking generator.md.
- **Epic, phase 6 Decided**: a new item — the keys of SQL Server use the grammar of phase 5.2 as amended here, a part in double quotes compared exactly and two keys that differ by case kept apart; whether `[dbo].[Name]` is also accepted is phase 6's decision, taken against that grammar, and must not change what a quoted or plain key means.
- **Tech debt**: delete both files and both rows.  `grep -rn "TD-0057\|TD-0058" .` finds nothing outside `docs/superpowers/plans/` and git history.

- [ ] **Step 1: Edit** the files above.
- [ ] **Step 2: Run** the manual's check: `grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0" docs/manual/settings.md docs/manual/postgresql.md docs/manual/diagnostics.md` — no new hit.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"` — pass (sample fences and links).
- [ ] **Step 4: Run `./pre-commit-validation.sh`, then commit** `Document quoted, case-exact type-override keys and resolve TD-0057 and TD-0058`.
