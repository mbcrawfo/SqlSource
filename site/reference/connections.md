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

The first block is a [`.env` file](#env-file) in the folder of the project or solution that the tool runs on.  It gives the database `billing` its connection.  The database `reports` gets its connection on the command line.

Each query that the tool describes belongs to a [logical database](settings.md#setting-database).  That is the name that the [database setting](settings.md#setting-database) gives the query, such as `billing`, or the default that the section of the setting gives.  The tool connects to each logical database through the connection that you give under that name.

You can give a connection in four ways.  The tool takes the connection from the first of these that gives one:

| Order | Source | For the database `billing` |
|----|----|----|
| 1 | `--connection <name>=<connection string>` on the command line | `--connection billing="Host=localhost;Database=billing"` |
| 2 | The environment variable of the name | `SQLSOURCE_CONNECTION_BILLING` |
| 3 | The variable of the name in a [`.env` file](#env-file) | `SQLSOURCE_CONNECTION_BILLING=...` in `.env` |
| 4 | `SQLSOURCE_CONNECTION`, from the environment, or else from the `.env` file | Used only when the run has exactly one database |

The environment wins over the `.env` file, variable by variable.  The file gives a variable only when the environment does not set it.  So `SQLSOURCE_CONNECTION_BILLING` in the file still wins over `SQLSOURCE_CONNECTION` in the environment.

- **The variable of a name is `SQLSOURCE_CONNECTION_`, then the name in upper case.**  In the name, every character that is not a letter or a digit is written as `_`.  So the database `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.
- **Two names that give one variable are [SQLSRC214](../diagnostics.md#sqlsrc214)**, such as `app-v2` and `app_v2`.  Give both on the command line, or rename one of the two.
- **`SQLSOURCE_CONNECTION` is for a project with one database.**  The tool uses it only when the run has exactly one database.  A database that is in the run only through queries with errors of their own does not count.  If the run has more than one database, the tool uses the variable for none of them ([SQLSRC215](../diagnostics.md#sqlsrc215)).  Then set the variable of each name instead.
- **A variable that is empty, or holds only white space, is not set.**  This is true in the environment and in the `.env` file.
- **A connection never goes in the project file or in a settings file**, because it holds credentials.  A settings file has no key for a connection.
- **Put the value of `--connection` in quotes.**  A shell splits a value at a space.
- **On a command line that has `--connection`, the tool refuses any other argument that holds `=` or `;`.**  Such an argument can be the rest of a connection string that the shell split.  If the argument is a project path or a `.sql` path that holds `=` or `;`, do one of these:
  - Run the command from the folder of that file, and give the name of the file alone.
  - Give the connection in its variable instead.
- **A database that needs nothing described needs no connection.**  If every query of a database has a current entry in its sidecar, the tool asks the database nothing.  If that database also has no connection, its summary line says `no connection`.
- **`describe --force` and `describe --check` describe every query.**  So with these, every database of the run needs a connection.
- **A database that has a query to describe and no connection is [SQLSRC213](../diagnostics.md#sqlsrc213).**  The `help:` line of the error names these:
  - the option;
  - the variable to set;
  - the `.env` file to set it in, or that the run reads no `.env` file;
  - each name of a `--connection` that matches no database of the run but is close to one, so that you see a misspelt name.
- **The `help:` line never prints a `--connection` name that is far from every database of the run.**  It counts that value by its position on the command line.  Such a name can be the start of a connection string typed without its name.

**What the tool hides.**  The tool keeps connection strings to itself as far as it can, wherever they came from.  In what it prints, and in what it writes to a [log](cli.md#logs), it writes `***` in the place of these:

- the whole value of a connection, wherever the tool finds it;
- each password of the connection string, where it stands apart from other letters, digits and underscores;
- in the same way, a password that the PostgreSQL driver reads from `PGPASSWORD` or a password file, when the connection string gives none.

The tool finds the whole value as you wrote it, without the white space at its ends, or as the database driver writes it again.

The tool also does these things with a connection:

- **It refuses to write the value of a connection into a sidecar.**
- **If a database cannot be opened, the error says what went wrong in the tool's own words.**  It names neither the host nor the user.
- **It takes the connection variables out of the environment of the `dotnet msbuild` that it runs to read your projects.**  It never hands that `dotnet msbuild` the values of a `.env` file.

A short password, or a connection string typed in the wrong place on the command line, can still appear: see [the limit](#limit-short-password).  The connection string itself is the database's own: see connecting to [PostgreSQL](databases/postgresql/index.md#connecting) and to [SQL Server](databases/sqlserver/index.md#connecting).

<a id="env-file"></a>

## A `.env` file

```dotenv
# Connections of the sqlsource tool
SQLSOURCE_CONNECTION_BILLING="Host=localhost;Database=billing;Username=app;Password=secret"
export SQLSOURCE_CONNECTION_REPORTS='Host=localhost;Database=reports;Username=app;Password=p#ss'

# Your application's own settings, which the tool passes over
ASPNETCORE_ENVIRONMENT=Development
```

A `.env` file keeps the connections of your machine in one place.  Then you do not set them in each shell, or type them on the command line.  Every run of `describe` reads the file, `describe --check` and `describe --force` included.

- **The tool reads the file named `.env` in the folder of the solution or project that it runs on, and no other.**  It never looks in a folder above that folder.  So the tool reads a `.env` at the root of your repository, or in your home folder, only when you name that file.  You name it with `--env-file` or, for a build, `SqlSourceEnvFile`.
- **The tool reads one file, and never combines two.**
- **If there is no file of that name, or only a folder, the run reads no file, and says nothing.**
- **A run on a solution reads the `.env` beside the solution**, and not one in the folder of a project below it.
- **If a run on a solution has no connection for a database, [SQLSRC213](../diagnostics.md#sqlsrc213) names each `.env` that the run did not read.**  Its `help:` line names each `.env` in the folder of a project that has a query of that database to describe.  It also names each `.env` in a folder between that folder and the folder of the solution.  If there is no such file, the line says that the run reads the one file alone.  The line then also says that `--env-file`, or `SqlSourceEnvFile` in a build, names another.
- **`--env-file <path>` names another file**, for `describe`, `describe --watch` and `diagnose`.  For example, `dotnet sqlsource describe src/App/App.csproj --env-file .env`, run at the root of your repository, reads the `.env` of the root.
- **A relative path of `--env-file` is from the current folder.**
- **`--env-file none` reads no file.**  `none` can be in upper or lower case.  Then the connections come from the command line and the environment alone.  To name a file called `none`, write `--env-file ./none`.
- **If the path of `--env-file` is a folder, or no file is there, the run gets no connection from it, and reports no error.**
- **The tool refuses an `--env-file` value that is empty or holds only white space.**  It also refuses a path that holds `=` or `;`, as it does for `--log`.
- **A build reads the `.env` in the folder of the project**, as a run on the project does.  The property [`SqlSourceEnvFile`](msbuild.md#setting-env-file) names another file, only for the build's own run.
- **The tool reads only the connection variables**: `SQLSOURCE_CONNECTION`, and the variables that start with `SQLSOURCE_CONNECTION_`.  They are written in upper case: `sqlsource_connection_billing` is not one of them.
- **The tool passes over every other line of the file without a word, whatever it holds.**  The rest of the file belongs to your application or to other tools.  One line is not passed over.  A line that names a connection variable but cannot be read is [SQLSRC286](../diagnostics.md#sqlsrc286).  Such a line can set a name that starts with `SQLSOURCE_CONNECTION` and holds a character other than a letter, a digit or `_`, such as `SQLSOURCE_CONNECTION-A`.
- **The environment wins over the file**, variable by variable, as the table of [connections](#connections) shows.
- **`--verbose` names the file that a connection came from, and never the value**, as `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`.  A [log](cli.md#logs) names the file in the same way.
- **Keep the file out of git**, because it holds credentials.  Add `.env` to your `.gitignore`.  To show others what to set, commit a `.env.example` file with the variables and no passwords.  Each person copies it to `.env`.
- **The tool keeps the values of the file as it keeps the other connections.**  It writes them as `***` in what it prints and logs, and refuses them in a sidecar.  It never hands them to the `dotnet msbuild` that it runs, nor to anything else.

How a line is written:

- **One variable on a line**, as `NAME=value`.  The tool ignores white space around the name and the `=`.
- **A line can start with `export `**, as in a shell script.
- **The tool passes over a blank line, and a line whose first character other than white space is `#`.**
- **A value without quotes loses the white space at its ends.**  It ends at a `#` that follows white space, because that `#` starts a comment.  So `Password=a#b` keeps its `#`, and `Password=a #b` ends at `a`.
- **A value in single quotes is taken exactly as written.**  It can hold `#`, `"` and `\`, but not `'`.  It is the safest form for a connection string.
- **A value in double quotes takes the escapes `\\`, `\"`, `\n`, `\r` and `\t`, and no other.**  Write any other backslash as `\\`.
- **After a closing quote, only white space can follow**, or white space and a comment.
- **A value is on one line.**  A quote that is not closed on its line is an error.
- **`${NAME}` is not replaced by the value of another variable.**  It stays as written.
- **A variable set twice** takes the value of its last line.
- **The file is UTF-8**, with or without a byte order mark.  Its lines can end as on Windows, or as on Linux and macOS.
- **A line of a connection variable that is not saved as UTF-8 is [SQLSRC286](../diagnostics.md#sqlsrc286).**  An example is a connection string with an `ä` in a file saved as Windows-1252.  The other lines of the file can be in any encoding.

If a line of a connection variable cannot be read, the tool reports [SQLSRC286](../diagnostics.md#sqlsrc286) at that line.  If the system does not let the tool read the file, the tool reports [SQLSRC285](../diagnostics.md#sqlsrc285).  In both cases the run takes no connection from the file, not even from its good lines.  The run goes on with the other ways to give a connection.

<a id="limits"></a>

## Limits

<a id="limit-short-password"></a>

### A short password, or a password typed in the wrong place, shows in the output or a log

**What you see.**  A short password, or a connection string typed in the wrong place, can appear as written in the output or in a log.  These can appear:

- a password shorter than four characters;
- a password that is the name of its logical database or of its dialect, such as `postgres`;
- the password of a PostgreSQL password file that is a pipe, such as `PGPASSFILE=<(...)` in a shell;
- the login, in an error of SQL Server about a query, such as an error for a database that the login may not open;
- a secret that holds neither `=` nor `;`, typed alone where a path goes, or as the value of `--log`, `--env-file` or `--out`.  The tool prints it as a path, and logs it as typed.  A path goes in these places: the path of `describe` or `diagnose`, a `.sql` path, and a value of `--project`, `--query` or `--scrub`.

You can also see `***` in the place of a word that is not a secret, such as the name of a column.  That word equals a password that the tool hides.

**Why.**  The tool writes a connection value as `***` wherever it finds the whole value.  It writes each password of the value as `***` only where the password stands apart from other letters, digits and underscores.  It does the same for a password from `PGPASSWORD` or a PostgreSQL password file.  It hides every password of a password file that it reads, not only the one that the connection uses.  The tool leaves these passwords as they are:

- a password shorter than four characters, which would match too much ordinary text;
- a password that is the name of its logical database or of its dialect, because the tool prints these words anyway;
- the password of a password file that is a pipe, because the tool leaves that file for the driver to read.

A password that the tool hides hides each word of a message that equals it, such as a column named like it.

SQL Server can name the login in an error about a query.  The tool prints and logs such an error as the server wrote it.

On the command line, the tool guards against a connection string typed in the wrong place:

- It does not print a connection string typed where a path goes, or as the value of `--project`, `--query`, `--scrub` or `--out`.
- It does not print a connection string typed after `--database`.
- It does not print a connection string typed after `--connection` without its `name=`, except for its first word.  It prints that word, such as `Host`, only when the word is close to the name of a database of the run.  Nothing after the first `=` is printed.
- In the log, it writes `***` for every value of `--database` and `--connection`.
- In the log, it writes `***` for a path that holds `=` or `;`, a value of `--project`, `--query` or `--scrub` included.
- It refuses a `--log`, `--env-file` or `--out` path that holds `=` or `;`.

To the tool, a secret that holds neither `=` nor `;` is a path like any other.

**What to do.**

1. Give each connection as `--connection <name>="..."`, or in its variable.
2. Before you share what the tool printed, or a log above all, search it for your password.
