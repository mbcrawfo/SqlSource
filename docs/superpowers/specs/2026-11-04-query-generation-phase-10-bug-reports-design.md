# Query generation, phase 10: bug reports - design

Date: 2026-11-04

Phase 10 of the [query generation epic](2026-10-07-query-generation-epic-design.md), its last.  A user whose model is wrong runs `sqlsource diagnose` on the one query and gets a bundle they can attach to a public GitHub issue without giving away their schema, their SQL or their data.  A maintainer runs `sqlsource replay` on the bundle with no database: when replay gives the bundle's answer the bug is in the pipeline and the bundle is a test, and when it does not the live step went another way.  It is five sub-phases, 10.0 to 10.4, each one pull request, run by an implementation manager from this document; each sub-phase's plan is written when the sub-phases it builds on have merged.

Written against `main` at `b050fffc`, after phase 9, the settings files moving every setting off the attribute, and the closed sets.  The epic's phase 10 section and its bug-report bullet under Diagnostics were written on 2026-10-07; this design replaces the section, amends the bullet where the owner changed it, and the epic outline is amended in the same pull request to point here.

## How to read this document

Each statement is one of three kinds, as in the phase 9 design:

- **Decided.**  Agreed with the owner on 2026-11-04.  A sub-phase must not contradict it without asking the owner and updating this document and the epic outline.
- **Designer's call.**  A proposal made while writing this design, which the owner has seen and not changed.  A sub-phase may change one when the code shows a better way, and says so in its pull request.
- **Verify first.**  A fact about the captures, the pipeline or the engines that the design depends on and that was found by reading code, not by running it.  Sub-phase 10.0 confirms each before any code depends on it.

A statement with no label is Decided.

## Intent

The describer is a pipeline, and when a user says "the model for this query is wrong", the step that gave the wrong answer has to be found from what the user will share.  Most users will share neither their schema nor their SQL, and an issue on GitHub is public.  Phase 10 gives them one command that makes a report a maintainer can run, and that is safe to post.

The seam that makes it possible is built: every call a describer makes goes through one exchange, a capture records the calls and their answers, and a replaying exchange answers from a capture with no database.  The describers' 91 fixtures are such captures.  What phase 10 adds is a bundle a user can make, a way to rename everything in it that is the user's, proof that the renamed bundle still gives the same answer, and the command that replays one.

## Goals

1. **`sqlsource diagnose --query <file>:<name>`** describes one query again against the user's database, through the capturing exchange, and writes a bundle and, beside it, a mapping file.
2. **Safe to post.**  Every name and literal of the user's is renamed by default.  The bundle holds only what the pipeline reads, and an error of the server only its code and the parameters it names.  `diagnose` refuses to write a bundle that still holds a token it cannot account for.
3. **Proven before it is written.**  `diagnose` replays the renamed bundle and writes it only when replay gives the renamed entry, so a bundle that is written reproduces what the user saw.
4. **A private route.**  `--no-obfuscate` writes a bundle with nothing renamed, which the tool says must not be posted; `--scrub` renames such a bundle later, with no database.
5. **`sqlsource replay <bundle>`** runs the pipeline from the bundle's capture with no database, prints each walk and table-match decision, compares its entry with the bundle's, and says which of the two outcomes it is.
6. **A bundle is a fixture.**  An unzipped bundle has a fixture's layout, so a maintainer turns a report into a regression test by copying a folder.

## Non-goals

- **The raw log in a bundle.**  Decided by the owner on 2026-11-04: the log is free text written by the server and the drivers, which can be neither renamed reliably nor checked.  Replay makes the walk and table-match events again from the capture.  A user attaches a log privately when a maintainer asks for it.  Not deferred work.
- **An error message's text in a bundle.**  Decided by the owner on 2026-11-04: an error keeps its SQLSTATE or error number and the parameters it names, in a text the tool writes.  The worst a message can hold is data, through a function PostgreSQL evaluates while it plans, then a literal of the query, names the query never used, from a hint, and roles, logins and paths of the server.  Not deferred work.
- **Blanking out a part that cannot be made safe.**  A part the pipeline reads cannot be blanked without changing what replay gives, and a part it does not read is dropped anyway.  `diagnose` refuses instead.
- **A bundle of several queries, or of a whole project.**  Designer's call: a report is about one query, and one query's bundle is one fixture.
- **Sending a bundle anywhere.**  The tool writes files; the user attaches them.
- **An example project.**  A bug report is no feature a user's code shows, so the examples do not change.

