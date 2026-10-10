# TD-0023 - An unknown option and a path are printed as they were given

## Problem

The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  Four things are still printed as the user typed them.

- **The name of an unknown option**, which is the token cut at its first `=` or `:`.  A token that starts with `-` and holds a secret with neither character is printed whole: `-pS3cret`, the way `mysql` takes a password, gives `sqlsource: unknown option '-pS3cret'`.
- **The path of `describe`**, in `SQLSRC201` to `SQLSRC203` and `SQLSRC222`, from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs).  A value typed where the path stands is printed as a path: `sqlsource describe "Host=db;Password=S3cret"` gives `SQLSRC203` with the whole string in it.
- **A path given with `--project`**, in `SQLSRC204`, `SQLSRC207` and `SQLSRC220`, from [`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs), as a full path.  A value typed after `--project` by mistake is printed whole in `SQLSRC207`.
- **A `.sql` path of `describe`**, in `SQLSRC212`, from [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs), as a full path.  Any token of `describe` that ends in `.sql` is taken for a file of the run, so a value that ends that way and is typed where a path can stand is printed whole.

## Why it exists

A message that names nothing is of no use: a user who misspells `--force` must be told which word was not known, and a path that was not found must be shown.  Cutting the option at `=` or `:` covers the forms a value is given in beside an option of this tool, and reading nothing after the first unknown option covers the value that follows one.

## Impact

Low.  Both need a command line that the tool does not offer: it has no short option that takes a value, and a connection is always given with `--connection`.  What is printed goes to the terminal of the person who typed it, and to the log of a build that runs the tool.

## Proposed fix

Print an unknown option only when it is close to one the tool has, and by its position otherwise.  For the path, print it only when it has no `=` and no `;` in it, and its position otherwise.

## Trigger

The tool gains a short option that takes a value, or a report of a secret in the log of a build.
