# TD-0038 - Duplicated and uneven patterns in the generator and the tool

## Problem

A maintainability review of `src/` on 2026-10-10 found these.  None changes what a user sees; each makes a change cost more than it should.

Items 1 to 8, 10 and 11 of the review are resolved, and so is the generator's part of 9; the number of what remains is kept, written out so that a renderer does not count it again, since the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) names it.

- **9. Methods that do several things.**  `Cli.RunAsync` (about 130 lines in a nested `try`), `RunPlanner.ReadProject` and `RunPlanner.PlanFile` (about 100 lines each), `SidecarReader.ReadEntry`, `ProjectEvaluator.EvaluateOneAsync`.  `DatabaseRuns.DescribeAllAsync` takes eight parameters, several of which always travel together.

## Why it exists

The code was written in sub-phases, each adding the next reader, record or helper beside the last, and reviewed for correctness in each pull request rather than for the shape of the whole.

## Impact

Low.  Nothing here is wrong today.  Each method makes a change to one of its responsibilities read, and risk, the others.

## Proposed fix

As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull request 4, held by the existing tests: split each by the responsibilities named, `SidecarReader.ReadEntry` within its allocation budget, with one record for what belongs to a session in `DatabaseRuns.DescribeAllAsync`.

## Trigger

Before the next phase that changes one of the methods of 9.
