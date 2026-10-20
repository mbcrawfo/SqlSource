# One override key for arrays outside pg_catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0059: an array of a type outside `pg_catalog` is found by its element's key with `[]` alone, `public.user_status[]`, and an entry for the array's own name in the database, `public._user_status`, is a new error, `SQLSRC261`, at the query, naming the key to write.

**Architecture:** `QueryTyper.OverrideLookup.Find` stops asking the map for an array's own key.  When that key has an entry, the winning one including `default`, it returns a failure of a new `TypeMappingKind`, which `QueryTyper.Diagnose` turns into `SQLSRC261`.  `TypeOverrideMap` gains `Winner`, the entry `Find` weighs before it drops a winning `default`.  The lookup is now made for any non-empty map, so an entry of `default` for an array's own name is reported too.

**Tech Stack:** C# (netstandard2.0 generator), xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0059-array-of-another-schema-has-two-keys.md` (Problem and Proposed fix), against the lookup of `docs/superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md` ("How a type is looked up") and the keys of `docs/superpowers/plans/2026-10-20-quoted-case-exact-type-override-keys.md`.

## Where the code differs from the TD file

- The TD names `TypeOverrideMap.Over` and `TypeOverrideGrammar.ReadList` as keeping `public._x` and `public.x[]` apart.  They still do, and must: whether `public._x` is an array is in the sidecar, not in the key, so the two are not one key and two entries for them in one scope are no `SQLSRC112`.  That part of the TD does not fall out of the change and is not done; `SQLSRC261` tells a user who wrote the database's name at the query that uses the array, which is what the `SQLSRC112` would have caught.  The PR says so.
- The key to write is the element's key with `[]`, from the sidecar's element, never `_` taken off the array's name: PostgreSQL names the array `__x` when `_x` is taken, so the name alone can be wrong.
- `QueryTyper.LookupOf` made no lookup for a map with no entry that names a C# type.  An entry of `default` for an array's own name maps nothing either, and is reported the same way, so a lookup is now made for any non-empty map.  A project without the override still makes none.
- The new id is `SQLSRC261`, the next free one.  The epic and `src/SqlSource/AGENTS.md` reserve the ids from `SQLSRC261` for later phases; this unit takes 261 and moves that start to `SQLSRC262`, in both.  The run's notes say a unit takes the next free id after syncing with main.
- The phase 5.2 spec's line "an array's own key, `int4[]` or `_int4`, wins over its element's" is history and is left as it is; the specs are not among the documents rule 4 of the root `AGENTS.md` keeps current.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run after the last edit (root `AGENTS.md`, "Commits").
- A new diagnostic touches four places: the descriptor and `SqlDiagnostics.All`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, and its tests (`src/SqlSource/AGENTS.md`, "Diagnostics").  Write the descriptor out in full with a literal id; it is an error tagged `NotConfigurable`.
- User docs describe effects, never internals; never name a TD id, the describer or the sidecar's fields in `docs/manual/` (root `AGENTS.md`, rule 4).
- Resolving TD-0059 deletes its file, its row in `docs/tech-debt/README.md`, every reference to its id (`grep -rn TD-0059 .` outside `docs/superpowers/`), and its known issue in `docs/manual/settings.md`.
- `src/SqlSource/Generation/Types/MappedTypeResolver.cs` is not touched (another unit is open there).
- Generator tests compile on Roslyn 4.8.0 too and hand the compiler C# 12 at most.
- House style: plain sentences, two spaces after a full stop.

## Review Focus

- **A type of another schema whose name starts with `_` and is no array**, `public._tag`, composite — its own entry still maps it, and the array of it, `public.__tag`, is found by `public._tag[]`.  Test in Task 1.
- **An entry of `default` alone for an array's own name** — reported as `SQLSRC261`, though the map names no C# type.  Test in Task 1.
- **An entry for the array's own name beside a valid one for its element's key with `[]`**, at the same or another place — still `SQLSRC261`: the dead entry is the user's to remove.  Test in Task 1.
- **A domain over an array outside `pg_catalog`** whose own name has an entry — reported, since the lookup reaches the array through the domain's base.  Test in Task 1.
- **An array without its element** (a sidecar edited by hand) and an entry of its own name — no key to suggest, so it stays `SQLSRC246` with the help to describe again.  Test in Task 1.

