# TD-0117: Get started's running example differs from `examples/*/Repository`

## Problem

The pages of [Get started](../../site/start/) teach with one running example, `UserRepository`, whose text [`site/start/AGENTS.md`](../../site/start/AGENTS.md) says is that of [`examples/PostgresExamples/Repository`](../../examples/PostgresExamples/Repository/) and [`examples/SqlServerExamples/Repository`](../../examples/SqlServerExamples/Repository/).  It is not, in two ways:

- In both examples, the query `GetUser` of `UserRepository.sql` has the line `-- generator: method-type=both`.  So the example generates a synchronous `GetUser` and an asynchronous `GetUserAsync`, and its `UserGreeter.cs` calls both.  The pages never show that line, and call `await users.GetUser(...)` (`first-query.md`, `call-queries.md`); the `UserGreeter` of `testing.md` is not the example's.
- `write-queries.md` adds the queries `FindUsers`, `ListUsersByIds` and `ListDisplayNames`, and the fake repository of `testing.md` implements them.  The examples have none of the three.

## Why it exists

The Get started pages were written new in phase 10 of the documentation overhaul, and the outline of that epic kept the example projects as they were: "The example projects do not grow in this epic."  The last read of the site, in the epic's closing phase, found the difference; the owner chose to record it rather than change the examples there.

## Impact

Low.  A reader who opens an example beside the pages sees other method names than the pages give, and three queries that the pages add are missing.  The pages are true of SqlSource; only the comparison with the example fails.

## Proposed fix

In both `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`:

1. Remove `-- generator: method-type=both` and its comment from `GetUser` in `UserRepository.sql`, call `GetUser` in `UserGreeter.cs` as the pages do, and the `Repository/` sample of each example's `README.md`, whose `greeter.Greet(1)` calls the synchronous `GetUser`; show the synchronous method in another example if one is still wanted.
2. Add the queries `FindUsers`, `ListUsersByIds` and `ListDisplayNames` to `UserRepository.sql`, in the words of `site/start/write-queries.md`.
3. Write the sidecars again with `tools/describe-end-to-end.sh`, and check that each Get started page reads true against the files.

## Trigger

The next change to `site/start/` or to the `Repository` examples.