## Anti-goals

- **A bundle that leaks a name, a literal or a value of the user's.**  Every design choice below gives way to this one, but for the owner's decision that a name spelled as a word of the engine's stays verbatim.
- **A posted bundle that does not reproduce.**  A bundle that replays to another answer wastes a maintainer's time and the user's trust; the proof refuses to write it.
- **A renaming that changes what the pipeline decides.**  The renamed bundle gives the same provenance, `nullableSource`, `plan`, `tableMatch` and `typeSource`, as the original, on every fixture of both engines.
- **A `diagnose` that ships before the renaming.**  It could only write raw bundles, and people would post them.  So `diagnose` is the last sub-phase.

## Constraints

- **The capture format is a contract with every capture that exists** ([postgres-describer.md](../../architecture/postgres-describer.md)), and `captureVersion` is `1`.  A renamed capture is a capture of version 1: the renaming changes values, never the shape.
- **The replaying exchange keys an answer by the method and the request as it is serialized.**  So a renamed request must be byte for byte the request the pipeline makes from the renamed query.  The proof catches a mismatch as a capture miss.
- **Both describers read the user's SQL.**  PostgreSQL's walk reads the plan's expression strings with [`PlanTokens`](../../../src/SqlSource.Tool/Describing/Postgres/Walk/PlanTokens.cs), and SQL Server's proofs read the select list with the generator's lexer ([`Proofs/`](../../../src/SqlSource.Tool/Describing/SqlServer/Proofs/AGENTS.md)) and send slices of it to the server as `operands`.  The renamed SQL must read the same way to both.
- **Both describers read a server message's text in one place: the parameter it names.**  PostgreSQL takes the first `$n` of `42P18` and `42P08` ([`PostgresErrors.NamedIn`](../../../src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs)); SQL Server takes each `@name` of an error ([`SqlServerErrors.NamedIn`](../../../src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs)).  The tool also compares constant texts of its own that it writes into an answer, with or without a code: `PostgresLive.StatementNotListed`, `PostgresLive.TransactionRefused` with `08P01`, `ConnectionErrors.MultiplexingRefused` and `ConnectionErrors.UnreadableTypes`.  Verify first: no other read of a message's text, detail or hint, and no other such text.
- **A PostgreSQL error's position counts characters of the SQL that was sent**, and renaming changes lengths.  A position is mapped through the renaming.
- **Settings no longer come from the attribute.**  A query's dialect and database may come from a settings file, so a bundle's `query.sql` states both as markers of its own.
- **The closed sets.**  What differs by engine, the fields kept, the names kept verbatim, the renamer itself, is a `PerEngine<T>` or behind a switch over `DatabaseEngine` that names each engine ([closed sets](2026-11-04-closed-sets-design.md)).
- **The tool writes nothing into a project but its sidecars.**  A bundle and its mapping go where the user says, by default the current folder, never a project's folder unasked.
- **A connection's value goes nowhere but the describer.**  A bundle that `diagnose` describes is written through `CaptureJson.Write(DescribeCapture, ConnectionSecret)`, which refuses a capture that holds the secret, raw bundles included.  `--scrub` has no connection, and writes through `CaptureJson.Write(DescribeCapture)`: the raw bundle it reads passed the secret's check when it was written.
- The repository's rules apply in full: validate before each commit, `--full` before each push, branch-review, a describer's floor image after a change to it, and docs in the same pull request as the code they describe.

## What exists today

