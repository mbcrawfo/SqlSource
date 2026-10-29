# TD-0078 Canonical DataTypeName Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A generated PostgreSQL method sets each parameter's `DataTypeName` to the type's canonical, schema-qualified internal name (`pg_catalog.int4`, `public.email`, `pg_catalog._int4`), which Npgsql resolves with less allocation than the display name, `integer`, that it sets today.

**Architecture:** The generator derives the name from what the sidecar already holds for the parameter's `PostgresType`: its `schema` and `internalName`, and its `base` chain for a domain over a domain.  The sidecar's format, and `bindName`, which the tool's `check` binds by, do not change.  Where the canonical name cannot be derived safely, the binding falls back to `bindName` as it is today.

**Tech Stack:** C# source generator (`netstandard2.0`, Roslyn 4.8.0), Npgsql 8, 9 and 10, xunit v3, Testcontainers.

**Spec:** `docs/tech-debt/TD-0078-datatypename-is-resolved-on-every-call.md`

## What the code shows that the spec does not say

- A probe on PostgreSQL 18.6 bound `NpgsqlParameter<T>` by canonical names on Npgsql 8.0.9, 9.0.3 and 10.0.3: a base type, an array, `"char"` (`pg_catalog.char`), `bit`, `bpchar` and its array, `varbit`, `int2vector`, `oidvector`, a range, its array, a multirange, an enum and its array, an enum with a space and capitals (`probe.Mixed Case`), a domain and its array, an extension's type and its array, and nulls.  Every one bound to the expected type on all three.
- A domain over a domain is refused by its own name on every version (Npgsql 9 and 10 resolve it to the inner domain and refuse to write it; 8 says it is not present), as `PostgresBindNames` rule 2 says.  Its innermost base's canonical name binds.
- **An array of a domain over a domain, `probe._work_email`, is refused by Npgsql 8** ("isn't present in your database") and accepted by 9 and 10.  Its `bindName` is the innermost base's name and `[]`, whose array's internal name the sidecar does not hold.  So that one case keeps `bindName`.
- Measured by the probe, a whole call with a new parameter, median bytes: `integer` 1,480 to 1,536 against `pg_catalog.int4` 1,168 to 1,256; `integer[]` 1,384 to 1,408 against `pg_catalog._int4` 1,256 to 1,344; `timestamp with time zone` 1,352 to 1,368 against `pg_catalog.timestamptz` 1,176 to 1,280; `public.email[]` against `public._email` equal on 10, 112 bytes less on 8 and 9; `public.email` unchanged.
- Allocation harness before the change (`QueryMethodAllocation`, Postgres, PostgreSQL 18.6, Npgsql 10.0.3), commonest bytes of a generated call: `GetAccountName` 1,624 through the pool, 2,088 under no-array-pool; `ListClosedBefore` 2,008 and 2,088; the numbers queries at 3 rows 1,576 (`NumbersAsImmutableArray` 1,544); `NumbersAsArray` at 300 rows 11,048.

## Global Constraints

- The sidecar's format, its schema and `bindName` do not change; the tool's `check` still binds by `bindName`.
- `src/SqlSource` targets `netstandard2.0`, Roslyn 4.8.0; no collection expression targets `ImmutableArray<T>`; nothing a user writes may make the generator throw.
- A generated string is written with `SymbolDisplay.FormatLiteral`.
- No `NpgsqlDbType` is named; the code compiles alike on Npgsql 8, 9 and 10.
- A cached parameter per method is not an option: a method may run concurrently.
- Run `./format.sh` (it formats with CSharpier and `dotnet format`) before validating; `./pre-commit-validation.sh` before each commit.

## Review Focus

- A domain over a domain: binds its innermost base's canonical name, never its own (Npgsql refuses it).
- An array of a domain over a domain: binds `bindName`, since Npgsql 8 refuses the array's own name.
- An enum or domain whose name holds capitals, a space or a quote: binds `schema.name` unquoted, as Npgsql takes it, written as a C# literal.
- A type whose kind the generator does not know, or whose `schema` or `internalName` holds a `.`: binds `bindName`.
- A parameter with no `bindName`: writes no `DataTypeName` line, as today.

---

