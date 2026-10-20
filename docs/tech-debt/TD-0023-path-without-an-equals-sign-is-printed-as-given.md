# TD-0023 - A path with no = and no ; is printed and logged as it was given

## Problem

The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  An unknown option is named only when it is close to an option of the tool, a `--database` or `--connection` value only when it is close to a database of the plan, and a path that holds `=` or `;` by its position.  The log's `arguments` writes every `--database` and `--connection` value as `***`, and so a path or a `--project` value that holds `=` or `;`.  What is left is a token with neither `=` nor `;` that stands where a path stands: the unit of `describe`, a `.sql` path of it, or the value of `--project`.

- It is printed as a path, in `SQLSRC201` to `SQLSRC203`, `SQLSRC207`, `SQLSRC212` and `SQLSRC222`, from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs), [`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs) and [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs).  A password typed there by mistake, such as `sqlsource describe S3cret`, gives `SQLSRC203` with `S3cret` in it.
- It is written to the log's `arguments` as it was typed, by [`LoggedArguments`](../../src/SqlSource.Tool/Logging/LoggedArguments.cs), on every run with a log.  [The tool's manual page](../manual/tool.md#logs) tells a user that the log holds the rest of the command line, and invites them to attach it to an issue.

Two near misses are accepted in the same way: a token that starts with `-` and is within edit distance 2 of an option of the tool, such as `-x`, is named as an unknown option, and a `--database` or `--connection` name within distance 2 of a database of the plan is printed.  Neither is a secret in practice.

## Why it exists

A path must be shown to be of use: a user who gives a file that was not found must be told which one, and a path can hold anything, so no rule on its characters tells a path from a secret.  The tokens that nearly every connection string holds, `=` and `;`, are held back; a secret with neither is a password or a token typed on its own, which a user rarely types where a path goes.

## Impact

Low.  A secret that holds neither `=` nor `;` must be typed alone where the unit, a `.sql` path or a `--project` value goes, and it reaches the terminal of the person who typed it, the log of a build that runs the tool, and the `arguments` of a log file that a user is told to read before attaching it to an issue.

## Proposed fix

None planned.  A path cannot be told from a secret by what it holds.  If the trigger is met, the tool could print a path only when it exists or when it is relative to the working directory, and its position otherwise, and write the same rule to `arguments`.

## Trigger

A report of a secret in the output of a failed run, in the log of a build, or in a log that was attached to an issue.
