# TD-0093: a `-- param:` type's modifier may hold words — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On PostgreSQL, a `-- param:` type whose modifier holds bare words, such as PostGIS's `geometry(Point,4326)`, is sent to the server and described, instead of being refused as `SQLSRC228`, while the text sent to `PREPARE` stays a type name.

**Architecture:** `DeclaredTypeText.TryReadModifier` (PostgreSQL) reads the modifier as a list of items: a signed integer, as today, or a bare ASCII identifier, separated by commas and spaces.  It still refuses a quote, a `;`, a comment, a nested parenthesis, a `$` and anything else.  The server then checks the words (PostGIS's typmod input refuses a word it does not know).  A container test on the harness's PostGIS image shows both on PostgreSQL 14 and 18.

**Tech Stack:** C# / .NET, xunit v3, Shouldly, Testcontainers (PostGIS image built by `tests/DatabaseTests/TestHarness/Postgis/Dockerfile`).

**Spec:** `docs/tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md` (deleted by Task 3; read it from `git show origin/main:docs/tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md` after that).

## Global Constraints

- Identifier in a modifier: bare, `[A-Za-z_][A-Za-z0-9_]*` (ASCII only), as `geometry(Point,4326)` and `geography(PointZM,4326)` are.
- Still refused before anything is sent: a quote (`'` or `"`), a `;`, a comment (`--`, `/*`), a nested parenthesis.
- Nothing a `.sql` file holds may make the describer run a statement (`src/SqlSource.Tool/Describing/Postgres/AGENTS.md`); the text sent to `PREPARE` stays a type name.
- Every form accepted today stays accepted (digits, commas, spaces, `-` before a digit), so no query described today changes.
- Check against PostGIS on PostgreSQL 14 (`SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`) and 18 (default) with the describer's container tests and the harness's PostGIS image.
- Resolve the item as `docs/tech-debt/AGENTS.md` "Resolving an item" says; user docs describe effects, never internals (root `AGENTS.md` rule 4).

## Deviations from the spec

- The spec's "describer's container tests" live in `tests/DatabaseTests/Postgres/Describer/`, collection `UsesPostgres`, whose container has no PostGIS.  The PostGIS case is a new class there in the collection `UsesPostgis`, on `PostgisDatabases`' database `gis`, so it runs on the run's image (14 or 18) with PostGIS.
- `int(a)` is a refused case of today's `DeclaredTypeTextTests`; under the new rule the reader takes it and the server refuses it (`SQLSRC228` with a `server:` line).  It moves to the accepted cases.
- Historical plans and specs in `docs/superpowers/` keep the id as a record (as TD-0090's resolution did); their markdown links to the deleted item file become plain text.

## Review Focus

1. A word starting with a digit, `geometry(4326Point)`, or a `-` before a word, `geometry(-Point)`: refused by the reader (neither is a number nor a bare identifier).
2. A non-ASCII letter in a word, `geometry(Pöint)`: refused (the rule is ASCII), though the type's own name takes Unicode letters.
3. A word the server does not know, `geometry(Nonsense,4326)`: `SQLSRC228`, `it is not written as a type name`, with the server's line, on 14 and 18.
4. A modifier of words before array marks and with a schema, `public.geometry(Point,4326)[]`: accepted, described as `public.geometry[]` with no modifier.
5. Spaces around items, `geography( PointZM , 4326 )`: accepted, as spaces are today.

---

### Task 1: The reader takes a modifier of numbers and bare words

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/Statements/DeclaredTypeText.cs` (`TryReadModifier`, its comment, the class remarks' "one modifier in parentheses")
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/DeclaredTypeTextTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `DeclaredTypeText.Read(string)` unchanged in signature; it now returns the trimmed text for a modifier of words.

- [ ] **Step 1: Write the failing tests.**  Add to `Read_TypeName_IsTheText`: `"geometry(Point,4326)"`, `"geography(PointZM, 4326)"`, `"geography( PointZM , 4326 )"`, `"geometry(Point)"`, `"geometry(_point_1)"`, `"public.geometry(Point,4326)[]"`, `"int(a)"` (moved from the refused cases: the server refuses it, with a comment saying so).  Add to `Read_AnythingElse_IsNotATypeName`, under a comment saying a modifier holds numbers and bare words alone: `"geometry('Point',4326)"`, `"geometry(Point,'4326')"`, `"geometry(\"Point\",4326)"`, `"geometry(Point;4326)"`, `"geometry(Point--)"`, `"geometry(Point -- x)"`, `"geometry(Point/*x*/)"`, `"geometry((Point))"`, `"geometry(Point(1))"`, `"geometry(4326Point)"`, `"geometry(-Point)"`, `"geometry(Point$1)"`, `"geometry($$Point$$)"`, `"geometry(Pöint)"`, `"geometry(Point.Z)"`, `"geometry(Point,4326"`, `"geometry(Point)(4326)"`.  Remove `"int(a)"` from the refused cases.
- [ ] **Step 2: Run them and see the new accepted cases fail.**  `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Describing.Postgres.Statements.DeclaredTypeTextTests"`: the accepted word cases FAIL; the refused ones pass already.
- [ ] **Step 3: Implement.**  `TryReadModifier(string text, ref int position)`: from `position + 1` to `)`, skip `,` and ` `; at `-` followed by an ASCII digit, or at an ASCII digit, read ASCII digits; at `[A-Za-z_]` (`char.IsAsciiLetter` or `_`), read `[A-Za-z0-9_]`; after a run of digits, the next character must not be an ASCII letter or `_` (refuses `4326Point`); anything else, or the end of the text, is `false`.  Comment above it: `"(" with signed integers and bare ASCII words, separated by commas and spaces, up to ")".  A "-" stands before a digit, so that two cannot start a comment; a word is ASCII, as PostGIS's are.`  Update the remarks: "one modifier in parentheses" becomes "one modifier in parentheses, of numbers and bare words".
- [ ] **Step 4: Run the tests and see them pass.**  Same command: PASS.
- [ ] **Step 5: Commit** (after `./format.sh` and `./pre-commit-validation.sh`): `fix(tool): a -- param: type's modifier on PostgreSQL may hold bare words (TD-0093)`.

### Task 2: PostGIS checks the words, on 14 and 18

**Files:**
- Create: `tests/DatabaseTests/Postgres/Describer/PostgisParameterTests.cs`
- Modify: `tests/DatabaseTests/Postgres/README.md` (line 7: the tests that start `PostgisDatabases` now include this class), `tests/DatabaseTests/Postgres/AGENTS.md` (first bullet: "but for `EndToEnd/CheckTests`, of `UsesPostgis`" names the tests on PostGIS: the NetTopologySuite tests, `CheckTests` and `Describer/PostgisParameterTests`; the `gis` bullet: a describer test of PostGIS's types is in `UsesPostgis` too)

**Interfaces:**
- Consumes: `Sessions.DescribeAsync(string connection, DescribeRequest request, ...)` and `Sessions.ShouldDescribeAsync(string connection, DescribeRequest request, ...)` of `Describer/Sessions.cs`; `PostgisDatabases.ConnectionStringOf(PostgisDatabases.GisDatabase)`; `DescribeRequests.Of(sql, (name, type)...)`; `TestSidecars.Postgres(...)`; `DescribedParameter(name, type, SidecarValues.TypeSource.Declared)` with `BindName` as `ParameterTests.Declared` builds it.
- Produces: nothing other tasks use.

- [ ] **Step 1: Write the tests.**  `[Collection(UsesPostgis.Name)] public sealed class PostgisParameterTests(PostgisDatabases postgis)`, comment: a `-- param:` type with a modifier of words, as a column definition writes PostGIS's, is sent to the server, which checks the words and gives the parameter no modifier.
  - `DescribeAsync_DeclaredGeometryWithAModifierOfWords_IsTheTypeWithoutIt`: `SELECT id, @place, @shapes FROM spatial_values WHERE point_value = @point` with `("point", "geometry(Point,4326)")`, `("place", "geography(PointZM, 4326)")` and `("shapes", "public.geometry(Polygon,4326)[]")`.  Assert the parameters are `Declared` with the types `public.geometry`, `public.geography`, `public.geometry[]` (internal names `geometry`, `geography`, `_geometry`; build them with `TestSidecars.Postgres` as `ParameterTests` builds `UserStatus`/`TextArray`; take the exact kind and `bindName` from what the describer gives on 18, and see it equal on 14).
  - `DescribeAsync_DeclaredGeometryWithAWordPostgisDoesNotKnow_IsSqlsrc228WithTheServersLine`: `("point", "geometry(Nonsense,4326)")` on the same query.  Assert `Descriptor.Id` is `SQLSRC228`, `Arguments` are `["geometry(Nonsense,4326)", "@point", DescribeRequests.Query, DescribeFailures.NotATypeName]`, and `ServerLines[0]` is the server's line, pinned to what 18 and 14 both give (if they differ, `ShouldStartWith` the common part, with a comment saying why).
- [ ] **Step 2: Run on the default image.**  `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~PostgisParameterTests"`: PASS (Task 1 is in place; without it the first test fails with `SQLSRC228`).
- [ ] **Step 3: Run on the floor image.**  `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~PostgisParameterTests"`: PASS.  The first run on 14 builds the PostGIS image (a minute or two).
- [ ] **Step 4: Update the readme and AGENTS.md** as listed under Files; keep `tests/DatabaseTests/Postgres/AGENTS.md` under 8 KB, each bullet under 80 words.
- [ ] **Step 5: Commit** (after `./format.sh` and `./pre-commit-validation.sh`): `test(postgres): a -- param: modifier of words against PostGIS (TD-0093)`.

### Task 3: The docs and the item

**Files:**
- Modify: `docs/manual/parameters-and-tokens.md` (remove the known issue "On PostgreSQL, a `-- param:` type whose modifier holds words is refused."; keep the other known issues as they are)
- Modify: `docs/manual/diagnostics.md` `SQLSRC228`, first bullet: remove "On PostgreSQL the modifier holds numbers alone, … for what to write instead."; say instead that on PostgreSQL the modifier holds numbers and plain words separated by commas, as in PostGIS's `geometry(Point,4326)`, and that the server checks the words.  Keep to the `SQLSRC228` section (td-0101 and td-0106 edit others).
- Modify: `docs/manual/parameters-and-tokens.md` `#param-marker` section and `docs/manual/postgresql.md` if either says what a declared type may be in a way this change makes wrong (`grep -n "modifier\|type name" `); otherwise no change.
- Modify: `docs/architecture/postgres-describer.md` only if a sentence describes `DeclaredTypeText`'s grammar (`grep -n "DeclaredTypeText\|grammar of a type"`); today none does.
- Delete: `docs/tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md`
- Modify: `docs/tech-debt/README.md` (remove the TD-0093 row only)
- Modify: `docs/superpowers/specs/2026-10-26-query-generation-phase-7-spike.md`, `docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md` (links to the deleted file become the plain id `TD-0093`)

- [ ] **Step 1: Edit** as listed.  `grep -rn "TD-0093" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj` then shows only `docs/superpowers/` records (no links to the deleted file, and this plan).
- [ ] **Step 2: Check** `grep -rn "modifier holds words\|numbers alone" docs README.md src/SqlSource.Tool/README.md`: no match.  Run `dotnet test tests/SqlSource.Repository.Tests`: PASS (links, diagnostics sections, AGENTS sizes).
- [ ] **Step 3: Commit** (after `./pre-commit-validation.sh`): `docs: a -- param: modifier of words is described (resolves TD-0093)`.
