# Dialects

Databases disagree about where a comment or a string ends.  SqlSource removes comments from your SQL, so it has to know which rules your SQL follows.  This line, read by three dialects, is three different statements:

```sql fragment
SELECT 1 # x [y--z] -- c
```

| Dialect | The SQL that is kept | Why |
|----|----|----|
| `ansi` | `SELECT 1 # x [y` | `#` is SQL, `[` is SQL, and the first `--` starts a comment |
| `mysql` | `SELECT 1` | `#` starts a comment |
| `mssql` | `SELECT 1 # x [y--z]` | `[y--z]` is a quoted identifier, and the last `--` starts a comment |

The [dialect](concepts.md#dialect) is the set of rules that SqlSource reads a `.sql` file by.  It connects to nothing and is not the [logical database](concepts.md#logical-database).  This page covers how to choose one, its options, what each dialect changes, and what SqlSource still reads differently from the database.  Where a dialect is set, and which place wins, is on [the settings page](settings.md#setting-dialect).

<a id="choosing-a-dialect"></a>

## Choosing a dialect

| Dialect | Also accepted | Use it for |
|----|----|----|
| `ansi` | | The default.  Any database without a dialect of its own, such as Db2 |
| `mssql` | `sqlserver`, `tsql` | SQL Server, Azure SQL |
| `postgres` | `postgresql` | PostgreSQL, DuckDB |
| `cockroachdb` | `cockroach` | CockroachDB |
| `mysql` | | MySQL |
| `mariadb` | | MariaDB |
| `sqlite` | | SQLite |
| `oracle` | | Oracle, Firebird |

Names are not case-sensitive.

Choose the dialect named for your database.  A database that has no dialect of its own may be read correctly by one that has: the last column says which, such as `postgres` for DuckDB, whose files then need the output `sql` (see [database support](#database-support)).  For any other, use `ansi`.

`ansi` is the default, so a project that sets nothing is read by it.  It reads the SQL of every database by one set of rules, and misreads each construct in [the table below](#what-a-dialect-changes) that it says No to and your database says Yes to.  It also reads the quoting forms of PostgreSQL and MySQL that are rarely anything else: `` `...` ``, `$tag$...$tag$` and `E'...'`.  The fix for a misread is to set the dialect.

The dialect also decides which databases the tool can ask about a query: see [database support](#database-support).

<a id="dialect-options"></a>

## Options

```sql mysql,no-backslash-escapes
-- dialect: mysql, no-backslash-escapes

-- name: GetPath
SELECT 'C:\temp\' AS path;
```

MySQL and MariaDB have SQL modes that change how a string is read.  If your server runs with one, name it after the dialect, with a comma:

| Option | Also accepted | SQL mode | What it changes |
|----|----|----|----|
| `ansi-quotes` | `ansi_quotes` | `ANSI_QUOTES` | `"..."` is a quoted identifier, and a backslash does not escape in it |
| `no-backslash-escapes` | `no_backslash_escapes` | `NO_BACKSLASH_ESCAPES` | A backslash does not escape in `'...'` or in `"..."` |

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "mysql,ansi-quotes",
}
```

```jsonc
// Legacy/sqlsource.json
{
    "dialect": "mariadb,ansi-quotes,no-backslash-escapes",
}
```

- **Options are not case-sensitive** and come in any order.  Spaces around the name and each option are ignored.
- **Only `mysql` and `mariadb` have options.**  An option of another dialect, or one that does not exist, is the same error as a name that is not a dialect, and so is an empty one, as in `mysql,`.  Errors by place are on [the settings page](settings.md#setting-dialect).
- **In a `-- dialect:` marker, the value is the rest of the line**: the name, then the options, separated by commas.  Nothing else may follow it on the line, a comment included.
- **A value written in several places**, options included, follows [the rules of the dialect setting](settings.md#setting-dialect).

<a id="what-a-dialect-changes"></a>

## What each dialect changes

| | `ansi` | `mssql` | `postgres` | `cockroachdb` | `mysql` | `mariadb` | `sqlite` | `oracle` |
|----|----|----|----|----|----|----|----|----|
| A backslash escapes in `'...'` and `"..."` | No | No | No | No | Yes | Yes | No | No |
| A backslash escapes in `E'...'` | Yes | No | Yes | Yes | Yes | Yes | No | No |
| `` `...` `` is a quoted identifier | Yes | No | No | No | Yes | Yes | Yes | No |
| `[...]` is a quoted identifier | No | Yes | No | No | No | No | Yes | No |
| `$tag$...$tag$` is a string | Yes | No | Yes | Yes | Yes | No | No | No |
| `q'[...]'` is a string | No | No | No | No | No | No | No | Yes |
| A `/*` inside a block comment needs its own `*/` | Yes | Yes | Yes | Yes | No | No | No | No |
| `--` is a comment with no white space after it | Yes | Yes | Yes | Yes | No | No | Yes | Yes |
| `#` starts a comment | No | No | No | No | Yes | Yes | No | No |
| Kept as hints, besides `/*+ ... */` and `/*! ... */` | | | | | | `/*M! ... */` | | `--+ ...` |
| A name declared with `DECLARE @name` is a local variable, not a parameter ([declared variables](parameters-and-tokens.md#declared-variables)) | No | Yes | No | No | No | No | No | No |

Four details:

- In `mssql` a `]]` inside brackets stands for one `]`.  In `sqlite` the first `]` ends the identifier.
- In `mysql` and `mariadb` an option changes the first row (see [options](#dialect-options)).
- In `postgres` and `cockroachdb` an `E'...'` string that is continued on the next line is one string, and each later part takes backslash escapes too.  In `postgres`, `--` comments may stand between the parts, as PostgreSQL allows, and they are removed like any other comment.  In `cockroachdb` only white space may.
- In `cockroachdb` a bytes literal, `b'...'`, takes backslash escapes as an `E'...'` string does.  So does `b'...'` in `mysql` and `mariadb`, where every string does.

<a id="markers-under-mysql"></a>

### Markers under `mysql` and `mariadb`

A [marker](concepts.md#marker) is a `--` comment, so under `mysql` and `mariadb` it needs white space after the two dashes, as those databases need it: `-- name: GetUser` is a marker, and `--name: GetUser` is not a comment at all.  A `#` comment is never a marker, even where `#` starts a comment: `# name: GetUser` is a comment.

<a id="still-read-differently"></a>

## What is still read differently

SqlSource reads these differently from the database, whatever the dialect:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`, so the quote after it opens a string that is never closed: the error [SQLSRC101](diagnostics.md#sqlsrc101).  Where the server ends the comment depends on the server's version |
| A block comment that is still open at the end of the file | SQLite | The error [SQLSRC102](diagnostics.md#sqlsrc102).  SQLite accepts it, but it would take every later query of the file with it |
| A file whose lines end with a carriage return alone, with no line feed | MySQL, SQLite, CockroachDB | A line ends there, as in every dialect.  These databases end a `--` comment only at a line feed |
| A client command that is not SQL: `DELIMITER`, `GO`, SQL*Plus `PROMPT` and `REM`, a psql `\` command | All | As SQL, so a quote in it can open a string |
| A name that is not a parameter to the database: a user variable, `@x` for an absolute value, the parameter of a procedure that the SQL creates | MySQL, MariaDB, PostgreSQL, SQL Server | As a parameter: see [names that are read as parameters](parameters-and-tokens.md#names-read-as-parameters) |

A misread has one of two results:

- **A build error** that reports an unclosed quote or comment ([SQLSRC101](diagnostics.md#sqlsrc101), [SQLSRC102](diagnostics.md#sqlsrc102)) in SQL that is valid for your database.  `keep-comments` does not help.
- **Missing SQL.**  The misread quotes happen to balance, and the SQL after them is taken for a comment and removed: under `ansi`, `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  Nothing is reported.  `keep-comments` on the query prevents the removal.

If your SQL uses one of these constructs, check the generated SQL: hover over the member, or read its documentation.

<a id="database-support"></a>

## Database support

| Database | Dialect | Can be described | Page |
|----|----|----|----|
| PostgreSQL | `postgres` | Yes | [PostgreSQL](postgresql.md) |
| SQL Server, Azure SQL | `mssql` | Yes | [SQL Server](sqlserver.md) |
| MySQL | `mysql` | No | None |
| MariaDB | `mariadb` | No | None |
| SQLite | `sqlite` | No | None |
| Oracle | `oracle` | No | None |
| CockroachDB | `cockroachdb` | No | None |
| Any other database | `ansi` | No | None |

Every dialect is read, and the queries of every `.sql` file whose output is `sql` or `models` get their constants and token methods whatever its database.  *Described* is something more: the `sqlsource` tool can ask the database about a query's parameters and columns and write the answer to a sidecar.  The tool decides by the dialect of the file, and asks only the databases marked Yes.  A database that merely shares a dialect with one of them, such as DuckDB with `postgres`, is read by that dialect's rules and cannot be described.  The tool still tries to describe a file read as `postgres` whose output is `models` or `codegen`, and the build asks for its entries, so give a DuckDB file the output `sql`: see [the output setting](settings.md#setting-output).

A query that must be described and whose dialect has no database to ask, every row marked No, is [SQLSRC209](diagnostics.md#sqlsrc209), from the tool and in the build.  The default output is `codegen`, so a project that sets neither the dialect nor the output gets it for every file.  It goes away when the query's output is `sql`, which asks for the SQL alone and needs no database: see [the output setting](settings.md#setting-output).  Under the output `models` the query's constant or token method is generated either way; under `codegen` it gets neither a method nor a constant.

<a id="known-issues"></a>

## Known issues

- **A `.sql` file that two projects list** is described by the tool with the dialect of the first project in the run that claims it, and the dialect the second project gives it is ignored.  Give the file the same dialect in both.  See [known issues of `.sql` files](sql-files.md#known-issues).
- **Constructs that are read differently** are listed [above](#still-read-differently).
