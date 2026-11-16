# What describe does (watch, check, force)

<a id="describe"></a>

## What describe does

To describe a query is to ask a database for the parameters and the columns of the query, and to record the answer.  The query does not run.  The database needs your schema, not your data.  The `describe` command of the [tool](cli.md#tool) does it.  It connects to your database, asks for the parameters and columns of each query that it describes, and writes the answers into the [sidecar files](sidecars.md#sidecar-files).  See [which queries are described](#which-queries).

<a id="which-queries"></a>

## Which queries are described

```sql postgres
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: ResetAuditLog
-- output: sql
TRUNCATE TABLE audit_log;
```

The [output](settings.md#setting-output) of a query decides whether it is described.  The tool describes each query whose output is `repository`, `methods` or `models`.  A query whose output is `sql` needs no database and no connection.  In the sample, `GetUser` has the default output, `repository`, so the tool describes it.  `ResetAuditLog` asks for its SQL alone, and the tool leaves it out.

- **The tool reads the `.sql` files that a type with `[SqlSourceGenerate]` claims**, in the projects that it runs on.
- **The tool reads `[SqlSourceGenerate]` from your C# source without compiling it.**  This limits how you can write `Source` there: see [a limit](attributes.md#limit-source-written-as-text).
- **The tool describes the queries of the dialects `postgres` and `mssql`.**  The tool describes a query through its [dialect](settings.md#setting-dialect).  It asks PostgreSQL about a query of `postgres`, and SQL Server about a query of `mssql`.  The [database support table](databases/index.md#database-support) lists every dialect.
- **A query that must be described, and whose dialect cannot be described, is [SQLSRC209](../diagnostics.md#sqlsrc209).**  The dialects that cannot be described are those marked No in [database support](databases/index.md#database-support), such as `ansi`, `mysql` or `sqlite`.  The tool reports the error, and so does the build.
- **A project that sets neither the dialect nor the output gets SQLSRC209 for each file that a type claims.**  The default output is `repository`, and the default dialect is `ansi`.

To mend SQLSRC209, do one of these:

- Set the output of those queries to `sql`, as [the output setting](settings.md#setting-output) says.
- If the files are PostgreSQL or SQL Server, set the dialect.

**The settings files.**  The tool reads the same [settings files](settings-file.md#settings-files) as the build.  It reports their mistakes at the file, line and column where the build reports them:

- a file that is not a JSON object, a key that is not known or is written twice, or a value of the wrong JSON type: [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC018](../diagnostics.md#sqlsrc018);
- a value of `dialect`, `output` or `database` that is not valid: [SQLSRC019](../diagnostics.md#sqlsrc019).

The tool reports these mistakes only for a settings file that applies to a `.sql` or C# file of a project that it runs on.  It reports each once in a run, however many of its projects reach the file.  The build checks the values of the other settings, `typeOverrides` and `columnTypes` among them, and the tool does not.  The tool alone checks the value of `database`.

<a id="after-a-schema-change"></a>

## After a schema change

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
```

`describe --force` describes every query of the run again, current or not.  Run it after a migration, or after any other change to the database that a query reads.  Some examples of such a change:

- a column that can be null after the change;
- a type that changed;
- a table that was renamed.

Then commit the sidecars that it changed.  If the change broke a query, the tool reports the query with what the server said ([SQLSRC225](../diagnostics.md#sqlsrc225)).  An example is a query that reads a column that you dropped.

To describe again only the queries of one database, add `--database` and its name: `--database billing`.

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

`describe --watch` keeps the tool running while you work.  It describes once, as `describe` does.  Then it waits, and describes again each time that you save a file that can change what it describes.  It goes on until you stop it.

- **Each pass is a whole `describe`.**  It describes only the queries whose entry in the sidecar is out of date, writes their sidecars and prints its summary.  So the sidecar of a query that you just saved is current a moment later.
- **A pass that reports errors does not stop the watch.**  Fix what it reported, and save.  The next pass tries again.

**What starts a pass.**  One of these starts a pass:

- **A file that the tool reads is saved or deleted.**  That is a `.sql` file of your projects, a [settings file](settings-file.md#settings-files) that applies to one, or a C# file of a project.
- **What a project includes may have changed.**  That is one of these changes:
  - a new `.sql`, C# or `sqlsource.json` file, or a new folder, in the folder of a project or a folder below it, or in a folder that a project lists a file from;
  - a new `sqlsource.json` in a folder above a file that the tool reads, up to the root of your repository;
  - a `.sql` file, a C# file or a settings file that the tool reads renamed, or deleted and still gone when the pass starts;
  - a project file or a solution file created, saved, renamed or deleted;
  - a `Directory.Build.props`, `Directory.Build.targets` or `Directory.Packages.props` created, saved, renamed or deleted in the folder of a project or of the solution, in a folder below it, or in a folder above it up to the root of your repository;
  - a `dotnet restore`, into the `obj` folder of the project or into the `artifacts` folder of `UseArtifactsOutput`.

  The pass then reads your projects again before it describes.  So the pass describes a new file, or a new project of the solution.
- **The [`.env` file](connections.md#env-file) that the run reads is saved, deleted or created.**  That is the `.env` file in the folder of the project or solution that you watch, or the file that `--env-file` names, wherever it is.  A `.env` file in any other folder starts no pass.

**What starts no pass.**  The watch passes over everything else:

- the sidecars, and the temporary files that the tool writes itself;
- everything under `bin`, `obj` and `.git`, but what a restore writes and the `.env` file that the run reads;
- every other file.

So the tool's own writes never start a pass.

**When it describes.**  A pass starts 300 ms after the last change that it covers.  So an editor's save in several writes gives one pass, and so does a branch switch that changes hundreds of files.  If you save a change while a pass runs, one more pass follows once it ends.

**The two lines it writes.**  After every pass, the tool writes this line to standard output, alone on its line:

```text
sqlsource: watching for changes; press Ctrl+C to stop
```

Before every pass after the first, it writes this one:

```text
sqlsource: change detected; describing
```

A task of your editor can wait for these lines, to know when a pass begins and when it ends.

**The options.**

- **`--check` cannot be used with `--watch`.**  The tool writes `sqlsource: options '--watch' and '--check' cannot be used together` and exits with `1`.
- **`--force` forces the first pass only.**  The first pass describes every query once.  Each later pass describes what the changes made out of date.
- **`--project`, `--database` and `.sql` paths narrow every pass**, as they narrow a run.
- **`--connection` and `--verbose` apply to every pass.**
- **`--log` writes one file for the whole watch**, from its start until it is stopped: see [logs](cli.md#logs).

**Stopping it.**  Stop the watch with Ctrl+C.  You can also end it with `SIGTERM` or `SIGHUP`, as closing its terminal does.

- **A pass under way stops as a stopped `describe` does.**  It writes no further sidecar.
- **The tool exits with `0`, whatever its passes reported.**  Stopping is how a watch ends, and each pass wrote the errors that it found.
- **Three things end the watch with `1` before the first pass**: a wrong command line, a project or solution that cannot be found, and a log that cannot be opened.  See [exit codes](cli.md#exit-codes).

**One watch is enough.**  Run one watch for a repository.  A second watch, or a watch while a build runs, does no harm.  Each sidecar is written whole, and two passes that describe the same query write the same sidecar.  It only does the work twice.

**Where the system does not report changes.**  The tool asks the system to tell it when a file changes.  The system may not tell it about a folder mounted into a container, or a folder on a network share.

- **The tool can look at the folders every second instead.**  Before you start the watch, set the environment variable `DOTNET_USE_POLLING_FILE_WATCHER` to `true`, in any letter case, or to `1`.  Then the tool looks at the folders every second, and finds a change within a second or two.
- **The .NET SDK's container image, `mcr.microsoft.com/dotnet/sdk`, sets the variable to `true`.**  So a watch in a container of that image looks every second.  To use the system's events there, set the variable to `false`.
- **On Linux, the watch can reach one of the system's limits on watching folders.**  A machine with many projects, or other programs that watch folders, can reach one.  That is [SQLSRC287](../diagnostics.md#sqlsrc287), whose section says how to raise each limit.

**Editors.**  The saves of an editor were checked with Rider on macOS alone.  Visual Studio, VS Code and other editors, and Windows, have not been checked: see [the limit](#limit-saves-not-seen).

<a id="what-your-editor-shows"></a>

### What your editor shows

In Rider, with the watch running:

```text
12:00:00  You add a column to GetUser in Users.sql, and save it
12:00:01  The watch writes Users.sql.json; Rider still shows GetUser's old models, and no error
12:00:30  You build; Rider shows GetUser's new models
```

Your editor shows the models and methods of the sidecars that it has read.  After the watch writes a sidecar, the editor shows what the sidecar holds when the editor reads it again.  When that happens depends on the editor.  To have your editor start the watch, see [the recipe](../guides/describe-on-save.md#start-the-watcher-with-your-editor).

- **Rider shows what a sidecar holds within seconds after the watch rewrites or creates it**, with nothing else done.  This is true for the queries that you did not change in Rider.
- **Rider shows nothing new for a query that you change and save in Rider, until you build.**  The watch has already written the sidecar.  But Rider shows neither the error for the out-of-date entry nor the new models and methods of the query.  Build after such a change to see them.
- **Visual Studio and VS Code read the sidecar at a time that their mode sets**, by their documentation:
  - in their default mode, at your next save of any file, or your next build;
  - in their "Automatic" mode, as soon as the watch writes the sidecar.

  The setting is under Tools > Options in Visual Studio, and in the settings of VS Code.  Search either for "source generator".  This has not been checked.

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

`describe --check` is the command for a build server.  It describes every query of the run, as `--force` does.  It compares what the database says with the sidecars in the repository, and it changes no file.

- **A sidecar that is missing or out of date is [SQLSRC219](../diagnostics.md#sqlsrc219).**  The error names the query and what differs.  Its `help:` line names the command that fixes it.
- **`--check` also finds an entry that a change to the schema made wrong.**  The build already reports an entry that is missing or older than its query.  The build cannot see an entry that a change to the schema made wrong.
- **A build in CI never [runs the tool](online-build.md#running-in-the-build) itself.**  So this step is how a job describes.  For a whole GitHub Actions job, see [the recipe](../guides/ci.md#check-sidecars-in-ci).
- **It needs a database with your schema**, such as one that the job creates and migrates.  It also needs a connection for every database of the run.
- **It compares what changes generated code**:
  - the queries;
  - their parameters and columns;
  - their types, and whether they can be null;
  - the table column that each column is read from, which a [column type](model-properties.md#setting-column-types) is matched against.
- **It does not compare the versions of the tool or of the server.**  So a sidecar that a developer wrote passes against a server of another patch release.
- **Its exit code tells the two failures apart.**  See [exit codes](cli.md#exit-codes).
  - `2` means that a sidecar is out of date and nothing else went wrong: someone forgot to describe, or the schema changed.  Run `describe`, or `describe --force` after a change to the schema.
  - `1` means that the check could not be made, such as when a database could not be opened.
- **With `--database`, `--project` or a `.sql` path, it reports only what `describe` with the same filters would change**:
  - the queries that it would describe;
  - the leftover entries, of whatever database, in a sidecar that it would rewrite.

<a id="limits"></a>

## Limits

<a id="limit-several-target-frameworks"></a>

### SQLSRC241 for a query that only another target framework includes

**What you see.**  The build for a target framework other than the first reports [SQLSRC241](../diagnostics.md#sqlsrc241) for a query that must be described, after you ran `describe`.  The first framework does not include the query.  A project that references SqlSource for a later framework only is left out of a solution's run, and the tool says nothing.  If you name that project, the tool reports [SQLSRC204](../diagnostics.md#sqlsrc204).  The build of that project reports SQLSRC241 in the same way.

**Why.**  `describe`, and a build that runs the tool, read a project with several target frameworks for its first framework alone.  So they do not see these, and do not describe their queries:

- a `.sql` file or a C# file that the project includes only for another framework;
- a `[SqlSourceGenerate]` under an `#if` that only another framework defines.

**What to do.**  Include the `.sql` files and the attributed types for every framework, or for the first one listed.

<a id="limit-saves-not-seen"></a>

### Saving a file starts no pass of `describe --watch`

**What you see.**  A save in an editor other than Rider on macOS may not start a pass of `describe --watch`.

**Why.**  The saves of Visual Studio, VS Code and other editors, and saves on Windows, have not been checked.

**What to do.**  If saving a file does not start a pass:

1. Stop the watch.
2. Set `DOTNET_USE_POLLING_FILE_WATCHER` to `true`.
3. Start the watch again.

The watch then finds a save within a second or two.

<a id="limit-editors-not-checked"></a>

### In Visual Studio or VS Code, the models do not change after the watch writes a sidecar

**What you see.**  In Visual Studio or VS Code, the models and methods can stay as they were after the watch writes a sidecar.

**Why.**  In Visual Studio and VS Code, the recipes that start `describe --watch`, and what the editor shows after a pass, have not been checked.

**What to do.**  Save any file, or build.

<a id="limit-folder-changes-not-seen"></a>

### After a folder is deleted and made again, saves in it start no pass

**What you see.**  A few changes to folders far from your project start no pass of `describe --watch`.  After one of these changes, later saves in the folder start no pass:

- a folder deleted and made again where the watch was not looking one level above it, such as the folder of your repository itself on Linux;
- outside a git repository, a folder deleted, or made, together with the folder above it.

A first `dotnet restore` into an `artifacts` folder that the restore creates starts no pass either, when that folder is in one of these places:

- above the folder of your repository;
- outside a repository, above the folder of your nearest `Directory.Build.props`;
- outside a repository, when there is no `Directory.Build.props`, above the folder of your project.

Outside a git repository, a project or solution file saved in the folder above a folder that the watch covers starts a pass that changes nothing.

**Why.**  The watch does not see these changes, as they are in folders where it was not looking.  Outside a git repository, the watch also looks at the folder one level above each folder that it covers.  An example is the folder above the folder of your project.

**What to do.**  After such a change, stop the watch and start it again.  If you work outside a git repository, you can also work inside one instead.
