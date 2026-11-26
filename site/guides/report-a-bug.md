# Report a wrong model

Make a bug report of one query that gets a wrong type, a wrong nullability or a wrong model, with your own names renamed.

**Use this when** a query gets a model, a type or a nullability that is wrong for what your database returns.  Use it too when the tool refuses a query that your database runs.

<a id="report-a-bug"></a>

## Reporting a wrong model

Run `diagnose` where you run `describe`, with the same connection.  Name the query with `--query`, and the zip to write with `--out`:

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
sqlsource: bundle '/work/report.zip' written
sqlsource: mapping '/work/report.zip.mapping.json' written
sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build may copy a .json file into its output
```

1. Run the command above.
2. To see what the maintainer sees, run [`replay`](../reference/bundles.md#replay) on the zip.  It says `reproduced`, and its lines name the new names.
3. Attach the zip to a [bug report on GitHub](https://github.com/mbcrawfo/SqlSource/issues/new?template=bug-report.yml).
4. Keep the mapping to yourself.  With it, you can read what a maintainer finds in your own names.

**What you get.**  `diagnose` describes that one query again, and writes a bundle of it: a zip file.  A maintainer runs SqlSource on the bundle with no database and no schema of yours.  Before `diagnose` writes the bundle, it renames your names and strings in it.  It also checks that the renamed bundle still gives the answer of your query.  So you can attach the bundle to a bug report.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents) for what is renamed and kept.

If a bundle cannot be renamed safely, `diagnose` writes nothing.  Then see [When a bundle cannot be made safe](#raw-bundle).

**The rules you need.**

- **Run it where you run `describe`**, with the same project or solution, `--project`, connections, `.env` file or `--env-file`, `--configuration`, `--verbose` and `--log`.  See [Making a bundle with `diagnose`](../reference/bundles.md#diagnose).
- **It describes the one query again against your database, whether or not its sidecar is current.**  It writes no sidecar.
- **`--query` names the `.sql` file, a colon and the name of the query.**  Write the name as the `-- name:` line of the query gives it, letter for letter ([SQLSRC292](../diagnostics.md#sqlsrc292)).
- **`--out` is required**: the path of the zip.  The mapping is written beside it, with `.mapping.json` added to its name.  A file at either path is overwritten.
- **Give `--out` a path outside the folder of your project**, such as `../report.zip`.  A build can copy a `.json` file of the folder of the project into its output, and the mapping is such a file.
- **`diagnose` refuses an `--out` path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`**, before it describes anything ([SQLSRC218](../diagnostics.md#sqlsrc218)).
- **If the tool refuses the query, `diagnose` makes a bundle too**, which holds the error.  The run prints `sqlsource: the query was refused: SQLSRC226`, with the id of the error, before the line of the bundle.  The exit code is `0`.  The error itself is not printed again, since `describe` showed it to you.
- **If the database cannot be opened, `diagnose` makes no bundle.**  It prints the error as `describe` prints it, and the exit code is `1`.
- **An error in another `.sql` file of the project is not printed.**  It does not stop the bundle or change the exit code, since it cannot change the query.  `describe` shows it, and the log holds it.
- **Any other error is printed, and makes no bundle**, since it can change the query.  The exit code is `1`.  Fix the error, and run `diagnose` again.  These errors include an error in:
  - the file of the query, or another query of that file;
  - a settings file;
  - a project;
  - the `.env` file.

<a id="what-is-safe"></a>

## What is safe to post

If you are not sure that you can post the zip, this list says what a bundle holds, renames and keeps.

**What you get.**  A bundle holds the one query and what your database answered when it was described.  It holds what SqlSource made of that answer, or the error that refused the query.  It also holds the versions of `sqlsource`, of the server and its driver, of .NET and of the operating system.  It holds none of your other queries, your settings files, your code, your connection string or a log.

**The rules you need.**

- **Every name of yours gets a new name**, such as `t_4f0a9c21` for a table.  This includes schemas, tables, views, columns, aliases, functions, types, indexes, the database's name, the logical database, the query's name, its query parameters and its tokens.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents).
- **Every string literal gets a new one**: `'active'` becomes `'l_9b3e07d4'`.  A comment is left empty.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents).
- **A server's error keeps its code**, and the query parameters it names, renamed.  Its message is left out.  A SQL Server error also keeps its class, state and type.  A PostgreSQL error from the server's first read of the query also keeps its position in the SQL.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents).
- **Numbers are kept.**  A name of yours that is spelled as a word of the database is kept too, such as a column `name` or `date`.  So are the database's own names, such as `public` and `dbo`.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents).
- **If a number in your query is a value that you must not share**, such as an account number, do not post the bundle.  Take the private route instead.  See [When a bundle cannot be made safe](#raw-bundle).
- **`diagnose` checks the bundle before it writes it.**  It writes no bundle that still holds a word of yours other than the names and numbers this list keeps.  See [What is renamed before you share it](../reference/bundles.md#bundle-contents).
- **Keep the mapping to yourself.**  It is written beside the zip, and lists each new name beside your own name.  See [Making a bundle with `diagnose`](../reference/bundles.md#diagnose).

<a id="raw-bundle"></a>

## When a bundle cannot be made safe

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
/work/report.zip : error SQLSRC293: The bundle cannot be made safe to post: a word is not one of the new names, at query.sql, found ssn_hash
    help: make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it
    see: https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc293
```

`diagnose` writes nothing rather than a bundle that it cannot make safe.  That is one of these:

- a bundle that would still hold a text of yours ([SQLSRC293](../diagnostics.md#sqlsrc293));
- a bundle whose renamed copy no longer gives the answer of your query ([SQLSRC294](../diagnostics.md#sqlsrc294)).

The error names the text that it found, as `ssn_hash` in the sample.  That text is yours, such as a name of your schema, so do not paste the error into an issue.

Take the private route instead:

1. Run the same command with `--no-obfuscate`.
2. Keep the raw bundle.
3. Open an issue on GitHub.  Say in it that a raw bundle is ready.
4. Do not attach the bundle.  A maintainer tells you how to send it.

The command of step 1, and what it prints:

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip --no-obfuscate
sqlsource: raw bundle '/work/report.zip' written
sqlsource: this bundle holds your own names and SQL, and must not be posted: open an issue that says a raw bundle is ready, without attaching it, and a maintainer will say how to send it
```

**What you get.**  `--no-obfuscate` writes a raw bundle.  It holds your names and SQL as they are, and what the server said in full.  It writes no mapping.

**The rules you need.**

- **`--scrub` renames a raw bundle later**, with no database, no project and no connection.  It uses the same steps and checks as `diagnose` without `--no-obfuscate`.  It writes the renamed bundle and its mapping at `--out`.
- **`--scrub` takes no option but `--out`.**
- **`--scrub` refuses a bundle that is renamed already** ([SQLSRC295](../diagnostics.md#sqlsrc295)).
- **`--scrub` refuses a raw bundle that another version made, when this version does not answer it as it was made.**  Scrub it with the version that made it ([SQLSRC290](../diagnostics.md#sqlsrc290), [SQLSRC296](../diagnostics.md#sqlsrc296)).

Here `--scrub` renames the raw bundle of step 1:

```console
$ dotnet sqlsource diagnose --scrub ../report.zip --out ../report-renamed.zip
sqlsource: bundle '/work/report-renamed.zip' written
sqlsource: mapping '/work/report-renamed.zip.mapping.json' written
sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build may copy a .json file into its output
```

## If it does not work

- **SQLSRC293 or SQLSRC294, and no bundle is written.**  `diagnose` cannot make the bundle safe to post.  Make a raw bundle, and send it by the private route.  See [When a bundle cannot be made safe](#raw-bundle).
- **SQLSRC292 for the query.**  The name after the colon of `--query` must be the name on the `-- name:` line, letter for letter.  See [SQLSRC292](../diagnostics.md#sqlsrc292).
- **SQLSRC218 for the path of `--out`.**  `diagnose` refuses a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`.  Give the zip a name of its own, outside the folder of your project.  See [SQLSRC218](../diagnostics.md#sqlsrc218).
- **An error is printed, and no bundle is written.**  The database could not be opened, or an error can change the query.  Fix the error, and run `diagnose` again.  See [Reporting a wrong model](#report-a-bug).
- **SQLSRC295, SQLSRC290 or SQLSRC296 from `--scrub`.**  `--scrub` refuses a bundle that is renamed already, and a raw bundle that this version does not answer as it was made.  Scrub such a raw bundle with the version that made it.  See [When a bundle cannot be made safe](#raw-bundle).