---

### Task 1: Find an array by its element's key alone, and report its own key as `SQLSRC261`

**Files:**
- Modify: `src/SqlSource/Settings/TypeOverrideMap.cs` (add `Winner`, make `Find` use it)
- Modify: `src/SqlSource/Generation/Types/TypeMappingKind.cs` (add `ArrayNamedByItsOwnKey` at the end)
- Modify: `src/SqlSource/Generation/Types/TypeMapping.cs` (doc of `Text` and `Detail` for the new kind)
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs` (`OverrideLookup.Find`, `Diagnose`, `LookupOf`, their comments and the class remarks)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (descriptor after `ColumnMarkerUnusable`, and in `All`)
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `docs/manual/diagnostics.md` (table row, section, summary paragraph)
- Test: `tests/SqlSource.Tests/Settings/TypeOverrideMapTests.cs`, `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`

**Interfaces:**
- Produces: `TypeOverrideMap.Winner(EquatableArray<TypeOverride> map, string key) : TypeOverride?` — the entry that wins for the key by the rules of `Find`, an entry of `default` included; `Find` is `Winner` with a `default` winner given as null.
- Produces: `TypeMappingKind.ArrayNamedByItsOwnKey`; its `TypeMapping` has `Key` = the entry's key as written, `Text` = the key to write (the element's `PostgresTypeMap.KeyOf` plus `[]`), `Detail` = the place, from `PlaceOf`.
- Produces: `SqlDiagnostics.ArrayEntryMapsNothing`, id `SQLSRC261`, title `Entry names an array as the catalog does`, format below, help link `HelpLinkBase + "sqlsrc261"`.

Message format, with `{0}` the column or parameter as `column 'c0'`, `{1}` the entry's key, `{2}` the place, `{3}` the key to write:

```
The type override's entry for '{1}', in {2}, maps nothing: '{1}' is the database's own name of the array type of {0}, and an entry names that array as its element's key with [].  Write the entry for '{3}' instead.
```

- [ ] **Step 1: Write the failing tests of `Winner`** in `TypeOverrideMapTests`:
  - `Winner_DefaultThatWins_IsGivenWhereFindGivesNull`: map `[Entry("public._tag", null, Property)]` (the file's existing helper for a `default` entry); `Winner(map, "public._tag")!.Type.ShouldBeNull()` and `Find(map, "public._tag").ShouldBeNull()`.
  - `Winner_EntryOfAHigherPlace_WinsOverTheOneBelow`: `Over` of a `Query` entry `public._tag=default` over a `Property` entry `public._tag=global::App.A`; `Winner` gives the `Query` entry.
  - `Winner_NoEntryMatches_IsNull`.

- [ ] **Step 2: Write the failing generator tests** in `TypeOverrideLevelTests`.  Use a helper `ArrayOf(PostgresType element, string internalName)` that makes `TestSidecars.Postgres(name: element.Name + "[]", kind: Array, schema: element.Schema, internalName: internalName, element: element)`, and the file's `EnumType("user_status")`.  The expected `SQLSRC261` text is the format above with the place from `Places`.
  - Remove the row `("citext", "citext=System.Uri public._citext=System.Collections.Generic.List<int>", "public._citext")` from `Run_ArraysOwnKey_WinsOverItsElements`, and correct the comment above it ("for an array of an extension's type in any schema, bare or qualified" no longer holds for the qualified own name).
  - `Run_ArraysOwnKeyOutsideTheCatalog_IsSqlsrc261NamingTheKeyToWrite(string level, string place)`, `[MemberData(nameof(Places))]`: the entry `public._user_status=System.Guid` written at the level (property, metadata, header marker, query marker, as `Run_MappedTypeNotFound_IsSqlsrc250AtEachQueryThatUsesItNamingThePlace` does); a column `ArrayOf(EnumType("user_status"), "_user_status")`; `run.Diagnostics.ShouldBe([$"SQLSRC261 /app/Repo/Users.sql(…): The type override's entry for 'public._user_status', in {place}, maps nothing: 'public._user_status' is the database's own name of the array type of column 'c0', and an entry names that array as its element's key with [].  Write the entry for 'public.user_status[]' instead."])`; `TextsOf(run)` is empty.
  - `Run_DefaultForAnArraysOwnKey_IsSqlsrc261`: property `public._user_status=default` alone; the same error at the property's place.
  - `Run_ArraysOwnKeyBesideItsElementsKey_IsStillSqlsrc261`: property `public._user_status=System.Guid`, query marker `-- type-override: public.user_status[] = System.Collections.Generic.List<string>`; one `SQLSRC261` at the query, naming the property's place.
  - `Run_ElementsKeyWithBrackets_IsTheArraysOneKeyAtEveryLevel`: property `public.user_status[]=System.Collections.Generic.List<string>`; query `GetFirst` with marker `-- type-override: public.user_status[] = default`, query `GetSecond` without; no diagnostics; `TextsOf(run)` is `GetFirst` → `string[]`, `GetSecond` → `global::System.Collections.Generic.List<string>`.
  - `Run_ArraysOwnKeyOfAnExtensionsType_IsSqlsrc261NamingItsQualifiedKey`: property `public._citext=System.Collections.Generic.List<int>`; column `ArrayOf(TestSidecars.Postgres(name: "citext", schema: "public", internalName: "citext"), "_citext")`; `SQLSRC261` writing `'public.citext[]'`.
  - `Run_TypeNamedWithAnUnderscore_IsMappedByItsOwnKeyAndItsArrayByThatKeyWithBrackets`: property `public._tag=System.Uri`; columns `Composite("public", "_tag")` and `ArrayOf(Composite("public", "_tag"), "__tag")`; no diagnostics; column texts `global::System.Uri` and `global::System.Uri[]`.
  - `Run_DomainOverAnArrayWhoseOwnKeyHasAnEntry_IsSqlsrc261`: property `public._user_status=System.Guid`; column a domain `public.statuses` (`kind: Domain`, `base`: the array); `SQLSRC261` naming `column 'c0'` and `public.user_status[]`.
  - `Run_ArrayWithoutItsElement_IsSqlsrc246EvenWithAnEntryOfItsOwnKey`: property `public._user_status=System.Guid`; column an array `public._user_status` with no element; the diagnostic is `SQLSRC246` (assert the id prefix and that no `SQLSRC261` is reported).
  - `Run_CatalogsNameAndElementsKeyInOneScope_AreNoConflict`: a query marker scope with `-- type-override: public._user_status = System.Guid` and `-- type-override: public.user_status[] = System.Uri`; the only diagnostic is `SQLSRC261` (no `SQLSRC112`).

- [ ] **Step 3: Run the tests and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TypeOverrideLevelTests|FullyQualifiedName~TypeOverrideMapTests"`
Expected: the new tests fail (no `Winner`, no `SQLSRC261`); the others pass.

