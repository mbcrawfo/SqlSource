# AGENTS.md - src/SqlSource.Tool/Describing/SqlServer

- **`tests/SqlSource.Tool.Tests/SqlServerScript.cs` answers a `SqlServerSession` with no server**, from a capture that the test builds in code, so its tests hold what is asked as well as what is made of the answers ([fixtures](../../../../docs/architecture/sqlserver-describer.md#fixtures)).  What only a server can show is tested in `tests/DatabaseTests/SqlServer/Describer/`.

## What reaches the server

- **In `Describing/SqlServer/`, the user's text and a declaration reach the server only as the values of `@tsql` and `@params`** of `sys.sp_describe_undeclared_parameters`, called as a `CommandType.StoredProcedure`, and of `sys.dm_exec_describe_first_result_set` in the tool's own text (`Describing/SqlServer/SqlServerLive.cs`), through `columns` and `operands`, whose text is the query's own tokens rearranged (`Describing/SqlServer/Proofs/OperandsQuery.cs`), and are never put into a text the tool sends.
- **Both analyse a text and run nothing, so that is what keeps a `.sql` file from making the describer run a statement** ([why a query cannot run a statement](../../../../docs/architecture/sqlserver-describer.md#why-a-query-cannot-run-a-statement)).
- **The canary tests prove it.**  Those of `tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs` prove it through `parameters`, `probe`, `declared` and a describe, and those of `SafetyTests.cs` beside it through `columns`, each finding `dbo.victim` there after a text that drops it, and through `operands`, finding a sequence not advanced by an operand that takes its next value.
- **A new place that sends a user's text sends it the same way and gets a canary beside them.**
- **`DeclaredTypeText` is the first refusal, with an error that says what is wrong, and not the guarantee**: the server reads `@params` as a list of declarations and refuses one that is not.

## The order of a describe

- **`SqlServerSession.DescribeAsync`, with `ResultAsync`, `ProvedAsync` and `MatchAsync`, holds the order of a describe** ([one describe, step by step](../../../../docs/architecture/sqlserver-describer.md#one-describe-step-by-step)):
  1. every declared type read before anything is sent;
  2. `declared` once for each declared parameter, before `parameters`, so that a `2715` or `2724` belongs to one declaration;
  3. the lists compared before any `probe`;
  4. a `probe` only for a reused parameter whose copies' types differ;
  5. `columns` once the parameters are typed, with every parameter declared;
  6. then `operands`, only when a proof needs the server, with the declarations of `columns`;
  7. and `table` only when every column has an origin in one table, since a call that fails for a table the match never reads would fail the query.
- **A `parameters` that fails with `11501` asks `columns` with the declared parameters alone**, and `SqlServerErrors.AfterRefusal` reports the two answers.
- **A parameter is declared only when its `-- param:` marker gives a type.**
- **The type of an inferred one is declared to `columns` by `SqlServerTypes.Declared`, a user type's names in brackets, and never by `Spelled`, which is for messages**: the server refuses `dbo.User`, whose name is a keyword.

## Calls, connection and errors

- **The copies of a reused parameter are renamed only through the lexer's `Parameter` lexemes, and a copy is ignored only when the probe binds it declared `xml` and declared `int`, never by the lexer's reading of what surrounds it** (`ParameterCopies`, [the copies and the probe](../../../../docs/architecture/sqlserver-describer.md#the-copies-and-the-probe)).  Every type in a sidecar is one the server gave, so the lexer never decides a type.
- **The calls of `Describing/SqlServer/` and their records are a contract with the captures** ([the calls and their records](../../../../docs/architecture/sqlserver-describer.md#the-calls-and-their-records)).  The names are `SqlServerCallNames`, added and never renamed, and the records are in `SqlServerCalls.cs`, with the rule of a record of a call that `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` holds.  A change to what the describer asks changes the fixtures: record them with `tools/record-fixtures.sh mssql` and read the diff.
- **SQL Server's connection is made in `SqlServerConnection.cs`, inside a `live` function and nowhere else, and every `live` function answers through `SqlServerLive.AnswerAsync`, where anything but a `SqlException`, an `UnusableException` or a cancellation is a bug** ([the connection](../../../../docs/architecture/sqlserver-describer.md#the-connection)).  SqlClient's texts name the host, the login or the value, so what reaches an answer is a fixed text of `ConnectionErrors`, and a replay touches no driver.
- **`SqlServerErrors` chooses an id by the error's number, or by the `error_type` of a row of `columns` or `table`, and never by its message** ([errors](../../../../docs/architecture/sqlserver-describer.md#errors)), since a message may be translated.  The message is read only to choose the parameter that `SQLSRC226` names.

## Columns and the table match

- **`columns` and `table` read `sys.dm_exec_describe_first_result_set` in browse mode 2, and a hidden row is dropped before anything is built** (`SqlServerLive`, `SqlServerColumns`, [browse mode 2](../../../../docs/architecture/sqlserver-describer.md#browse-mode-2)).  Mode 1 names a view's base table as the origin and adds hidden columns that differ between versions, and mode 2's one hidden row, `ROWSTAT`, must reach no model.
- **A column's `nullable` is the server's `is_nullable`, narrowed only by a proof of `Proofs/` (`nullableSource` `expression`) or by a `PERSISTED NOT NULL` computed column read from its one table (`catalog`), and never widened** ([columns](../../../../docs/architecture/sqlserver-describer.md#columns), [the proofs of not null](../../../../docs/architecture/sqlserver-describer.md#proofs-of-not-null)).
- **There is no plan walk, and every entry with rows records `plan` as `skipped`.**
- **What `operands` answers never fails the describe**: an answer that proves nothing leaves the column as the server answered it.
- **Nothing but `ColumnProofs`, which reads the text for one table alone, acts on a row's `PersistedNotNull`**: it says what the catalog holds, not what the query reads, and through an outer join the column is `NULL` while the server still names it as the source.
- **A column has an origin only when its source is a table or a view of the session's database, the `DB_NAME()` that `open` reads** (`SqlServerColumns.OriginOf`, [origins](../../../../docs/architecture/sqlserver-describer.md#origins)).  The sidecar's origin names no database, so a column of another database, a linked server or the system resource must not match a table of the same name in the session's.
- **`table`'s text is built from the catalog names the server gave as the origin, and never from the user's text** (`SqlServerLive.TableSelect`): each name in brackets with `]` written twice.
- **`SqlServerTableMatcher` runs the format's checks through `TableMatchChecks`, the one order both matchers share**, and the server is asked for `table` only when its first two checks pass ([the table match](../../../../docs/architecture/sqlserver-describer.md#the-table-match)).
- **The matcher reads the table's columns and their nullability from `table`'s answer, never from `sys.columns`**, so that a hidden period column is out of the list, and compares a query with those rows corrected by `ColumnProofs.TableRows`.

## Driver logging

- **SqlClient's events reach the log only through the allowlist of `SqlClientRunListener.cs`** ([driver logging](../../../../docs/architecture/sqlserver-describer.md#driver-logging)).  An event or a member is written only when it is listed, and none that can name the server, the database, the user or a password, nor a free-form `message`, is ever listed.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
