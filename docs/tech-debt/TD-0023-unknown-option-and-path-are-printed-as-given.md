# TD-0023 - An unknown option and a path are printed as they were given

## Problem

The `sqlsource` tool never repeats a token of a wrong command line, because a token can be a secret: [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) names an unexpected argument by its position.  Six things are still printed as the user typed them, or in part.

- **The name of an unknown option**, which is the token cut at its first `=` or `:`.  A token that starts with `-` and holds a secret with neither character is printed whole: `-pS3cret`, the way `mysql` takes a password, gives `sqlsource: unknown option '-pS3cret'`.
- **The path of `describe`**, in `SQLSRC201` to `SQLSRC203` and `SQLSRC222`, from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs).  A value typed where the path stands is printed as a path: `sqlsource describe "Host=db;Password=S3cret"` gives `SQLSRC203` with the whole string in it.  [`DescribeCommand.CheckUsage`](../../src/SqlSource.Tool/DescribeCommand.cs) closes the case that a shell makes likely: on a line with a `--connection`, an argument that holds `=` or `;`, a `.sql` path too, such as the rest of a value split at a space, is reported by its position.  Two cases are left: a value typed where the path stands on a line with no `--connection`, and a part of a split value that holds neither `=` nor `;`.
- **A path given with `--project`**, in `SQLSRC204`, `SQLSRC207` and `SQLSRC220`, from [`RunProjects`](../../src/SqlSource.Tool/Projects/RunProjects.cs), as a full path.  A value typed after `--project` by mistake is printed whole in `SQLSRC207`.
- **A `.sql` path of `describe`**, in `SQLSRC212`, from [`RunPlanner`](../../src/SqlSource.Tool/Planning/RunPlanner.cs), as a full path.  Any token of `describe` that ends in `.sql` is taken for a file of the run, so a value that ends that way and is typed where a path can stand is printed whole.  `CheckUsage` refuses it by its position when the line has a `--connection` and the token holds `=` or `;`; what is left is a token with neither, and any token on a line with no `--connection`.
- **The name of a `--database` that no query has**, on standard output, in the summary line `<name>: 0 described, 0 skipped, 0 failed` from [`RunSummary`](../../src/SqlSource.Tool/Describing/RunSummary.cs).  `CheckUsage` holds a value of `--database` to the rule of a database name, which is letters, digits, `-`, `_` and `.`, and reports one that breaks it by its position, but a value that passes is printed as typed.  A secret that holds only such characters, as a token or a password often does, and is typed after `--database` by mistake, is in the output.  The line is there so that a filter that matched nothing is seen.
- **The name of a `--connection` that no database has**, in the `help:` line of `SQLSRC213`.  A connection string typed without its name is read as a name and a value: `--connection "Host=db;Password=S3cret"` gives the database `Host`, and `--connection c2VjcmV0cGFzcw==` gives the name `c2VjcmV0cGFzcw`.  The help lists the names that no database of the run has, so the start of a secret is printed.  This is chosen and a test holds it, `Run_ConnectionStringGivenWithoutAName_IsNeverPrinted`: the name is what tells a user that the option was misread, and a line that said only that some connection was unused would not.

Since sub-phase 2.6 the same tokens are also written to a file.  A run with a log, by `--log` or `SQLSOURCE_LOG`, begins it with `run.start`, whose `arguments` holds every token of the command line, built by [`LoggedArguments`](../../src/SqlSource.Tool/Logging/LoggedArguments.cs).  It replaces the value of each `--connection` with `***` and keeps its name, and writes every other token as it was typed: the path of `describe`, a `.sql` path, a path of `--project`, a name of `--database`, a part of a split value, and the name that a `--connection` was read to have.  The name of an unknown option is the one thing not logged, since a wrong command line is never logged.  [The tool's readme](../../src/SqlSource.Tool/README.md) tells a user that the log holds the rest of the command line, and invites them to attach it to an issue.

## Why it exists

A message that names nothing is of no use: a user who misspells `--force` must be told which word was not known, and a path that was not found must be shown.  Cutting the option at `=` or `:` covers the forms a value is given in beside an option of this tool, and reading nothing after the first unknown option covers the value that follows one.  The log records the command line, and only the value of a `--connection` is known to be a secret, so it is the one thing removed.

## Impact

Low.  The unknown option needs a command line that the tool does not offer: it has no short option that takes a value.  A connection string is given with `--connection`, and the part of it that a shell leaves where the path stands is no longer printed when it holds `=` or `;`, as nearly every connection string does, whether or not it ends in `.sql`.  What is left is:

- on a line with no `--connection`, a value typed where a path stands, whatever it holds;
- on a line with a `--connection`, a split part with neither `=` nor `;`, which is printed as a path;
- on any line, a part that starts with `-`, which is printed as an unknown option up to its first `=` or `:`, and a value typed after `--project`, which `SQLSRC207` prints whole;
- on any line, a value typed after `--database` that passes the rule of a database name, which the summary prints whole on standard output;
- the start of a `--connection` value that is read as a name.

What is printed goes to the terminal of the person who typed it, and to the log of a build that runs the tool.  What is written to the log's `arguments` goes to a file that a user is told to read before attaching it to an issue; all of this but the name of an unknown option is in it on every run with a log, whether or not the run fails.

## Proposed fix

Print an unknown option only when it is close to one the tool has, and by its position otherwise.  For the path, print it only when it has no `=` and no `;` in it, and its position otherwise, whether or not the line has a `--connection`.  For the name of a `--connection` and of a `--database`, print it only when it is close to the name of a database of the plan.  Give `LoggedArguments` the same rule, so that a token the rule would print only by its position is written to `arguments` by its position too.

## Trigger

The tool gains a short option that takes a value, or a report of a secret in the log of a build or in a log that was attached to an issue.
