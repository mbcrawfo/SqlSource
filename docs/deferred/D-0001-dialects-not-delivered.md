# D-0001 - Dialects beyond the first seven

## Planned

The [dialect design](../superpowers/specs/2026-10-06-sql-dialects-design.md) researched every engine a .NET project is likely to use, and lists what each would need in its section "Engines not delivered".

## Delivered instead

Seven dialects: `ansi`, `mssql`, `postgres`, `mysql`, `mariadb`, `sqlite` and `oracle`, in [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs).  `README.md` says which of them to use for DuckDB, CockroachDB, Firebird and Db2.

## Why deferred

The seven cover every engine whose ADO.NET providers have more than 100 million NuGet downloads.  The next engine has under 40 million.  BigQuery needs lexer machinery that nothing else does, and several rules of the rest could not be verified from a primary source.

## Remaining work

A dialect is a value of `SqlDialect`, a name in `SqlDialectName`, a static property of `SqlDialectRules`, a row in the theory `Lex_Construct_IsReadByTheRulesOfTheDialect`, and a column in `README.md`.  A quoting form that no reader has is a new `QuoteReader` in `Parsing/Quoting/`.

| Engine | Read correctly today by | What it needs |
|----|----|----|
| DuckDB | `postgres` | A name of its own, if wanted.  No difference was found. |
| CockroachDB | `postgres`, but for `b'\''` | A reader for `'` that takes backslash escapes after a `b` prefix |
| Firebird | `oracle` | A name of its own, if wanted.  Its `q'...'` has no `n` prefix and the same pairing rule. |
| Db2 | `ansi` | Confirm whether block comments nest on Linux, UNIX and Windows: the documentation is silent.  `$`, `#` and `@` are letters there, so `$$` is an identifier and dollar quotes should be off. |
| Redshift | Neither | `postgres` with the Backslash reader for `'`.  Confirm whether block comments nest outside PL/pgSQL. |
| Snowflake | Neither | `//` line comments, but not in `://`; untagged `$$...$$` strings; the Backslash reader for `'`; no nesting.  Confirm whether `#` starts a comment: sqlparser-rs says yes, sqlglot says no, the documentation is silent. |
| BigQuery | Neither | `#` comments; `"..."` as a string; backslash escapes with no doubling; `'''` and `"""` strings over several lines; `r` and `b` prefixes, where a raw string still does not end at `\'`; backtick identifiers with backslash escapes; no nesting |
| ClickHouse | Neither | `#` as a comment only before a space or `!`; `//` comments; backslash escapes in strings and in both identifier quotes; `$tag$` heredocs; nesting |
| Spark and Databricks | Neither | Backslash escapes; `"..."` as a string by default; `r'...'` raw strings that end at the first quote; a backslash before a line break continues a `--` comment; nesting.  Confirm `$tag$` on Databricks. |
| Trino and Presto | Neither | No nesting; otherwise `ansi` without backticks and dollar quotes |

The sources are in the design's "Sources" section.

## Trigger

A user asks for one of these engines.
