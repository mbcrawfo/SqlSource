# The tool

The `SqlSource.Tool` package adds the `sqlsource` command to a project.  Its `describe` command connects to your database, asks it what the parameters and columns of each of your queries are, and writes the answers into [sidecar files](#sidecar-files) beside your `.sql` files.  You commit the sidecars with the project, so that a build needs no database.  On a machine that has a connection, the build [runs `describe` itself](#running-in-the-build) before it compiles.

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
```

The generator writes a constant or a token method for every query whose output is `sql` or `models`, with the tool or without it.  The tool is for the queries that must be described, those whose [output](settings.md#setting-output) is `models` or `codegen`, the default: the build checks each of them against its file's sidecar, and reports one that has no current entry there.  A project whose queries all have the output `sql` needs no tool.

The generator builds the [models](generator.md#models) of each described query from the sidecars that the tool writes, and the [method](generator.md#query-methods) that runs a query whose output is `codegen`.

This page has two parts: [the workflow](#install-the-tool), from installing the tool and running it in the build to checking the sidecars in CI, and [the command reference](#command-reference).

<a id="install-the-tool"></a>

## Install the tool

Install it as a local tool of your repository, from the folder at its root.  Until the first release the package is a preview, so the command includes `--prerelease`:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool --prerelease
```

The first command creates the tool manifest, a `dotnet-tools.json` file, which records the tool and its version.  Commit that file.  Anyone who clones the repository, and a build server, then gets the same version with `dotnet tool restore`.  A build that [runs the tool](#running-in-the-build) starts this version too.  To run the tool, give it a connection to your database: see [connections](#connections).

Keep the tool at the version of the `SqlSource` package your projects use: the two are released together under one version number.  When you update the package, update the tool with `dotnet tool update SqlSource.Tool --prerelease`.  A package and a tool of different versions can stop the tool from reading your project ([SQLSRC206](diagnostics.md#sqlsrc206)).  What else the tool needs is under [requirements](#requirements).

<a id="which-queries"></a>

## Which queries are described

```sql postgres
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: ResetAuditLog
-- output: sql
TRUNCATE TABLE audit_log;
```

`describe` finds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it runs on, and describes each query whose [output](settings.md#setting-output) is `models` or `codegen`.  `codegen` is the default, so `GetUser` above is described.  `ResetAuditLog` asks for its SQL alone, and the tool leaves it out: it needs no database and no connection.

A query is described through its [dialect](concepts.md#dialect).  The tool can ask PostgreSQL, which is the dialect `postgres`, and SQL Server, which is `mssql`; the [database support table](dialects.md#database-support) lists every dialect.  A query that must be described and whose dialect cannot be, every row marked No in [database support](dialects.md#database-support), such as `ansi`, `mysql` or `sqlite`, is [SQLSRC209](diagnostics.md#sqlsrc209), from the tool and in the build.  The default dialect is `ansi`, so a project that sets neither the dialect nor the output gets it for every file.  Set the output of those queries to `sql`, as [the output setting](settings.md#setting-output) says, or set the dialect, when the files are PostgreSQL or SQL Server.

The tool reads `[SqlSourceGenerate]` from your C# source without compiling it, which limits how `Source` and `Output` can be written there: see [the generator's known issues](generator.md#known-issues).

The tool reads the same [settings files](settings.md#settings-files) as the build, and reports their mistakes at the file, line and column that the build gives them: a file that is not a JSON object, a key that is not known or is written twice, or a value of the wrong JSON type, [SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC018](diagnostics.md#sqlsrc018), and a value of `dialect`, `output` or `database` that is not valid, [SQLSRC019](diagnostics.md#sqlsrc019).  It reports them only for a settings file that applies to a `.sql` file or a C# file of a project it runs on, and once in a run, however many of its projects reach the file.  The other settings' values, `typeOverrides` and `columnTypes` among them, are checked by the build and not by the tool, and `database` by the tool alone.

<a id="connections"></a>

## Connections

```dotenv
SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
```

```console
$ dotnet sqlsource describe --connection reports="Host=localhost;Database=reports;Username=app;Password=secret"
billing (postgres): 4 described, 0 skipped, 0 failed
reports (postgres): 2 described, 0 skipped, 0 failed
```

The first block is a [`.env` file](#env-file) at the root of the repository, which gives the database `billing` its connection; `reports` gets its connection on the command line.

Each query that is described belongs to a [logical database](concepts.md#logical-database): the name that the [database setting](settings.md#setting-database) gives it, such as `billing`, or the default that the setting's section gives.  The tool connects to each logical database through the connection you give it under that name.

A connection is given in one of four ways.  The first that gives one wins:

| Order | Source | For the database `billing` |
|----|----|----|
| 1 | `--connection <name>=<connection string>` on the command line | `--connection billing="Host=localhost;Database=billing"` |
| 2 | The environment variable of the name | `SQLSOURCE_CONNECTION_BILLING` |
| 3 | The variable of the name in a [`.env` file](#env-file) | `SQLSOURCE_CONNECTION_BILLING=...` in `.env` |
| 4 | `SQLSOURCE_CONNECTION`, from the environment, or else from the `.env` file | Used only when the run has exactly one database |

The environment wins over the `.env` file variable by variable: the file gives a variable only when the environment does not set it.  So `SQLSOURCE_CONNECTION_BILLING` in the file still wins over `SQLSOURCE_CONNECTION` in the environment.

- **The variable of a name** is `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`: `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  Two names that give one variable, such as `app-v2` and `app_v2`, are [SQLSRC214](diagnostics.md#sqlsrc214): give both on the command line, or rename one.
- **`SQLSOURCE_CONNECTION`** is for a project with one database.  With more than one database in the run it is used for none of them, though a database that is in the run only through queries with errors of their own does not count ([SQLSRC215](diagnostics.md#sqlsrc215)): set the variable of each name instead.
- **A variable that is empty or holds only white space** is not set, in the environment or in the file.
- **A connection never goes in the project file or a settings file**, since it holds credentials.  A settings file has no key for one.
- **Put the value of `--connection` in quotes.**  A shell splits it at a space.  On a command line that has `--connection`, any other argument that holds `=` or `;` is refused, since it may be the rest of a connection string that the shell split.  If it is a project or `.sql` path that holds `=` or `;`, run the command from that file's folder and give the name alone, or give the connection in its variable instead.
- **A database that needs nothing described needs no connection.**  When every query of a database has a current sidecar entry, the tool asks the database nothing, and without a connection its summary line says `no connection`.  `--force` and `--check` describe every query, so every database of the run needs one then.
- **A database with a query to describe and no connection** is [SQLSRC213](diagnostics.md#sqlsrc213).  Its `help:` line names the option, the variable to set and the `.env` file to set it in, and lists any `--connection` names that matched no database of the run but are close to one, so that a misspelt name is seen.  A name that is far from every database is counted by the position of its value on the command line and never printed, since it may be a connection string typed without its name.

The tool keeps connection strings to itself as far as it can, wherever they came from.  In what it prints or writes to a [log](#logs), it writes `***` in the place of the whole value of a connection wherever it finds it, as you wrote it or as the database driver writes it again, and of each password of the connection string where it stands apart from other letters, digits and underscores.  A password that the PostgreSQL driver reads from `PGPASSWORD` or a password file, for a connection string that gives none, is written `***` in the same way.  It refuses to write a connection's value into a sidecar.  When a database cannot be opened, the error says what went wrong in the tool's own words and names neither the host nor the user.  A short password, or a connection string typed in the wrong place on the command line, can still appear: see [known issues](#known-issues).  The tool also takes the connection variables out of the environment of the `dotnet msbuild` it runs to read your projects, and never hands it the values of a `.env` file.  The connection string itself is the database's own: see connecting to [PostgreSQL](postgresql.md#connecting) and to [SQL Server](sqlserver.md#connecting).

<a id="env-file"></a>

### A `.env` file

```dotenv
# Connections of the sqlsource tool
SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
export SQLSOURCE_CONNECTION_REPORTS='Host=localhost;Database=reports;Username=app;Password=p#ss'

# Your application's own settings, which the tool passes over
ASPNETCORE_ENVIRONMENT=Development
```

A `.env` file keeps the connections of your machine in one place, so that you need not set them in each shell or type them on the command line.  Every run of `describe` reads it, `--check` and `--force` included.

- **Where it is found.**  The tool looks for a file named `.env` in the folder of the solution or project it runs on, then in each folder above it, up to the root of your repository, the folder that holds `.git`, and reads the nearest.  Outside a repository it looks in that one folder alone, so that a `.env` in your home folder is never read by surprise.  It reads one file, and never combines two.  A folder named `.env` is passed over.
- **A run on a solution reads the `.env` beside the solution**, or one above it, and not one in a project's folder below it.  Put the file beside the solution or at the root of the repository.  When such a run has no connection for a database, the `help:` line of [SQLSRC213](diagnostics.md#sqlsrc213) names each `.env` that the run did not read in a project's folder, or in a folder between it and the solution's.
- **Only the connection variables are read**: `SQLSOURCE_CONNECTION` and the variables that start with `SQLSOURCE_CONNECTION_`, written in upper case: `sqlsource_connection_billing` is not one of them.  The rest of the file belongs to your application or to other tools, and the tool passes over each of its lines without a word, whatever it holds.
- **The environment wins**, variable by variable, as the table of [connections](#connections) shows.
- **`--verbose` names the file** that a connection came from, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`, and never the value.  So does a [log](#logs).
- **Keep it out of git**, since it holds credentials: add `.env` to your `.gitignore`.  To show others what to set, commit a `.env.example` with the variables and no passwords, for each to copy to `.env`.
- **Its values are kept as the other connections are**: written `***` in what the tool prints and logs, refused in a sidecar, and never handed to the `dotnet msbuild` the tool runs, nor to anything else.

How a line is written:

- **One variable on a line**, `NAME=value`.  White space around the name and the `=` is ignored, and a line may start with `export `, as in a shell script.  A blank line, and a line whose first character other than white space is `#`, are passed over.
- **A value without quotes** loses the white space at its ends, and ends at a `#` that follows white space, which starts a comment: `Password=a#b` keeps its `#`, and `Password=a #b` ends at `a`.
- **A value in single quotes** is taken exactly as written: it may hold `#`, `"` and `\`, but not `'`.  It is the safest form for a connection string.
- **A value in double quotes** takes the escapes `\\`, `\"`, `\n`, `\r` and `\t`, and no other: write any other backslash as `\\`.
- **After a closing quote**, only white space or a comment may follow.
- **A value is on one line**: a quote that is not closed on its line is an error.  `${NAME}` is not replaced by another variable's value, and stays as written.
- **A variable set twice** takes the value of its last line.
- **The file is UTF-8**, with or without a byte order mark, and its lines may end as on Windows or as on Linux and macOS.  A line of a connection variable that is not saved as UTF-8, as a connection string with an `ä` in a file saved as Windows-1252, is [SQLSRC286](diagnostics.md#sqlsrc286); the other lines of the file may be in any encoding.

A line of a connection variable that cannot be read is [SQLSRC286](diagnostics.md#sqlsrc286), at its line, and a file the system will not let the tool read is [SQLSRC285](diagnostics.md#sqlsrc285).  Either way the run takes no connection from the file, not even from its good lines, and goes on with the other ways.

<a id="sidecar-files"></a>

## Sidecar files

A sidecar is the tool's record of what the database said about each query's parameters and columns.  It is a JSON file named after the `.sql` file, beside it: `Users.sql.json` for `Users.sql`.

```text
Queries/
    Users.sql
    Users.sql.json      written by sqlsource describe
```

**Commit the sidecars with the project.**  The build reads them in place of a database, so that every developer and every build server builds without a connection.  A copy of the project without them reports [SQLSRC241](diagnostics.md#sqlsrc241) for each query that must be described.  Only the person who changes a query, or the schema, needs a database to describe it again.  By default the build reads a sidecar and copies it nowhere: like your `.sql` files, it is not in the build output or in what `dotnet publish` writes, in a web project too.  A project that copies its sidecars itself, with `CopyToOutputDirectory` or `CopyToPublishDirectory` on their items, still gets the copy; in a web project that setting goes in `Directory.Build.targets`.

Rider shows each sidecar under its `.sql` file, with nothing to set.  To show each sidecar under its `.sql` file in Visual Studio Code, which nests files by a setting of its own, add this to your settings:

```json
{
    "explorer.fileNesting.enabled": true,
    "explorer.fileNesting.patterns": {
        "*.sql": "${capture}.sql.json"
    }
}
```

**Never edit a sidecar by hand.**  The tool writes it whole.  To change what a sidecar says, change the query or the schema and run `describe` again.  A sidecar with a merge conflict is no exception: see [sidecars in git](#sidecars-in-git).

To keep the sidecars current:

| When | Run |
|----|----|
| You add, change, rename or remove a query, the database type or the nullability of a `-- param:` marker (not its C# type after `->`), or a token's default | `dotnet sqlsource describe`, or a build that [runs the tool](#running-in-the-build) |
| The schema of the database changes | `dotnet sqlsource describe --force`: see [after a schema change](#after-a-schema-change) |
| While you work on your queries, to describe each one as you save it | `dotnet sqlsource describe --watch`: see [describe on save](#describe-on-save) |
| You update the tool | `dotnet sqlsource describe`, or a build that runs the tool, which describes every query again |
| On a build server, to catch a sidecar that someone forgot | `dotnet sqlsource describe --check`: see [check in CI](#check-in-ci) |

The build tells you when a sidecar is not current.  A query without an entry is [SQLSRC241](diagnostics.md#sqlsrc241), and one that changed since its entry was written is [SQLSRC244](diagnostics.md#sqlsrc244).  An entry that names no query that must be described is the warning [SQLSRC245](diagnostics.md#sqlsrc245), and a sidecar that another version of the tool wrote the warning [SQLSRC240](diagnostics.md#sqlsrc240).  Each of the first three goes away when you run `describe` and commit what it writes.  SQLSRC240 goes away once the tool and the `SqlSource` package have one version and `describe` has run with that tool.  A sidecar with a merge conflict in it is [SQLSRC237](diagnostics.md#sqlsrc237).

<a id="sidecars-in-git"></a>

### Sidecars in git

```text
# .gitattributes
*.sql.json linguist-generated=true
```

This line in the `.gitattributes` file at the root of your repository marks the sidecars as generated, so that GitHub collapses them in the diff of a pull request.  Give them no other rule.  Git merges two branches that changed one sidecar as it merges any text, and that is safe: the build knows which version of each query an entry was written for, so an entry that a merge leaves older than its query is [SQLSRC244](diagnostics.md#sqlsrc244) until `describe` writes it again.  A rule that stops git merging sidecars, such as `-merge` or `binary`, would turn every sidecar that both branches changed into a conflict, and would keep one side's file whole, losing what the other side's `describe --force` wrote after a schema change.

**After a merge conflict in a sidecar**, leave the conflict in the file and run `dotnet sqlsource describe`.  A sidecar with a conflict in it cannot be read, so the tool describes every query of its file again and writes the file whole: commit what it writes.  Until then the build reports [SQLSRC237](diagnostics.md#sqlsrc237).  If you have already taken one side of the conflict, run `dotnet sqlsource describe --force` on that `.sql` file instead, such as `dotnet sqlsource describe --force Queries/Users.sql`: the side you took may hold entries written before a schema change that the other side described, and plain `describe` keeps an entry whose query has not changed.  Both need a [connection](#connections) to the database.

The format of a sidecar is described by its JSON Schema, [`sidecar-v1.schema.json`](../../schemas/sidecar-v1.schema.json), for the curious.  Nothing in this manual depends on it.

<a id="describing-again"></a>

## Describing again

`describe` describes only what changed.  A query whose sidecar entry is current is skipped, and is not sent to the database.  An entry stops being current when any of these change:

- The query's SQL.  A change to a comment alone does not count.
- The database type or the nullability of a `-- param:` marker of the query, `not null` included, or the default of one of its tokens.  The C# type after `->`, and a `-- column:` marker, do not count.
- The query's dialect, or its [logical database](#connections).
- The version of the tool: a sidecar written by another version is described again whole.  Until it is, the build warns of it ([SQLSRC240](diagnostics.md#sqlsrc240)).

The tool cannot see a change to the schema, so a query whose SQL is as it was is skipped even when its table changed.  That is what `--force` is for.

A sidecar is written whole or not at all.  When a query of a file fails, or a filter leaves one out whose entry is not current, the file's sidecar is left as it was, and [SQLSRC217](diagnostics.md#sqlsrc217) says so if the run described any of its queries.  Fix the query, or describe the whole file, and run `describe` again.

When no query of a file needs an entry any more, because each was removed or now has the output `sql`, the tool deletes the file's sidecar.  It does so only in a run with no `--project`, `--database` or `.sql` path, and only when the run reported no error, so that a run over part of the project never removes what another part needs.

A run that is stopped while it writes a sidecar leaves the old file or the new one, never part of either.  It can leave a temporary file beside it, named like `Users.sql.json.<id>.tmp`; the next `describe` of the whole project removes it once it is a minute old.

<a id="running-in-the-build"></a>

## Running in the build

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

```console
$ dotnet build -tl:off
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist; query: GetUser, database postgres, postgres 18.6, Npgsql 10.0.3; step: describe parameters; server: 42P01: relation "user" does not exist; server: at character 15; see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc225 [/work/App/App.csproj]
/work/App/Queries/Users.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'codegen' needs one.  Run 'sqlsource describe'. [/work/App/App.csproj]
```

With a [`.env` file](#env-file) like the one above, `dotnet build` runs `dotnet sqlsource describe` on the project before it compiles.  So you do not run `describe` after you change a query: the build describes it, compiles the models and methods of what your database says, and reports a query that the database rejects as a build error at the query, here a table named `user` where the schema has `users`.  A build with no connection, a build in CI, and a build of a project that turns it off build from the committed sidecars, as a build without the tool does.  Commit the sidecars that a build writes, as you commit those that `describe` writes.

- **What turns it on.**  By default a build runs the tool when it finds a connection: the variable `SQLSOURCE_CONNECTION` in the environment, or a `.env` file in the project's folder or a folder above it.  A build in CI never runs it.  [`SqlSourceOnline`](settings.md#online-build), a property of the project, turns it on whatever the build finds, or off.
- **Which builds.**  `dotnet build`, `dotnet run`, `dotnet publish`, a rebuild, and the build command of an IDE.  An IDE that loads the project, or reads it again after you save a file, does not run the tool, and nor does a build of the `Compile` target alone.  This was checked in Rider on macOS; Visual Studio, Visual Studio Code, and Rider on another system have not been checked, and may differ.
- **Only when something changed.**  A build runs the tool only when something it reads has changed since the tool last succeeded: a `.sql` file, a C# file, a settings file, a sidecar, changed or deleted, the `.env` file, the tool manifest, which updating the tool rewrites, the version of the `SqlSource` package, or the value of `SqlSourceOnline`.  A build after which nothing changed does not start it.  A change to the schema changes none of them: run `describe --force`, as [after a schema change](#after-a-schema-change) says.
- **It describes the whole project**, as `describe` with no path does: it writes the sidecar of each file whose queries changed, deletes a sidecar that no query needs any more, and the same build compiles with what it wrote, a sidecar it wrote for the first time too.  Otherwise it compiles the sidecars your project lists, as a build offline or in CI does: a project that [lists its own files](settings.md#included-files), or a `.sql` file from outside its folder, lists their sidecars too, or the next build reports [SQLSRC241](diagnostics.md#sqlsrc241) for the queries of a sidecar it left out, as CI would.  It reads the `.env` nearest to the project, up to the root of the repository, in a build of a solution too.  It gets the build's environment, so a connection variable that your IDE sets reaches it.
- **The tool must be installed** in the repository's tool manifest and restored, as [install the tool](#install-the-tool) says, at the version of the `SqlSource` package.  A build that cannot start it is [SQLSRC026](diagnostics.md#sqlsrc026), a warning, and the build goes on with the committed sidecars; under `SqlSourceOnline` `true` it is the error [SQLSRC025](diagnostics.md#sqlsrc025).  A tool older than the package, which does not know what the build asks of it, is [SQLSRC027](diagnostics.md#sqlsrc027).

**Offline.**  A `.env` file or a variable that gives no connection for any database with a query to describe, such as your application's own `.env`, starts the tool, which then says it is offline and changes nothing; the build goes on with the committed sidecars, and the next build tries again.  The generator still reports each query whose entry is missing or out of date, as [sidecar files](#sidecar-files) says.  When every entry is current the tool asks no database anything, and needs no connection.  A connection for some databases and not for others is [SQLSRC213](diagnostics.md#sqlsrc213) at the queries of the others, and fails the build, as do a variable that two databases share, when it is set, [SQLSRC214](diagnostics.md#sqlsrc214), and `SQLSOURCE_CONNECTION` set for several databases, [SQLSRC215](diagnostics.md#sqlsrc215).

**Errors.**  Each error of the tool is an error of the build, at the `.sql` file and the place of the query, in the compiler's format, so your IDE lists it with the build's other errors.  The lines that `describe` writes under an error, such as `help:` and `see:`, follow it on the same line, each after `; `.

- **One failing query holds back its file.**  A sidecar is written whole or not at all, as [describing again](#describing-again) says, so the file keeps its old sidecar, and the generator then reports the failing query too, as having no entry ([SQLSRC241](diagnostics.md#sqlsrc241)) or an entry older than the query ([SQLSRC244](diagnostics.md#sqlsrc244)), and so each other query of the file whose entry was to change.  The tool's error says why, and the generator's what follows from it.  Fix the query and build again.
- **An error that the build's compiler reports too is listed once**, by the compiler, at its place in the file: a problem in a settings file ([SQLSRC015](diagnostics.md#sqlsrc015) to [SQLSRC019](diagnostics.md#sqlsrc019)), a `.sql` file that does not parse, a token with no default ([SQLSRC210](diagnostics.md#sqlsrc210)) and an output that the dialect cannot describe ([SQLSRC209](diagnostics.md#sqlsrc209)).  The exception is [SQLSRC019](diagnostics.md#sqlsrc019) for a `database` that is not a valid name: only the tool reads that setting, so the tool writes the error and the build lists it.  The build still fails on each of them.  While one is in the project the tool deletes no sidecar, and writes none for a `.sql` file that does not parse, so the generator may follow with the queries of that file as having no entry, [SQLSRC241](diagnostics.md#sqlsrc241).  `describe` run by hand lists these errors itself.
- **A database that cannot be opened** is [SQLSRC224](diagnostics.md#sqlsrc224), and fails the build.  Its `help:` line says how to build without the database: remove its connection, or set `SqlSourceOnline` to `false`.

**Several target frameworks.**  The tool runs once in a build, for the first framework that `TargetFrameworks` lists, which is the framework `describe` reads, and every framework compiles with the sidecars it wrote.  A build of another framework alone, with `-f` or through a reference from a project of that framework, still runs it, for the first framework.  A query that only another framework claims is not described: see [known issues](#known-issues).

**In CI**, a build never runs the tool, whatever `SqlSourceOnline` says, so that a job checks the sidecars that were committed and does not write its own.  Describe in a step of its own, with `describe --check`: see [check in CI](#check-in-ci).

**What the build prints.**  The build shows the tool's errors at every verbosity, and nothing else that the tool prints.  The tool's [summary](#summary), each line of it after `sqlsource: summary: `, or its line that says it is offline, is a message of the detailed verbosity, which `dotnet build -tl:off -v:d` shows.  `dotnet build` in an interactive terminal shows no such message at any verbosity unless `-tl:off` turns its live display off; a binary log, `-bl`, holds them too.

**Logs.**  `--log` cannot be given to the build's run, and a failed build does not suggest it.  Set the variable `SQLSOURCE_LOG`, to any value, and each build that runs the tool writes the log of its run to a file of the project and framework in the intermediate folder, such as `obj/Debug/net10.0/App.csproj.SqlSource.log`; the variable's value is not used as a path, since projects built at once would write one file.  [Read a log before you share it](#logs).

**Your connection in the build's logs.**  The build itself reads only whether `SQLSOURCE_CONNECTION` is set, never its value nor what a `.env` file says, so neither reaches its logs, at any verbosity or in a binary log.  What the tool prints is in them, and the tool hides a connection there as it does in your terminal ([known issues](#known-issues)).  `MSBUILDLOGALLENVIRONMENTVARIABLES=1`, a setting of MSBuild, writes every variable of the environment into the build's logs, every connection variable among them: leave it unset on a machine whose environment holds a connection, or search such a log for your password before you share it.

<a id="after-a-schema-change"></a>

## After a schema change

```console
$ dotnet sqlsource describe --force
billing (postgres): 16 described, 0 skipped, 0 failed
```

`--force` describes every query of the run again, current or not.  Run it after a migration, or any other change to the database that a query reads: a column that became nullable, a type that changed, a table that was renamed.  Then commit the sidecars it changed.  A query that the change broke, such as one that reads a column you dropped, is reported with what the server said ([SQLSRC225](diagnostics.md#sqlsrc225)).

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

- **A file that the tool reads is saved**: a `.sql` file of your projects, a [settings file](settings.md#settings-files) that applies to one, or a C# file of a project.
- **What a project includes may have changed**: a new `.sql`, C# or `sqlsource.json` file, or a new folder, in a project's folder or in a folder that a project lists a file from; a new `sqlsource.json` in a folder above a file the tool reads, up to the root of your repository; a `.sql` file renamed, or deleted and still gone when the pass starts; a project file or a solution file saved; a `Directory.Build.props`, `Directory.Build.targets` or `Directory.Packages.props` saved in the folder of a project or of the solution, below it, or above it up to the root of your repository; and a `dotnet restore`, into the project's `obj` folder or into the `artifacts` folder of `UseArtifactsOutput`.  The pass then reads your projects again before it describes, so a new file, or a new project of the solution, is described.
- **The [`.env` file](#env-file)** is saved, deleted or created in a folder where the tool looks for it.

Everything else is passed over: the sidecars and the temporary files the tool writes itself, everything under `bin`, `obj` and `.git` but what a restore writes, and every other file.  So the tool's own writes never start a pass.

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
- **`--log` writes one file for the whole watch**, from its start until it is stopped: see [logs](#logs).

**Stopping it.**  Stop the watch with Ctrl+C, or end it with `SIGTERM` or `SIGHUP`, as closing its terminal does.  A pass under way stops as a stopped `describe` does, and writes no further sidecar.  The tool exits with `0`, whatever its passes reported, since stopping is how a watch ends; each error was written in the pass that found it.  A wrong command line, a project or solution that cannot be found, and a log that cannot be opened end it with `1` before the first pass.  See [exit codes](#exit-codes).

**One watch is enough.**  Run one for a repository.  A second one, or a watch while a build runs, does no harm: each sidecar is written whole, and two passes that describe the same query write the same sidecar.  It only does the work twice.

**Where the system does not report changes.**  The tool asks the system to tell it when a file changes.  A folder mounted into a container or on a network share may not tell it.  Set the environment variable `DOTNET_USE_POLLING_FILE_WATCHER` to `true` or `1`, in any case, before you start the watch, and the tool looks at the folders every second instead, so a change is found within a second or two.  The .NET SDK's container image, `mcr.microsoft.com/dotnet/sdk`, sets it to `true`, so a watch in a container of that image looks every second; set it to `false` there to use the system's events.  On Linux, a machine with many projects, or other programs that watch folders, can reach one of the system's limits on watching folders: that is [SQLSRC287](diagnostics.md#sqlsrc287), whose section says how to raise each.

**Editors.**  The saves of an editor were checked with Rider on macOS alone.  Visual Studio, VS Code and other editors, and Windows, have not been checked: see [known issues](#known-issues).

<a id="what-your-editor-shows"></a>

### What your editor shows

In Rider, with the watch running:

```text
12:00:00  You add a column to GetUser in Users.sql, and save it
12:00:01  The watch writes Users.sql.json; Rider still shows GetUser's old models, and no error
12:00:30  You build; Rider shows GetUser's new models
```

Your editor shows the models and methods of the sidecars it has read.  When the watch writes a sidecar, the editor shows what it holds when it reads it again, and that depends on the editor.  To have your editor start the watch, see [the recipe](recipes.md#start-the-watcher-with-your-editor).

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
    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/diagnostics.md#sqlsrc219
billing (postgres): 15 same, 1 differ, 0 failed
```

`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars in the repository, and changes no file.  A sidecar that is missing or out of date is [SQLSRC219](diagnostics.md#sqlsrc219), which names the query and what differs, and its `help:` line names the command that fixes it.  The build already reports an entry that is missing or older than its query; `--check` also finds one that a change to the schema made wrong, which the build cannot see.  A build in CI never [runs the tool](#running-in-the-build) itself, so this step is how a job describes.  For a whole GitHub Actions job, see [the recipe](recipes.md#check-sidecars-in-ci).

- **It needs a database with your schema**, such as one the job creates and migrates, and a connection for every database of the run.
- **It compares what changes generated code**: the queries, their parameters and columns, their types, whether they can be null, and the table column each column is read from, which a [column type](settings.md#column-types) is matched against.  It does not compare the versions of the tool or of the server, so a sidecar that a developer wrote passes against a server of another patch release.
- **Its exit code tells the two failures apart.**  `2` means a sidecar is out of date and nothing else went wrong: someone forgot to describe.  `1` means the check could not be made, such as a database that could not be opened.  See [exit codes](#exit-codes).
- **With `--database`, `--project` or a `.sql` path**, it reports only what `describe` with the same filters would change: the queries that it would describe, and the leftover entries, of whatever database, in a sidecar that it would rewrite.

<a id="summary"></a>

## The summary

Every run ends with one line for each database it ran on:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

- **described**: the queries the database was asked about in this run.
- **skipped**: the queries whose sidecar entry was current.
- **failed**: the queries that could not be described.  Each has an error above the summary.
- **no connection**: the database has no connection and needed none, since all its queries were current.

Under `--check` the counts are `same`, `differ` and `failed`.  A name given to `--database` that no query of the run has gets a line of zeros, so that a misspelt name is seen.  The line names it when it is close to a database of the run, in the spelling you gave, and otherwise says `--database at position <n>`, with the position of the value on the command line, since a value typed after `--database` by mistake may be a secret.  A run on a solution in which no project uses SqlSource prints `sqlsource: no project of '<path>' uses SqlSource` and no summary.

<a id="command-reference"></a>

## Command reference

```text
dotnet sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
                          [--connection <name>=<connection string>]...
                          [--force | --check] [--verbose] [--log <path>]
                          [--watch]
```

`dotnet sqlsource --help` and `dotnet sqlsource describe --help` print the options, and `dotnet sqlsource --version` the version of the tool.

<a id="arguments"></a>

### Arguments

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx
$ dotnet sqlsource describe src/App/Queries/Users.sql src/App/Queries/Orders.sql
```

- **The project or solution to run on**: a `.sln`, `.slnx` or `.csproj` file, or a folder that holds exactly one of them.  Without it, the current folder.  Only one may be given.  The tool looks in that folder alone, not in the folders below it: none is [SQLSRC201](diagnostics.md#sqlsrc201) and more than one [SQLSRC202](diagnostics.md#sqlsrc202).
- **In a solution**, the tool runs on every C# project that uses the SqlSource package, and leaves the others out without a word.  Every C# project of the solution must have been restored, one with no queries too ([SQLSRC220](diagnostics.md#sqlsrc220)).
- **A path that ends in `.sql`** restricts the run to that file.  Several may be given.  It must be a file that a type of the run claims ([SQLSRC212](diagnostics.md#sqlsrc212)); the rest of the run goes on.

A relative path is taken from the current folder.

<a id="options"></a>

### Options

| Option | What it does |
|----|----|
| `--project <path>` | Runs on this project of the solution and not on the others.  May be given several times.  A path that is not a C# project of the solution is [SQLSRC207](diagnostics.md#sqlsrc207) |
| `--database <name>` | Describes only the queries of this [logical database](#connections).  May be given several times.  Every other sidecar is left as it was |
| `--connection <name>=<connection string>` | The connection of a database: see [connections](#connections).  May be given several times, once for each name |
| `--force` | Describes every query of the run again, current or not: see [after a schema change](#after-a-schema-change) |
| `--check` | Describes every query of the run, compares with the sidecars, writes nothing: see [check in CI](#check-in-ci).  With `--force` too, it is `--check` |
| `--verbose` | Prints what the run decides as it goes: see [`--verbose`](#verbose) |
| `--log <path>` | Writes a record of the run to a file; a path that holds `=` or `;` is refused: see [logs](#logs) |
| `--manifest <path>` | For the build's use: describes the project that the build's record of it names, without reading the project again.  It takes no project or solution, `.sql` path, `--project`, `--database` or `--watch`.  See [running in the build](#running-in-the-build) |
| `--online <auto\|true>` | For the build's use, with `--manifest`: takes a `--manifest` or `--log` path that holds `=` or `;`, as the build's own may, writes each error on one line, writes no log for `SQLSOURCE_LOG`, and under `auto` says it is offline, and exits with `0`, when no database with a query to describe has a connection.  See [running in the build](#running-in-the-build) |
| `--watch` | Describes once, then again each time a file of the run changes, until stopped: see [describe on save](#describe-on-save).  Not with `--check` or `--manifest` |

A run with `--project`, `--database` or a `.sql` path writes only the sidecars of the files whose queries it selected, and deletes none.

<a id="environment-variables"></a>

### Environment variables

| Variable | What it does |
|----|----|
| `SQLSOURCE_CONNECTION_<NAME>` | The connection of the database `<name>`: see [connections](#connections) |
| `SQLSOURCE_CONNECTION` | The connection of a run with exactly one database |
| `SQLSOURCE_LOG` | The path of the log, when `--log` is not given.  In a build, any value writes a log of each project's run: see [running in the build](#running-in-the-build) |
| `SQLSOURCE_DEBUG` | Any value that is not empty adds the stack trace to [SQLSRC200](diagnostics.md#sqlsrc200), for a bug report |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `true` or `1`, in any case, makes `--watch` look at the folders every second instead of asking the system: see [describe on save](#describe-on-save) |

A connection or log variable that is empty or holds only white space is not set.  Each connection variable can also be set in a [`.env` file](#env-file), and the environment wins over it; `SQLSOURCE_LOG`, `SQLSOURCE_DEBUG` and `DOTNET_USE_POLLING_FILE_WATCHER` are read from the environment alone.

<a id="exit-codes"></a>

### Exit codes

| Code | When |
|----|----|
| `0` | The run reported no error; or a `--watch` was stopped with Ctrl+C, `SIGTERM` or `SIGHUP`, whatever its passes reported |
| `1` | An error was reported other than a sidecar out of date; the command line is wrong; the log could not be written; or a run without `--watch` was stopped with Ctrl+C, `SIGTERM` or `SIGHUP` |
| `2` | `--check` found a sidecar out of date, and nothing else went wrong |

A run that is stopped writes no further sidecar.  A second Ctrl+C ends the process at once.

<a id="verbose"></a>

### `--verbose`

```console
$ dotnet sqlsource describe --verbose
sqlsource: unit '/work/App/App.csproj', a project, found in the working directory
sqlsource: project '/work/App/App.csproj': net10.0, 1 .sql files
sqlsource: file '/work/App/Queries/Users.sql': postgres, ready, claimed by /work/App/Queries/UserQueries.cs(3)
sqlsource: database 'postgres' (postgres): connection from SQLSOURCE_CONNECTION_POSTGRES
sqlsource: database 'postgres': server 18.6
sqlsource: query 'GetUser' in '/work/App/Queries/Users.sql': described in 25 ms
sqlsource: sidecar '/work/App/Queries/Users.sql.json': written
postgres (postgres): 1 described, 0 skipped, 0 failed
```

`--verbose` prints a line for each project, file, database, query and sidecar, saying what the run decided for it, each starting with `sqlsource:`.  A database's line names where its connection came from, and never the connection: `--connection`, a variable's name, or, for a connection from a [`.env` file](#env-file), the variable and the file, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`.  Use it to see why a query was skipped or a project left out.  Under `--watch` it says the same of each pass, and nothing more.  Without it the tool prints its errors, the summary, and the line that ends a failed run or says that no project uses SqlSource.

<a id="logs"></a>

### Logs

```console
$ dotnet sqlsource describe --log sqlsource.log
```

`--log <path>`, or the variable `SQLSOURCE_LOG`, writes a record of the run to a file, one JSON object on each line.  It is what to attach to an issue when the tool does something you did not expect.

- **The file is overwritten by each run.**  The tool refuses a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, so that a slip on the command line never empties a file of your project ([SQLSRC218](diagnostics.md#sqlsrc218)).  It refuses a `--log` path that holds `=` or `;` too, which is more likely a connection string typed in the wrong place than a path; a path from `SQLSOURCE_LOG` is not held to that.
- **A `--watch` writes one file from its start until it is stopped**, with every pass in it and where each begins and ends.
- **A run that reports an error and writes no log** ends with the line `sqlsource: run again with --log <file> for a record of what the run did`.  A `--check` whose only errors are sidecars out of date does not print it, nor does a `--watch`, nor the run of a build, which is given no `--log`: see [running in the build](#running-in-the-build) for its logs.

**Read a log before you share it.**  The tool writes each connection value as `***` wherever it finds it whole: the value of each `--connection` on the command line, and the value of each connection, from the command line or a variable, wherever the database driver repeats it in a message, as written, with the spaces at its ends trimmed, or as the driver writes it again.  It writes each password of a connection string as `***` too, where it stands apart from other letters, digits and underscores and is at least four characters long, and so the password that the PostgreSQL driver reads from `PGPASSWORD` or a password file when the connection string gives none.  In the record of your command line, every value of `--database` and `--connection` is written `***`.  A value typed where a path goes (the path of `describe`, a `.sql` path or a `--project` value) is written `***` too when it holds `=` or `;`, as a connection string does.  A value typed there or after `--log` with neither, such as a password typed alone, can appear as written, and so can a password shorter than four characters, and one that is the name of its logical database or of its dialect, such as `postgres` ([known issues](#known-issues)): search the log for your password before you share it.  The log also holds:

- the version of the tool, of .NET and of the operating system, and the rest of your command line;
- the paths of your projects and files, and the names of your queries and databases;
- the SQL of your queries as the tool sent it to the database, and what the server answered, its errors and warnings included;
- for a database that could not be opened, what went wrong, in the tool's own words and with the server's SQLSTATE, or SQL Server's error number, which names neither its host nor its user;
- each error, and how the run ended, with its exit code.  A log that cannot be finished, as when the disk is full, is an error of its own once the run has ended ([SQLSRC218](diagnostics.md#sqlsrc218)): the tool then exits with `1`, and the log's last line may still hold the exit code the run had before.

<a id="requirements"></a>

### Requirements

- **The .NET SDK**, and not only a runtime.  The tool asks MSBuild about your projects through `dotnet msbuild`.  It runs on .NET 8 or later.
- **Restored projects.**  The tool does not restore: run `dotnet restore` first ([SQLSRC220](diagnostics.md#sqlsrc220)).
- **The same version as the `SqlSource` package** of your projects ([SQLSRC206](diagnostics.md#sqlsrc206)).
- **A database the tool can reach**, of a version it supports, for each logical database with a query to describe.  See [PostgreSQL](postgresql.md#connecting) and [SQL Server](sqlserver.md#connecting).

<a id="known-issues"></a>

## Known issues

- **A project path or a `.sql` path through a symbolic link is not found.**  When the current folder is reached through a symbolic link and you give a full path that goes through the link, the tool compares it with the real path and finds no match: `--project` reports [SQLSRC207](diagnostics.md#sqlsrc207), and a `.sql` path [SQLSRC212](diagnostics.md#sqlsrc212), though the file is there.  The message shows both spellings.  Give the path relative to the current folder, or the full real path.
- **A project with several target frameworks is read for its first framework alone**, by `describe` and by a build that runs the tool.  A `.sql` file or a C# file that the project includes only for another framework, and a `[SqlSourceGenerate]` under an `#if` that only another framework defines, are not seen, and their queries are not described: the build for that framework reports [SQLSRC241](diagnostics.md#sqlsrc241) for each that must be.  A project that references SqlSource for a later framework only is left out of a solution's run without a word, and is [SQLSRC204](diagnostics.md#sqlsrc204) when you name it; its build reports SQLSRC241 in the same way.  Include the `.sql` files and the attributed types for every framework, or for the first one listed.
- **A `.sql` file that one of your own build targets adds is not described**, by `describe`, nor by the build of a project with several target frameworks.  See [the known issues of settings](settings.md#known-issues) for what goes wrong and the one line that fixes it.
- **A `.sql` file that two projects list with two outputs** has one sidecar for both, and a run on one of the two projects can leave out, or remove, the entries that only the other needs.  Give the file the same output in both projects, and describe the whole solution in one run.  See [the known issues of `.sql` files](sql-files.md#known-issues).
- **A save in an editor other than Rider on macOS may not start a pass of `describe --watch`.**  The saves of Visual Studio, VS Code and other editors, and saves on Windows, have not been checked.  If saving a file does not start a pass, stop the watch, set `DOTNET_USE_POLLING_FILE_WATCHER` to `true` and start it again: it then finds a save within a second or two.
- **In Visual Studio and VS Code, the recipes that start `describe --watch` and what the editor shows after a pass have not been checked.**  If the models and methods do not change after the watch writes a sidecar, save any file or build.
- **A few changes to folders far from your project start no pass of `describe --watch`.**  A folder deleted and made again where the watch was not looking one level above it, such as your repository's own folder on Linux, or, outside a git repository, a folder deleted together with the folder above it, is not seen, and later saves in it start no pass.  A first `dotnet restore` into an `artifacts` folder that does not exist yet and lies above your repository's folder, or outside a repository above the folder of your nearest `Directory.Build.props`, or above your project's folder when there is none, starts no pass either.  Outside a git repository the watch also looks at the folder one level above each folder it covers, such as the folder above your project's folder, so a project or solution file saved there starts a pass that changes nothing.  Stop the watch and start it again after such a change, or work inside a git repository.
- **A short password, or a connection string typed in the wrong place, can appear in the output or a log.**  The tool writes a connection value as `***` where it finds the whole value, and each password of it, or of `PGPASSWORD` or a PostgreSQL password file, where it stands apart from other letters, digits and underscores.  A password shorter than four characters is not hidden, nor is one that is the name of its logical database or of its dialect, such as `postgres`, words that the tool prints anyway.  A password that is hidden hides each word of a message that equals it, such as a column named like it, and every password of a PostgreSQL password file that the tool reads is hidden so, not only the one the connection uses.  A password file that is a pipe, such as `PGPASSFILE=<(...)` in a shell, is left for the driver to read, and its password is not hidden.  An error of SQL Server about a query can name the login, such as one for a database that the login may not open, and it is printed and logged as the server wrote it.  A connection string typed where a path or a `--project` goes, or after `--database` or `--connection` without its `name=`, is not printed.  In the log, every value of `--database` and `--connection` is written `***`, and so is a path, a `--project` value included, that holds `=` or `;`; a `--log` path that holds either is refused.  A secret with neither, typed alone where a path, a `--project` value or a `--log` path goes, is printed as a path and logged as typed.  Give each connection as `--connection <name>="..."` or in its variable, and search what you share, a log above all, for your password first.
