# Bug-report bundles (diagnose, replay)

To report a query that gets a wrong model, run `diagnose` on that query where you run `describe`.  Attach the zip that it writes to a bug report, and keep the mapping beside the zip to yourself.  For the steps, see [report a wrong model](../guides/report-a-bug.md).

<a id="bundle-contents"></a>

## What is renamed before you share it

A bundle holds these:

- the query, as a `.sql` file of its own, with its dialect and its logical database written in it;
- what your database answered when the tool described the query;
- what SqlSource made of that answer, or the error that refused the query;
- the versions of `sqlsource`, of the server and its driver, of .NET and of the operating system.

A bundle holds none of these:

- your other queries;
- your settings files;
- your code;
- your connection string;
- a log.

The `.sql` file of a bundle leaves out each marker that changes no description, such as `-- summary:`, `-- column:` and `-- output:`.  A `-- param:` line keeps the type and the nullability of its query parameter, and leaves out its C# type.

- **Every name of yours gets a new name.**  These are your names:
  - the schemas, tables, views, columns, aliases, functions, types and indexes of your database;
  - the name of your database;
  - the logical database;
  - the name of the query;
  - its query parameters;
  - its tokens;
  - any other name in your SQL, such as the name of another database or of a linked server.
- **A new name is a prefix for what it names, then eight hexadecimal digits or more.**  An example is `t_4f0a9c21` for a table.  A spelling has the same new name throughout the bundle.
- **Under SQL Server, two spellings of one name that differ by case alone usually get new names that differ by case alone.**  So `Email` and `EMAIL` get `c_4f0a9c21` and `C_4f0a9c21`.  If a name is written in three or more cases, one of them can now and then get a new name of its own.
- **Token names are case-sensitive under either database.**  So `{{Extra}}` and `{{extra}}` get two new names.
- **Every string literal gets a new one, of the same form.**  `'active'` becomes `'l_9b3e07d4'`.
- **The label of an enum is renamed as the literal of the same spelling**, so the two still match.
- **A comment is left empty.**
- **An error of the server keeps only these parts:**
  - its code: the SQLSTATE of PostgreSQL, or the error number of SQL Server;
  - under SQL Server, the other numbers of the error, such as its severity and its state;
  - under PostgreSQL, the position in the query of an error that the server found when it first read the query, moved to the same place in the renamed query;
  - the query parameters that it names, renamed.

  Every other part is left out, such as its message, which can quote your names and values.
- **Each run picks its new names at random.**  So two bundles of one schema share no new name.  And no one can check a guess at what a new name stands for.
- **Numbers are kept.**  A number in a query is as often a length, a precision or a `LIMIT` as a value.  Another number could change what the database decides.
- **A name of yours that is spelled as a keyword or as a word of the database is kept, wherever it stands.**  Examples are a column `name`, `date`, `count`, `type` or `value`, and an alias `old`, `new` or `left`.  SqlSource cannot tell such a name from the word of the database.  If SqlSource renamed such names, it could not rename many everyday queries.
- **Under PostgreSQL, the name of a token is kept only when it is written as the database writes the word.**  For a keyword, that is lower case.  So `{{table}}` is kept, and `{{Table}}` is renamed.
- **The names of the database itself are kept.**  These include:
  - its built-in functions, operators and types;
  - its own schemas, such as `pg_catalog`, `information_schema` and `public`, or `sys`, `INFORMATION_SCHEMA` and `dbo`;
  - its other names, such as the system databases `master` and `tempdb` of SQL Server, and the system columns `ctid` and `xmin` of PostgreSQL.
- **A logical database that is named for its dialect is kept**: `postgres` or `mssql`.

Before `diagnose` writes the bundle, it checks two things:

- Every word in the bundle is a new name, a word of the database, a number or punctuation.
- The query, run from the renamed bundle as `replay` runs it, gives the answer that your database gave.