- [ ] **Step 4: Implement**
  - `TypeOverrideMap.Winner` as in Interfaces; `Find` calls it.
  - `OverrideLookup.Find`: for a type whose kind is `array` and that has an element, ask `TypeOverrideMap.Winner(map, PostgresTypeMap.KeyOf(type))`; when it gives an entry, return the `ArrayNamedByItsOwnKey` mapping; otherwise return the mapping of `EntryOf(element, "[]")`.  An array never asks for its own key's mapping.  Any other type is as today.  Rewrite the class's summary and remarks: an array is found by its element's key with `[]` alone, in every schema, and its own key's entry is the failure.
  - `QueryTyper.Diagnose`: a branch for `ArrayNamedByItsOwnKey` giving `SqlDiagnostics.ArrayEntryMapsNothing` with `subject`, `failure.Key`, `failure.Detail`, `failure.Text`.
  - `QueryTyper.LookupOf`: make the lookup when the map is not empty; update its comment and the class remarks ("A map without an entry … is not asked at all" becomes an empty map).
  - The descriptor, `All`, `AnalyzerReleases.Unshipped.md` (`SQLSRC261 | SqlSource | Error | Entry names an array as the catalog does`).
  - `docs/manual/diagnostics.md`: a group of its own after "The models of a query", titled `**An entry of the type override that maps nothing**`, with the row; a sentence after the `SQLSRC250`–`SQLSRC252` paragraph saying `SQLSRC261` is reported at a query that has a column or parameter of an array of a type outside `pg_catalog`, when the type override has an entry for the array's name in the database; and the section after `SQLSRC260`: title line, what it means (the database names the array of `public.user_status` `public._user_status`; an entry for that name maps nothing, since the type override names such an array as its element's key with `[]`; a type whose own name starts with `_` and is no array is still mapped by its own), a `console` example with the format above, the fix (rename the entry to the key the message gives; the query's constant or method is still generated), and `See: [Type overrides](settings.md#setting-type-overrides)`.

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~TypeOverrideLevelTests|FullyQualifiedName~TypeOverrideMapTests|FullyQualifiedName~Diagnostics"`
Expected: PASS.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh` — every step passed.

```bash
git add -A
git commit -m "Find an array outside pg_catalog by its element's key alone"
```

### Task 2: The docs, and TD-0059 resolved

**Files:**
- Modify: `docs/manual/settings.md` (the "An array" bullet near line 368; delete the known issue near line 575)
- Modify: `docs/manual/generator.md:235` (add `SQLSRC261` to the errors that leave a query without models)
- Modify: `docs/architecture/generator.md` (lines 97, 187, 188, 412: the lookup, the errors of `FileTypes`)
- Modify: `src/SqlSource/Settings/TypeKeys.cs` (the `TryNormalize` paragraph that cites TD-0059)
- Modify: `src/SqlSource/AGENTS.md:76` and `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:386` (`SQLSRC261` taken by this unit; later phases from `SQLSRC262`)
- Delete: `docs/tech-debt/TD-0059-array-of-another-schema-has-two-keys.md`, and its row in `docs/tech-debt/README.md`

- [ ] **Step 1: settings.md.**  The array bullet: keep that `_int4` and `int4[]` are one key; say an array of a type outside `pg_catalog` is written with its schema, its element's name and `[]`, `public.user_status[]`, and only so; an entry for the database's name of that array, `public._user_status`, maps nothing and is [SQLSRC261](diagnostics.md#sqlsrc261) at a query that uses the array; such a key still maps a type whose own name starts with `_` and that is no array.  Delete the known issue "An array of a type outside `pg_catalog` can be named two ways".
- [ ] **Step 2: generator.md (manual).**  Add `[SQLSRC261](diagnostics.md#sqlsrc261)` beside the C# type errors in line 235.
- [ ] **Step 3: architecture.**  Line 187: an array is found by its element's key with `[]` alone, in every schema; when its own key has an entry, `TypeOverrideMap.Winner` including `default`, the lookup's answer is the failure `ArrayNamedByItsOwnKey`, which carries the key to write.  Lines 97, 188 and 412 add `SQLSRC261`.  The "A key whose winning entry" sentence and the `LookupOf` description, if any, follow Task 1.
- [ ] **Step 4: `TypeKeys.TryNormalize`.**  The paragraph keeps that `public._tag` is not `public.tag[]` and why, without the id: the lookup finds an array outside `pg_catalog` by its element's key alone.
- [ ] **Step 5: id range.**  `src/SqlSource/AGENTS.md`: "`SQLSRC200` to `SQLSRC261` are taken, and a later phase takes the ids from `SQLSRC262` …".  Epic line 386: "… the later phases take ids from `SQLSRC262`; `SQLSRC261`, an entry for an array's name in the database, was taken by tech debt after phase 5."
- [ ] **Step 6: TD-0059.**  Delete the file and its row; `grep -rn "TD-0059" . --exclude-dir=superpowers` finds nothing outside `docs/superpowers/`.
- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh` — every step passed.

```bash
git add -A
git commit -m "Document the one key of an array outside pg_catalog and resolve TD-0059"
```
