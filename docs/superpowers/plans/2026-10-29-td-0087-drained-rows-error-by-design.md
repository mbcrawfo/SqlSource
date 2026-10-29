# TD-0087 closed as by design: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0087 as by design: a database error in the rows that an early-exited stream discards is thrown at the `break` and in place of the loop body's exception, and the manual documents that as behaviour with its remedy instead of as a known issue.

**Architecture:** Documentation only.  The owner's early-exit rule for td-0085, td-0088 and td-0087 is "Option 1": always throw, even when the database's error replaces the loop body's exception.  Asked how TD-0087 ends under it, the owner chose "C: by design".  Neither of the item's two proposed fixes is built: way 1 (drop the error) reverses the rule, and way 2 (a delegate helper with an `AggregateException`) is not asked for.  So the generated iterator, its tests and its allocations are unchanged.

**Tech Stack:** Markdown docs; the documentation tests of `tests/SqlSource.Tests/Documentation/`.

**Spec:** `docs/tech-debt/TD-0087-drained-rows-error-replaces-the-loop-bodys-exception.md`, as decided by the owner's answers in the run's `notes.md` (Decisions).

## Where the merged code differs from the spec

- The spec asks for the PostgreSQL and SQL Server tests to be flipped and the `StreamAccounts` allocation pair measured before and after a code change.  With the owner's answer there is no code change: the tests `StreamNumbersFailingHalfway_*` of both engines already pin the ruled behaviour and stay as they are, and the allocation pair is measured once, at `7b9a0dc0`: PostgreSQL 2096 bytes per call, SQL Server 5216 (open) and 5648 (closed), each equal to its hand-written method.
- The spec names `settings.md`, `postgresql.md` and `generator.md`.  `docs/manual/sqlserver.md` says the same thing ("which is then lost") and gets the same rewording.

## Global Constraints

- User documentation describes effects, never internals; it names no tech-debt id.
- Resolving follows `docs/tech-debt/AGENTS.md`: delete the item file, its row, every `TD-0087` reference outside plan and spec history, and its known issue.
- `docs/manual/postgresql.md` is edited beside td-0077: touch only the streaming bullet.

## Review Focus

- A link to `settings.md#known-issues` that only pointed at the removed known issue must move to `settings.md#streaming-rows`, or the reader lands on unrelated issues.
- The remedy (catch the body's exception, `break`, catch the database's exception around the loop and rethrow the kept one; or a `WHERE`/`LIMIT`/`TOP`) must survive the move from the known issue into the streaming section.

---

### Task 1: Close TD-0087 and document the behaviour

**Files:**
- Delete: `docs/tech-debt/TD-0087-drained-rows-error-replaces-the-loop-bodys-exception.md`
- Modify: `docs/tech-debt/README.md` (its row)
- Modify: `docs/manual/settings.md` (the "Leaving the loop early" bullet of streaming rows; remove the "An exception thrown in a loop over streamed rows can be lost" known issue)
- Modify: `docs/manual/postgresql.md` (the streamed-rows bullet of method results)
- Modify: `docs/manual/sqlserver.md` (the streamed-rows bullet of method results)
- Modify: `docs/architecture/generator.md` ("A method that streams": replace the TD-0087 link with the owner's reason)

- [ ] **Step 1:** Delete the item file and its row.
- [ ] **Step 2:** Rewrite the streaming bullet of `settings.md` so the error is thrown "whatever ended" the loop, by design, and give the known issue's remedy there; remove the known issue.
- [ ] **Step 3:** In `postgresql.md` and `sqlserver.md`, point the bullet at `settings.md#streaming-rows` for the remedy, not at known issues.
- [ ] **Step 4:** In `generator.md`, replace `([TD-0087](…))` with the reason: the database's error is never hidden, since it may have aborted or rolled back the transaction, and the iterator cannot see the body's exception.
- [ ] **Step 5:** `grep -rn TD-0087 . --exclude-dir=.git` shows only `docs/superpowers/` history.
- [ ] **Step 6:** Run `./pre-commit-validation.sh`; expected: pass.  Commit `docs: resolve TD-0087 as by design`.
