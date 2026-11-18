# Lint and format SQL with sqlfluff

Lint and format the `.sql` files of a SqlSource project with sqlfluff, from a terminal, a pre-commit hook, Docker or CI.  sqlfluff reformats the SQL around your tokens and query parameters, and never changes them.

**Use this when** you want sqlfluff to check the style of your queries, or to format them.  sqlfluff cannot read a `{{name:default}}` token, a query parameter on every database, or a list after `IN` by itself, so it needs the `sqlsource` templater.

<a id="set-up-sqlfluff"></a>

## Lint and format your queries

Install sqlfluff with the templater.  In each command of this page, replace `<version>` with the version of your `SqlSource` package.

# [pip](#tab/pip)

```console
$ pip install sqlfluff sqlfluff-templater-sqlsource==<version>
```

# [uv](#tab/uv)

```console
$ uv tool install sqlfluff --with sqlfluff-templater-sqlsource==<version>
```

# [pipx](#tab/pipx)

```console
$ pipx install sqlfluff
$ pipx inject sqlfluff sqlfluff-templater-sqlsource==<version>
```

---

Put this `.sqlfluff` file in the folder that you run sqlfluff from, such as the top of the repository:

```ini
[sqlfluff]
templater = sqlsource
dialect = postgres
```

Keep `"root": true` in the settings file of your project's folder:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

A query file, `Queries/Users.sql`, before sqlfluff formats it:

```sql postgres
-- name: FindUser -> one
select id, name from users where id = @id;

-- name: ListUsers
SELECT id, name
FROM users
WHERE id IN @ids {{filter:AND deleted_at IS NULL}}
ORDER BY {{orderBy:name}}
```

Check the files, then fix what sqlfluff can fix:

```console
$ sqlfluff lint Queries
$ sqlfluff fix Queries
```

**What you get.**  `sqlfluff lint` reports each problem with its line, position and rule.  `sqlfluff fix` rewrites `Queries/Users.sql` to this:

```sql postgres
-- name: FindUser -> one
select
    id,
    name
from users
where id = @id;

-- name: ListUsers
select
    id,
    name
from users
where id in @ids {{filter:AND deleted_at IS NULL}}
order by {{orderBy:name}}
```

The keywords and the layout change.  The tokens, their defaults, `@id`, the list `@ids` and the `-- name:` lines stay as you wrote them.  `sqlfluff format` fixes with a smaller set of sqlfluff's rules, mostly those of layout and case, and leaves the same things as written.

**The rules you need.**

