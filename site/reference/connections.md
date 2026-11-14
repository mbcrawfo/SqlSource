# Connections and the .env file

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

The first block is a [`.env` file](#env-file) in the folder of the project or solution the tool runs on, which gives the database `billing` its connection; `reports` gets its connection on the command line.

Each query that is described belongs to a [logical database](../concepts.md#logical-database): the name that the [database setting](../settings.md#setting-database) gives it, such as `billing`, or the default that the setting's section gives.  The tool connects to each logical database through the connection you give it under that name.

A connection is given in one of four ways.  The first that gives one wins:

| Order | Source | For the database `billing` |
|----|----|----|
| 1 | `--connection <name>=<connection string>` on the command line | `--connection billing="Host=localhost;Database=billing"` |
| 2 | The environment variable of the name | `SQLSOURCE_CONNECTION_BILLING` |
| 3 | The variable of the name in a [`.env` file](#env-file) | `SQLSOURCE_CONNECTION_BILLING=...` in `.env` |
| 4 | `SQLSOURCE_CONNECTION`, from the environment, or else from the `.env` file | Used only when the run has exactly one database |

The environment wins over the `.env` file variable by variable: the file gives a variable only when the environment does not set it.  So `SQLSOURCE_CONNECTION_BILLING` in the file still wins over `SQLSOURCE_CONNECTION` in the environment.

- **The variable of a name** is `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`: `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  Two names that give one variable, such as `app-v2` and `app_v2`, are [SQLSRC214](../diagnostics.md#sqlsrc214): give both on the command line, or rename one.
- **`SQLSOURCE_CONNECTION`** is for a project with one database.  With more than one database in the run it is used for none of them, though a database that is in the run only through queries with errors of their own does not count ([SQLSRC215](../diagnostics.md#sqlsrc215)): set the variable of each name instead.
- **A variable that is empty or holds only white space** is not set, in the environment or in the file.
- **A connection never goes in the project file or a settings file**, since it holds credentials.  A settings file has no key for one.
- **Put the value of `--connection` in quotes.**  A shell splits it at a space.  On a command line that has `--connection`, any other argument that holds `=` or `;` is refused, since it may be the rest of a connection string that the shell split.  If it is a project or `.sql` path that holds `=` or `;`, run the command from that file's folder and give the name alone, or give the connection in its variable instead.
- **A database that needs nothing described needs no connection.**  When every query of a database has a current sidecar entry, the tool asks the database nothing, and without a connection its summary line says `no connection`.  `--force` and `--check` describe every query, so every database of the run needs one then.
- **A database with a query to describe and no connection** is [SQLSRC213](../diagnostics.md#sqlsrc213).  Its `help:` line names the option, the variable to set and the `.env` file to set it in, or says that the run reads none, and lists any `--connection` names that matched no database of the run but are close to one, so that a misspelt name is seen.  A name that is far from every database is counted by the position of its value on the command line and never printed, since it may be a connection string typed without its name.

The tool keeps connection strings to itself as far as it can, wherever they came from.  In what it prints or writes to a [log](cli.md#logs), it writes `***` in the place of the whole value of a connection wherever it finds it, as you wrote it or as the database driver writes it again, and of each password of the connection string where it stands apart from other letters, digits and underscores.  A password that the PostgreSQL driver reads from `PGPASSWORD` or a password file, for a connection string that gives none, is written `***` in the same way.  It refuses to write a connection's value into a sidecar.  When a database cannot be opened, the error says what went wrong in the tool's own words and names neither the host nor the user.  A short password, or a connection string typed in the wrong place on the command line, can still appear: see [limits](#limit-short-password).  The tool also takes the connection variables out of the environment of the `dotnet msbuild` it runs to read your projects, and never hands it the values of a `.env` file.  The connection string itself is the database's own: see connecting to [PostgreSQL](databases/postgresql/index.md#connecting) and to [SQL Server](databases/sqlserver/index.md#connecting).

<a id="env-file"></a>

## A `.env` file

```dotenv
# Connections of the sqlsource tool
SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
export SQLSOURCE_CONNECTION_REPORTS='Host=localhost;Database=reports;Username=app;Password=p#ss'

# Your application's own settings, which the tool passes over
ASPNETCORE_ENVIRONMENT=Development
```

A `.env` file keeps the connections of your machine in one place, so that you need not set them in each shell or type them on the command line.  Every run of `describe` reads it, `--check` and `--force` included.

- **Where it is found.**  The tool reads the file named `.env` in the folder of the solution or project it runs on, and no other.  It never looks in a folder above that one, so a `.env` at the root of your repository, or in your home folder, is read only when you name it, as below.  It reads one file, and never combines two.  When there is no file of that name, or only a folder, the run reads none, without a word.
- **A run on a solution reads the `.env` beside the solution**, and not one in a project's folder below it.  When such a run has no connection for a database, the `help:` line of [SQLSRC213](../diagnostics.md#sqlsrc213) names each `.env` that the run did not read in a project's folder, or in a folder between it and the solution's.  A run that has no such file says in that line that it reads the one file alone, and that `--env-file`, or `SqlSourceEnvFile` in a build, names another.
- **`--env-file <path>` names another file**, to `describe`, `describe --watch` and `diagnose`: `dotnet sqlsource describe src/App/App.csproj --env-file .env`, run at the root of your repository, reads the root's `.env`.  A relative path is taken from the current folder.  `--env-file none`, in upper or lower case, reads no file, so the connections come from the command line and the environment alone; `--env-file ./none` names a file called `none`.  A file that is not there, or a folder, gives no connection and no error.  An empty value is refused, and so is a path that holds `=` or `;`, as for `--log`.
- **A build reads the `.env` in the project's folder**, as a run on the project does, and [`SqlSourceEnvFile`](../settings.md#setting-env-file), a property of the project, names another, such as one `.env` at the root of your repository for every project.  It reaches only the build's own run: a `describe` you run yourself reads the `.env` of the folder of what it runs on, or the file `--env-file` names.
- **Only the connection variables are read**: `SQLSOURCE_CONNECTION` and the variables that start with `SQLSOURCE_CONNECTION_`, written in upper case: `sqlsource_connection_billing` is not one of them.  The rest of the file belongs to your application or to other tools, and the tool passes over each of its lines without a word, whatever it holds.
- **The environment wins**, variable by variable, as the table of [connections](#connections) shows.
- **`--verbose` names the file** that a connection came from, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`, and never the value.  So does a [log](cli.md#logs).
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
- **The file is UTF-8**, with or without a byte order mark, and its lines may end as on Windows or as on Linux and macOS.  A line of a connection variable that is not saved as UTF-8, as a connection string with an `ä` in a file saved as Windows-1252, is [SQLSRC286](../diagnostics.md#sqlsrc286); the other lines of the file may be in any encoding.

A line of a connection variable that cannot be read is [SQLSRC286](../diagnostics.md#sqlsrc286), at its line, and a file the system will not let the tool read is [SQLSRC285](../diagnostics.md#sqlsrc285).  Either way the run takes no connection from the file, not even from its good lines, and goes on with the other ways.

<a id="limits"></a>

## Limits

<a id="limit-short-password"></a>

### A short password, or a connection string in the wrong place, shows in the output or a log

- **A short password, or a connection string typed in the wrong place, can appear in the output or a log.**  The tool writes a connection value as `***` where it finds the whole value, and each password of it, or of `PGPASSWORD` or a PostgreSQL password file, where it stands apart from other letters, digits and underscores.  A password shorter than four characters is not hidden, nor is one that is the name of its logical database or of its dialect, such as `postgres`, words that the tool prints anyway.  A password that is hidden hides each word of a message that equals it, such as a column named like it, and every password of a PostgreSQL password file that the tool reads is hidden so, not only the one the connection uses.  A password file that is a pipe, such as `PGPASSFILE=<(...)` in a shell, is left for the driver to read, and its password is not hidden.  An error of SQL Server about a query can name the login, such as one for a database that the login may not open, and it is printed and logged as the server wrote it.  A connection string typed where a path or a `--project` goes, or after `--database` or `--connection` without its `name=`, is not printed.  In the log, every value of `--database` and `--connection` is written `***`, and so is a path, a value of `--project`, `--query` or `--scrub` included, that holds `=` or `;`; a `--log` or `--out` path that holds either is refused.  A secret with neither, typed alone where a path, a `--project` value or a `--log` path goes, is printed as a path and logged as typed.  Give each connection as `--connection <name>="..."` or in its variable, and search what you share, a log above all, for your password first.
