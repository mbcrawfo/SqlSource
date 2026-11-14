# Bug-report bundles (diagnose, replay)

For the steps to make a bundle and send it with a bug report, see [report a wrong model](../guides/report-a-bug.md).

<a id="bundle-contents"></a>

## What is renamed before you share it

A bundle holds the one query, as a `.sql` file of its own with its dialect and logical database written in it, what your database answered when the tool described the query, what SqlSource made of that answer, or the error that refused the query, and the versions of `sqlsource`, of the server and its driver, of .NET and of the operating system.  It holds none of your other queries, your settings files, your code, your connection string or a log.  The markers that change no description, such as `-- summary:`, `-- column:` and `-- output:`, are left out of its `.sql` file, and a `-- param:` line keeps its query parameter's type and nullability and leaves out its C# type.

- **Every name of yours gets a new name**: the schemas, tables, views, columns, aliases, functions, types and indexes of your database, its name, the logical database, the query's own name, its query parameters and its tokens.  A new name is a prefix for what it names and eight hexadecimal digits or more, `t_4f0a9c21` for a table, and a spelling has the same new name throughout the bundle.  Under SQL Server, two spellings of one name that differ by case alone, such as `Email` and `EMAIL`, usually get new names that differ by case alone, `c_4f0a9c21` and `C_4f0a9c21`; a name written in three or more cases may now and then give one of them a new name of its own.  Token names are case-sensitive under either database, so `{{Extra}}` and `{{extra}}` get two new names.
- **Every string literal gets a new one**, of the same form: `'active'` becomes `'l_9b3e07d4'`, and an enum's label is renamed as the literal of the same spelling, so the two still match.  A comment is left empty.
- **A server's error keeps only its code**, the SQLSTATE or SQL Server's error number, and the query parameters it names, renamed.  Its message, which can quote your names and values, is left out.
- **Each run picks its new names at random**, so two bundles of one schema share no new name, and no one can check a guess at what a new name stands for.
- **Numbers are kept**, since a number in a query is as often a length, a precision or a `LIMIT` as a value, and another number could change what the database decides.
- **A name of yours that is spelled as a keyword or a word of the database's own is kept**, wherever it stands: a column `name`, `date`, `count`, `type` or `value`, or an alias `old`, `new` or `left`.  Under PostgreSQL a token's name is kept only when it is written as the database writes the word, in lower case for a keyword, so `{{table}}` is kept and `{{Table}}` renamed.  SqlSource cannot tell such a name from the database's word, and renaming it would make many everyday queries impossible to rename.  The database's own names stay too: `pg_catalog`, `public`, `sys` and `dbo`, and its built-in functions, operators and types, as does a logical database named for its dialect, `postgres` or `mssql`.

Before it writes the bundle, `diagnose` checks that every word in it is a new name, a word of the database's, a number or punctuation, and runs the query from the renamed bundle, as `replay` does, to see that it gives the answer your database gave.  So a bundle it writes holds no name, literal or value of yours but those kept above, and shows the problem as you saw it.  If a number in your query is a value you may not share, such as an account number written into its SQL, take the [private route](../guides/report-a-bug.md#raw-bundle) instead.

<a id="diagnose"></a>

## Making a bundle with `diagnose`

```console
$ dotnet sqlsource diagnose --query Queries/Users.sql:GetUser --out ../report.zip
$ dotnet sqlsource diagnose App.slnx --project src/App/App.csproj --query src/App/Queries/Users.sql:GetUser --out ../report.zip
$ dotnet sqlsource diagnose --scrub ../report.zip --out ../report-renamed.zip
```

