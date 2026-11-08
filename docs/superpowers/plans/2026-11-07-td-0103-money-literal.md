# TD-0103 SQL Server's money literal kept as a number Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQL Server's renaming of a bundle keeps a money literal such as `$12` verbatim, as it keeps `12`, instead of renaming it to a name.

**Architecture:** The engine's contract (`IEngineRenaming`) gains `IsNumberStart(text, at, end)`, which says where a number starts: an ASCII digit for both engines, and for SQL Server also a `$` that no word character but an ASCII digit follows.  One neutral reader, `SqlRenamer.NumberAt`, reads the run a number starts and says whether it is one; the SQL renamer, the token check, `BundleLeaks` and `CorpusWordsTests` all read numbers through it, so the four agree.

**Tech Stack:** C# / .NET, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0103-sql-server-renaming-reads-a-money-literal-as-a-name.md`

## Where the code differs from the spec

- The spec reads only "a `$` that a digit follows, then digits, an optional `.` and digits".  Probing SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`) found more forms of money: `$` alone is `0.0000`, `$ 12`, `$-12`, `$+12`, `$.5` and `$-.5` are money, `$12.` is `12.0000`, and `$a` is a pseudo-column (`Invalid pseudocolumn "$a"`).  So a `$` starts a number when what follows it is no word character, or is an ASCII digit.  A `$` with nothing of a number after it is kept alone as the sign, and the rest (a sign, `.5`, white space) is read as today: punctuation and numbers, all kept.  Without this, `$-12` would still rename `$` to a name.
- The server reads `$1e5` as `$1 AS e5`, `$1_000` as `$1 AS _000` and `$12abc` as `$12 AS abc`.  So the digits after a `$` must be a decimal (digits, then optionally `.` and digits, possibly none), with no exponent and no `_`, and any other run from a `$` is refused whole, as a run from a digit that is no number is today (`1users`).  Today those three are renamed to a name; after this change a bundle holding one is refused.
- `$` and a digit run of PostgreSQL never reach a `Text` lexeme as a money form: `$1` is a parameter lexeme and `$q$` a quoted one, and `$` is no word start of PostgreSQL's.  PostgreSQL's `IsNumberStart` is an ASCII digit, its behavior today.

## Global Constraints

- `IEngineRenaming` changes only with both engines' parts and its contract in `docs/architecture/bundles.md#the-engines-contract` (`src/SqlSource.Tool/Bundles/AGENTS.md`).
- The neutral part never names an engine in code; examples in comments may.
- Resolving the item: delete the item file, its README row, every `TD-0103` reference (`git grep -n TD-0103`); no manual known issue exists.
- `./format.sh`, then `./pre-commit-validation.sh` before every commit.

## Review Focus

- `$` followed by a letter, `_`, `@`, `#` or `$` (a pseudo-column such as `$action`, `$IDENTITY`): still a word, `$action` kept as the vocabulary's word.  Test in Task 1.
- `$` at the very end of a text or a hint: kept alone, no index past `end`.  Test in Task 1.
- A money literal in the token check and in `BundleLeaks.Find`: passes the check, is not sought as a word of the user's.  Tests in Task 1 (token check) and Task 2 (leak search).
- `x$1` (a `$` inside a word) and `12$3` (a run from a digit through `$`): unchanged, a word and a refused run.  Test in Task 1.
- PostgreSQL's numbers unchanged: the existing `SqlRenamerTests` rows of `postgres` still pass.

---