- **Install the templater at the version of your `SqlSource` package.**  They are released together, so each version of the templater reads `.sql` files as the same version of SqlSource does.
- **The templater needs sqlfluff 4.5 or later, before sqlfluff 5.**  See [sqlfluff 4.4 or older](#limit-sqlfluff-version).
- **Set `templater = sqlsource` in the `.sqlfluff` of the folder that you run sqlfluff from.**  sqlfluff ignores a templater set in the `.sqlfluff` of a folder below it.  A `.sqlfluff` below it can still set the options of sqlfluff's rules.
- **Set sqlfluff's `dialect` to your database.**  It must suit the SqlSource dialect of each file, as [the table below](#sqlfluff-dialects) says.
- **The templater takes each file's SqlSource dialect from where SqlSource takes it**: the file's `-- dialect:` marker or a `sqlsource.json`.  See [the dialect](../reference/settings.md#setting-dialect).
- **Put `"root": true` in the settings file of your project's folder.**  Then a settings file in a folder above the project changes neither your build nor what sqlfluff checks.  See [which settings files apply](../reference/settings-file.md#files-of-a-sql-file).

<a id="sqlfluff-dialects"></a>

## Set sqlfluff's dialect

Set sqlfluff's `dialect` to one that suits the SqlSource dialect of your files:

| SqlSource dialect | sqlfluff dialect |
|----|----|
| `ansi` | Any |
| `mssql` | `tsql` |
| `postgres`, `cockroachdb` | `postgres`, `duckdb`, `redshift`, `greenplum`, `materialize` |
| `mysql`, `mariadb` | `mysql`, `mariadb` |
| `mysql`, `mariadb` with `ansi-quotes` or `no-backslash-escapes` | None |
| `sqlite` | `sqlite` |
| `oracle` | `oracle` |

If a file's SqlSource dialect does not suit sqlfluff's, sqlfluff reports the file as `TMP` and checks nothing in it:

```text
L:   0 | P:   0 |  TMP | The SqlSource dialect mssql cannot be linted with the
                       | sqlfluff dialect postgres.
```

If your repository has files of two databases, run sqlfluff once for each, as [a migration folder](#sqlfluff-migrations) is run.  SqlSource's dialect names are on [the dialects page](../reference/dialects.md#choosing-a-dialect).

<a id="sqlfluff-mysql-options"></a>

### MySQL and MariaDB options

sqlfluff's `mysql` and `mariadb` dialects read `"..."` as a string, and a backslash in a string as an escape.  The options [`ansi-quotes` and `no-backslash-escapes`](../reference/dialects.md#dialect-options) change these rules, so `fix` could make a string an identifier.  So sqlfluff reports each file read with either option as `TMP`, and checks nothing in it:

```text
L:   0 | P:   0 |  TMP | The SqlSource dialect mysql with ansi-quotes cannot be
                       | linted with the sqlfluff dialect mysql, or with any
                       | other: ansi-quotes makes "..." a quoted identifier,
                       | which sqlfluff reads as a string.
```

Do not lint the files read with either option.  Leave their folders out of the run with a `.sqlfluffignore`, as [a migration folder](#sqlfluff-migrations) is left out.  The run then checks the files that SqlSource reads without the option.

<a id="what-sqlfluff-checks"></a>

## What sqlfluff checks, and what it never changes

- **sqlfluff never changes a token, a default, a query parameter, a list or a marker line.**  `fix` and `format` change the SQL around them: case, spaces and line breaks.  A line in a marker's form, such as `-- token:` or `-- name:`, stays where it is.
- **A query is checked with each token's default in its place.**  `WHERE 1 = 1 {{filter:AND deleted_at IS NULL}}` is checked as `WHERE 1 = 1 AND deleted_at IS NULL`.  A default from a `-- token:` line counts too.  See [defaults](../reference/tokens.md#token-defaults) and [`-- token:`](../reference/tokens.md#token-marker).
- **A token without a default is checked as its name.**  `ORDER BY {{orderBy}}` is checked as `ORDER BY orderBy`, but `WHERE 1 = 1 {{filter}}` does not parse.  So give each token a default that is valid SQL where the token stands.
- **A query parameter is checked as a value, and a list as a value in parentheses.**  So `WHERE id IN @ids` parses in every sqlfluff dialect.  See [query parameters](../reference/parameters.md#query-parameters).
- **A query that does not end in `;` is checked as if it did.**  sqlfluff can then tell it from the next query of the file.  `fix` and `format` never add the `;`.
- **A `{{...}}` inside a comment is part of the comment**, as it is for SqlSource.  See [tokens](../reference/tokens.md#tokens).
- **An error that stops SqlSource reading a file is reported as `TMP`, and nothing else in the file is checked.**  These errors are [SQLSRC101](../diagnostics.md#sqlsrc101), a quote that is not closed, [SQLSRC102](../diagnostics.md#sqlsrc102), a comment that is not closed, [SQLSRC111](../diagnostics.md#sqlsrc111), a `-- dialect:` marker that names no dialect, and [SQLSRC120](../diagnostics.md#sqlsrc120), a token that crosses a comment.
- **sqlfluff reports no other SqlSource error.**  Your build reports them.  Fix them before you trust what sqlfluff reports, since sqlfluff checks such a file as best it can.

<a id="sqlfluff-pre-commit"></a>

## Run sqlfluff in a pre-commit hook

Add sqlfluff's hooks to `.pre-commit-config.yaml`, with the templater in `additional_dependencies`:

```yaml
repos:
  - repo: https://github.com/sqlfluff/sqlfluff
    rev: 4.5.0
    hooks:
      - id: sqlfluff-lint
        additional_dependencies:
          - sqlfluff-templater-sqlsource==<version>
      - id: sqlfluff-fix
        additional_dependencies:
          - sqlfluff-templater-sqlsource==<version>
```

**What you get.**  Each commit runs `sqlfluff lint` and `sqlfluff fix` on the `.sql` files that it changes.

**The rules you need.**

- **pre-commit runs sqlfluff from the top of the repository**, so the `.sqlfluff` that sets `templater = sqlsource` must be there.
- **Name the same version of the templater in both hooks**, the version of your `SqlSource` package.  `rev` is the version of sqlfluff: 4.5.0 or later.

<a id="sqlfluff-docker"></a>

## Run sqlfluff in Docker

sqlfluff's image does not have the templater.  Build an image from it that does, with this `Dockerfile`:

```dockerfile
FROM sqlfluff/sqlfluff:4.5.0
RUN pip install --no-cache-dir sqlfluff-templater-sqlsource==<version>
```

Build the image in the folder of the `Dockerfile`.  Then run it in the folder of your `.sqlfluff`:

```console
$ docker build -t sqlfluff-sqlsource - < Dockerfile
$ docker run --rm -v "$PWD:/sql" sqlfluff-sqlsource lint .
```

**What you get.**  The image runs sqlfluff with the templater on the folder that you mount at `/sql`, which is where the image runs sqlfluff from.

**The rules you need.**

- **Name a sqlfluff version of 4.5 or later in `FROM`**, and the version of your `SqlSource` package in `RUN`.
- **`- < Dockerfile` sends Docker the `Dockerfile` alone**, not the files of its folder, which the image does not need.

<a id="sqlfluff-migrations"></a>

## Lint migration scripts in a separate run

The templater reads SqlSource's tokens and query parameters, not the variables of a migration tool, such as Flyway's `${schema}`.  Lint a folder of migration scripts in a run of its own, with its own `.sqlfluff`.

Leave the folder out of the run of your queries with a `.sqlfluffignore` file beside your `.sqlfluff`:

```text
Migrations/
```

Give the folder a `.sqlfluff` of its own, `Migrations/.sqlfluff`, with the templater of its tool:

```ini
[sqlfluff]
templater = placeholder
dialect = postgres

[sqlfluff:templater:placeholder]
param_style = flyway_var
```

Run sqlfluff in that folder:

```console
$ cd Migrations
$ sqlfluff lint .
```

**What you get.**  A run from the top of the repository checks your queries with the `sqlsource` templater and skips `Migrations/`.  A run from `Migrations/` checks the migration scripts with the `placeholder` templater.

**The rules you need.**

- **Each run takes its templater from the `.sqlfluff` of the folder that it runs from**, so each folder needs its own run.
- **The `placeholder` templater and its `param_style` are sqlfluff's.**  See [sqlfluff's templaters](https://docs.sqlfluff.com/en/stable/configuration/templating/index.html).

## If it does not work

- **"Requested templater 'sqlsource' which is not currently available".**  The templater is not installed beside the sqlfluff that ran.  Install it in the same environment, as [Lint and format your queries](#set-up-sqlfluff) shows.
- **"Attempt to set templater to sqlsource failed", then `TMP` errors about Jinja.**  The `.sqlfluff` that sets the templater is in a folder below the one that sqlfluff ran from.  Move it, or run sqlfluff from its folder.
- **`TMP` and "The SqlSource dialect … cannot be linted with the sqlfluff dialect …".**  Set sqlfluff's dialect to one that suits the file.  See [Set sqlfluff's dialect](#sqlfluff-dialects).
- **`TMP` and "… with ansi-quotes cannot be linted …", or "… with no-backslash-escapes …".**  No sqlfluff dialect reads the file as the option does.  Leave the file out of the run.  See [MySQL and MariaDB options](#sqlfluff-mysql-options).
- **`TMP` and a SqlSource error, such as "SQLSRC101: Quote is not closed."**  Fix the error at its line and position.  See [the list of errors](../diagnostics.md).
- **`PRS` at a token.**  The token has no default, so sqlfluff checks its name.  Give it a default.  See [defaults](../reference/tokens.md#token-defaults).
- **`CV06` that `fix` does not fix.**  See [CV06 on a query without `;`](#limit-cv06-without-semicolon).
- **No report on the white space or comments after a query.**  See [white space and comments after a query without `;`](#limit-text-after-query).
- **pip upgrades sqlfluff, or a pre-commit hook fails to install.**  See [sqlfluff 4.4 or older](#limit-sqlfluff-version).

<a id="limits"></a>

## Limits

<a id="limit-cv06-without-semicolon"></a>

### CV06 on a query without `;`

**What you see.**  sqlfluff reports `CV06`, "Semi-colon should be on a new line after a multi-line statement", at the end of a query that has no `;`.  `fix` does not fix it.

**Why.**  This happens only when your `.sqlfluff` sets `multiline_newline = True` for the rule `convention.terminator`.  The query is checked as if it ended in `;`, but that `;` is not in your file, so sqlfluff cannot move it.

**What to do.**  End each query of more than one line with `;`.  `fix` then moves the `;` onto a line of its own.

<a id="limit-text-after-query"></a>

### White space and comments after a query without `;`

**What you see.**  sqlfluff reports nothing on the white space and comments between a query without `;` and the next query, and `fix` changes nothing there.  For example, trailing white space after the query's last SQL gets no `LT01`, and a long comment line gets no `LT05`.

**Why.**  The query is checked as if it ended in `;`.  The white space and comments after its last SQL stand where that `;` would be, and the `;` is not in your file, so sqlfluff shows no report there.

**What to do.**  End the query with `;`.  The white space and comments after it are then checked and fixed.

<a id="limit-sqlfluff-version"></a>

### sqlfluff 4.4 or older

**What you see.**  If your sqlfluff is 4.4 or older, pip upgrades it when it installs the templater, in a Docker image too.  A pre-commit hook whose `rev` is older than 4.5.0 fails to install.

**Why.**  The templater needs sqlfluff 4.5 or later, before sqlfluff 5.  Before 4.5, `fix` can move a `-- token:` comment from the end of a line onto a line of its own, which changes how SqlSource reads the query.

**What to do.**  Use sqlfluff 4.5 or later: in the `rev` of your pre-commit hooks, in the `FROM` of your `Dockerfile`, and wherever else you install it.