- **The seam** ([`Describing/Capture/`](../../../src/SqlSource.Tool/Describing/Capture)): `CapturingExchange` wraps the live exchange and records each call; `ReplayingExchange` answers from a `DescribeCapture` and throws `CaptureMissException` on a call it lacks; `CaptureJson` reads and writes the file.  Nothing in the released tool writes a capture.
- **The fixtures**: 65 PostgreSQL and 26 SQL Server cases under `tests/SqlSource.Tool.Tests/Fixtures/Describers/<engine>/<case>/`, each `query.sql`, `capture.json` and `expected.json`, and for the 8 cases of the walk under each join method, `walk-{left,right,full}-{hash,merge,nestloop}`, an `explain-settings.txt` of planner settings that the describer is made with and its `explain` requests carry (`FixtureCase.ExplainSettings`), recorded by `tools/record-fixtures.sh` and replayed by `FixtureReplay` in `tests/SqlSource.Tool.Testing/Fixtures/`.  `expected.json` is a sidecar of one entry with `toolVersion` `fixture`.
- **The calls a capture holds.**  PostgreSQL's include `open`, `prepare`, `columns`, `tables`, `types`, `spellings`, `check`, `explain`, `overloads`, `relations`, `partitions` and `deallocate`; SQL Server's `open`, `parameters`, `columns`, `table`, `operands`, `declared` and `probe`.  The calls of `PostgresCalls` and `SqlServerCalls` are the list of record, which 10.0 reads.  The names of the user's are in the SQL of requests, in catalog rows, in the plan's JSON and in errors.
- **The vocabulary the tool already holds.**  PostgreSQL's `overloads` asks which function and operator names only `pg_catalog` holds ([`CatalogOverloads`](../../../src/SqlSource.Tool/Describing/Postgres/Walk/CatalogOverloads.cs)); `PlanVocabulary` names the plan's node types; `TotalFunctions` the functions the walk interprets; a type row says its schema.
- **The walk's events**, `walk`, `walk.node`, `walk.column` and `table-match`, are written to the log through `PostgresEvents` and `SqlServerEvents`.
- **The architecture page** has a placeholder, [Bug-report commands](../../architecture/tool.md#bug-report-commands), and the manual a row, "Bug-report bundles: Coming", in its "What works today" table.

## Approaches

Decided by the owner on 2026-11-04: approach A.

- **A. Rename a finished capture (chosen).**  One function per engine takes a capture, its query and its entry, and gives back a renamed copy of each and the mapping.  It keeps only the fields the pipeline reads, renames names in catalog rows by their field, relexes SQL with the generator's lexer and the plan's strings with `PlanTokens`, and then checks every token and replays the result.  `--scrub` is the same function on a raw bundle, and every fixture tests it with no database.
- **B. Rename the query, and describe it through a translating exchange.**  The describer runs on the renamed SQL, and an exchange between it and the server turns each request back into the real names and renames each answer.  The requests match by construction, but each answer needs the same renaming by field as A, and the SQL is translated in both directions.  More work, for one gain the proof already gives.
- **Dropped: replacing text over the whole capture's JSON.**  A column named `name`, `status` or `user` collides with keywords and with the plan's text, case folding and quoting break it, and the classes of the epic need to know which field a name came from.

## Design

### The bundle

A zip file, by default `sqlsource-<query>.zip` in the current folder, `<query>` being the query's renamed name, or its own in a raw bundle.  Designer's call for the name and folder.  It holds:

| File | What it is |
|----|----|
| `bundle.json` | `bundleVersion` (`1`), whether it is renamed, the tool's version, the sidecar format's version, the engine, the server's version and the driver's, the runtime and the system, and the outcome: `entry`, or `failure` with the diagnostic's id and step when the query was refused |
| `query.sql` | The one query, renamed, with `-- dialect:` and `-- database:` written as markers of its own, so it plans the same with no settings file; markers that change no description, such as `-- summary:`, dropped.  Designer's call for what is dropped |
| `capture.json` | The capture, renamed, of the fields the pipeline reads alone |
| `expected.json` | When the outcome is an entry: a sidecar of that one entry, renamed, as a fixture's is |

Unzipped, it has a fixture's layout, which `replay` reads, and a recorded fixture folder, which has no `bundle.json`, replays too.  A fixture's `explain-settings.txt`, when there is one, is read by `replay` as `FixtureCase.ExplainSettings` reads it, and a renamed copy keeps it as it is, since its settings are the planner's words; a user's bundle has none, since `diagnose` sets no planner setting.  The mapping file is never inside it.

**A fixture made from a bundle** lives apart from the recorded ones, in `tests/SqlSource.Tool.Tests/Fixtures/Bundles/<engine>/<case>/`, keeping its `bundle.json`.  Designer's call.  It is replayed by the tool's tests alone: no `FixtureCasesTests` of `tests/DatabaseTests` describes it, since its renamed tables are in no `schema.sql`, and `tools/record-fixtures.sh` does not record it again when the default image moves, as root rule 5 requires of the recorded ones; when the capture's interface moves past it, it is deleted and its case tested again by hand, as the epic decided.  Its expected answer is `expected.json`, or, for a refused query, the failure of its `bundle.json`, compared by id and step.

### The mapping file

`<bundle>.mapping.json`, written beside the zip: each renamed name with its class and its original, so that the user can read a maintainer's findings in their own names.  It is the user's and stays private; `diagnose` says so when it writes it.  No key is written: the mapping holds every pair, and the key is thrown away with the run.

### Renaming

The steps that are the same for both engines, the keyed hash and the mapping, the literal and comment rules on the generator's lexer, the entry, the token check and the replay, are built once in 10.1, behind an interface of the engine's part: which fields it keeps, which names are its own, and how a request's SQL is made from the query.  10.2 and 10.3 each build that part for their engine.  Designer's call, so that 10.2 and 10.3 can be built beside each other; in `src/SqlSource.Tool/Bundles/` or a folder 10.1 names.

1. **Keep what the pipeline reads.**  A list, for each call of the engine, of the members of its request and its answer that the pipeline reads, and of the plan's keys the walk reads.  Every other member is dropped from the bundle, whether or not it could leak: a server's detail and hint, the plan's costs, rows and widths, an index name the walk does not read.  10.0 writes the list; Verify first.
2. **Find the user's names.**  Every name of a catalog row is the user's unless the row says it is the engine's, a type or relation of `pg_catalog`, `information_schema`, `sys` or `INFORMATION_SCHEMA`.  Every identifier of the SQL, of a marker, of a token's name and default, of the plan's strings and of `open`'s answer (SQL Server's `database`, and the values of PostgreSQL's settings, such as the schemas of `search_path`) is the user's unless it is a keyword, in the engine's vocabulary, or a name the planner makes.
   **Names the planner makes** stay verbatim as the engine's: `*SELECT* <k>`, `*VALUES*`, `column<n>`, `col<n>`, `InitPlan <k>`, `SubPlan <k>`, `unnamed_subquery`, `ANY_subquery`, `excluded`, `worktable` and the like, which the walk reads by their form, as `NullabilityWalk.SetOperations` reads `*SELECT* `.  10.0 lists them from the planner's source and the plans of each supported major.  The engine's own names stay verbatim, as the epic decided: `pg_catalog`, `public`, `sys`, `dbo`, and the built-in functions, operators and types.  **A name of the user's that is spelled as a keyword or a word of the engine's vocabulary stays verbatim too**, wherever it stands: a column `name`, `date`, `count`, `type` or `value`, an alias `old`, `new` or `left`.  Decided by the owner on 2026-11-04.  The renamer cannot tell the user's `name` from the type `name`, or a column `count` from the function, PostgreSQL names an unaliased `count(*)` column `count`, and the outer-join gate reads `LEFT`, `OLD` and `NEW` from the SQL; such a word says only that the schema uses a word of the engine's, and renaming it would make everyday queries fail the proof.  The manual says that such names are kept.  10.0 settles where the vocabulary of functions comes from: the capture's own `overloads` answer, a list committed from the containers of the floor and the default images, or both; Verify first.
3. **Give each a name.**  The names include the query's own `-- name:`, which says what the query is for, a token's name, the logical database's, and the physical database's, which SQL Server's `open` answers and each column's `sourceDatabase` repeats.  A name is replaced by a keyed hash: HMAC-SHA256 of its class and its folded spelling, keyed by 32 random bytes made for the run and never written, cut to eight hex digits and prefixed by its class, `s_` schema, `t_` table or view, `c_` column, `a_` alias, `f_` function, `y_` type, `i_` index, `p_` parameter, `k_` token, `q_` query, `d_` database, logical or physical, `l_` literal.  A collision lengthens the hash.  Designer's call for the prefixes and length.  The key makes two bundles of one schema unlinkable and a guessed name uncheckable.
4. **One spelling, one name.**  A spelling that is renamed has one replacement throughout a bundle, whatever class it is found in, and its class is that of the first catalog field it is found in, else of where the lexer finds it.  Designer's call, which narrows the epic's "per class": the plan's strings and the SQL cannot tell a column `status` from a table `status`, and a name renamed two ways breaks the walk's matching.  The class shows in the prefix and the mapping file.
5. **Fold and quote as the engine does.**  PostgreSQL folds an unquoted name to lower case, so `Users` unquoted and the catalog's `users` are one name, and `"Users"` another; SQL Server compares names ignoring case, by ordinal.  Designer's call for SQL Server's comparison; a database whose collation is case-sensitive is compared by ordinal.  A replacement is lower case, digits and `_`, which needs no quoting on either engine, and a name quoted in the SQL stays quoted.
6. **Literals.**  Every string literal, of each form the lexer reads (`E''`, `N''`, `U&''`, dollar-quoted, and the bit and hex strings), is a literal `'l_...'` of the same form, and an enum's label in a `types` answer is renamed as the literal of the same spelling, so `status = 'active'` and the label `active` stay one.  Numbers are kept: a number is a length, a precision, a `LIMIT` or a type modifier as often as a value, and renaming one changes what the server would have planned.  Designer's call; the manual says that numbers are kept.
7. **Comments** in the SQL become empty comments of the same kind, `--` and `/**/`, line breaks kept.  Designer's call.  A hint, `/*+ ... */` or `/*!...*/`, which the lexer keeps apart from comments, is SQL that changes the plan: its words are renamed by the mapping and its form kept.
8. **Rename the SQL once and derive the rest.**  `query.sql` is renamed token by token with the generator's lexer; the SQL of each request is then made from the renamed query as the pipeline makes it, the sample SQL, PostgreSQL's `$n` rewrite and its `@name` text for `check`, SQL Server's copies of a reused parameter and its `operands` slices, and must equal the request renamed token by token.  A difference is a refusal, never a guess.
9. **The plan's strings** are renamed through `PlanTokens`, a word or a quoted name by the mapping and a string by the literal rule.  A string `PlanTokens` cannot read is a refusal when the walk reads its key, and is dropped when it does not.  Decided as the fallback, whatever 10.0 finds.
10. **Errors.**  An error keeps its SQLSTATE or number, and its text is replaced by one the tool writes: for PostgreSQL's `42P18` and `42P08` `parameter $n`, for SQL Server each `@name` it named, renamed, and otherwise nothing.  A constant text of the tool's own that it writes into an answer and the pipeline compares is kept verbatim with its code, whatever the code: the four the constraints name, and any 10.0 finds.  A position is mapped through the renaming of the SQL it counts in.  This settles what TD-0034 left open for a user's capture.
11. **The entry** is renamed by the same mapping: names, origins, types of the user's, parameters, the logical database.  Its `hash` is computed again from the renamed `query.sql`, as replay computes it, and no digest of the original SQL goes into the bundle: a plain SHA-256 of the user's SQL would let a guessed query be checked.
12. **Check every token.**  Every string of the bundle that came from the user's database or query, the capture's requests and answers, `query.sql`, and the entry's names, types, origins and parameters, is split into tokens, and each must be a replacement, a word of the engine's vocabulary or a keyword, a name the planner makes, a number, or punctuation.  The strings the tool writes itself, the sidecar's fixed texts and closed values, `bundle.json`'s versions and the computed hash, are not the user's and are not checked; nor are the engine's fixed texts that 10.0 lists, such as the assembly-qualified name of a system CLR type in SQL Server's answer.  The first that is none is a refusal that names its place, the call, the member and the token.  Designer's call to print the token: the output is the user's own and they need to see it, and the manual says not to paste a refusal's line into an issue.
13. **Replay the result.**  The renamed bundle is replayed through `ReplayingExchange`, and its entry, or its failure's id and step, must equal the renamed one, whole.  A capture miss or a difference is a refusal.

A refusal of step 8, 9, 12 or 13 writes nothing and says to make a raw bundle with `--no-obfuscate`, keep it, and open an issue without it.

**The private route** is, for now, an issue on GitHub that says a raw bundle is ready and does not attach it; a maintainer says how to send it.  Decided by the owner on 2026-11-04: how a private report is received is settled when the need arises, and is neither tech debt nor deferred work, since it is no code.  The manual, the refusal, `--no-obfuscate`'s line and the issue template say this and no more.

### `diagnose`

```text
dotnet sqlsource diagnose --query <file>:<name> [<path>] [--project <path>]
                          [--connection <name>=<connection string>]...
                          [--out <path>] [--no-obfuscate] [--verbose] [--log <path>]
dotnet sqlsource diagnose --scrub <bundle> [--out <path>]
```

- **The run** plans the unit as `describe` does, with the `.sql` path as its filter, and describes the one query whatever its hash, as `--force` would, through a `CapturingExchange` over the live one.  It writes no sidecar and deletes none.  Its connections, `.env`, `--verbose` and `--log` are `describe`'s; the log stays the user's private file.
- **`--query <file>:<name>`** names the file as a `.sql` path of `describe` and the query by its `-- name:`.  The colon is the last one, so a Windows path reads.  A name that is no query of the file is an error.  Designer's call for the syntax; the epic gave it.
- **A refused query** still makes a bundle: a report of a refusal is a report too, and `bundle.json` holds the failure.  Designer's call: the refusal is a line of the outcome, `sqlsource: the query was refused: SQLSRC<n>`, not an error of the run, so the run exits `0` with no `RunAgainLine`; the user saw the error itself when `describe` ran.
- **`--out <path>`** is the zip's path.  A path that ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx` is refused, as `--log`'s is, and so is one that holds `=` or `;`.  The mapping goes beside it.
- **`--no-obfuscate`** writes the bundle with nothing renamed and no mapping, after the same `CaptureJson.Write` with the secret, and says on standard output that it must not be posted, and to open an issue without it, the private route above.
- **`--scrub <bundle>`** reads a raw bundle, renames it by the steps above, and writes the renamed bundle and its mapping, with no database and no project.  It refuses a bundle that is renamed already.
- **What it prints**: the paths it wrote, and a line that says the mapping is private.  Under `--online` it does not run: `--online` is `describe`'s.
- **Exit codes**: `0` when it wrote a bundle, `1` otherwise, by `Cli.ExitCodeOf` as for `describe`.

### `replay`

```text
dotnet sqlsource replay <bundle or folder> [--verbose] [--log <path>]
```

- **The run** reads a zip or a folder of the bundle's layout, plans `query.sql` by its own markers as `FixtureReplay.Plan` does, opens the engine's describer, made with the folder's `explain-settings.txt` when it has one, over a `ReplayingExchange` of the capture, describes, and builds the entry.
- **It prints** each walk and table-match decision, a line for each `walk.node`, `walk.column` and `table-match` event, which the describers already write; `--log` writes every event.  Designer's call: the lines are built from the events by `replay`, so no describer changes for it.
- **It compares** its entry with `expected.json`, whole, provenance included, or its failure with `bundle.json`'s, and prints the members that differ.  The same answer is `reproduced: the pipeline gives this answer`; another is `not reproduced`, with the versions of `bundle.json` beside the tool's, since a newer tool may decide otherwise.  A bundle with no expected answer prints what replay gives.
- **A capture miss** is an error that names the call the pipeline asked and says the bundle was made by another version of the tool, from `bundle.json`.
- **Exit codes**: `0` reproduced, `2` not reproduced, `1` an error, as `--check` uses them.
- **A raw bundle** replays like any other, and `replay` says it is raw.

### The issue template

`.github/ISSUE_TEMPLATE/bug-report.yml`: what was expected and what was given, the versions, and the bundle, with the line that the mapping file and a raw bundle are never attached, and that a user with a raw bundle says so in the issue.  Designer's call for its fields.

## Diagnostics

The tool's new errors take the next free `SQLSRC2xx` ids when their sub-phase is planned, in `ToolDiagnostics`, each with its section in `docs/manual/diagnostics.md`.  Designer's call: the ids are not fixed here, since 10.1 and 10.4 are planned at different times and other work may take ids between them.  The conditions:

- **10.1**: a bundle that cannot be read (not a zip or a folder of the layout, a file missing, JSON that does not read); a `bundleVersion` or `captureVersion` newer than the tool's; a call the capture lacks; an engine with no describer.
- **10.4**: a `--query` value with no `:` or naming no query of the file; a bundle that cannot be made safe, at a token, at the SQL of a request, or at a plan string; a renamed bundle whose replay differs; an `--out` path that is refused or cannot be written; `--scrub` on a renamed bundle.

## Sub-phases

Decided by the owner on 2026-11-04, and amended by the designer's call that the renaming's engine-neutral part is 10.1's: 10.0 comes first, 10.1 follows it, 10.2 and 10.3 can be built beside each other on 10.1, and 10.4 needs both.

| Sub-phase | Delivers |
|----|----|
| 10.0 Before the bundle | The spike |
| 10.1 The bundle and `replay` | The bundle's layout, its reader and writer, the command that replays one, and the renaming's engine-neutral part |
| 10.2 Renaming for PostgreSQL | The steps of renaming for PostgreSQL, with no command |
| 10.3 Renaming for SQL Server | The same for SQL Server |
| 10.4 `diagnose`, `--scrub` and close | The command, the user documentation and the issue template; closes phase 10 and the epic |

### 10.0 Before the bundle

A spike, throwaway code and a document, `docs/superpowers/specs/<date>-query-generation-phase-10-spike.md`, which amends this design where a finding changes a detail, as 7.0 and 9.0 did.  No database is needed but for the vocabulary.

1. **The fields the pipeline reads.**  For each call of each engine, each member of the request and the answer, and each key of PostgreSQL's plan: replay every fixture with that member removed or changed, and record whether the entry or the requests change.  Read with the code, it is the list of step 1.  A call that no fixture holds, as `partitions` is held by none, is listed from the code, and 10.2's realistic cases over partitions prove it.
2. **The plan's strings.**  Every string of every fixture's plan through `PlanTokens`: which read, which do not, and which keys the walk reads them from.
3. **The vocabulary.**  Where each engine's keywords come from, since the generator's lexer has no list of them: `pg_get_keywords()` on the floor and default images, and SQL Server's reserved and future keywords.  Whether `overloads`' answer covers the functions and operators of the fixtures and the realistic corpora, and what a committed list of `pg_catalog`'s functions, operators and types and of SQL Server's built-ins and system types would weigh, from the containers of the floor and default images.  The choice of step 2.
4. **A crude renaming** of every fixture, by a prototype, replayed: whether the provenance holds, where requests differ from those made from the renamed query, and whether SQL Server's proofs read the renamed select lists as they read the originals.
5. **Messages and positions.**  Every read of an error's text, detail, hint or position in both describers, to confirm the constraints above.

### 10.1 The bundle and `replay`

The layout, `bundle.json` with its version, a reader of a zip or a folder and a writer of a zip, in the tool; `sqlsource replay` as above; its errors; the renaming's engine-neutral part, with no engine's part yet, tested through the describer of the tests; `Fixtures/Bundles/` and its replay test; `tests/README.md`'s procedure for turning a bundle into a fixture; the architecture's bug-report section replaced by the bundle and replay.  Tests: `replay` on every fixture folder gives `reproduced`, on a fixture with `expected.json` changed gives `not reproduced` with the difference, on a capture with a call removed gives the miss, and on a bundle zipped from a fixture gives what the folder gives.  `docs/manual/tool.md`'s command reference documents `replay` as a command that checks a bundle someone gave you, with its errors in `diagnostics.md`, and the manual's "Bug-report bundles" row stays "Coming": no user can make a bundle yet.  Decided by the owner on 2026-11-04 (review finding R1-F13), by the repository's rule that the manual describes what works today.

### 10.2 Renaming for PostgreSQL

The steps of renaming for PostgreSQL, as a function of the tool with no command, and its refusals as values that 10.4 reports.  Tests, over every PostgreSQL fixture: the renamed bundle passes the token check, replays to its renamed entry, and gives the original's provenance; no name or string literal of `schema.sql` or of the case's `query.sql` that is not the engine's is found in any file of it; two runs give other names and the same provenance; a fixture that must be refused, such as a plan string that does not read, is refused at its place.  10.2 adds fixture cases of a `VALUES` list and of a scalar subquery, recorded on the default image as root rule 5 says, so that names the planner makes are in the fixtures, and its container test includes an `INTERSECT`, which PostgreSQL 14 to 17 plan with `*SELECT* <k>`, run on the floor image too.  And in `tests/DatabaseTests/Postgres`, each case of the realistic corpus captured on the default image, renamed and replayed to the same entry, so that 59 more queries prove it.  A change to the describer runs its tests on the floor image.

### 10.3 Renaming for SQL Server

The same for SQL Server, over its 26 fixtures and its 56 realistic cases, with the proofs' `operands` slices and the copies of a reused parameter derived from the renamed query.

### 10.4 `diagnose`, `--scrub` and close

`sqlsource diagnose` with `--query`, `--out`, `--no-obfuscate` and `--scrub`; its errors; the end-to-end test in each engine's `tests/DatabaseTests` project, which makes a bundle of a query of `app` against the container, finds none of the schema's names in it, and replays it to `reproduced`; the issue template; the user documentation; TD-0034 updated; the epic outline closed.  Designer's call: `tools/check-package-install.sh` replays a small committed bundle through the packed tool, so the packed `replay` is proven with no database.

## Testing

What each sub-phase proves is listed under it.  Across them, the epic's test holds: every fixture of both engines renamed gives the same provenance on replay as the original.  A fixture that the renaming must refuse is listed with why, and the list is expected to be short; each sub-phase's pull request says what is on it.

## Documentation

- **10.1**: `docs/architecture/tool.md`'s bug-report section becomes the bundle and `replay`, or a new page `docs/architecture/bundles.md` linked from it, the sub-phase's choice; `tests/README.md`, turning a bundle into a fixture; `docs/manual/tool.md`'s command reference, `replay`, and `docs/manual/diagnostics.md`, its errors.
- **10.2 and 10.3**: the architecture of renaming for the engine, in the same page; each describer's page names the fields its calls must keep as long as the pipeline reads them, since a describer that reads a new member makes the renaming keep it.
- **10.4**: `docs/manual/tool.md`, making a bundle, what it holds and what is renamed before you share it, numbers and names spelled as the engine's words being kept, the private route, `diagnose` in the command reference and `replay`'s entry, which 10.1 wrote, extended to a user's own bundle; `docs/manual/README.md`'s row flipped; `docs/manual/diagnostics.md`; a new FAQ answer in the README on reporting a wrong model, and the two commands in the `Use` section of `src/SqlSource.Tool/README.md`; TD-0034's capture paragraph and TD-0046's trigger, which name a bundle of phase 10.  `docs/manual/tool.md#logs`, which says a log is what to attach to an issue, says instead that a log is the user's own record, sent privately when a maintainer asks for it, and that a bundle is what an issue attaches; `Cli.RunAgainLine`, which offers a log for the user's own reading, stays as it is (designer's call).  The phase map of `docs/manual/AGENTS.md` already names `tool.md`'s pages.
- **The statements that name phase 10's bundle**, the rule at `src/SqlSource.Tool/AGENTS.md` that an event is read by the bundle, the comments of `Logging/RunEvents.cs`, `Logging/LogMember.cs` and `Describing/Capture/CaptureJson.cs`, and `docs/architecture/postgres-describer.md`'s note that a bundle's capture is a contract, are rewritten in the sub-phase that makes them wrong: the bundle reads no log, so an event's members bind the log's readers and `replay`'s lines, and a capture's members bind the renaming's list.

## Changes to the epic outline

In this design's pull request:

- The phase 10 row becomes five rows, 10.0 to 10.4, Not started, pointing here, and the paragraph under the table gains the order of phase 10's sub-phases.
- The phase 10 section is replaced by a pointer here and what changed from it: the bundle holds no log and no walk events, which replay makes again; an error keeps its code and the parameters it names; the bundle keeps only the fields the pipeline reads; one spelling has one name, whatever its class; `diagnose` replays what it writes; `diagnose` is the last sub-phase.
- The Diagnostics bullet "A bug-report bundle and replay" is amended to match.

## Left to the builder

- The folder and the types of the renaming, the bundle and the two commands.
- How `replay` and `diagnose` share `describe`'s planning, connections and logging.
- The exact text of each line `diagnose` and `replay` print, and of each error.
- The fields of the issue template.
- Where the vocabulary lives, once 10.0 has chosen its source.

## Calls made in this design

- One spelling, one name, whatever its class, narrowing the epic's "per class".
- Numbers kept; comments emptied; the class prefixes and an eight-digit hash.
- SQL Server's names compared ignoring case, by ordinal.
- A refused query still makes a bundle.
- A refusal prints the token it found.
- A refused query is a line of `diagnose`'s outcome, exit `0`.
- `Cli.RunAgainLine` unchanged.
- The bundle's default name, `sqlsource-<query>.zip` by the renamed name, in the current folder; the query's own name renamed, class `q_`.
- A fixture made from a bundle in `Fixtures/Bundles/`, never described against a container or recorded again, its refusal compared by id and step.
- The renaming's engine-neutral part built in 10.1, so that 10.2 and 10.3 stay side by side.
- `--scrub` as an option of `diagnose`, with no database.
- The ids of the new errors taken when each sub-phase is planned.
- The packed `replay` proven by the package-install check on a committed bundle.