### Task 1: The contract, both engines and the shared reader

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/IEngineRenaming.cs` (new member; `IsWordStart`'s remark)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs` (`IsNumberStart`; remove the TD-0103 comment)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresRenaming.cs` (`IsNumberStart`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlRenamer.cs` (`NumberAt`, `IsDecimal`; `Text` reads through `NumberAt`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/TokenCheck.Sql.cs` (`Words` reads through `NumberAt`)
- Modify: `tests/SqlSource.Tool.Tests/FakeEngineRenaming.cs` (`IsNumberStart`: an ASCII digit)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs`, `tests/SqlSource.Tool.Tests/Bundles/Renaming/TokenCheckTests.cs` or the SQL Server error renaming tests, whichever holds a check of renamed SQL

**Interfaces:**
- Produces: `bool IEngineRenaming.IsNumberStart(string text, int at, int end)`.
- Produces: `internal static (int End, bool IsNumber)? SqlRenamer.NumberAt(IEngineRenaming engine, string text, int at, int end)`: null when `IsNumberStart` is false; from an ASCII digit, `NumberRunEnd` and `IsNumber`; from any other character, that one character and, when an ASCII digit follows before `end`, the run from it by `NumberRunEnd`, a number when that run is a decimal (`IsDecimal`: ASCII digits, then optionally `.` and digits, possibly none).

- [ ] **Step 1: Replace `Sql_AMoneyLiteral_IsRenamedToAName` with failing tests**
  - `Sql_AMoneyLiteral_IsKept` (Theory): `SELECT $12 AS price`, `SELECT $12.50 AS price`, `SELECT $12. AS price`, `SELECT $ AS price`, `SELECT $ 12 AS price`, `SELECT $-12 AS price`, `SELECT $+12 AS price`, `SELECT $.5 AS price`, `SELECT $-.5 AS price`: no refusal, and the renamed SQL is the input with `price` replaced by its replacement.
  - `Sql_AMoneyLiteralWithMore_IsRefused` (Theory): `SELECT $1e5` → `$1e5`, `SELECT $1_000` → `$1_000`, `SELECT $12abc` → `$12abc`, `SELECT $1.2.3` → `$1.2.3`: a refusal of `RefusalStep.Token` whose token is the run.
  - `Sql_ADollarAWordFollows_IsAWord`: `SELECT $action, $secret` → `$action` kept, `$secret` renamed to its replacement (the `$` is part of the word as today).
  - `Sql_ADollarAtTheEnd_IsKept`: `SELECT 1 /*+ $*/` and `SELECT $` keep `$`.
  - `IsNumberStart_Character_IsTheEnginesRule`: SQL Server true at `1`, at `$` of `$12`, `$`, `$ 1`, `$-1`, `$.5`; false at `$` of `$a`, `$_`, `$@`, `$#`, `$$`, and at `a`.  PostgreSQL (in `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/`'s renaming test of the part, or a new row in the same file) true at a digit, false at `$` of `$12`.
  - A token check test: the SQL Server renamed text `SELECT $12.50 AS <replacement>` passes the token check, and `SELECT $1e5` is refused at `$1e5`.

- [ ] **Step 2: Run them; the new ones fail**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Renaming"`
Expected: the new tests FAIL (the member does not exist / `$12` renamed).

- [ ] **Step 3: Implement**
  - `IEngineRenaming.IsNumberStart`: doc says where a number starts, read before `IsWordStart` is asked; `IsWordStart`'s remark points at it instead of "an ASCII digit".
  - SQL Server: `char.IsAsciiDigit(text[at])`, or `text[at] == '$'` and (`at + 1 == end` or `char.IsAsciiDigit(text[at + 1])` or `!IsWordPart(text[at + 1])`).
  - PostgreSQL: `char.IsAsciiDigit(text[at])`.  Fake: the same.
  - `SqlRenamer.NumberAt` and a private `IsDecimal`; `Pass.Text` and `TokenCheck.Words` replace their `char.IsAsciiDigit` branch with `NumberAt(...) is { } number`.  The `SqlRenamer` remarks and the `Text`/`Words` comments say "a run a number starts" instead of "a run from a digit".

- [ ] **Step 4: Run the renaming tests; all pass**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Renaming"`
Expected: PASS.

- [ ] **Step 5: Commit** (after `./format.sh` and `./pre-commit-validation.sh`)

`fix(bundles): SQL Server's renaming keeps a money literal as a number`

### Task 2: The leak search and the corpus words read numbers the same way

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/Bundles/BundleLeaks.cs` (`EngineWordsOf` reads through `SqlRenamer.NumberAt`)
- Modify: `tests/DatabaseTests/SqlServer/Renaming/CorpusWordsTests.cs` (its word reader reads through `SqlRenamer.NumberAt`)
- Test: `tests/SqlSource.Tool.Tests/Bundles/BundleLeaksTests.cs` (or where `BundleLeaksTests` lives); `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs`

**Interfaces:**
- Consumes: `SqlRenamer.NumberAt` (Task 1).

- [ ] **Step 1: Failing test** — a SQL Server error-renaming test (fixture raw bundle edited as the file's other tests do) whose query holds `$12.50`: the bundle is renamed, the proof passes, `renamed query.sql` still holds `$12.50`, and `BundleLeaks.Find` finds nothing.
- [ ] **Step 2: Run; it fails** with a leak of `$12.50` (the leak search reads it as a word).
- [ ] **Step 3: Implement** the two readers through `NumberAt`.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Bundles"`; PASS.  `CorpusWordsTests` runs in the SQL Server database tests under the full validation.
- [ ] **Step 5: Commit** `test(bundles): the leak search reads a money literal as a number`

### Task 3: Docs and resolving the item

**Files:**
- Modify: `docs/architecture/bundles.md` — "The SQL renamer" (a number starts where `IsNumberStart` says; a run from another character is it and a decimal), the contract table (new `IsNumberStart` row; `IsWordStart, IsWordPart`'s "must never" points at it), PostgreSQL's table (`IsNumberStart`: an ASCII digit), SQL Server's table (`IsNumberStart`: a digit, or a `$` that no word character but a digit follows), "What building SQL Server's part found" (the money bullet rewritten as what was found: the forms the server reads as money and the refused runs), "SQL Server's tests" if it names the old test.
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` only if a rule changes (none expected).
- Delete: `docs/tech-debt/TD-0103-sql-server-renaming-reads-a-money-literal-as-a-name.md`; remove its row from `docs/tech-debt/README.md`.
- Check: `git grep -n TD-0103` finds only this plan and the phase-10.4 plan's historical line.

- [ ] **Step 1: Edit the docs and delete the item**
- [ ] **Step 2: Run** `./format.sh` and `./pre-commit-validation.sh`; PASS.
- [ ] **Step 3: Commit** `docs: TD-0103 is fixed`

### Finish

- [ ] `./pre-commit-validation.sh --full`; SQL Server's describer is not touched, so no floor-image run is needed, but `tests/DatabaseTests/SqlServer` (its renaming tests) runs in the full validation.
- [ ] `/branch-review:review --base origin/main` to convergence.