### Task 1: Derive the canonical name in the PostgreSQL binding

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs`
- Modify: `tests/SqlSource.Testing/Generator/MethodRuns.cs` (`ParameterKinds` gains the expected name)
- Modify: `tests/SqlSource.Tests/Generator/MethodTests.cs` (`Run_ParameterOfEachKind_BindsItsBindName`)
- Modify: `tests/SqlSource.Tests/Generator/MethodCompileTests.cs` (`Run_ParameterOfEachKindOnNpgsql_Compiles`)
- Modify: `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs` and any other test whose expected output holds `DataTypeName = "<display name>"`
- Modify: `docs/architecture/generator.md` (the PostgreSQL bullet of the binding, line 587, and line 296's "whose `bindName` the PostgreSQL binding sets")

**Interfaces:**
- Produces: `internal static string? PostgresParameterBinder.DataTypeNameOf(SidecarType? described)`: null when `described?.BindName` is null; otherwise the canonical name, or `BindName` where none can be derived.

- [ ] **Step 1: Write the failing tests** in `PostgresParameterBinderTests`, each through `Write`, asserting the line `    p0.DataTypeName = "<name>";\n`:
  - `Append_BaseType_BindsTheCanonicalName`: `TestSidecars.Postgres(bindName: "integer")` → `pg_catalog.int4` (rename the existing `For_NotNullValueType_BindsATypedParameterByItsBindName` to bind the canonical name, expected block with `pg_catalog.int4`).
  - `Append_Array_BindsTheArraysOwnName`: kind array, schema `pg_catalog`, internalName `_int4`, element int4, bindName `integer[]` → `pg_catalog._int4`.
  - `Append_ArrayOfADomain_BindsTheArraysOwnName`: array `public._email` of domain `public.email` over text, bindName `public.email[]` → `public._email`.
  - `Append_Domain_BindsItsOwnName`: domain `public.email` over text, bindName `public.email` → `public.email`.
  - `Append_DomainOverADomain_BindsItsInnermostBase`: domain `public.work_email` over domain `public.email` over `pg_catalog.text`, bindName `text` → `pg_catalog.text`.
  - `Append_ArrayOfADomainOverADomain_BindsItsBindName`: array `public._work_email` of that domain, bindName `text[]` → `text[]`.
  - `Append_CatalogChar_BindsCatalogChar`: base `pg_catalog.char`, bindName `char` → `pg_catalog.char`.
  - `Append_NameWithASpaceAndAQuote_IsWrittenAsALiteral`: enum, schema `public`, internalName `Odd "name"`, bindName `public.Odd "name"` → C# text `"public.Odd \"name\""` (replaces `Append_BindNameWithAQuote_IsWrittenAsALiteral`).
  - `Append_UnknownKind_BindsItsBindName`: kind `"shell"`, bindName `public.thing` → `public.thing`.
  - `Append_DotInTheName_BindsItsBindName`: schema `my.schema`, internalName `t`, bindName `x.t` → `x.t` (the fallback is whatever `bindName` holds).
  - keep `Append_NoBindName_SetsNoDataTypeName`.
- [ ] **Step 2: Run them and see the new ones fail**: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Generation.Emitting.PostgresParameterBinderTests`.
- [ ] **Step 3: Implement `DataTypeNameOf` and use it in `Append`** in place of `bound.Described?.BindName`.  Rules, in order, for a `PostgresType` with a `BindName`:
  1. `TryGetKind` fails → `BindName`.
  2. Array: when its `Element` is a domain whose `Base` is a domain → `BindName`; otherwise the array's own `Schema.InternalName`.
  3. Domain whose `Base` is a domain → follow `Base` down to the first type that is no domain, and give its `Schema.InternalName` (a missing `Base` on the way → `BindName`).
  4. Anything else → its `Schema.InternalName`.
  Any `Schema` or `InternalName` used that holds a `.` → `BindName`.  A described type that is not a `PostgresType` → `BindName`.  Update the class's `<remarks>`: the name is the canonical internal name, why (Npgsql resolves a display name with more work on every new parameter), and the two fallbacks with their reasons (Npgsql 8 refuses an array of a domain over a domain by its own name; a kind it does not know).  Remove the `TD-0078` mention.
- [ ] **Step 4: `MethodRuns.ParameterKinds` gains a third member, `DataTypeName`**: `pg_catalog.int4`, `pg_catalog._int4`, `public.mood`, `public.email`, `pg_catalog.int4range`, `pg_catalog.int4multirange`, `pg_catalog.int8`.  `MethodTests.Run_ParameterOfEachKind_BindsItsBindName` becomes `Run_ParameterOfEachKind_BindsItsCanonicalName`, its `InlineData` third value the canonical name; `MethodCompileTests.Run_ParameterOfEachKindOnNpgsql_Compiles` asserts `row.DataTypeName`, and its `maybe` parameter (`Int4 with { BindName = "integer" }`) asserts `pg_catalog.int4` too.  Update the comments that say "by its bindName".
- [ ] **Step 5: Fix every other expected `DataTypeName` in `tests/SqlSource.Tests`** (`QueryMethodWriterTests`, `MethodTests` line 99, and whatever `grep -rn 'DataTypeName = \\"' tests/SqlSource.Tests` finds) to the canonical name of the type the test describes.
- [ ] **Step 6: Update `docs/architecture/generator.md`**: the PostgreSQL binding bullet says `DataTypeName` is the canonical name derived from the described type's `schema` and `internalName` (a domain over a domain its innermost base's), why, the two fallbacks to `bindName`, and that `bindName` remains the name the tool's `check` proves; line 296 says the binding derives its name from the `SidecarType`.
- [ ] **Step 7: Run** `./format.sh -- <changed files>`, then `dotnet test --project tests/SqlSource.Tests` — all pass.
- [ ] **Step 8: Commit** `feat(generator): bind PostgreSQL parameters by the canonical type name`.

### Task 2: Hand-written baselines, end-to-end check, measurement and the tech-debt item

**Files:**
- Modify: `tests/DatabaseTests/QueryMethodAllocation/Postgres/AllocationTests.cs` (hand-written `DataTypeName`s: `integer` → `pg_catalog.int4`, `timestamp with time zone` → `pg_catalog.timestamptz`)
- Delete: `docs/tech-debt/TD-0078-datatypename-is-resolved-on-every-call.md`
- Modify: `docs/tech-debt/README.md` (remove the row)

- [ ] **Step 1:** Change the hand-written methods so each pair still makes the same calls, and run the allocation harness (command in `tests/DatabaseTests/QueryMethodAllocation/README.md`, `-c Release --output Detailed`): every test passes, and the generated bytes per call are recorded for the PR.  If a margin comment's numbers no longer hold, update the comment's measured numbers, never the margin.
- [ ] **Step 2:** Run `tests/DatabaseTests/Postgres` (end-to-end `EveryTypeTests` and the rest, Npgsql 10) and `tests/DatabaseTests/Postgres.Npgsql8`: all pass.
- [ ] **Step 3:** Delete the item file and its row; `grep -rn "TD-0078" src tests docs/architecture docs/manual docs/tech-debt` finds nothing (historical `docs/superpowers/` records stay, as for resolved items before).
- [ ] **Step 4:** Commit `docs(tech-debt): resolve TD-0078`.
