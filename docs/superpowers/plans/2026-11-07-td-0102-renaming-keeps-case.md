# TD-0102 Renaming Keeps Case Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQL Server's renaming gives two spellings of one name that differ by case alone replacements that differ by case alone, so a query that spells `EMAIL` for the column `email` is renamed instead of refused, and a case-sensitive database's `Users` and `users` stay apart.

**Architecture:** `NameMapping` keeps one hash for every spelling of one fold, as today, and writes it in a case of its own for each spelling the engine's pipeline tells apart: the first spelling gets today's lower-case replacement, the k-th other one the same letters with the case of its letters set by the bits of k.  Which spellings of one fold are told apart is a new `IEngineRenaming` member, `Spelled`: PostgreSQL's is its fold, so a PostgreSQL bundle is unchanged; SQL Server's is the spelling as written.

**Tech Stack:** C# (.NET), xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0102-sql-server-renaming-folds-ignoring-case.md`, with `docs/architecture/bundles.md` ("The mapping", "The engine's contract", "SQL Server's fold").

## Where the code differs from the spec

- **The collation half of the proposed fix is not built** (owner, 2026-11-07, Q1: "agree").  With a case of its own for each spelling, every comparison the pipeline makes keeps its answer whatever the collation: one spelling has one replacement, so an ordinal comparison holds, and one fold has one set of letters, so a comparison ignoring case holds.  Folding by ordinal in a case-sensitive database would give `@Id` and `@id` two hashes, which `SqlParameterName.Comparer` takes for one parameter, so the proof would refuse more.  The contract still changes, by `Spelled` instead of a collation.
- **The case follows the order spellings are met, not the original's pattern.**  The spec asks for "a case of its own for each spelling"; the first spelling keeps today's lower-case replacement, so every bundle with one spelling per name is renamed exactly as today on both engines.
- **`NameMapping.TryGet` becomes `IsMapped`**: its replacement was never read, and a new spelling of a known fold would have to make one.

## Global Constraints

- PostgreSQL's renaming behaves as it does: its `Spelled` is its `Fold`, and no PostgreSQL test or fixture changes.
- No name or literal of the user's may leak into a bundle: `BundleLeaks`, the proof and the container corpus tests pass on both engines.
- Every replacement comes from the run's `NameMapping` (`src/SqlSource.Tool/Bundles/AGENTS.md`).
- Nothing new is captured, so no fixture is re-recorded.

## Review Focus

