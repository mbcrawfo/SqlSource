# The spike of phase 10, before the bundle - appendix

Date: 2026-11-05

Appendix to [the phase 10 design](2026-11-04-query-generation-phase-10-bug-reports-design.md) of the [query generation epic](2026-10-07-query-generation-epic-design.md), written by sub-phase 10.0.  Before any code of the bundle, a throwaway prototype replayed every capture the repository holds with each member changed, read every string of every plan, dumped the engines' vocabularies from the floor and default images, renamed every capture crudely and replayed it, and read every use of a server error in both describers.  Each answer here is what was run and what it gave; [the prototype](#the-prototype) says where the code is and how to run it again.

## What ran

| | Version |
|----|----|
| .NET SDK | 10.0.401, the repository's `global.json`; xUnit v3 4.0.1 on Microsoft.Testing.Platform |
| SqlSource.Tool | `main` at `3b7d5c9b`, run in-process by the tool's tests |
| Drivers | Npgsql 10.0.3, Microsoft.Data.SqlClient 7.1.1 |
| PostgreSQL | `postgres:14.24` and `postgres:18.6`, for the vocabulary; 18.6 recorded the fixtures and captured the realistic corpus; `postgres:15.19`, `16.15` and `17.11` captured the realistic corpus for the names the planner makes |
| SQL Server | `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` and `2025-CU9-GDR1-ubuntu-24.04`, for the vocabulary; 2025 recorded the fixtures and captured the realistic corpus |
| Host | macOS on Apple silicon, Docker 29.8 |

**The cases.**  The 91 fixtures, 65 PostgreSQL and 26 SQL Server, and the realistic corpora captured on the default images for the spike, 56 of PostgreSQL's 59 cases and 53 of SQL Server's 56, in a fixture's layout: 121 PostgreSQL and 79 SQL Server captures, 200 in all, each of which replays to its own `expected.json` before anything is changed.  The six cases not captured are those the describer refuses: PostgreSQL's `array-agg-of-row` (`SQLSRC229`), `interval-without-cast` and `parameter-of-two-types` (`SQLSRC225`), and SQL Server's `report-dynamic-exec` (`SQLSRC264`), `report-label-parameter` (`SQLSRC226`) and `staging-temp-table` (`SQLSRC265`).  94 of the PostgreSQL captures hold a plan.  None of the 200 holds an error: 1,424 calls, each with `error: null`, since every case is a query that was described.

## 1. The fields the pipeline reads

**How it was established.**  For each call of each capture, each member of its request and of its answer, at any depth, was changed one at a time and the capture replayed: removed; a string with `_x` appended, and emptied; a number plus one; a boolean negated; an array's last element removed, and its first two elements swapped.  An array of objects was changed at once in every element, and in its first element alone.  The outcome was compared with the original replay: the same, another entry, another failure, a capture miss of some call, an answer that cannot be read, a throw.  50,555 replays over the 200 cases.  The plan's keys were changed the same way, by key name, at every occurrence in each of the 94 plans.  Then each record member was looked up in the compiled tool, by symbol, for the code that reads it, and each plan key in `Walk/PlanNode.cs`, its one reader.

