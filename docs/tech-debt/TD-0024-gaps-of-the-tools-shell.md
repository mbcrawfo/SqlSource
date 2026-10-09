# TD-0024 - Small gaps of the `sqlsource` tool's shell

## Problem

The review of sub-phase 2.2 of query generation found these in [`src/SqlSource.Tool`](../../src/SqlSource.Tool/AGENTS.md) and left them.

1. **[`Reporter`](../../src/SqlSource.Tool/Reporting/Reporter.cs) is not safe for several threads.**  It writes an error with several calls and counts with `Count++`.  Two errors reported at once would mix their lines.
2. **A line break is kept out of an argument and a continuation line's text, and nowhere else.**  The name of an unknown option in a line of [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs), a `ToolDiagnostic.Path` and a `ContinuationLine.Label` are written as they are.  A path or an option with a line break in it gives an error over two lines, the second of which starts in the first column.
3. **A directory that cannot be read is `SQLSRC200`**, "sqlsource failed unexpectedly", from [`RunUnitFinder`](../../src/SqlSource.Tool/Projects/RunUnitFinder.cs), and [`docs/diagnostics.md`](../diagnostics.md) uses it as that error's example.  It is an expected condition of a path the user named, and reads as a bug of the tool.
4. **`SQLSRC200` prints the message of any exception.**  A driver's exception, or one of a parser of connection strings, can quote a part of a connection string.
5. **Three settings of [`Cli`](../../src/SqlSource.Tool/Cli.cs) have no test that fails without them:** `EnablePosixBundling = false`; `Arity = ArgumentArity.Zero` on `--version`; and that the line for a command line System.CommandLine rejects is the tool's own and not System.CommandLine's message.  Nothing checks that every option of the real commands is declared as `src/SqlSource.Tool/AGENTS.md` says.
6. **Ctrl+C is always swallowed**, and the run is cancelled through its token.  A run that does not look at its token cannot be stopped from the keyboard.
7. **The tool's package carries the `.pdb` and `.xml` files** of `SqlSource.Tool` and of `SqlSource`, about 0.3 MB, because the repository builds documentation files for every project.

## Why it exists

None of them can be reached, or matters, in the tool as sub-phase 2.2 leaves it: it runs on one thread, takes no connection, ends at once, and the person at the keyboard is the only source of its command line.  Each is cheaper to settle in the sub-phase that makes it matter.

## Impact

Low today.  The first becomes a defect in sub-phase 2.3, which runs several MSBuild processes at once, and the fourth and the sixth in sub-phase 2.5, which opens connections and can wait on a database.

## Proposed fix

1. Build the whole text of an error and write it once, under a lock, with the count.
2. Pass the path, the label and the name of an unknown option through the same replacement as an argument.
3. Catch `UnauthorizedAccessException` and `IOException` in `RunUnitFinder` and report an error of its own with the path, or `SQLSRC203` with a `help:` line.
4. Catch the exceptions of a driver where a connection is opened, and report them with an id of their own and without the connection's text.
5. Add `describe -hx`, `--version=x`, and a test that walks the options of the real commands; test the tool's own line when sub-phase 2.5 gives the tool an option that System.CommandLine can reject.
6. Let a second Ctrl+C end the process.
7. Leave the `.xml` files out of the tool's package, and decide whether the `.pdb` files belong in it.

## Trigger

Sub-phase 2.3 for the first, and sub-phase 2.5 for the fourth and the sixth.  The rest when the code around them is next changed.