`diagnose` writes the bug-report bundle of one query: see [what is renamed before you share it](#bundle-contents) for what it holds, and [reporting a wrong model](../guides/report-a-bug.md#report-a-bug) for what it prints.

| Option | What it does |
|----|----|
| `--query <file>:<name>` | The query to report: its `.sql` file, which a type of the run must claim ([SQLSRC212](../diagnostics.md#sqlsrc212)), a colon, and its name as its `-- name:` line gives it ([SQLSRC292](../diagnostics.md#sqlsrc292)).  The last colon ends the file, so `C:\App\Queries\Users.sql:GetUser` reads.  A relative path is taken from the current folder |
| `--out <path>` | Required, with `--scrub` too.  Where to write the bundle, a zip; its mapping is written beside it, with `.mapping.json` added to its name.  A file there is overwritten, and a path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`, or holds `=` or `;`, is refused |
| `--no-obfuscate` | Writes a raw bundle, with your names and SQL as they are, and no mapping: see [when a bundle cannot be made safe](../guides/report-a-bug.md#raw-bundle) |
| `--scrub <bundle>` | Renames a raw bundle, with no database, no project and no connection, and writes it and its mapping at `--out`.  It takes no other option.  A bundle renamed already is [SQLSRC295](../diagnostics.md#sqlsrc295), and one that cannot be read, or that another version made and that does not answer what this one asks ([SQLSRC290](../diagnostics.md#sqlsrc290)), is an error as for [`replay`](#replay); one whose answer this version does not reproduce is [SQLSRC296](../diagnostics.md#sqlsrc296), and nothing is written |
| `<path>`, `--project`, `--connection`, `--env-file`, `--verbose`, `--log` | As for `describe`: see [arguments](cli.md#arguments) and [options](cli.md#options).  `<path>` names the project or solution alone, since `--query` names the one `.sql` file |

It has none of `describe`'s other options.  Its connections come from the same places as `describe`'s ([connections](connections.md#connections)), and `SQLSOURCE_CONNECTION` gives the connection of the query's logical database even in a run with several, since `diagnose` describes one query.  The exit code is `0` when it wrote the bundle, a refused query's included, and its mapping unless the bundle is raw, and `1` otherwise.

<a id="replay"></a>

## Checking a bundle with `replay`

```console
$ dotnet sqlsource replay report.zip
sqlsource: mssql.table-match: query="UsersWithOrders" tableMatch="several-tables" table=null
sqlsource: reproduced: the pipeline gives this answer
```

A bug-report bundle holds one query, what its database answered when the query was described, and what SqlSource made of it.  `replay` runs the query through SqlSource again from the bundle alone, and says whether it gives the same answer.  Give it the zip file, or the folder you unzipped it into.  It needs no database, no connection, no project and no `.env` file, and none of the [requirements](cli.md#requirements) but .NET 8 or later, so you can check a bundle that someone gave you on any machine with the tool.  Run it on a bundle that [`diagnose`](../guides/report-a-bug.md#report-a-bug) made for you, too, before you attach it: it shows what a maintainer will see.  The lines of a renamed bundle give its new names, which its mapping turns back into yours.

Before its outcome, it prints a line for each decision it made about the query's columns and the table its rows match, each starting with `sqlsource:` and the name of the bundle's database engine.  With `--verbose` it prints a line for everything else it did too.  `--log <path>`, or `SQLSOURCE_LOG`, writes a record of the replay to a file, with the same rules as for `describe` ([logs](cli.md#logs)).  These are its only options.

It ends with one of three outcomes:

- **`sqlsource: reproduced: the pipeline gives this answer`.**  This version gives the answer the bundle holds, the same columns, types, nullability and everything else a sidecar records for the query.  If that answer is wrong, the bundle shows SqlSource giving it with no database.  The exit code is `0`.
- **`sqlsource: not reproduced`**, then a line for each difference, the bundle's value first, then a line that names the version of `sqlsource` that made the bundle beside yours.  A newer version may decide otherwise; to see the bundle's answer, install the version that made it.  The exit code is `2`.

  ```console
  $ dotnet sqlsource replay report.zip
  sqlsource: not reproduced
  sqlsource:   columns[2].nullable: true -> false
  sqlsource: the bundle was made by sqlsource 1.4.0, sidecar format 1; this is sqlsource 1.6.0, sidecar format 1
  ```

- **`sqlsource: replayed:` and an answer**, for a bundle that holds no answer of its own to compare with: the query's entry as a sidecar holds it, on one line, or the error that refused the query, as `sqlsource: replayed: SQLSRC226 at describe parameters`.  The exit code is `0`.

A bundle of a query that SqlSource refused holds the error and the step it was reported at.  `reproduced` then means that this version refuses the query with the same error at the same step, and a difference reads `expected SQLSRC231, replayed SQLSRC226 at describe parameters`.  Columns are counted from 0, so `columns[2]` is the third.

A bundle that holds its maker's own names and SQL, as a raw bundle of yours or one sent to you privately does, says so first, with `sqlsource: this bundle is not renamed, and must not be posted`.  Never attach it to an issue or post it anywhere: a raw bundle of yours goes to a maintainer by [the private route](../guides/report-a-bug.md#raw-bundle), and one sent to you stays with you.

A control character in a text that `replay` prints from a bundle, such as the escape that starts a sequence a terminal would act on, or a line break, is printed as its code, so that a bundle from someone else cannot change what your terminal shows: `\u001B` for the escape and `\u000A` for a line break in a message, and inside a quoted value as JSON writes it, `\n` for a line break and `\t` for a tab.  A tab outside a quoted value is printed as it is.

A bundle that cannot be read or replayed is an error, from [SQLSRC288](../diagnostics.md#sqlsrc288) to [SQLSRC291](../diagnostics.md#sqlsrc291), at its full path, and the exit code is `1`.  A path that holds `=` or `;` is named as `The path at position <n>` instead, on a line labelled `bundle`, as a value of the command line that may be a secret is.