**What the marks mean.**  *read*: a change moved an outcome, and the code reads it.  *read, never moved* (`*` below): only the code shows it is read, since no capture moved it; each is listed with why in [where the replay and the code disagree](#where-the-replay-and-the-code-disagree).  *code only* (`†`): no capture holds a value, so the code alone says it is read.  *not read*: neither.  A request member is *key*: the replaying exchange keys on it, so a change is a miss of its own call, and none is read by code on the replay path; every request member is kept whole.

### PostgreSQL

Every member is kept but `deallocate.answer.error`, `partitions.answer.error` and `relations.answer.error`, which only the log reads, the members of an error that only the failure's printed lines read, and, below, the plan's 28 keys not read.  `error` is the one record `PostgresServerError` of every answer, `{sqlState, message, detail, hint, position}`, code only for every call, since no capture holds one; [finding 5](#5-messages-and-positions) says which members decide.  Of an error that is kept, `sqlState` and `message` are kept, the message as the text step 10 writes, and `position` on `prepare` alone; `detail`, `hint` and the position of every other call are read by `PostgresErrors.ServerLines` alone, for the failure's `server:` lines, which a bundle does not hold, and are dropped.

| Call | Request (key) | Answer: read | Answer: read, never moved `*` or code only `†` | Not read |
|----|----|----|----|----|
| `open` | none | `serverVersion`, `settings[].{name, value}` | `driverVersion*`, `error†` | |
| `prepare` | `statement`, `sql`, `types` | `parameterTypes` | `error†` | |
| `columns` | `statement`, `parameters` | `columns[].{ordinal, name, typeOid, tableOid, attributeNumber, typeModifier}` | `columns[].{length, precision, scale}*`, `error†` | |
| `tables` | `oids` | `tables[].{oid, relkind, schema, name, relhassubclass, hasForeignPartition, hasInsteadRule}`, `tables[].columns[].{attnum, name, notNull, identity, generated, typeOid, notNullValidated}` | `error†` | |
| `relations` | `relations[].{schema, name}` | as `tables`, but `columns[].attnum` and `hasForeignPartition`, which only `tables` moved | `tables[].columns[].attnum*`, `tables[].hasForeignPartition*` | `error` (logged alone) |
| `partitions` | `relations[].{schema, name}` | `rows[].{schema, name, ancestors}` (7 realistic captures, no fixture) | | `error` (logged alone) |
| `types` | `oids` | `rows[].{oid, typname, nspname, typtype, typcategory, typelem, typbasetype, rngsubtype, multirangeRngsubtype, formatType, enumLabels, typtypmod}` | `error†` | |
| `spellings` | `pairs[].{oid, modifier}` | `rows[].{oid, modifier, formatType}` | `error†` | |
| `check` | `sql`, `parameters[].{name, dataTypeName}` | `columns[].{name, typeOid, dataTypeName}` | `sent.{text, parameters}*`, `textRefused*`, `driverError.{parameter, message}†`, `error†` | |
| `explain` | `statement`, `parameters`, `settings` | `plan`, by the keys below | `error†` | |
| `overloads` | `functions`, `operators` | `functions`, `operators` (every answer of the corpora is empty) | the lists' elements `†`, `error†` | |
| `deallocate` | `statement` | | | `error` (logged alone) |

**The plan's keys**, the `plan` string of `explain`, read by `PlanNode.Read` alone:

| Kind | Keys |
|----|----|
| Read and moved (cases) | `Plan` 94, `Plans` 90, `Node Type` 94, `Output` 94, `Alias` 94, `Relation Name` 93, `Schema` 93, `Parent Relationship` 90, `Join Type` 79, `Strategy` 28, `Group Key` 18, `Subplans Removed` 12, `Subplan Name` 7, `Grouping Sets` 4, `CTE Name` 3, `Command` 2 |
| Read, never moved | `Filter` (50 cases): only its presence is read, by the walk of a scalar sub-plan; `Group Key` is read for its presence too |
| Not read, dropped | `Async Capable`, `Conflict Arbiter Indexes`, `Conflict Resolution`, `Disabled`, `Function Call`, `Function Name`, `Group Keys`, `Hash Cond`, `Hash Keys`, `Index Cond`, `Index Name`, `Inner Unique`, `Join Filter`, `Merge Cond`, `One-Time Filter`, `Operation`, `Parallel Aware`, `Partial Mode`, `Plan Rows`, `Plan Width`, `Planned Partitions`, `Recheck Cond`, `Run Condition`, `Scan Direction`, `Sort Key`, `Startup Cost`, `Total Cost`, `Window` |

`Node Type`, `Join Type`, `Parent Relationship`, `Strategy` and `Command` are compared with the fixed words of `PlanVocabulary` and stay verbatim; `Subplans Removed` is a number.  `Relation Name` and `Schema` make the `relations` and `partitions` requests, so they are renamed in step with them.  The keys dropped hold the user's text too, conditions, sort keys, index names, function calls, and a bundle that keeps none of them has none of it to rename.

### SQL Server

Every member is kept.  An error row's `class` and `state` are kept, though on the replay's path only the failure's printed line `Msg <n>, Level <class>, State <state>` reads them, and so is `declared.answer.rows[].columnOrdinal`, which only the log reads: they are numbers of a value type that is not nullable, and hold nothing of the user's.  `error` is `SqlServerServerError`, `errors[].{number, class, state, message}`, and a row error of `columns`, `declared`, `operands` and `table` is `SqlServerDescribeError`, `{number, type, message}`; both are code only for every call.

| Call | Request (key) | Answer: read | Answer: read, never moved `*` or code only `†` | Not read |
|----|----|----|----|----|
| `open` | none | `productVersion`, `database` | `driverVersion*`, `engineEdition*`, `settings[].{name, value}*`, `error†` | |
| `parameters` | `sql`, `params` | `rows[].{name, suggestedSystemTypeName, suggestedMaxLength, suggestedPrecision, suggestedScale, suggestedUserTypeSchema, suggestedUserTypeName, suggestedAssemblyQualifiedTypeName}` | `error†` | |
| `probe` | `sql`, `params` | `binds` | `error†` | |
| `declared` | `sql`, `params` | `rows[].{systemTypeName, maxLength, precision, scale, userTypeSchema, userTypeName}` | `rows[].assemblyQualifiedTypeName*`, `rows[].columnOrdinal` (logged alone, kept), `errors†`, `error†` | |
| `columns` | `sql`, `params` | `rows[].{isHidden, name, isNullable, systemTypeName, maxLength, precision, scale, userTypeSchema, userTypeName, assemblyQualifiedTypeName, sourceDatabase, sourceSchema, sourceTable, sourceColumn, isIdentityColumn, isComputedColumn, sourceType, persistedNotNull}` | `rows[].{columnOrdinal, sourceServer}*`, `errors†`, `error†` | |
| `operands` | `sql`, `params` | `rows[].{isHidden, name, isNullable, systemTypeName}` | every other member of the row `*`, `errors†`, `error†` | |
| `table` | `schema`, `table` | `rows[].{isHidden, name, isNullable, persistedNotNull, sourceType}` | every other member of the row `*`, `errors†`, `error†` | |

The rows of `columns`, `operands` and `table` are one record, `SqlServerColumnRow`, read through `ISqlServerTypeRow` for its type members, so the code reads every type member of every row of the three, and the replay moved those of `operands` and `table` for none.  The list keeps them all: a member a later change of the describer reads cannot be dropped from a bundle that exists.

### Where the replay and the code disagree

1. **Read by the code, never moved, because no capture has the case**: every `error` of both engines, `check.answer.driverError`, and the elements of `overloads`' lists.  A bundle of a refused query holds them, and 10.2 and 10.3 build their tests of errors by hand, since no recorded case has one.
2. **Read by the code, never moved, though the capture has a value**: PostgreSQL `columns[].{length, precision, scale}` (`ColumnFacets.Of`, which the type's modifier decides first in every case), `check.answer.{sent, textRefused}`, `open.driverVersion`, `relations`' `attnum` and `hasForeignPartition`; SQL Server `open.{driverVersion, engineEdition, settings}`, `columns`' `columnOrdinal` and `sourceServer`, and the members of `operands` and `table` above.
3. **Moved only when removed**, as an answer that cannot be read, since the member is not nullable and its value is never used: PostgreSQL `check.columns[].dataTypeName`, `check.sent.parameters`, `open.settings[].value`, `relations`' `columns[].{generated, identity}` and `relkind`; SQL Server `open.settings[].{name, value}`.  The renaming keeps them, renamed by their field's rule.
4. **Read by the log alone**, `not read`: `deallocate.answer.error`, `partitions.answer.error`, `relations.answer.error` (`RelationsOfAsync` reads its `tables` alone and goes on without them), and `declared.answer.rows[].columnOrdinal`, which is kept since it is a number of a value type that is not nullable and holds nothing of the user's.  **Read by the failure's printed lines alone**, `not read`: PostgreSQL's `detail`, `hint` and the `position` of every call but `prepare`, which `PostgresErrors.ServerLines` alone reads.  The mutation run counted both kinds as read, since it looked for any reader in the code; a member is kept only when a decision of the pipeline reads it, or when it is not nullable: of a reference type, or a number of a value type that holds nothing of the user's.  No answer member moved an outcome that the code does not read on the replay path.

## 2. The plan's strings

**Every string reads.**  The 94 plans hold 4,674 string values and string elements of arrays under 32 keys, and `PlanTokens.Read` gives tokens for every one: the design's step 9 refusal, a string of a key the walk reads that `PlanTokens` cannot read, is never taken by the corpora.

| Key the walk reads | Strings | What they are |
|----|----|----|
| `Output` | 2,299 | expressions: `u.id`, `COALESCE(...)`, `("Order".user_id)`, a name quoted with `"..."` when it needs it |
| `Group Key`, `Filter` | 30, 73 | expressions, read for their presence alone |
| `Alias` | 253 | names, **raw**: `*SELECT* 1` reads as four tokens |
| `Relation Name`, `Schema` | 229, 231 | names, raw, one `Word` each in the corpora |
| `CTE Name`, `Subplan Name` | 5, 11 | names; `Subplan Name` is `CTE <name>`, `SubPlan <n>` or `InitPlan <n>` |
| `Node Type`, `Join Type`, `Parent Relationship`, `Command`, `Strategy` | 595, 113, 501, 2, 30 | the server's fixed words |

- **A raw name is one name.**  `Alias`, `Relation Name`, `Schema` and `CTE Name` are not quoted, so a name with a space, a dot or a quote would read as several tokens; the same name is quoted inside `Output` (`"Order"`, `"left"`, `"*SELECT* 1"`).  A raw key is renamed as one name, and the same spelling quoted in `Output` gets the same replacement.
- **Literals.**  121 strings in 30 cases hold a `String` token: `Output` 94 (`'Active'::text`, `'1 day'::interval day to second(3)`), `Filter` 14, `Sort Key` 8, one each in five other keys.  A literal in a plan is always followed by `::` and its type.
- **Names only the plan holds.**  `Output` and `Filter` name columns of tables that no `tables` or `relations` call answered, since the result does not use them (`note`, `coupon`, `placed_at`), and an enum type of a cast (`'active'::account_status`).  A renaming driven by the catalog's answers alone misses them; the SQL holds them too, so the lexer finds them.

## 3. The vocabulary

**Where each engine's words are.**  The SQL Server lists as fetched are [beside this document](2026-11-05-query-generation-phase-10-spike/SOURCES.md).  PostgreSQL's keywords are `pg_get_keywords()`; its functions, operators and types are `pg_catalog`'s rows of `pg_proc`, `pg_operator` and `pg_type`, with `information_schema`'s types.  SQL Server has no catalog of its keywords or of its intrinsic functions: the keywords are the three lists of Microsoft's [Reserved keywords (Transact-SQL)](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/reserved-keywords-transact-sql), reserved (185), ODBC (235) and future (272, of the page's 273: the extraction lost `REGR_R2`), one for every version; the functions are the 318 names of the Transact-SQL functions reference, each tried as `SELECT F()` through `sys.dm_exec_describe_first_result_set`, error `195` meaning the server does not know it; the types are `sys.types` with `is_user_defined = 0`.

| List | Floor | Default | Only in the floor | Only in the default |
|----|----|----|----|----|
| PostgreSQL keywords | 457 (14.24) | 494 (18.6) | `recheck` | 38: `merge`, `matched`, `source`, `target`, `path`, `plan`, `error`, `empty`, the `json_*` words and others |
| PostgreSQL functions, types, `information_schema` types | 3,223 (2,622, 453 and 148) | 3,408 (2,787, 473 and 148) | 22 internal functions | 187 functions (`any_value`, `array_sort`, many `pg_*`) and 20 types |
| PostgreSQL operators | 74 | 74 | | |
| SQL Server types | 34 (2017) | 36 (2025) | | `json`, `vector` |
| SQL Server functions the server knows | 260 of 318 | 297 of 318 | | `GREATEST`, `LEAST`, the `JSON_*` constructors, `REGEXP_*`, `VECTOR_*`, `DATETRUNC`, `PRODUCT` and others |

No PostgreSQL keyword changes its category between 14 and 18.  The probe of `195` cannot tell a table-valued built-in, which `SELECT F()` refuses as unknown whatever it is: the rowset function `OPENJSON`, and 2025's `REGEXP_MATCHES` and `REGEXP_SPLIT_TO_TABLE`, which the spike's run counted unknown and the second opinion of its review found known by `SELECT * FROM F()`, which answers `208` for a name the server does not know and `313`, too few arguments, for those two; the table counts them known.  A list made again probes a name that `SELECT F()` calls unknown as `SELECT * FROM F()` too.  Nor can the probe find a keyword that is not a function.

**What the corpora's words are.**  Every word of each case's SQL, split by the generator's lexer and the engine's word rule, and of each plan string, read by `PlanTokens`:

| | PostgreSQL | SQL Server |
|----|----|----|
| Distinct words | 339 | 351 |
| A keyword of the engine's lists | 97 | 85 (reserved 55, ODBC 22 more, future 8 more) |
| In the function, operator or type lists alone | 22 | 29 |
| A name the planner makes | 9 | |
| A name of the user's in a catalog answer or a plan's name | 183 | 184 |
| Of no class | 28: 25 the user's (aliases, CTE and window names, derived columns), and `ctid`, `xmax` and `hashed` | 53: 40 the user's, `master`, and 12 keywords no list holds |

The 12 SQL Server words are non-reserved keywords and a rowset function: `APPLY`, `INSERTED`, `DELETED`, `MATCHED`, `OFFSET`, `PRECEDING`, `UNBOUNDED`, `TIES`, `RECOMPILE`, `SYSTEM_TIME`, `ABSENT`, `OPENJSON`.  The crude renaming of [finding 4](#4-a-crude-renaming-replayed) found more engine words in no list, since a renamed engine word still replays: SQL Server's `$action` and its other `$` pseudo-columns, `SOURCE` and `TARGET` of `WHEN NOT MATCHED BY`, the system databases `master` and `msdb`, the hidden row's name `ROWSTAT`, and PostgreSQL's window names `w1`, `w2` and `EXCLUDED`, which must be folded before it is compared.

**`overloads` is no source of the vocabulary.**  It is asked only for the names of `CatalogOverloads`, and only for a column with no origin: 18 of the 121 PostgreSQL cases ask it, for 5 functions (`array_agg`, `count`, `json_build_object`, `jsonb_build_object`, `tstzrange`) and the operator `*`, and every answer is empty.  It covers 5 of the 339 words, no keyword, no type and no name the planner makes; the cases call `avg`, `date_trunc`, `lower`, `max`, `row_number`, `string_agg`, `sum`, `unnest` and others, and use the operators `&&`, `->>`, `@>`, `||`, `~~*` and the comparisons, none of which it is asked about.  Its answer says whether a name is `pg_catalog`'s alone, which the walk needs, not what a word is.

**The size of a committed list.**  PostgreSQL's keywords, functions, types and `information_schema` types of both images, folded, one word a line: 3,826 words, 55,554 bytes, 14,720 gzipped; the keywords alone 495 words, 3,808 bytes; the operators 74 more.  SQL Server's three keyword lists, the functions each image knows and both images' types: 755 words, 7,202 bytes, 2,934 gzipped.

**The names of the user's that the owner's rule keeps verbatim**, found in the corpora: PostgreSQL `name` (76 cases), `path`, `action`, `at`, `depth`, `document`, `label`, `level`, `month`, `names`, `nothing`, all keywords; SQL Server `action`, `depth`, `method`, `path`, from the ODBC and future lists, and `product` and `user_id`, from the function list alone.  The larger the list, the more of a user's names it keeps; the function and type list adds none to PostgreSQL's keywords in these corpora, and two to SQL Server's.

**The choice of the design's step 2: a committed list for each engine, made from the containers of its floor and default images and merged as a union**, since 38 keywords and 207 names of PostgreSQL and 37 functions of SQL Server exist on the default image alone, and a word that only a newer server knows is still the engine's word in a bundle from it.  PostgreSQL's list is its keywords with their category, `pg_catalog`'s functions, operators and types, `information_schema`'s types, the system columns `ctid`, `xmin`, `xmax`, `cmin`, `cmax` and `tableoid`, and the names the planner makes ([below](#the-names-the-planner-makes)).  SQL Server's is the three documented keyword lists, the non-reserved keywords and rowset functions they lack, the pseudo-columns, the documented functions that either image knows, both images' types, and the system databases `master`, `msdb`, `model` and `tempdb`.  `overloads` keeps the one job it has.  The lists are made again when a default image moves, beside the fixtures.

### The names the planner makes

The design's step 2 asked for them from the planner's source and the plans of each supported major.  The source of `REL_14_STABLE` to `REL_18_STABLE` was read for every alias, sub-plan name and column name that the parser, the planner, `ruleutils.c` and `explain.c` make up (`makeAlias`, the `%d` and `%s_%d` formats, the literal names); the realistic corpus was captured again on `postgres:14.24`, `15.19`, `16.15` and `17.11` (55, 55, 55 and 56 cases, `merge-returning` needing 17; no major refuses a case 18 accepts); and 33 probe statements ran on each major for the forms no corpus prints.

| Form | Made by | Majors that print it | Keys |
|----|----|----|----|
| `*SELECT* <k>` | `transformSetOperationStmt` | 14 to 18 | `Alias`, `Output`, `Sort Key`; `Group Key` on 14 to 16 |
| `*VALUES*`, and `column<n>` its columns | `addRangeTableEntryForValues` | 14 to 18 | `Alias`, `Output`, `Sort Key` |
| `*VALUES*_<n>`, a second list of one query | `set_rtable_names` | 14 to 18, probes alone | `Alias` |
| `SubPlan <k>` | `build_subplan` | 14 to 18 | `Subplan Name`, `Output`, `Sort Key` |
| `hashed SubPlan <k>` | `get_rule_expr` | 14 to 18 | `Output`, `Filter` |
| `InitPlan <k> (returns $<n>)` | `build_subplan` | 14 to 16 | `Subplan Name` |
| `InitPlan <k>` | `SS_make_initplan_from_plan` | 17, 18 | `Subplan Name` |
| `(InitPlan <k>).col<n>`, `(hashed SubPlan <k>).col<n>` | `get_parameter` | 17, 18 | `Output`, `Filter`, `One-Time Filter` |
| `$<n>`, a parameter of the executor | `get_parameter` | 14 to 18 | `Output`, `Filter` and conditions |
| `CTE <name>`, the name the user's | `SS_process_ctes` | 14 to 18 | `Subplan Name` |
| `<alias>_<n>`, an alias made unique: a partition, an inheritance child, a subquery repeated | `set_rtable_names` | 14 to 18 | `Alias`, `Output`, `Filter` and conditions |
| `unnamed_subquery` | `addRangeTableEntryForSubquery` | 16 to 18, probes alone; 14 and 15 refuse a subquery of `FROM` with no alias | `Alias` |
| `excluded` | `transformOnConflictClause` | 14 to 18, probes alone | `Conflict Filter`, dropped |
| `w<n>`, a window with no name | `name_active_windows` | 18; 14 to 17 print `OVER (?)` | `Output`, `Window`, `Run Condition` |
| `old.` and `new.` of a `RETURNING` | `transformReturningClause` | 18, probes alone | `Output` |

**Made, and printed by no plan seen**: `ANY_subquery` (`convert_ANY_sublink_to_join`, in every major; seven probes of `IN` and `ANY` never print it), `*SELECT*` with no number, `*RESULT*`, `*ANTI*` (16 on), `*MERGE*` (15 on), `*GROUP*` (18), `unnamed_join`, a repeated output column's `<column>_<n>`.  **Listed by the design and made by none**: `worktable`; a work table scan names the user's CTE.  **Names of a result column, in the `columns` answer and never in a plan**: `?column?` and `FigureColname`'s words, `case`, `coalesce`, `array`, `row`, `exists` and the like, four `json_*` words more from 16 and seven from 17, all keywords or functions of the list.  **Not the planner's**: an index name, `<table>_pkey` or `_key`, is made from the user's names when the table is made, and is a name of the user's in the keys the walk does not read.

A scan of every plan word of each major that is no word of its case's SQL, its answers or the lists found no other form.  A user's name spelled as a planner form, `w1`, `column1`, `col1`, `excluded`, differs from the planner's only by its place, and stays verbatim by the owner's rule as any engine word does.

**The crude renaming on each major**, with the six rules of [finding 4](#4-a-crude-renaming-replayed) and nothing changed: every case replays with the same provenance and leaks nothing, 199 of 199 on 14, 15 and 16 and 200 of 200 on 17 and 18, the forms only some majors print included.  The design as written fails the same cases on every major.

## 4. A crude renaming, replayed

**What ran.**  A prototype of steps 1 to 11: the members finding 1 marks not read dropped; names taken from the answers by field, then the plans' name keys, then the SQL, each replaced by its class's prefix and eight hex digits of HMAC-SHA256 under a fixed key, one spelling one name, folded as the engine folds; the words of [finding 3](#3-the-vocabulary)'s lists, the engine's schemas and the planner's names kept; literals of every form the lexer reads renamed, enum labels as literals, comments emptied.  `query.sql` was renamed lexeme by lexeme with the generator's lexer and the engine's word rule, its markers too; every request and answer string by its field's rule, SQL by the lexer, a plan string by `PlanTokens`, a catalog name as one name; `expected.json` by field.  Each copy was replayed, through `ReplayingExchange` and through an exchange that answers a missing request from the nearest one of its method so that every differing request is listed, and its entry compared with the renamed `expected.json`, its provenance with the original's.

| Run | PostgreSQL fixtures | PostgreSQL, all | SQL Server fixtures | SQL Server, all | Fixtures | All |
|----|----|----|----|----|----|----|
| The design as written | 63 of 65 | 101 of 121 | 20 of 26 | 32 of 79 | 83 of 91 | 133 of 200 |
| With the six rules below | 65 of 65 | 121 of 121 | 26 of 26 | 79 of 79 | 91 of 91 | 200 of 200 |

With the six rules every request the pipeline made from the renamed query equals one renamed in the capture, every entry equals the renamed one, and every provenance, `nullableSource`, `plan`, `tableMatch` and `typeSource`, holds.  No run leaked a name or a literal: every name and literal of the engine's `schema.sql` and the case's `query.sql` that the renamer did not keep as the engine's was searched for in every file of the copy, a search that finds 6,489 in the cases before renaming.

**The cases the design as written does not replay**, 67, one cause each but one:

| Cause | Step | Cases | What the replay shows |
|----|----|----|----|
| SQL Server's `declared` and `operands` alias each column `AS [n]`, a quoted name of digits that is the tool's, not the user's; renamed, it no longer matches the `[1]` the pipeline writes | 8 | 4 fixtures, 41 realistic | a miss of `declared` or `operands` |
| `relations` and `partitions` ask their relations in schema, then name, order (`PlanRelations.InOrder`); renamed in place, they keep the original order | 8 | 1, 13 | a miss of `relations` |
| The planner's alias of a partition or an inheritance child, `<alias>_<n>`, renamed as a name of its own: `PlanPartitions` no longer finds the alias's children | 2, 9 | 0, 5 | **no miss**: the entry differs, `outer-join` or `catalog` becoming `heuristic` |
| `Subplan Name`'s `CTE <name>`, whose `CTE` is in no list and is renamed | 2, 9 | 1, 1 | **no miss**: the entry differs |
| A copy of a reused parameter, `@before_1`, renamed token by token as a name of its own, while `ParameterCopies` makes `@<renamed before>_1` | 8 | 2, 0 | a miss of `parameters` |

No other request differs: the SQL of `prepare` with its `$n`, its type list, `check`'s `@name` text and its `sent.text`, `types` and `spellings` by OID, and SQL Server's `parameters`, `columns`, `table` and `probe` texts and declarations all equal the request renamed token by token.  Two causes change the entry with no capture miss, so only step 13's comparison of the whole entry catches them.

**SQL Server's proofs read the renamed select lists as they read the originals.**  18 cases ask `operands`, once each.  Without the first rule each request differs from the renamed one only in its `AS [n]`; the slices of the user's text are equal token for token.  With it, all 79 SQL Server entries keep the original's provenance, every column's `nullableSource` included.

**The six rules**, each general, each a derivation by the pipeline's own rule and not a guess:

1. **The tool's own aliases stay.**  A quoted name spelled as digits alone, `[1]`, is the `AS [n]` of `operands` and `declared`.
2. **A list asked in name order is put in that order again** after renaming: `relations` and `partitions`, by schema, then name, ordinal.
3. **`<alias>_<n>` is renamed through its alias**, `<renamed>_<n>`, when `<alias>` is a name the renaming maps and `<alias>_<n>` itself is held by no catalog answer and does not occur in the query's SQL; it is always in the plan's `Alias`, so the plan is not asked.
4. **`CTE <name>` in `Subplan Name` keeps `CTE`**, and renames the name as `CTE Name` is renamed.
5. **A copy of a reused parameter is renamed through its parameter**, `<renamed><separator><n>`, as `ParameterCopies` makes it.
6. **A name's nullability mark stays**: `"name!"` and `"name?"` keep their `!` and `?`, and PostgreSQL's own `?column?` stays whole.  The describer reads no mark, so the replay passes without it, but the generator would read the bundle's alias as another nullability.

**What else the renaming had to know** that the design does not say:

- **A table of fields for each call of each engine**: which strings are the tool's fixed texts (`statement`, `sqlsource_<n>`; `explain.settings`; the names and values of `open`'s settings but `search_path`; the versions; the `assemblyQualifiedTypeName` of a system CLR type, `geometry`, `geography` or `hierarchyid`), which are closed codes (`relkind`, `identity`, `generated`, `typtype`, `typcategory`, `sourceType`), which are type spellings to lex (`formatType`, `dataTypeName`, `systemTypeName`, SQL Server's `params`), which are labels, and which are catalog names with their class.  These are step 12's "strings the tool writes itself"; no field was met outside the table.  The corpora hold no CLR type of the user's, whose `assemblyQualifiedTypeName` names the user's assembly and type: it is a name of the user's, renamed whole as one `y_` name, since the pipeline reads only whether it is null (`SqlServerTypes.MayBeClr`), on every row that carries it, `parameters`' `suggestedAssemblyQualifiedTypeName` and the rows of `declared`, `columns`, `operands` and `table`, whose `table` rows hold every column of the table, selected or not.
- **A row of a system database is the engine's**: `msdb.dbo.sysjobs` has the schema `dbo`, so the test by schema of step 2 would rename `sysjobs` and `job_id`; its database says it is the engine's.
- **The logical database of a query that no marker or setting names is the dialect's name**, `postgres` or `mssql`, a word of the tool's; it stays.
- **`search_path`** is a list whose elements may be quoted; `$user`, `public` and `pg_catalog` stay.
- **The class is often the lexer's guess.**  SQL Server answers only the result's columns and their origins, so 82 names of its corpus, aliases and the columns and tables of `WHERE`, joins and `OUTPUT`, are first found in the SQL and take `a_`; PostgreSQL's plans hold 11 names that no answer has and 4 tables found only in `Relation Name`.  One spelling still has one name; only the prefix is wrong.
- **A token's default** (`{{filter:AND deleted_at IS NOT NULL}}`) is renamed as SQL and its name kept when it is a keyword; both replay.

Not exercised by the corpora: an error, so step 10 was written and never run; a comment other than a marker; a hint; an `E''`, `U&`, dollar-quoted or bit string; a bracketed name of the user's; a quoted name holding `""`.

## 5. Messages and positions

Every read in `src/SqlSource.Tool/Describing/` of `PostgresServerError.{Message, Detail, Hint, Position}`, `PostgresBindError.Message`, `SqlServerErrorRow.Message` and `SqlServerDescribeError.Message`, and every constant text the tool writes into an answer.

- **The parameter a message names**: confirmed.  `PostgresErrors.NamedIn` takes the first `$n` of a `42P18` or `42P08` message, and a message without one moves the id from `SQLSRC226` or `SQLSRC227` to `SQLSRC225`.  `SqlServerErrors.NamedIn` takes each `@name` of every row, but only when the first row is `11502`, `11503`, `11506` or `11507`; it chooses which parameter `SQLSRC226` names, never the id, and takes a copy back to its parameter through `CopiedSql.ParameterOfCopy`, so a message and the `parameters` request's SQL are renamed alike.
- **The four constant texts**: confirmed, and all four are PostgreSQL's.  `PostgresErrors.Open` compares an error with no SQLSTATE to `ConnectionErrors.MultiplexingRefused` and `ConnectionErrors.UnreadableTypes`; `PostgresErrors.Prepare` compares one with no SQLSTATE to `PostgresLive.StatementNotListed`, and `08P01` to `PostgresLive.TransactionRefused`.  SQL Server compares no text: from a captured answer it decides by `Number` and `Type` alone.  Every other text of `ConnectionErrors` and `ConnectionTexts` is written into an answer and never compared, and holds nothing of the user's.
- **Detail and hint are never read** by a decision, only copied into `ServerLines` and the log, so a bundle drops them.
- **A message is copied too**: PostgreSQL's `SQLSRC225` and SQL Server's failures of `columns` and `declared` put the first message into the failure's `Arguments`, and every message is a `server:` line.  None of it is in `bundle.json`, which keeps a failure's id and step, and none reaches an entry.
- **Two reads are of the live exchange alone** and never see a capture: `PostgresLive.RefusedTransaction` matches PgBouncer's text, and `ConnectionErrors.AtOpen` Npgsql's `No password has been provided`; each answers a constant.
- **`Position` is read, and decides, which the design's constraint does not say.**  Only `prepare`'s is read.  It counts code points from one in the text of the `PREPARE`, the head `PREPARE <name> (<types>\n) AS ` and the request's `sql`, not in the query's sample SQL and not in `check`'s.  A position inside the head names the parameter whose declared type is at fault, `SQLSRC228` in place of `SQLSRC225`; one after it is printed as a character of the SQL.  A renamed declared type moves the head's end and every position after it, and a renamed name in the SQL every position after the name.  The positions of every other call are dropped when the answer is read.  SQL Server's errors have none.

So the design's constraint holds, with `Position` added.

## The prototype

The prototype was explicit tests, which `dotnet test` runs only when asked, in `Spike10/` folders of three test projects.  It was committed once on this branch, as `f51ac107` "spike(10.0): the prototype of the spike, removed in the next commit", and removed by the commit after it, so the merged tree does not hold it and the history does.  To run it again, from the root of a checkout that holds this document:

```bash
export SPIKE10_OUT="$(mktemp -d)"
mkdir -p "$SPIKE10_OUT/spike10/sources"
cp docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike/mssql-*.tsv "$SPIKE10_OUT/spike10/sources/"
git switch --detach f51ac107
```

The prototype reads SQL Server's keyword and function lists from `$SPIKE10_OUT/spike10/sources/`, which the `cp` fills before the switch: the lists fetched from Microsoft's documentation on 2026-11-04 are [beside this document](2026-11-05-query-generation-phase-10-spike/SOURCES.md), with their URLs and what was left out or added.  A later fetch of the same pages may give other counts.

Then, each from the repository's root, with Docker running for the two container projects:

| Finding | Command |
|----|----|
| The harness, and the 91 fixtures replayed | `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Spike10.SpikeHarnessTests" --explicit only` |
| The realistic corpora captured (before 1, 2, 4 and 5 read them) | `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.Spike10.RealisticCaptureTests" --explicit only`, and the same in `tests/DatabaseTests/SqlServer` with `SqlSource.DatabaseTests.SqlServer.Spike10` |
| 1, the members | `--filter-class "SqlSource.Tool.Tests.Spike10.FieldMutationTests"`, and `PlanKeyMutationTests` for the plan's keys |
| 2 and 5 | `--filter-class "SqlSource.Tool.Tests.Spike10.PlanStringTests"`; finding 5's reading is in this document |
| 3, the vocabulary | `VocabularyTests` in each container project, with `SQLSOURCE_TESTS_POSTGRES_IMAGE` or `SQLSOURCE_TESTS_SQLSERVER_IMAGE` set to each image; then `CoverageTests` in `tests/SqlSource.Tool.Tests` |
| 4, the renaming | `--filter-class "SqlSource.Tool.Tests.Spike10.CrudeRenamingTests"` |
| The names the planner makes | the PostgreSQL capture above with `SQLSOURCE_TESTS_POSTGRES_IMAGE` set to each major's image and `SPIKE10_REALISTIC_FOLDER` to a folder of its own, then `CrudeRenamingTests` with that folder as `$SPIKE10_OUT/spike10/cases/realistic-postgres`; the probes are `tests/DatabaseTests/Postgres/Spike10/planner-probes.txt`, run with `psql` in a container of each image |

Each test writes its tables to `$SPIKE10_OUT/spike10/results/`.

## What the later sub-phases take from this

**Owner decisions.**  None was needed: no finding contradicts a Decided statement of the design.  The owner's rule that a user's name spelled as an engine word stays verbatim held in every case, as did one spelling, one name.

**Amended in the design**, each marked there "(10.0, 2026-11-05)":

- **The constraint on messages**: `Position` added, read for `prepare` alone; the four constants are PostgreSQL's; SQL Server's `NamedIn` reads only four numbers' rows.  Its "Verify first" is answered.
- **Step 1**: the list is [finding 1](#1-the-fields-the-pipeline-reads); a member is kept when a decision reads it, and dropped when only the log or a failure's printed lines read it, as an error's detail and hint are; a member of a reference type that is not nullable is kept even when its value is never read, since its absence makes the answer unreadable, and so is a number of a value type that is not nullable, which holds nothing of the user's, as SQL Server's `class`, `state` and `declared`'s `columnOrdinal` are; 28 plan keys are dropped.
- **Step 2**: the vocabulary is a committed list per engine, as [finding 3](#3-the-vocabulary) chose; the names the planner makes, and the engine's words found by the renaming; a row of a system database is the engine's; a word is folded before it is compared.
- **Step 3**: the logical database that no marker or setting names stays; the class of a name the lexer finds first is its guess.
- **Step 5**: a nullability mark stays.
- **Step 8**: the tool's own `AS [n]`, a list asked in name order, and the copies of a reused parameter are derived by the pipeline's rule.
- **Step 9**: a raw name key is renamed as one name; the planner's `<alias>_<n>` and `CTE <name>`.
- **Step 10**: the position rule.
- **Step 12**: the table of fields; a user's CLR type's assembly-qualified name renamed whole.
- **Step 13**: two of the causes are caught by no miss, only by the whole entry.

**For 10.1**: nothing changes its scope.  The renaming's engine-neutral part takes, from its engine's part, a table of fields, the derived requests of step 8 and the vocabulary.

**For 10.2 and 10.3**: the lists of finding 1 are their allowlists; the vocabulary lists are made from the floor and default containers and committed, with a test that the corpora's words are classified; the six rules; and tests of errors built by hand, since no recorded case has one.

**Where merged code differs from the design**, as of `3b7d5c9b`: the realistic corpora hold no capture, so 10.0 captured them for the spike alone and 10.2's realistic test captures them again; SQL Server has no catalog of its keywords or intrinsic functions.
