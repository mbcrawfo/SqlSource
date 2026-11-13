# Docs overhaul: the guide to a diagnostic section

This guide is for the agents of phases 21 to 24, which rewrite the 161 sections of `site/diagnostics.md`, and for their reviewers.  It is the part that the four briefs share.  It adds to [`rewrite-guide.md`](rewrite-guide.md), which holds the rules of every rewrite, the language rule, the procedure, the checks and the reviewer's kit.  Read that file first: a diagnostics phase uses its sections 1 to 3, 6, 7 and 10 to 12, and the report of its section 8.  This file says only what is special about a diagnostic section.

Which document wins: the [outline](../2026-11-12-docs-overhaul-epic-design.md), then the [content map](content-map.md), then the brief of the phase, then this guide, then the rewrite guide.

## The reader

- The reader has a build error or a tool error on the screen.  The help link of the error opened this section.
- The reader has read no other section and no other page.  So a section stands alone.
- The reader wants three things, in this order: "is this my error?", "what do I do?", "why?".
- A section can state the number that its own error enforces.  `SQLSRC235` says "PostgreSQL 14", and `SQLSRC247` says "Npgsql 8.0".  The reader has no other page open.
- A section does not teach the feature.  Its `See:` line links to the reference section that owns the rule.

## What never changes

No phase changes these, in any section:

- **The heading**, `## SQLSRCnnn`, and so its anchor, `#sqlsrcnnn`.  Every help link is `https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrcNNN`, and it is compiled into the packages.
- **The id.**
- **The bold title line.**  It is the first line under the heading that is not empty.  `DiagnosticsDocumentTests.Document_EachSection_StartsWithItsDescriptorsTitle` compares it with the descriptor's title, so do not change one character.  A title can use a word that the prose does not: `SQLSRC222` is "Directory cannot be read", and its text says "folder".
- **The order of the sections.**  `DiagnosticsDocumentTests.Document_Sections_AreTheDescriptorsInOrder` holds it.
- **The "Reported by" line and the "Database" line.**  Phase 2 wrote them.  Keep them word for word, directly under the title line.
- **Each fenced block**, byte for byte, unless your brief lists it.
- **The one paragraph above `## SQLSRC001`.**

Two more rules follow from the tests:

- No line of a section starts with `## `, also not inside a fenced block.  The test reads each such line as a new section.
- A section holds no heading of any level.  A heading adds an anchor and an entry to the list at the right of the page.

## The one shape

Every section has these parts, in this order.  A part in parentheses is there only when the section has the facts for it.

````markdown
## SQLSRCnnn

**The descriptor's title, word for word**

Reported by: (the line of phase 2, word for word)

Database: (the line of phase 2, in the 18 sections that have one)

What happened.  One short paragraph.

(Why.  One short paragraph.)

(A sample of the mistake, or of the error as the build or the tool prints it.)

**Fix:**

1. The first step.
2. The next step.

(A sample of the query or the file after the fix.)

(Notes: a list of facts that are not a cause and not a step.)

(The paragraph on severity, in the eight sections that have one.)

