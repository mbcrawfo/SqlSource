# What describe does (watch, check, force)

<a id="describe"></a>

## What describe does

`describe` connects to your database, asks it for the parameters and columns of each query it describes, and writes the answers to the [sidecar files](sidecars.md#sidecar-files).

<a id="which-queries"></a>

## Which queries are described

```sql postgres
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: ResetAuditLog
-- output: sql
TRUNCATE TABLE audit_log;
```

`describe` finds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it runs on, and describes each query whose [output](../settings.md#setting-output) is `models`, `methods` or `repository`.  `repository` is the default, so `GetUser` above is described.  `ResetAuditLog` asks for its SQL alone, and the tool leaves it out: it needs no database and no connection.

A query is described through its [dialect](../concepts.md#dialect).  The tool can ask PostgreSQL, which is the dialect `postgres`, and SQL Server, which is `mssql`; the [database support table](databases/index.md#database-support) lists every dialect.  A query that must be described and whose dialect cannot be, every row marked No in [database support](databases/index.md#database-support), such as `ansi`, `mysql` or `sqlite`, is [SQLSRC209](../diagnostics.md#sqlsrc209), from the tool and in the build.  The default dialect is `ansi`, so a project that sets neither the dialect nor the output gets it for every file.  Set the output of those queries to `sql`, as [the output setting](../settings.md#setting-output) says, or set the dialect, when the files are PostgreSQL or SQL Server.

The tool reads `[SqlSourceGenerate]` from your C# source without compiling it, which limits how `Source` can be written there: see [the generator's known issues](../generator.md#known-issues).

The tool reads the same [settings files](../settings.md#settings-files) as the build, and reports their mistakes at the file, line and column that the build gives them: a file that is not a JSON object, a key that is not known or is written twice, or a value of the wrong JSON type, [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC018](../diagnostics.md#sqlsrc018), and a value of `dialect`, `output` or `database` that is not valid, [SQLSRC019](../diagnostics.md#sqlsrc019).  It reports them only for a settings file that applies to a `.sql` file or a C# file of a project it runs on, and once in a run, however many of its projects reach the file.  The other settings' values, `typeOverrides` and `columnTypes` among them, are checked by the build and not by the tool, and `database` by the tool alone.

<a id="after-a-schema-change"></a>

## After a schema change

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
```

`--force` describes every query of the run again, current or not.  Run it after a migration, or any other change to the database that a query reads: a column that became nullable, a type that changed, a table that was renamed.  Then commit the sidecars it changed.  A query that the change broke, such as one that reads a column you dropped, is reported with what the server said ([SQLSRC225](../diagnostics.md#sqlsrc225)).

To describe again only the queries of one database, add `--database billing`.

<a id="describe-on-save"></a>

## Describe on save

```console
$ dotnet sqlsource describe --watch
billing (postgres): 0 described, 16 skipped, 0 failed
sqlsource: watching for changes; press Ctrl+C to stop
sqlsource: change detected; describing
billing (postgres): 1 described, 15 skipped, 0 failed
sqlsource: watching for changes; press Ctrl+C to stop
```

`--watch` keeps the tool running while you work.  It describes once, as `describe` does, then waits, and describes again each time you save a file that can change what it describes, until you stop it.  Each pass is a whole `describe`: it describes only the queries whose sidecar entry is no longer current, writes their sidecars and prints its summary, so the sidecar of a query you just saved is current a moment later.  A pass that reports errors does not stop the watch: fix what it reported and save, and the next pass tries again.

**What starts a pass:**

- **A file that the tool reads is saved**: a `.sql` file of your projects, a [settings file](../settings.md#settings-files) that applies to one, or a C# file of a project.
- **What a project includes may have changed**: a new `.sql`, C# or `sqlsource.json` file, or a new folder, in a project's folder or in a folder that a project lists a file from; a new `sqlsource.json` in a folder above a file the tool reads, up to the root of your repository; a `.sql` file renamed, or deleted and still gone when the pass starts; a project file or a solution file saved; a `Directory.Build.props`, `Directory.Build.targets` or `Directory.Packages.props` saved in the folder of a project or of the solution, below it, or above it up to the root of your repository; and a `dotnet restore`, into the project's `obj` folder or into the `artifacts` folder of `UseArtifactsOutput`.  The pass then reads your projects again before it describes, so a new file, or a new project of the solution, is described.
- **The [`.env` file](connections.md#env-file) that the run reads** is saved, deleted or created: the one in the folder of the project or solution you watch, or the one `--env-file` names, wherever it is.  A `.env` in any other folder starts no pass.

Everything else is passed over: the sidecars and the temporary files the tool writes itself, everything under `bin`, `obj` and `.git` but what a restore writes and the `.env` file the run reads, and every other file.  So the tool's own writes never start a pass.

**When it describes.**  A pass starts 300 ms after the last change it covers, so a save that an editor makes in several writes, or a branch switch that changes hundreds of files, gives one pass.  A change saved while a pass runs gives one more pass once it ends.

**The two lines it writes.**  After every pass the tool writes this line, alone on its line, to standard output:

```text
sqlsource: watching for changes; press Ctrl+C to stop
```

and before every pass after the first, this one:

```text
sqlsource: change detected; describing
```

A task of your editor can wait for them to know when a pass begins and ends.

**The options.**

- **`--check` cannot be used with `--watch`.**  The tool writes `sqlsource: options '--watch' and '--check' cannot be used together` and exits with `1`.
- **`--force` forces the first pass only.**  It describes every query once; each later pass describes what the changes made stale.
- **`--project`, `--database` and `.sql` paths narrow every pass**, as they narrow a run, and **`--connection` and `--verbose` apply to every pass.**
- **`--log` writes one file for the whole watch**, from its start until it is stopped: see [logs](cli.md#logs).

**Stopping it.**  Stop the watch with Ctrl+C, or end it with `SIGTERM` or `SIGHUP`, as closing its terminal does.  A pass under way stops as a stopped `describe` does, and writes no further sidecar.  The tool exits with `0`, whatever its passes reported, since stopping is how a watch ends; each error was written in the pass that found it.  A wrong command line, a project or solution that cannot be found, and a log that cannot be opened end it with `1` before the first pass.  See [exit codes](cli.md#exit-codes).

**One watch is enough.**  Run one for a repository.  A second one, or a watch while a build runs, does no harm: each sidecar is written whole, and two passes that describe the same query write the same sidecar.  It only does the work twice.

**Where the system does not report changes.**  The tool asks the system to tell it when a file changes.  A folder mounted into a container or on a network share may not tell it.  Set the environment variable `DOTNET_USE_POLLING_FILE_WATCHER` to `true` or `1`, in any case, before you start the watch, and the tool looks at the folders every second instead, so a change is found within a second or two.  The .NET SDK's container image, `mcr.microsoft.com/dotnet/sdk`, sets it to `true`, so a watch in a container of that image looks every second; set it to `false` there to use the system's events.  On Linux, a machine with many projects, or other programs that watch folders, can reach one of the system's limits on watching folders: that is [SQLSRC287](../diagnostics.md#sqlsrc287), whose section says how to raise each.

**Editors.**  The saves of an editor were checked with Rider on macOS alone.  Visual Studio, VS Code and other editors, and Windows, have not been checked: see [known issues](#limit-saves-not-seen).

<a id="what-your-editor-shows"></a>

### What your editor shows

In Rider, with the watch running:

```text
12:00:00  You add a column to GetUser in Users.sql, and save it
12:00:01  The watch writes Users.sql.json; Rider still shows GetUser's old models, and no error
12:00:30  You build; Rider shows GetUser's new models
```

Your editor shows the models and methods of the sidecars it has read.  When the watch writes a sidecar, the editor shows what it holds when it reads it again, and that depends on the editor.  To have your editor start the watch, see [the recipe](../guides/describe-on-save.md#start-the-watcher-with-your-editor).

- **Rider** shows what a sidecar holds within seconds of the watch rewriting or creating it, with nothing else done, for the queries you did not change in Rider.  A query that you change and save in Rider is the exception: Rider shows neither the error for its out-of-date entry nor its new models and methods until you build, though the watch has already written its sidecar.  Build after such a change to see them.
- **Visual Studio and VS Code**, by their documentation, read a sidecar that the watch writes at your next save of any file, or your next build, in their default mode, and as soon as it is written in their "Automatic" mode.  The setting is under Tools > Options in Visual Studio, and in the settings of VS Code: search either for "source generator".  This has not been checked.

<a id="check-in-ci"></a>

## Check in CI

```console
$ dotnet tool restore
$ dotnet restore
$ SQLSOURCE_CONNECTION_BILLING="$BILLING_CONNECTION" dotnet sqlsource describe --check
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': the query has changed since it was described
    help: run 'dotnet sqlsource describe' and commit the sidecars
    see: https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc219
billing (postgres): 15 same, 1 differ, 0 failed
```

`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars in the repository, and changes no file.  A sidecar that is missing or out of date is [SQLSRC219](../diagnostics.md#sqlsrc219), which names the query and what differs, and its `help:` line names the command that fixes it.  The build already reports an entry that is missing or older than its query; `--check` also finds one that a change to the schema made wrong, which the build cannot see.  A build in CI never [runs the tool](msbuild.md#running-in-the-build) itself, so this step is how a job describes.  For a whole GitHub Actions job, see [the recipe](../guides/ci.md#check-sidecars-in-ci).

- **It needs a database with your schema**, such as one the job creates and migrates, and a connection for every database of the run.
- **It compares what changes generated code**: the queries, their parameters and columns, their types, whether they can be null, and the table column each column is read from, which a [column type](../settings.md#column-types) is matched against.  It does not compare the versions of the tool or of the server, so a sidecar that a developer wrote passes against a server of another patch release.
- **Its exit code tells the two failures apart.**  `2` means a sidecar is out of date and nothing else went wrong: someone forgot to describe.  `1` means the check could not be made, such as a database that could not be opened.  See [exit codes](cli.md#exit-codes).
- **With `--database`, `--project` or a `.sql` path**, it reports only what `describe` with the same filters would change: the queries that it would describe, and the leftover entries, of whatever database, in a sidecar that it would rewrite.

<a id="limits"></a>

## Limits

<a id="limit-several-target-frameworks"></a>

### SQLSRC241 for a query that only another target framework includes

- **A project with several target frameworks is read for its first framework alone**, by `describe` and by a build that runs the tool.  A `.sql` file or a C# file that the project includes only for another framework, and a `[SqlSourceGenerate]` under an `#if` that only another framework defines, are not seen, and their queries are not described: the build for that framework reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each that must be.  A project that references SqlSource for a later framework only is left out of a solution's run without a word, and is [SQLSRC204](../diagnostics.md#sqlsrc204) when you name it; its build reports SQLSRC241 in the same way.  Include the `.sql` files and the attributed types for every framework, or for the first one listed.

<a id="limit-saves-not-seen"></a>

### Saving a file starts no pass of `describe --watch`

- **A save in an editor other than Rider on macOS may not start a pass of `describe --watch`.**  The saves of Visual Studio, VS Code and other editors, and saves on Windows, have not been checked.  If saving a file does not start a pass, stop the watch, set `DOTNET_USE_POLLING_FILE_WATCHER` to `true` and start it again: it then finds a save within a second or two.

<a id="limit-editors-not-checked"></a>

### In Visual Studio or VS Code, the models do not change after the watch writes a sidecar

- **In Visual Studio and VS Code, the recipes that start `describe --watch` and what the editor shows after a pass have not been checked.**  If the models and methods do not change after the watch writes a sidecar, save any file or build.

<a id="limit-folder-changes-not-seen"></a>

### After a folder is deleted and made again, saves in it start no pass

- **A few changes to folders far from your project start no pass of `describe --watch`.**  A folder deleted and made again where the watch was not looking one level above it, such as your repository's own folder on Linux, or, outside a git repository, a folder deleted, or made, together with the folder above it, is not seen, and later saves in it start no pass.  A first `dotnet restore` into an `artifacts` folder that does not exist yet and lies above your repository's folder, or outside a repository above the folder of your nearest `Directory.Build.props`, or above your project's folder when there is none, starts no pass either.  Outside a git repository the watch also looks at the folder one level above each folder it covers, such as the folder above your project's folder, so a project or solution file saved there starts a pass that changes nothing.  Stop the watch and start it again after such a change, or work inside a git repository.