- A third spelling of one name, or more spellings than the replacement's letters can tell apart: each still gets a replacement of its own by ordinal (the overflow test of Task 1).
- A spelling kept from an engine's row, met again in another case (`sys` and `SYS`): SQL Server keeps each spelling as written, PostgreSQL maps it to the first kept (Task 1's keep tests).
- A parameter spelled in two cases: the pipeline takes them for one, and the replacements are equal ignoring case (Task 1's bundle test).
- A fixture or realistic case whose query already spells a name in another case than the catalog, which renamed before by merging: it must still pass the proof and the leak search (the full fixture and container runs).
- The mapping file: each spelling of one name is listed with its own replacement and original (Task 1's `Names` test).

---

### Task 1: A case of its own for each spelling

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/IEngineRenaming.cs` (new member after `Fold`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/NameMapping.cs`
- Modify: `src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs:66`, `RenamingRun.cs` (`TryGet` callers at `Name` and `Scope.IsMapped`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresRenaming.cs`, `SqlServer/SqlServerRenaming.cs` (member and class remarks)
- Modify: `tests/SqlSource.Tool.Tests/FakeEngineRenaming.cs`, `tests/SqlSource.Tool.Tests/Bundles/Renaming/EntryRenamerTests.cs`, `SqlRenamerTests.cs` (constructor calls)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`, `SqlServer/SqlServerRenamingTests.cs`, `Postgres/PostgresRenamingTests.cs` (or wherever PostgreSQL's members are tested), `SqlServer/SqlServerErrorRenamingTests.cs`

**Interfaces:**
- Produces: `string IEngineRenaming.Spelled(string spelling, bool quoted)` — the spelling as the engine's pipeline may compare it by ordinal; two spellings of one `Fold` with different `Spelled` get replacements that differ by case alone.
- Produces: `NameMapping(RenamingKey key, Func<string, bool, string> fold, Func<string, bool, string> spelled)` and `internal NameMapping(RenamingKey key, Func<string, bool, string> fold, Func<string, bool, string> spelled, int firstLength)`; `bool NameMapping.IsMapped(string spelling, bool quoted)` replaces `TryGet`.

- [ ] **Step 1: Write the failing mapping tests** in `NameMappingTests.cs`.  `Mapping()` passes `Fold` as `spelled` too (today's behaviour).  Add `CaseKeeping(byte seed = 0, int? firstLength = null)` whose `fold` is `spelling.ToUpperInvariant()` and whose `spelled` is the spelling itself.

```csharp
[Fact]
public void Name_SpellingsTheEngineTellsApart_DifferByCaseAlone()
{
    var mapping = CaseKeeping();
    var lower = mapping.Name("email", false, NameClass.Column);
    var upper = mapping.Name("EMAIL", false, NameClass.Column);
    lower.ShouldMatch("^c_[0-9a-f]{8}$");
    upper.ShouldNotBe(lower);
    string.Equals(upper, lower, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    upper.ShouldBe("C" + lower[1..]);               // the second spelling is variant 1: the first letter upper
    mapping.Name("EMAIL", false, NameClass.Column).ShouldBe(upper);
    mapping.Name("email", false, NameClass.Column).ShouldBe(lower);
    mapping.IsReplacement(upper).ShouldBeTrue();
}

[Fact]
public void Name_SpellingsTheEngineTellsApart_AreEachListedWithTheirOriginal()
// Names: two items, replacements lower and upper, originals "email" and "EMAIL", both NameClass.Column.

[Fact]
public void Name_SpellingsTheEngineTakesForOne_AreOneName()
// Mapping(): Name("Users") == Name("users"), Names has one item (today's Name_FoldedAlike_IsOneName stays).

// firstLength 1: a replacement holds two letters at most, so four spellings at most differ by case alone.
[Fact]
public void Name_MoreSpellingsThanTheCaseCanTellApart_EachHasItsOwnName()
// CaseKeeping(firstLength: 1); the eight case spellings of "abc"; all eight replacements distinct by ordinal,
// all IsReplacement, at least two and at most four equal ignoring case to the first, asked again each the same.

[Fact]
public void Keep_SpellingsTheEngineTellsApart_KeepEachAsWritten()
// CaseKeeping(): Keep("sys"); Name("SYS") is "SYS", Name("sys") is "sys"; Names is empty.

[Fact]
public void Keep_SpellingsTheEngineTakesForOne_MapToTheFirstKept()
// Mapping(): Keep("pg_catalog"); Name("PG_CATALOG") is "pg_catalog".

[Fact]
public void KeepFromEngineRow_AnotherCase_IsKeptFromTheRow()
// CaseKeeping(): KeepFromEngineRow("name"); IsKeptFromEngineRow("NAME") is true.

[Fact]
public void IsMapped_AnotherCaseOfAMappedName_IsTrue()
// CaseKeeping(): Name("email"); IsMapped("EMAIL") true, IsMapped("x") false.  Rename TryGet_UnknownSpelling_IsFalse to IsMapped_UnknownSpelling_IsFalse.
```

- [ ] **Step 2: Run them and see them fail to compile**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~NameMappingTests"`
Expected: build error, no `NameMapping` constructor of three arguments.

- [ ] **Step 3: Implement `NameMapping`.**  Per fold keep the first `MappedName` (its class decides the variants' class) and a `Dictionary<string, string>` by `spelled` (ordinal) of replacements.  A new `spelled` of a known fold gets variant k, k = the number of spellings met before it: the base's letters (the prefix's letter, then each hex letter `a` to `f`, in order) with letter i upper-cased when bit i of k is set.  When k reaches 2^letters, it gets a fresh replacement from `Make` keyed by the class word, the fold and the spelling (`word + ":" + folded + ":" + spelled`), which differs from the others ignoring case too: the proof refuses the bundle if that changes an answer.  Each variant is added to `_replacements` and to `Names` with its own original.  `_kept` becomes fold → (`spelled` → spelling first kept with it); a kept fold met with a new `spelled` stores and returns the spelling as written.  `_keptFromEngineRows` stays by fold.  Rewrite the class remarks to say all this; keep "The renaming starts at eight digits; a test gives a shorter firstLength".

- [ ] **Step 4: Add `Spelled` to `IEngineRenaming`** after `Fold`, documented: "A spelling as the engine's pipeline may compare it by ordinal, which tells apart two spellings that `Fold` takes for one name: PostgreSQL's is its fold, since its pipeline folds a name before it compares it; SQL Server's is the spelling as written, since its pipeline compares some names by ordinal and others ignoring case.  The mapping writes two spellings of one fold whose `Spelled` differ in cases of their own, so that they stay apart by ordinal and equal ignoring case."  Implement: `PostgresRenaming.Spelled` → `PostgresWords.Fold(spelling, quoted)`; `SqlServerRenaming.Spelled` → `spelling`; `FakeEngineRenaming.Spelled` → `Fold(spelling, quoted)`.  `BundleRenamer` builds `new NameMapping(key, engine.Fold, engine.Spelled)`; test constructors likewise.  `RenamingRun` calls `IsMapped`.  `SqlServerRenaming`'s remarks: a name folds ignoring case, and each spelling keeps a case of its own through `Spelled`.  Add `Spelled_IsTheSpellingAsWritten` (`EMAIL`, quoted and not, is `EMAIL`) to `SqlServerRenamingTests` and `Spelled_IsTheFold` (`Users` unquoted is `users`, quoted is `Users`) to PostgreSQL's part's tests.

- [ ] **Step 5: Turn the refusal test into a renaming test** in `SqlServerErrorRenamingTests.cs`: `RenameAsync_ANameThatDiffersFromTheCatalogsByCaseAlone_IsRefusedByTheProof` becomes `RenameAsync_ANameThatDiffersFromTheCatalogsByCaseAlone_IsRenamed`: same edit of `select-computed-not-null`; the renaming succeeds (use the file's helper for a successful rename and its proof), the renamed entry's table match is still `names-differ`, the renamed query holds the column's two replacements, equal ignoring case and different by ordinal, and `BundleLeaks.Find` finds nothing.  Add `RenameAsync_AParameterSpelledInTwoCases_IsOneParameter`: a fixture whose query names one parameter that is not an engine word, edited so `query.sql` and the capture's request SQL spell one of its uses in upper case; raw replay gives the entry, the renaming succeeds and the proof passes.  If no fixture names a parameter twice, edit the `parameters` row's `name` to the other case instead.

- [ ] **Step 6: Run the tests that cover the change**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles.Renaming|FullyQualifiedName~BundleLeaks"`
Expected: all pass, PostgreSQL's fixture renaming tests unchanged.

- [ ] **Step 7: Format, validate and commit**

Run `./format.sh`, then `./pre-commit-validation.sh` (foreground, timeout 600000); fix what it reports.

```bash
git add -A
git commit -m "fix(bundles): SQL Server's renaming keeps each spelling's case apart (TD-0102)"
```

### Task 2: Docs and resolving TD-0102

**Files:**
- Delete: `docs/tech-debt/TD-0102-sql-server-renaming-folds-ignoring-case.md`; Modify: `docs/tech-debt/README.md` (its row only)
- Modify: `docs/architecture/bundles.md` ("The mapping", the contract table's new `Spelled` row, the `Fold` row of SQL Server's table and of PostgreSQL's words, "SQL Server's fold" rewritten without the TD link)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` (the fold bullet: one fold by `SqlServerVocabulary.Fold`, each spelling's case through `Spelled`; no TD link)
- Modify: `docs/manual/tool.md` (remove the known issue "A SQL Server query that spells a name in another case…"; in "Every name of yours gets a new name" add that under SQL Server two spellings of one name that differ by case alone get new names that differ by case alone, `c_4f0a9c21` and `C_4f0a9c21`)

- [ ] **Step 1:** Make the edits; `grep -rn "TD-0102" . --exclude-dir=.git --exclude-dir=docs/superpowers` finds nothing outside `docs/superpowers/`.
- [ ] **Step 2:** `./format.sh`, `./pre-commit-validation.sh`; commit `docs: TD-0102 resolved, each spelling's case kept apart`.