See: [the section that owns the rule](reference/page.md#anchor)
````

What goes into each part:

| Part | What it holds |
|----|----|
| What happened | What SqlSource found, in the reader's words.  What the message names.  Where the error points, when the section says so |
| Why | The rule that makes it an error.  What the error costs until it is fixed: which methods, models or members are not generated, and which still are |
| Fix | The steps, as instructions.  One instruction in a step, and at most 20 words |
| Notes | What is not this error.  What another id reports instead.  A fact about one case that few readers meet |
| Severity | That it is a warning or information, why, and how a project configures it |
| `See:` | One to three links.  The first is the reference section that owns the rule |

Rules for the parts:

- **Write a "why" only from the section, or from the reference section that it links.**  If neither gives a reason, the section has no "why" paragraph.  Do not invent one.
- **"Fix" is the one label.**  Write it as `**Fix:**` on a line of its own.  "What happened" and "why" have no label: they are the first two paragraphs.
- **Every section has a "Fix" part, but for two.**  `SQLSRC216` and `SQLSRC243` say that no dialect gets the error, so a reader has nothing to fix.  Those two have no "Fix" part.
- **Steps in order are a numbered list**, also when there is one step.
- **Fixes that are alternatives are a bulleted list.**  Write `**Fix:** do one of these.` and then one bullet for each alternative.  If an alternative has steps of its own, number them under its bullet.
- **A sample stands after the paragraph that it shows.**  A sample of the mistake or of the error line stands before the fix.  A sample of the fix stands after it.  The samples of a section keep their order.
- **No fenced block stands inside a list.**  An indented fence is another block of bytes.  Put the block after the list, and name in one sentence what it shows.
- **A note is a bullet that opens with a bold sentence**, as `SQLSRC219` has today: `- **Only the first difference of a query is named.**`
- **The `See:` line is the last line.**  Seven sections have none today, and your brief gives each one.
- **The first link of a `See:` line goes to a section of `reference/`.**  A guide can be a second link.  A `start/` page is never the target for a rule.
- **A rewrite changes a `See:` line only where its brief says so.**  The briefs give the seven new lines, the link to the table of supported versions for `SQLSRC235`, `247` and `267`, and the first link of `SQLSRC293` to `296`.  Each added link is a `new` row of the ledger.

If phase 2 put a fact under the "Reported by" line (where the error points, how often it is reported, what it costs), leave it there.  If the section's own text states the same fact, keep one statement and delete the other from the section's own text.  If the two differ in detail, keep the detail.  If the two disagree, the source decides, and the ledger gets a `corrected` row.

## Several causes

Some errors have several causes.  `SQLSRC236` has three, `SQLSRC277` has thirteen reasons, and `SQLSRC225` has two databases.

- Give each cause one bullet.  Open the bullet with a bold sentence that names the cause in the words of the message, where the message has words for it.
- Under the bullet, write a sub-list for that cause: what the reader sees, and what it costs.
- If each cause has its own fix, end each sub-list with the fix: `Fix:` and one sentence, or numbered steps.
- If all causes have one fix, write one `**Fix:**` part after the list.
- If each cause has its own sample, as the three of `SQLSRC120` have, a bullet cannot hold it.  Write each cause as a paragraph that opens with the bold sentence, and put its sample directly after it.
- If a section covers PostgreSQL and SQL Server, write the two as two causes: `**On PostgreSQL.**` and `**On SQL Server.**`.  Use no tab: text in a tab that is not selected is hidden from the browser's find, and that find is how a reader looks for a title on this page.
- Keep the order of today's causes.  The message, or another section, can refer to "the second reason".

## Three sections, before and after

The sections are not quite the baseline's when you start.  Phase 2 added its lines.  The move phases changed the page of each link, and mended the words that a move made wrong, such as a page of today named in words: the ledger of the move has each such unit as a row whose note starts with `position`.  A sentence that a move mended or corrected arrives true.  Keep it true.

In each "after", the "Reported by" and "Database" lines are stand-ins.  Keep the lines that phase 2 wrote.  Each "before" is the text of the baseline, `git show 28e20e3c:site/diagnostics.md`, with its links as the move phases left them.

### A short section: SQLSRC104

Before:

````markdown
## SQLSRC104

**Query name is used twice**

Two `-- name:` markers in one file have the same name.  The error is at the second.

Rename one of the queries.

See: [Queries and names](reference/sql-files.md#queries-and-names)
````

After:

````markdown
## SQLSRC104

**Query name is used twice**

Reported by: the build and the tool.

Two `-- name:` markers in one file have the same name.  The error is at the second marker.

**Fix:**

1. Rename one of the two queries.

See: [Queries and names](reference/sql-files.md#queries-and-names)
````

What to see: the section gives no reason, so it has no "why" paragraph.  A short section stays short.

### A section with several causes: SQLSRC236

Before, with its two fenced blocks cut short here:

````markdown
## SQLSRC236

**Connection does not keep a session**

The tool prepares a statement on the server and asks about it in later steps, in one transaction, which only works when every step reaches the same session.  This connection does not keep one.  There are two causes:

- The connection string sets `Multiplexing=true`, which shares physical connections between commands.  It is found when the database is opened, before the tool connects.
- A pooler in statement mode, such as PgBouncer with `pool_mode = statement`, sits between the tool and the server.  It refuses the transaction and closes the connection, so the first query of the database reports this error and every query after it reports [SQLSRC225](#sqlsrc225) with `the connection to the server was lost`.

The tool also reports this error at a query when a step finds that it reached another session: the server does not list the statement it just prepared, or answers `26000` (it does not exist) or `42P05` (it already exists).

For `Multiplexing=true` the tool refuses before it connects, so no query has been sent and the server is not yet known; the `server:` line is the tool's own text:

```console
(the first sample)
```

Behind a pooler in statement mode the session has opened, so the query line names the server and the driver, and the error is at the first step.  The `server:` line is the tool's own text, after the pooler's SQLSTATE; for `26000` and `42P05` it is the server's:

```console
(the second sample)
```

Connect directly to the server, or through a pooler in session or transaction mode, and without `Multiplexing=true`.  With PgBouncer, give the tool a database entry or a user whose `pool_mode` is `session` or `transaction`.  Only the tool needs this: your application can keep its own connection.  The error for `Multiplexing=true` is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is, and each of the database's queries counts as failed; one found at a query is reported at that query.

See: [Connecting](reference/databases/postgresql/index.md#connecting)
````

After:

````markdown
## SQLSRC236

**Connection does not keep a session**

Reported by: the tool.

Database: PostgreSQL.

The connection to the database does not keep one session.

The tool prepares a statement on the server and asks about it in later steps, in one transaction.  That works only when every step reaches the same session.

The cause is one of these:

- **The connection string sets `Multiplexing=true`.**  That setting shares physical connections between commands.
  - The tool finds it when it opens the database, before it connects.  No query has been sent, and the server is not yet known.
  - The `server:` line is the tool's own text.
  - The error is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is.  Each query of the database counts as failed.
- **A pooler in statement mode stands between the tool and the server.**  PgBouncer with `pool_mode = statement` is one.
  - The pooler refuses the transaction and closes the connection.
  - The first query of the database reports this error.  Every query after it reports [SQLSRC225](#sqlsrc225) with `the connection to the server was lost`.
  - The session has opened, so the query line names the server and the driver.  The error is at the first step.
  - The `server:` line is the tool's own text, after the pooler's SQLSTATE.
- **A step reached another session.**  The server does not list the statement that the tool just prepared, or it answers `26000` (it does not exist) or `42P05` (it already exists).
  - The error is reported at that query.
  - For `26000` and `42P05`, the `server:` line is the server's text.

The error for `Multiplexing=true`:

```console
(the first sample, byte for byte)
```

The error behind a pooler in statement mode:

```console
(the second sample, byte for byte)
```

**Fix:**

1. Remove `Multiplexing=true` from the connection string that the tool uses.
2. Connect directly to the server, or through a pooler in session or transaction mode.  With PgBouncer, give the tool a database entry or a user whose `pool_mode` is `session` or `transaction`.

Only the tool needs this connection.  Your application can keep its own connection.

See: [Connecting](reference/databases/postgresql/index.md#connecting)
````

What to see:

- Today's text says "two causes" and then gives a third in the next paragraph.  The rewrite lists three.  That is a change of a numeral, so the ledger notes it.
- Each cause has its sub-list.  The three causes share one fix, so one `**Fix:**` part follows.
- Both fenced blocks moved out from between the paragraphs, unchanged, each with one sentence that says what it shows.
- "Only" stays: "works only when", "Only the tool needs".  The checker lists a sentence that loses one.
- The codes `26000` and `42P05`, the two ids and every code span are there once each, as before.

### A section with a sample and a table: SQLSRC116

Before, with its table cut short here:

````markdown
## SQLSRC116

**Marker is not allowed here**

A marker stands in the wrong part of its file.  Some markers describe one query and go inside it, after its `-- name:` line; some describe the whole file and go before the first `-- name:` line.  The message says which this one is.

```sql error
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

Move the marker to where the message says.  A file with no `-- name:` line is one query, and takes every marker.

| Marker | File header | Inside a query |
|----|----|----|
| `-- name:` | no | starts one |
(16 more rows)

The table is this error's, with two exceptions.  A `-- summary:` in the file header is [SQLSRC107](#sqlsrc107), and a `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

See: [Markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place)
````

After:

````markdown
## SQLSRC116

**Marker is not allowed here**

Reported by: the build and the tool.

A marker stands in the wrong part of its file.  The message says which of the two kinds this marker is.

Some markers describe one query.  They go inside the query, after its `-- name:` line.  Other markers describe the whole file.  They go before the first `-- name:` line.

```sql error
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

**Fix:**

1. Move the marker to the place that the message names.

A file with no `-- name:` line is one query, and takes every marker.

| Marker | File header | Inside a query |
|----|----|----|
| `-- name:` | no | starts one |
(16 more rows, each unchanged)

The table is this error's, with two exceptions:

- A `-- summary:` in the file header is [SQLSRC107](#sqlsrc107).
- A `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

See: [Markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place)
````

What to see:

- The `sql error` fence keeps its info string and its bytes.
- The table keeps its 17 rows, in order, cell for cell.  A table is not rewritten as prose, and its cells are not reworded.
- The two exceptions became a list, since they are alternatives.

## Tables

The page has five tables: one in `SQLSRC116`, two in `SQLSRC219` and two in `SQLSRC224`.

- Keep every row, and keep the order of the rows.
- Keep each cell as it is.  The rewrite guide says so of every table of facts: a cell is not held to the limit of 25 words, and it is not made into a list.
- Reword a cell only when it is false.  That is a `corrected` row of the ledger, with its source.
- The first column of the first table of `SQLSRC219`, and of both tables of `SQLSRC224`, quotes the tool.  The reader matches it against the screen.
- Rewrite the sentences around a table, not the table.

## Facts at risk

These are the facts that a rewrite of this page can break.  Check each one where the table says.

| Fact | Where it stands | Check it against |
|----|----|----|
| The title | The bold line | The descriptor's `title:` in `src/SqlSource/Diagnostics/SqlDiagnostics.cs` or `ToolDiagnostics.cs`.  A test holds it |
| The severity | A sentence such as "This is a warning" | `defaultSeverity:` of the descriptor.  Eight are not errors: `SQLSRC026`, `031`, `121`, `240`, `245` and `284` are warnings, and `SQLSRC248` and `268` are information.  The dictionary `Configurable` in `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` lists them |
| Whether a project can configure it | "A project can list it in `NoWarn`", `WarningsNotAsErrors`, a `.globalconfig` file | The same eight, and no other: every other descriptor has `WellKnownDiagnosticTags.NotConfigurable`.  `SQLSRC026` differs from the others: the build raises it, so it stays a warning under `-warnaserror`, by `MSBuildWarningsNotAsErrors` in `src/SqlSource/build/SqlSource.targets`.  The general rule is on `reference/errors.md#severity` |
| Who reports it | The "Reported by" line | Do not edit it.  If the text below it disagrees, check `reference/errors.md#who-reports`.  The 55 ids of `ToolDiagnostics.cs` are the tool's, and the build reports `SQLSRC209` and `SQLSRC210` too |
| The message, where a section quotes it | A code span, or a `console` block | `messageFormat:` of the descriptor.  The messages of `SQLSRC015` to `020` are completed in `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs`.  The build raises `SQLSRC021` and `024` to `027` from `src/SqlSource/build/SqlSource.targets`.  The tool's lines under an error (`query:`, `step:`, `server:`, `help:`) come from `src/SqlSource.Tool/`; your brief names the files |
| A reason that the message ends with | A code span, such as `it is not static` | The source file that your brief names.  A reason is quoted, never reworded |
| A setting, a key, a marker, an option, a generator parameter, a value | A code span | The reference page of the thing, and the source: `schemas/settings-v1.schema.json`, `src/SqlSource/Parsing/Markers/SqlMarkerKind.cs`, `src/SqlSource/Settings/GeneratorParameters.cs`, `src/SqlSource.Tool/Cli.cs` |
| A numeral or a version | In the text, and in samples | The descriptor, and `reference/databases/index.md#supported-versions` for a version of a server or a driver |
| Where the error points, and how often it is reported | "at the query's name", "once for the project" | The tests of `tests/SqlSource.Tests/Generator/` whose names end in `DiagnosticsTests`, and `reference/errors.md` |
| What the error costs | "gets no method until it is fixed; its models are still generated" | `reference/errors.md#what-an-error-costs`, and the same tests |
| What is not this error | "is not this error", "is [SQLSRC233](#sqlsrc233) and not this error" | The other section.  A sentence with "not", "only", "never" or "unless" keeps that word |
| A link | In the text, and in the `See:` line | `tools/build-site.sh`, which fails on a bad anchor |

Three habits protect these facts:

- **Count the code spans of a section before and after.**  A list of values is where a rewrite loses one.  `SQLSRC019` and `SQLSRC111` hold more than a hundred code spans each.
- **Do not replace a list of values with a link.**  The sections keep their lists.  A test reads the list of `SQLSRC109`.
- **Do not make two sentences agree by guessing.**  If two sections disagree, read the source.

## Words

The language rule of `rewrite-guide.md` applies, with its table of terms.  These points are special to this page:

- Say "the build" and "the tool", as the "Reported by" lines do.  "Is reported" hides which one.
- A title keeps its words, and the text under it uses the terms of the rewrite guide.  The title of `SQLSRC222` says "Directory", and the text says "folder".  The title of `SQLSRC244` says "stale", and the text says "out of date".
- **List the outputs with the default first**: `repository`, `methods`, `models`, `sql`.  The rule of section 7 of the rewrite guide holds on this page too, for a whole list and for a part of one.  The second search of section 10 of that guide finds 52 lines of today's sections that break it, most of them "`methods` or `repository`"; the planning counted 61 phrases.  Your brief says where yours are.  Change the order, and nothing else of the phrase.
- **Text that the build or the tool prints keeps the order in which it is printed.**  Before you change the order of a list, find out whether the section quotes it: a message in a code span, a `console` block, or the values of a setting as a message names them.  Check `messageFormat:` of the descriptor, and for `SQLSRC015` to `020` the texts of `src/SqlSource/Diagnostics/SettingsFileDiagnostics.cs`.  If the list is printed, keep its order: the message of `SQLSRC273` says "has the output methods or repository", and the `console` block of that section shows it so.  If it is the section's own sentence, put `repository` first.  "`output` takes `sql`, `models`, `methods` or `repository`" in `SQLSRC019` and `SQLSRC111` is the case to check with most care: each of those two messages can end with the values that a setting takes, so read what the message ends with for `output` before you change the order.
- A new order changes no counted set of the checker, so no line of its output shows it.  The accuracy reviewer reads each list of outputs old against new: the same values, and no value moved to another condition.
- Say "query parameter", "generator parameter" or "logical database" where "parameter" or "database" alone is unclear.
- Name what the reader sees: the message, the `help:` line, the `server:` line.
- Write effects, never internals.  Run the grep of `site/AGENTS.md` over your range before each commit.  A hit that is text of a message, or SQL that a user writes, stays: `captureVersion` in `SQLSRC289`, `EXPLAIN` in the list of `SQLSRC233`.
- State no status.  `StatusLanguageTests` fails on its phrases.  "This version of SqlSource" in `SQLSRC248` is a fact about a version, and stays.

## Samples and fenced blocks

- A fenced block is byte-identical, old to new, unless your brief lists it.
- A `sql` fence keeps its info string.  This page has `sql`, `sql error`, `sql postgres` and `sql mssql`, and other pages have `sql fragment` too.  `UserDocsSamplesTests` parses a block by that string.  A block marked `sql` alone is parsed as `ansi`.
- You can move a block inside its section.  You cannot split one, join two or indent one.
- Do not add a sample.  If a section needs one that it does not have, say so in the pull request.
- A `console` block shows the error as the build or the tool prints it.  The samples of the tool's errors leave out the `see:` line, as the paragraph at the top of the page says.

## One file, four phases

Phases 21 to 24 edit `site/diagnostics.md` at the same time, each in its own range of ids.

- Edit only the sections of your range.  Your range starts at its first heading and ends on the line before the first heading of the next range.
- Do not reflow, reformat or mend a line outside your range, also not a typing error.  Report it in the pull request.
- Do not let an editor or a script rewrite the whole file.  Check for changed line ends and trailing spaces.
- Edit no other file of `site/`, and not `site/AGENTS.md`.  Four phases that edit one small file collide.  Phase 26 lands the rule on the shape of a diagnostic section in that file, after the four have merged.
- Rebase on `main` before each push.  Another diagnostics phase can have merged.
- If a rebase gives a conflict inside your range, resolve it.  If it gives one outside your range, take the side of `main`, whole.  Never resolve it by hand in your favour.
- After each rebase, write the new commit in the `# base` line of your ledger file.  Then run "check rewrite" again, and the check below.

This check prints nothing when no line outside the range changed.  Give it the first heading of your range and the first heading of the next.  The last range ends at the end of the file, so its second heading matches nothing.  Keep the braces in `${base}`: without them zsh reads `:s` as a modifier.

```bash
base=$(git merge-base origin/main HEAD)
outside() { awk -v a="## SQLSRC122" -v b="## SQLSRC226" '$0 == a { skip = 1 } $0 == b { skip = 0 } !skip'; }
diff <(git show "${base}:site/diagnostics.md" | outside) <(outside < site/diagnostics.md)
```

## The check

- Run "check rewrite" for your phase: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh rewrite <nn>`.  Sections 5 and 6 of the [brief of phase 0](../2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) give the form of a ledger row and what the command prints.  For this page it works section by section, and it knows a section by its `## SQLSRCnnn` heading.
- For each section it compares old and new: the counted sets of code spans, of diagnostic ids, of numerals and of table rows.  It lists each sentence that lost "not", "only", "never" or "unless".  It lists each fenced block that changed.  It gives the word count.
- "Old" is the section at the commit that the `# base` line of your ledger names: the commit of `main` that your branch starts from.  The baseline, `git show 28e20e3c:site/diagnostics.md`, shows the text before phase 2 added its lines.
- The command fails in three cases: a section has a difference and no row of your ledger names the section or a unit of it; a fenced block changed or went and no row names it; a unit of the old section is not in the new one word for word and has no row.  So every unit of every section of your range has a row.  A row can name the units of a whole section as one range of baseline lines, with `site/diagnostics.md#sqlsrcnnn` in the column `to`.
- A line that phase 2 added is not in the inventory.  A row names it by its place at your base, as `@site/diagnostics.md:<line>`.
- Each difference that the checker lists gets one note in `ledger/phase-<nn>.tsv`, as section 10 of the rewrite guide shows.  The note says why the fact is still there, or names the source that decided a correction.
- A `See:` line that you add, and a link that you add to one, is a `new` row.  A statement that you found false and mended is a `corrected` row.
- Recount your range last.  Put both counts, before and after, in a `#` comment line of the ledger file and in the pull request.
- Run the checks of section 10 of the rewrite guide, each on the last commit.  For "only your files", the files are `site/diagnostics.md` and your ledger file.  Add the check of "One file, four phases" above.
- The description of the pull request holds what section 10 of the rewrite guide lists.  For "the word count of each page", give the count of your range.  "Facts with no owner" is written as section 8 of that guide says: a section keeps every fact, so the list holds only a rule that a section states and the reference section of its `See:` line lacks.

This counts the words of a range.  It gave the counts of the content map at the baseline.

```bash
awk '/^## SQLSRC122$/ { on = 1 } /^## SQLSRC226$/ { on = 0 } on' site/diagnostics.md | wc -w
```

The same `awk` gives a search its range: pipe it into `grep -n -i -E` with the pattern of the search for internals, for status words, or for the order of the outputs.  After the rewrite, each line that the last one prints is text that the build or the tool prints.

It also counts the sections and their closing lines.  Piped into `grep -c '^## SQLSRC'` and into `grep -c '^See: '`, it prints the same number twice after the rewrite: every section of the range ends with a `See:` line.

## Size

- A phase reads about 11,000 words of today's text.  The outline's limit for one rewrite is about 12,000.
- The lines that phase 2 added are new text.  The reviewer does not read them old against new, so they do not count against the limit.
- A section can grow.  Steps and lists are longer than one dense paragraph.
- A section does not grow by new facts.  If the new text of a section is more than half again as long as the old, read it once more for sentences that say nothing.
- `diagnostics.md` is never split.  The page stays one page whatever its count, by decision 7 of the outline.
- A section that feature work added after the baseline belongs to the range that its id falls in.  An id above `299` belongs to phase 24.  Rewrite it with your range, and give it rows in your ledger.
- If your range then holds more old text than the limit, make two pull requests, cut between two sections, and say so in both.

## Calls made in this guide

The outline gives the shape in one sentence.  These points settle what it leaves open.

- **"Fix" is the only label, and it is bold text, not a heading.**  Six sections use `**Fix:**` today.
- **A fix of one step is a numbered list of one step.**  The reader then finds the fix in the same place in every section.
- **Alternatives are bullets under "do one of these".**  The outline says "numbered steps", and its language rule says "a list for alternatives".  Numbers on alternatives read as an order.
- **A sample of the mistake stands before the fix.**  The outline names the sample after the fix.  Today's sections show the mistake first, and a reader checks "is this my error?" before reading the fix.
- **No "why" is invented.**  A section whose text gives no reason has no "why" paragraph.
- **No fix is invented.**  `SQLSRC216` and `SQLSRC243` have no "Fix" part.
- **Several causes are bullets with sub-lists**, and two databases are two causes, never tabs.
- **No fenced block stands inside a list**, so that every block stays byte-identical.
- **The lines and sentences of phase 2 stay as they are.**  A duplicate is removed from the section's own text.
- **The paragraph on severity is the last part before `See:`.**
- **A list of outputs has `repository` first on this page too**, as on every other, but for text that the build or the tool prints.  The map asks for the order "everywhere".
- **A `See:` line changes only where a brief says so.**  A link is a fact about which section owns a rule, so each added one is named in a brief and checked by the accuracy reviewer.
- **`site/AGENTS.md` gets the rule on this shape in phase 26**, not in a diagnostics phase.
- **The order of authority** puts this guide under the briefs and over the rewrite guide, since it is the shared part of four briefs.
- **A section that feature work added** is rewritten by the phase of its range, and one above `299` by phase 24.
