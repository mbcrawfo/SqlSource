# TD-0053, duplicated and uneven patterns in the PostgreSQL describer - design

Date: 2026-10-17

Resolves [TD-0053](../../tech-debt/TD-0053-duplicated-and-uneven-patterns-in-the-postgresql-describer.md).  The item lists eleven findings of the maintainability review of phase 3 in `src/SqlSource.Tool/Describing/Postgres/` and `Describing/Capture/`.  This document is the overview: why, what done is, how the work splits into three pull requests, and the rules every one of them keeps.  Each pull request has its own design, written to be built without this conversation:

| PR | Design | Items |
|---|---|---|
| 1 | [SQL text and names](2026-10-17-td-0053-1-sql-text-and-names-design.md) | 1, 2, 3, 11, and from 8 the `ServerLines` repeat, the `$n` number reader and `Invariant` |
| 2 | [The plan walk](2026-10-17-td-0053-2-plan-walk-design.md) | 4, 5, `RunSetOperation`'s parameters from 7, and from 9 `PlanInstances.Find` and `WalkScope.ColumnTypes` |
| 3 | [Types, calls and the rest](2026-10-17-td-0053-3-types-and-calls-design.md) | 6, the rest of 7, the rest of 8, the `Resolve` methods from 9, and 10; deletes TD-0053 |

The numbers 1 to 11 are the item's own and stay so in every document and commit.

## Why

The user's goal is to pay this debt off.  The item's trigger, a second engine's describer in phase 6, is the reason it is worth paying before more code copies the patterns.

## Done

- When PR 3 merges, `docs/tech-debt/TD-0053-*.md` and its row in `docs/tech-debt/README.md` are gone, and no file outside `docs/superpowers/` names the id.
- Every item is fixed, including what a check of the code on 2026-10-17 found the item had missed.  Each PR design lists those.
- Where the item's proposed fix would change an answer, the designs replace it with a fix that does not, and say why.

## Rules for every PR

1. **No answer changes, with two exceptions.**  Every fixture, `capture.json`, `expected.json` and `expected-pg*.txt` file stays byte for byte as it is, and no fixture is re-recorded.  The text of every log event stays as it is.  The only changes in behaviour allowed are the ones the user approved for PR 1, both of which bring the describer closer to how PostgreSQL reads SQL:
   - the user's SQL is read with PostgreSQL's own rule for a character of a word.  That changes answers for SQL that is not valid, and for valid SQL whose names hold `$` or a non-letter above U+007F, such as `€into` or `x$left` (PR 1 lists them);
   - `$1` after a digit, as in `1$1`, is read as a parameter, which today it is missed as.
   Each of those is pinned by a new test.
2. **Nothing recorded depends on what changes.**  A capture holds only a call's method name, its request and its answer; the SQL that `PostgresLive` sends is in no capture and no test.  So constants for call names keep today's values, a method name never changes (`src/SqlSource.Tool/AGENTS.md`), and the internal SQL of `PostgresLive` may change text without re-recording.
3. **Keep TD-0053 true as you go.**  PR 1 and PR 2 each rewrite the item file and its row to drop the items, or the parts of items, they fix, keeping the original numbers of the rest (`docs/tech-debt/AGENTS.md`, "Changing an item").  PR 1 and PR 2 may run at the same time, so whichever merges second resolves the conflict in the item file by keeping both removals.  PR 3 resolves the item: deletes the file and the row, and searches for `TD-0053`.
4. **The repository's own rules hold**, root `AGENTS.md` first: `./pre-commit-validation.sh` before every commit and push; the container tests also on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, since each PR changes the describer; every valid review finding is fixed in the same PR, minor ones included; every CodeRabbit finding gets a reply; a renamed or moved file updates any `AGENTS.md` that names it, in the same commit.  The branch is brought up to date with `main` before each push, and Auto-fix is turned on for the PR (`CLAUDE.local.md`).  The repository has no `v*` tag, so the version check of root `AGENTS.md` has nothing to compare.
5. **No new debt goes unrecorded.**  A part of an item a PR finds it cannot fix as designed is asked about, not dropped; if the user agrees to leave it, it goes in `docs/tech-debt` in that PR.
6. **User documentation does not change** unless a check finds a page made wrong.  The exceptions in rule 1 concern names and SQL that no manual page describes.

## Order

PR 1 and PR 2 share one file, `Walk/PlanPartitions.cs`, in different methods (`WithoutAlias` and its scanner in PR 1; `All`, `Appends` and `MembersOf` in PR 2), and the item file.  They run at the same time, each in its own worktree, and the second to merge brings itself up to date with `main` first.

PR 3 restructures `PostgresSession.cs` and `PostgresEvents.cs`, which PR 2 also changes, and touches `PostgresTypeNames.cs` and `PlanExpression.cs`, which PR 1 changes.  It starts from `main` after both have merged.

## Decided in the conversation

- The goal is removing the debt; done is the item deleted with every finding fixed.
- Behaviour may change only as rule 1 lists; the user approved PostgreSQL's word rule for the user's SQL.
- The work is several pull requests, each built by its own session from its design.

## Calls made in writing the designs (not asked of the user)

- The split into three PRs and their order.
- Item 3 takes the item's second option, one `Lex` that throws; the SQL is still lexed once per step.  The cost is a few lexes of a short text, which the item itself calls the whole cost, so lexing more than once is not debt.
- A declared type's text keeps its own, stricter rule, since it guards `PREPARE` and a shared rule would let `1$$` through.
- The `Resolve` methods of item 9 stay, with a remark saying why the tests need them.
- Item 6's `PairsOf` shares a walk with `Build` instead of being derived from it, since deriving it would change which describes fail.