So a bundle that `diagnose` writes holds no name, literal or value of yours but the ones that the rules above keep.  And it shows the problem as you saw it.  If a number in your query is a value that you cannot share, such as an account number written into its SQL, take the [private route](../guides/report-a-bug.md#raw-bundle) instead.

<a id="diagnose"></a>

## Making a bundle with `diagnose`

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
$ dotnet sqlsource diagnose App.slnx --project src/App/App.csproj --query src/App/Queries/Users.sql:GetUser --out ../report.zip
$ dotnet sqlsource diagnose --scrub ../report.zip --out ../report-renamed.zip
```

`diagnose` writes the bug-report bundle of one query.  The first two commands make a bundle of the query `GetUser`.  The second names a solution, and one project of it.  The third renames a raw bundle.  For what a bundle holds, see [what is renamed before you share it](#bundle-contents).  For what `diagnose` prints, see [reporting a wrong model](../guides/report-a-bug.md#report-a-bug).

| Option | What it does |
|----|----|
| `--query <file>:<name>` | The query to report: its `.sql` file, which a type of the run must claim ([SQLSRC212](../diagnostics.md#sqlsrc212)), a colon, and its name as its `-- name:` line gives it ([SQLSRC292](../diagnostics.md#sqlsrc292)).  The last colon ends the file, so `C:\App\Queries\Users.sql:GetUser` reads.  A relative path is taken from the current folder |
| `--out <path>` | Required, with `--scrub` too.  Where to write the bundle, a zip; its mapping is written beside it, with `.mapping.json` added to its name.  A file there is overwritten, and a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, or holds `=` or `;`, is refused.  A path that may be a secret is named by its position: see [how an error names a path](cli.md#paths) |
| `--no-obfuscate` | Writes a raw bundle, with your names and SQL as they are, and no mapping: see [when a bundle cannot be made safe](../guides/report-a-bug.md#raw-bundle) |
| `--scrub <bundle>` | Renames a raw bundle, with no database, no project and no connection, and writes it and its mapping at `--out`.  It takes no other option.  A bundle renamed already is [SQLSRC295](../diagnostics.md#sqlsrc295), and one that cannot be read, or that another version made and that does not answer what this one asks ([SQLSRC290](../diagnostics.md#sqlsrc290)), is an error as for [`replay`](#replay); one whose answer this version does not reproduce is [SQLSRC296](../diagnostics.md#sqlsrc296), and nothing is written |
| `<path>`, `--project`, `--connection`, `--env-file`, `--verbose`, `--log` | As for `describe`: see [arguments](cli.md#arguments) and [options](cli.md#options).  `<path>` names the project or solution alone, since `--query` names the one `.sql` file |

- **`diagnose` has none of the other options of `describe`.**
- **`diagnose` takes its connections from the same places as `describe`.**  See [connections](connections.md#connections).
- **`SQLSOURCE_CONNECTION` gives the connection of the logical database of the query, even in a run with several logical databases.**  The reason: `diagnose` describes one query.
- **The exit code is `0` when `diagnose` wrote the bundle and, unless the bundle is raw, its mapping.**  This includes the bundle of a refused query.  In every other case, the exit code is `1`.

<a id="replay"></a>

## Checking a bundle with `replay`

```console
$ dotnet sqlsource replay report.zip
sqlsource: mssql.table-match: query="UsersWithOrders" tableMatch="several-tables" table=null
sqlsource: reproduced: the pipeline gives this answer
```

A bug-report bundle holds one query, what its database answered when the query was described, and what SqlSource made of that answer.  `replay` runs the query through SqlSource again from the bundle alone.  Then it says whether SqlSource gives the same answer.

- **Give `replay` the zip file, or the folder that you unzipped it into.**
- **`replay` needs no database, no connection, no project and no `.env` file.**  Of the [requirements](cli.md#requirements), it needs only .NET 8 or later.  So you can check a bundle that someone gave you on any machine with the tool.
- **Run `replay` on a bundle that [`diagnose`](../guides/report-a-bug.md#report-a-bug) made for you, too, before you attach it.**  It shows what a maintainer sees.  The lines of a renamed bundle give its new names, and its mapping turns them back into yours.
- **Before the outcome, `replay` prints a line for each decision that it made.**  These are the decisions about the columns of the query, and about the table that its rows match.  Each line starts with `sqlsource:` and the name of the database engine of the bundle.
- **With `--verbose`, `replay` also prints a line for everything else that it did.**
- **`--log <path>`, or `SQLSOURCE_LOG`, writes a record of the replay to a file.**  The rules are the same as for `describe`: see [logs](cli.md#logs).
- **`--verbose` and `--log` are the only options of `replay`.**

`replay` ends with one of three outcomes:

- **`sqlsource: reproduced: the pipeline gives this answer`.**  This version gives the answer that the bundle holds: the same columns, types and nullability, and everything else that a sidecar records for the query.  If that answer is wrong, the bundle shows SqlSource giving it with no database.  The exit code is `0`.
- **`sqlsource: not reproduced`**, then a line for each difference, the value of the bundle first.  Then a line names the version of `sqlsource` that made the bundle, beside yours.  A newer version can decide otherwise.  To see the answer of the bundle, install the version that made it.  The exit code is `2`.

  ```console
  $ dotnet sqlsource replay report.zip
  sqlsource: not reproduced
  sqlsource:   columns[2].nullable: true -> false
  sqlsource: the bundle was made by sqlsource 1.4.0, sidecar format 1; this is sqlsource 1.6.0, sidecar format 1
  ```

- **`sqlsource: replayed:` and an answer**, for a bundle that holds no answer of its own to compare with.  The answer is the entry of the query as a sidecar holds it, on one line.  Or it is the error that refused the query, as `sqlsource: replayed: SQLSRC226 at describe parameters`.  The exit code is `0`.

More rules of the outcome:

- **A bundle of a query that SqlSource refused holds the error, and the step that the error was reported at.**  Then `reproduced` means that this version refuses the query with the same error at the same step.  A difference reads `expected SQLSRC231, replayed SQLSRC226 at describe parameters`.
- **Columns are counted from 0**, so `columns[2]` is the third.

If a bundle holds the names and the SQL of the person who made it, `replay` says so first, with `sqlsource: this bundle is not renamed, and must not be posted`.  A raw bundle of yours holds them, and so does a bundle sent to you privately.  Never attach such a bundle to an issue, and never post it anywhere:

- A raw bundle of yours goes to a maintainer by [the private route](../guides/report-a-bug.md#raw-bundle).
- A raw bundle sent to you stays with you.

`replay` prints a control character of a text from a bundle as its code.  An example is the escape that starts a sequence that a terminal acts on, or a line break.  So a bundle from someone else cannot change what your terminal shows:

- In a message, the escape is `\u001B`, and a line break is `\u000A`.
- Inside a quoted value, a control character is written as JSON writes it: `\n` for a line break, and `\t` for a tab.
- A tab outside a quoted value is printed as it is.

A bundle that cannot be read or replayed is an error, from [SQLSRC288](../diagnostics.md#sqlsrc288) to [SQLSRC291](../diagnostics.md#sqlsrc291).  The error names the bundle at its full path, and the exit code is `1`.  If the path holds `=` or `;`, the error names it as `The path at position <n>` instead, on a line labelled `bundle`.  The tool names each value of the command line that can be a secret in this way.
