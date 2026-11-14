# Report a wrong model

<a id="report-a-bug"></a>

## Reporting a wrong model

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
sqlsource: bundle '/work/report.zip' written
sqlsource: mapping '/work/report.zip.mapping.json' written
sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build may copy a .json file into its output
```

When a query gets a model, a type or a nullability that is wrong for what your database returns, or the tool refuses a query that your database runs, `diagnose` makes a bug report of that one query: a zip file, the bundle, which a maintainer runs SqlSource on with no database and no schema of yours.  Your names and strings in it are renamed first, as [what is renamed](../reference/bundles.md#bundle-contents) says, so you can attach it to a [bug report on GitHub](https://github.com/mbcrawfo/SqlSource/issues/new?template=bug-report.yml).

- **Run it where you run `describe`**, with the same project or solution, `--project`, connections, `.env` file or `--env-file`, `--verbose` and `--log`.  It describes the one query again against your database, whether or not its sidecar is current, and writes no sidecar.  `--query` names the `.sql` file, a colon and the query's name as its `-- name:` line gives it, letter for letter ([SQLSRC292](../diagnostics.md#sqlsrc292)).
- **`--out` is required**: the path of the zip.  The mapping is written beside it, with `.mapping.json` added to its name, and a file at either path is overwritten.  Give a path outside your project's folder, such as `../report.zip`, since a build may copy a `.json` file of the project's folder, such as the mapping, into its output.  A path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx` is refused before anything is described ([SQLSRC218](../diagnostics.md#sqlsrc218)).
- **A query that the tool refuses makes a bundle too**, which holds the error.  The run prints `sqlsource: the query was refused: SQLSRC226`, with the error's id, before the bundle's line, and exits with `0`.  The error itself is not printed again, since `describe` showed it to you.
- **A database that cannot be opened makes no bundle.**  Its error is printed as `describe` prints it, and the exit code is `1`.
- **An error in another `.sql` file of the project is not printed**, and does not stop the bundle or change the exit code, since it cannot change the query: `describe` shows it, and the log holds it.  Any other error, such as one in the query's own file, in another query of that file, in a settings file, in a project or in the `.env` file, is printed and makes no bundle, since it may change the query, and the exit code is `1`: fix it and run `diagnose` again.

Attach the zip to the issue, and keep the mapping to yourself: it lists each new name beside the name of yours it stands for, so that you can read what a maintainer finds in your own names.  To see what the maintainer will see, run [`replay`](../reference/bundles.md#replay) on the zip: it says `reproduced`, and its lines name the new names.

<a id="raw-bundle"></a>

## When a bundle cannot be made safe

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
/work/report.zip : error SQLSRC293: The bundle cannot be made safe to post: a word is not one of the new names, at query.sql, found ssn_hash
    help: make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it
    see: https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc293
```

`diagnose` writes nothing rather than a bundle it cannot make safe: one that would still hold a text of yours ([SQLSRC293](../diagnostics.md#sqlsrc293)), or whose renamed copy no longer gives your query's answer ([SQLSRC294](../diagnostics.md#sqlsrc294)).  The line names what it found, which is yours, such as a name of your schema: do not paste it into an issue.

Take the private route instead.  Run the same command with `--no-obfuscate`, which writes a raw bundle, with your names and SQL as they are, what the server said in full, and no mapping:

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip --no-obfuscate
sqlsource: raw bundle '/work/report.zip' written
sqlsource: this bundle holds your own names and SQL, and must not be posted: open an issue that says a raw bundle is ready, without attaching it, and a maintainer will say how to send it
```

Keep the raw bundle, and open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it.

`--scrub` renames a raw bundle later, with no database, no project and no connection, by the same steps and checks as `diagnose` without `--no-obfuscate`, and writes the renamed bundle and its mapping at `--out`.  It takes no other option.  A bundle that is renamed already is refused ([SQLSRC295](../diagnostics.md#sqlsrc295)).  So is a raw bundle that another version made, when this version does not answer it as it was made: scrub it with the version that made it ([SQLSRC290](../diagnostics.md#sqlsrc290), [SQLSRC296](../diagnostics.md#sqlsrc296)).

```console
$ dotnet sqlsource diagnose --scrub ../report.zip --out ../report-renamed.zip
sqlsource: bundle '/work/report-renamed.zip' written
sqlsource: mapping '/work/report-renamed.zip.mapping.json' written
sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build may copy a .json file into its output
```
