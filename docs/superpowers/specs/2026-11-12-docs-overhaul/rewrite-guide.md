# Docs overhaul: the rewrite guide

This guide says how a rewrite is done.  It is for the agent of each rewrite phase, 12 to 24, and for the reviewers of that phase.  It belongs to the epic of the documentation overhaul: [the outline](../2026-11-12-docs-overhaul-epic-design.md) and [the content map](content-map.md).

Each phase also has a brief.  The brief names the pages, the merges and the traps of that phase.  This guide holds what every rewrite shares, so that a brief can be short.

- **Which document wins.**  The outline, then the content map, then the brief of the phase, then this guide.
- **Which parts a diagnostics phase uses.**  Phases 21 to 24 use sections 1 to 3, 6, 7 and 10 to 12, and the report of section 8.  The shape of a diagnostic's section is in [`diagnostics-guide.md`](diagnostics-guide.md).
- **What this guide does not repeat.**  "How a phase is done", "The ledger" and "Reviews" of the outline apply to every phase.  Read them there.  The [brief of phase 0](../2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) is the specification of the inventory, the ledger, the checker and the merge worksheet: a unit is one line, a ledger row has six columns, and the checker is `check/check.sh`.

## 1. What a rewrite is

After phase 10, every anchor of the content map exists.  After phase 11, every page of the new site is at its final address.  The text of `guides/` and `reference/` is the text of the old pages, moved word for word.  A rewrite changes the words of that text, and nothing else.

A rewrite does four things to a page:

1. It puts each section in its shape (sections 4 and 5).
2. It writes each sentence in plain language (section 3).
3. It merges the near-duplicate texts that a move placed side by side (section 9).
4. It cuts a rule that another section owns down to a short restatement with a link (section 8).

It keeps every fact.  A reader who knew the old page must find each rule, number and error on the new one, or behind one link.

**Text that a rewrite leaves alone.**  Some text on a page is not old text.  A move phase, phase 2, phase 9 or phase 10 wrote it new, in plain language.  The ledger of that phase marks it `new`.  Do not reword it.  Change it only when a merge or the shape of the page needs the change, and say so in your ledger.

**Text that arrives corrected.**  A move made each correction that the map lists for a sentence it carried, C-01 to C-08, with a `corrected` row.  It also mended the words that the move made wrong: "this page", "above", "below", a page of today named in words.  The ledger of the move has each such unit as a row whose note starts with `position` and gives the old words and the new.  So no brief of a rewrite plans a correction.  Where your brief says "arrives corrected", check the sentence against the source that the brief names, and keep it true when you reword it.  A statement that you find false yourself is corrected in your phase (section 8).

## 2. The rules of a rewrite

A rewrite runs beside twelve other rewrites.  These rules keep them apart, and keep every link true.

- **You change only your files.**  They are the pages your brief names, your ledger file `ledger/phase-<nn>.tsv`, and the worksheets `merges/MP-<nn>.md` of your merge pairs.  You change no `toc.yml`, no `AGENTS.md`, no test, no example and no other page.
- **Two files are shared, each in parts that do not meet.**  Phases 21 to 24 each edit their own range of `site/diagnostics.md`.  Five phases write in `merges/MP-10.md`, each under the h2 sections of its own terms, which the move phases made.  If a rebase gives a conflict in such a file, keep your own part and take the side of `main` for the rest.
- **No anchor is added or removed.**  Each `<a id>` line of the page stays, once, above the heading of the same section.  You write no new `<a id>` line.  A merge moves the `<a id>` line of its second text up to the merged heading (section 9), and that is the only move of one.
- **No link in another page changes.**  So no section moves to another page, and no page is renamed.
- **A link of your own page keeps its target.**  You can reword the text of a link.  You can add a link to an anchor that exists.  You remove a link only with the sentence that holds it.
- **No page is created, split or removed.**  A page that is too long stays one page.  You report its size, and phase 25 splits it.
- **A fenced block is byte-identical**, its info string included, unless your brief lists the block.  You can move a block inside its section, so that the section opens with it.
- **You add no fenced block** unless your brief lists it.  A new sample is new text: it needs a source and a review of its own.
- **Sample code stays.**  Tests and the example projects mirror some samples (section 8 of the map).
- **Every fact survives.**  Section 6 says which facts drift first.  Section 8 says the three ways a fact can leave a page.
- **A heading can be reworded.  Its `<a id>` stays.**  A heading that you merge from two keeps both `<a id>` lines above it.  A heading that a file outside `site/` quotes keeps its words: section 6 lists them.
- **The order and the level of the headings are the map's** (section 3 of the map, "Per page").  Change a level only where the map or your brief says so.  You add a heading only where the map or your brief names it: the five h3 headings of `settings-file.md` in phase 14, under anchors that phase 7 made, and `## If it does not work` of a guide in phase 12, which has no `<a id>` line (section 5).  You remove a heading only in a merge (section 9).
- **The title of a page stays.**  The `# ` line and the sidebar title come from section 1 of the map.
- **You add no tab group** unless your brief names the place.  A tab holds a sample or a few sentences.  It never holds a heading or an anchor.
- **Write effects, never internals.**  The rule and its `grep` command are in `site/AGENTS.md`.  Run the command on each page you changed.
- **State what SqlSource does, as fact.**  No sentence says that a feature is coming, is new, or works "today", "for now" or "currently".  `StatusLanguageTests` holds five exact phrases.  The rule is wider than the test.
- **Links.**  A page links another page of the site relatively, and an error as `[SQLSRC241](../diagnostics.md#sqlsrc241)`, with the path that fits its folder.

## 3. The language rule

The outline gives the rule in seven lines.  This section gives each line with a real sentence of the old pages, before and after.  The pairs leave out link targets, to stay short.  On a page, every link stays.

### 3.1 One instruction in a sentence.  At most 20 words in a step and 25 in a description

Count a code span as one word.

Before, from "Output" of `settings.md` (48 words):

> `sqlsource describe` asks a database about the queries whose output is `models`, `methods` or `repository`, and the build checks that each of them has a current entry in its file's sidecar: a query that has none is SQLSRC241, and one that changed since it was described is SQLSRC244.

After:

> `sqlsource describe` asks a database about each query whose output is `repository`, `methods` or `models`.  The build checks that each of these queries has a current entry in the sidecar of its file.  If a query has no entry, the build reports SQLSRC241.  If a query changed after it was described, the build reports SQLSRC244.

The two ids, the three outputs and the word "current" are all there.  The outputs are in the order of section 7.

A step is a sentence that tells the reader to do something.  Before, from "When a bundle cannot be made safe" of `tool.md`:

> Keep the raw bundle, and open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it.

After:

> 1. Keep the raw bundle.
> 2. Open an issue on GitHub.  Say in it that a raw bundle is ready.
> 3. Do not attach the bundle.  A maintainer tells you how to send it.

### 3.2 Active voice and present tense.  Not "will"

Before, from "All settings" of `settings.md`:

> A value from a list is matched ignoring case, hyphens and spaces, so `Methods`, `methods` and `me-thods` are one value, and so are `SealedRecord` and `sealed record`, and `NestedClass` and `nested-class`.

After:

> SqlSource ignores case, hyphens and spaces when it matches a value from a list.  So each of these lines is one value:
>
> - `Methods`, `methods` and `me-thods`
> - `SealedRecord` and `sealed record`
> - `NestedClass` and `nested-class`

Before, from "Reporting a wrong model" of `tool.md`:

> To see what the maintainer will see, run `replay` on the zip: it says `reproduced`, and its lines name the new names.

After:

> To see what the maintainer sees, run `replay` on the zip.  It says `reproduced`, and its lines name the new names.

**An active sentence names who acts.**  The actors of these pages are few: you, SqlSource, the build, the tool, the database, the driver, the generated method.  Name the right one.

- "Is reported" hides whether the build or the tool reports an error.  The "Reported by" line of that error's section in `site/diagnostics.md` says which.
- The form "a query that has none is SQLSRC241" can stay.  It is short, and it names no actor.
- If you cannot find the actor in the source, keep the passive.  A wrong actor is a false statement.

### 3.3 One term for one thing

The old pages use several words for some things.  A rewrite uses one.  Every rewrite uses the same one, so the choice is here:

| The thing | Write | Not |
|----|----|----|
| A type and its `.sql` file | The type *claims* the file | "uses", "gets", "owns"; the file "belongs to" the type |
| A column or a value | It *can be null*, or it is *never null* | "possibly null", "may be null".  Keep "nullable" for a C# property or type |
| An entry of a sidecar | It is *current*, or it is *out of date* | "stale", "not current", "outdated" |
| What the tool does to a query | It *describes* the query | "inspects", "analyses", "checks against the database" |
| The file `sqlsource.json` | A *settings file* | "configuration file", "config" |
| A place on disk | A *folder* | "directory", in prose.  A name such as `Directory.Build.props` stays |

The terms of `site/AGENTS.md` hold too.  Say *query parameter*, *generator parameter* or *logical database* where "parameter" or "database" alone is unclear.  Say *inside a query* for a marker under a `-- name:` line.

The 17 terms of the old `concepts.md` each keep one sense:

- query, marker, file header, sidecar, settings file;
- output, query method, repository, token method;
- query parameter, generator parameter, token;
- dialect, logical database;
- generator, tool, describe.

The name of a control stays as its product writes it.  Rider's field is "Working directory".

### 3.4 A condition comes first

Before, from "List parameters" of `parameters-and-tokens.md`:

> A list that is null where its property cannot be is sent as an empty list.

After:

> If a list is null and its property cannot be null, the query method sends an empty list.

### 3.5 "Must" for what is required and "can" for what is allowed

Before, from "Settings files" of `settings.md`:

> It may hold `//` and `/* */` comments, and a comma after the last member of an object or the last element of an array.

After:

> It can hold `//` and `/* */` comments.  It can also hold a comma after the last member of an object or the last element of an array.

Write "must" only for a rule that an error or a failure holds.  Then name the error in the same sentence or the next.

### 3.6 A list for conditions and alternatives, not a long sentence

Before, one clause of the bullet "What does not fit" of "Visibility" in `settings.md`.  The clause has 65 words and four conditions.  The bullet has 161 words:

> `public` or `protected` for a query method that takes or returns a model that is internal, on the type or for its nested class, when the type, or a type around it, is `protected` or `protected internal`, and none of them is `internal`, `private` or `private protected`, since a class derived from it in another assembly could call the method, and its models are internal

After:

> `public` or `protected` does not fit a query method when all of these are true:
>
> - The visibility is for a method on the type, or for the nested class that holds the method.
> - The method takes or returns a model that is internal.
> - The type, or a type around it, is `protected` or `protected internal`.
> - None of those types is `internal`, `private` or `private protected`.
>
> Why: a class in another assembly can derive from the type and call the method, but the models are internal.

Say whether the items are joined by "all of these" or by "one of these".  The old sentence says it with "and", "or", "when" and "unless".  Read it twice.

Before, from "Running in the build" of `tool.md`, a paragraph that is a list:

> A build runs the tool only when something it reads has changed since the tool last succeeded: a `.sql` file, a C# file, a settings file, a sidecar, changed or deleted, the `.env` file, the tool manifest, which updating the tool rewrites, the version of the `SqlSource` package, the value of `SqlSourceOnline`, or which file `SqlSourceEnvFile` names.

After:

> A build runs the tool only when one of these changed after the tool last succeeded:
>
> - a `.sql` file, a C# file, a settings file or a sidecar, changed or deleted;
> - the `.env` file;
> - the tool manifest, which an update of the tool rewrites;
> - the version of the `SqlSource` package;
> - the value of `SqlSourceOnline`;
> - the file that `SqlSourceEnvFile` names.

The word "only" stays.  It is the rule.

### 3.7 A paragraph has one topic and at most six sentences.  A bullet has one rule

Before, one bullet of "List parameters" of `parameters-and-tokens.md`.  It holds five rules:

> - **An empty list matches no row after `IN`, and every row after `NOT IN`.**  So does a null one.  On SQL Server, under the output `methods` or `repository`, a list after a `NOT` inside an aggregate, a `GROUP BY` or an `OUTPUT` clause is SQLSRC127, and one inside an aggregate of your own fails when it is empty or null: see known issues.  On PostgreSQL, so does a list in the index predicate of `ON CONFLICT`: see PostgreSQL's parameters.  A null list makes `@categories IS NULL` true and an empty one makes it false, so above a null `Categories` keeps every product and an empty one keeps none.

After:

> - **An empty list matches no row after `IN`, and every row after `NOT IN`.**  A null list does the same.
> - **`@categories IS NULL` is true for a null list and false for an empty list.**  So in the sample, a null `Categories` keeps every product, and an empty one keeps none.
> - **On SQL Server, a list after a `NOT` inside an aggregate, a `GROUP BY` or an `OUTPUT` clause is SQLSRC127**, under the output `repository` or `methods`.
> - **On SQL Server, a list after a `NOT` inside an aggregate of your own fails when it is empty or null.**  See the limit.
> - **On PostgreSQL, a list in the index predicate of `ON CONFLICT` fails when it is empty or null.**  See PostgreSQL's parameters.

Two pointer words carried a fact here.  "One inside an aggregate" means a list after a `NOT`.  "So does" means "fails when it is empty or null", not "is SQLSRC127".  The section that owns each rule says so.  **Never guess what "so does", "the same", "one" or "it" points at.**  Read the owner's section or the source.

### What ASD-STE100 asks that this guide does not

ASD-STE100 is the target as a guideline.  The seven lines above are the whole rule.  The standard asks for more, and a rewrite does not apply these parts:

- **The controlled dictionary.**  The standard has a list of approved words, each with one meaning.  Use the common word.  The names of .NET, of SQL and of SqlSource stay as they are.
- **The rule against words that end in "-ing".**  "Describing", "streaming" and "padding" stay.
- **The limit of three nouns in a row.**  It is not a rule here.  Still, cut a chain of possessives: write "the `-- column:` marker of the query", not "its query's `-- column:` marker's".
- **The fixed forms of a warning and a caution.**  A docfx alert is enough where a page has one.

No test holds the language rule.  A reviewer applies it.

### When a rule and precision conflict, precision wins

- **Keep the limiter word, and split the sentence.**  Never drop "only", "not", "never", "unless", "alone", "at most" or "at least" to make a sentence shorter.
- **A sentence can pass 25 words** when the extra words are names in code spans that cannot be a list, as inside a table cell.
- **A table cell is not made into a list.**  Keep a table of facts as it is: a type table, the matrix of settings, the exit codes, the marker index.  Reword a cell only when it is false, or when it holds a rule that needs a sentence of its own under the table.
- **A sentence that you do not understand is not rewritten.**  Find the rule in the source or the tests first.  If you cannot, leave the sentence, and say so in the pull request.

## 4. The shape of a reference section

A reader arrives at a reference section by a link or by search.  The section opens with an example, then gives the rules.

```markdown
<a id="setting-collection-type"></a>

## Collection type

(the sample: the thing in use, in a fenced block)

One or two sentences: what the thing is, and what the sample shows.

| Value | What you get |
|----|----|

- **A lead-in that states one rule.**  One or two sentences more.
- **The next rule.**  ...

See: (the errors of the section, each a link)
```

- **Example first.**  If the section has a fenced sample, the sample opens the section.  If it has none, open with the concrete case that the old text holds, as a sentence: "`UserDto` and `UserDTO` are two models."
- **The term comes next.**  A section that defines a term defines it in the sentence after the sample, once.
- **A setting's section has a fixed order.**  The example, the values, the default, the places it can be written, what is special about its precedence, its errors.
- **One rule in a bullet.**  The bold lead-in states the rule, so that a reader who reads only the lead-ins gets the rules.
- **Reference states rules, not how-to.**  A procedure belongs to a guide.  Where the old text holds one, keep it short and keep it: a rewrite moves no text to another page.

A page that has limits ends with them.  The move phase wrote each `### ` heading and its anchor.  Do not reword the heading.  Write the body in three parts, with these labels:

```markdown
<a id="limits"></a>

## Limits

<a id="limit-short-password"></a>

### A short password shows in the output or a log

**What you see.**  The symptom, in the reader's words: the error id, the exception and its message, or the wrong result.

**Why.**  The cause, as an effect.  One or two sentences, and no internals.

**What to do.**  The way round it.  Numbered steps when there is more than one.
```

A limit names no item of `docs/tech-debt` or `docs/deferred`, and no id of one.

## 5. The shape of a guide

A guide is one task.  Its shape is fixed (section 3 of the map, "Guide pages"):

```markdown
# The title, in the reader's words

One sentence: what the reader can do after this page.  It carries the search words of the map's section 1.

**Use this when** the situation, in two sentences.

<a id="the-maps-anchor"></a>

## The task, as the reader says it

(the whole sample: every file the reader writes, each in its own fenced block)

**What you get.**  The generated members, by name, and the call.

**The rules you need.**

- One rule, in one or two sentences.  See [the owner's heading](../reference/page.md#anchor).

(more h2 sections of the map: a variation, with its sample and its rules in the same order)

## If it does not work

- **What the reader sees.**  One sentence: why, and what to do.  See [the limit or the error](owner#anchor).
```

- **The three labels are bold words that open a paragraph**, not headings.  The list on the right of a page then shows the tasks alone.
- **`## If it does not work` is the last h2 of a page, and has no `<a id>` line.**  The map names no anchor for it.  On `guides/dapper.md` alone, `## Limits` follows it and ends the page.
- **A guide is whole for its task.**  The reader does not need the link to finish.
- **A guide shows a setting in use and links it.**  It never lists the values, the default or the places of a setting.
- **A guide owns a sample and a procedure.**  It owns no value, no default and no precedence.
- **`## Limits` stands on a guide only when the guide owns a limit.**  Among the guides that is `guides/dapper.md` alone.
- **A long difference between the databases is two anchored sections**, never tabs.

The brief of phase 9 has the same skeleton, and phase 9 wrote its sections by it.  The brief of phase 12 says how the parts of the shape meet those sections.

## 6. Facts that must not drift

A rewrite loses facts in small ways.  These are the places to look.

- **Limiter words.**  "Not", "only", "never", "unless", "alone", "at most", "at least", "except", "whatever".  The checker lists a sentence that lost one of the first four.  Look for the others yourself.
- **Numerals and versions.**  Keep the number and its form: `1,000`, `65,535`, `2,098`, `512`, Npgsql 8, Microsoft.Data.SqlClient 6.1.  A word such as "two" stays a word.
- **Supported versions have one home**, `reference/databases/index.md#supported-versions`.  A rewrite adds no version to a page that had none.  A sentence on what a version changes stays where it is.
- **Diagnostic ids.**  Each id stays with its own condition, as a link.  When you split a sentence, check which half each id belongs to.
- **Code spans.**  A name, a value, a marker, an option, a variable, a file name.  Keep the spelling and the case.
- **Messages.**  The text of an exception or of the tool's output is what a reader searches for.  Keep it letter for letter, with its quotation marks.
- **Table rows.**  Each row of each table stays.  Tests read the first column of some tables: the marker index, the matrix of settings, the options of the tool.
- **Support facts.**  "Has not been checked", "is not tested" and "checked in Rider on macOS" are facts about support.  They stay.
- **Azure SQL** is "accepted, and not tested".  No page says that it is supported.
- **Words that a file outside `site/` quotes.**  Seventeen comments of the examples and the tests quote a heading, or the bold words that open a paragraph, by its words.  No test compares the quoted words with the page.  The rule: **the page keeps the words**, letter for letter, and the heading or the bold lead-in keeps them as its whole text.  The other way, to change the comment in the same pull request, is not open to a rewrite: a rewrite changes no file outside its pages, and that is what lets thirteen of them run at one time.

The seventeen, with their lines at the baseline.  The move phases changed the page that each comment names, and not the quoted words.

| Quoted words | Quoted by | On the page | Phase |
|----|----|----|----|
| "Run the models through Dapper", a heading | `examples/PostgresExamples/DapperSetup.cs` 10; `examples/SqlServerExamples/DapperSetup.cs` 8 | `guides/dapper.md#models-through-dapper` | 12 |
| "With SQL Server", a heading | `examples/SqlServerExamples/DateOnlyHandler.cs` 9; `examples/SqlServerExamples/TypedParameter.cs` 9 | `guides/dapper.md#dapper-with-sql-server` | 12 |
| "Converters", a heading | `EmailAddress.cs` 8, `OrderId.cs` 8 and `Price.cs` 8, in `examples/PostgresExamples/Methods/` and in `examples/SqlServerExamples/Methods/` | `reference/converters.md#converters` | 15 |
| "A class in a JSON column", a heading | `DeliveryDetails.cs` 6, in the same two folders | `reference/library-types-and-json.md#json-class` | 15 |
| "Query methods", a heading | `OrderLineQueries.cs` 12, in the same two folders | `reference/query-methods.md#query-methods` | 17 |
| "Give the method the transaction", bold words | `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` 12, and the message of its line 22 | `reference/connection-methods.md#method-connection` | 17 |
| "What a method reads and throws", a heading | `examples/PostgresExamples/Methods/StoreDataSource.cs` 9 | `reference/databases/postgresql/query-methods.md#method-results` | 19 |
| "A synchronous method needs a data source without multiplexing", bold words | `tests/DatabaseTests/Postgres.SyncAsync/KnownDifferences.cs` 10, and the message of its line 18 | The same section | 19 |

The brief of phase 8 turns the four comments of the first two rows into links to the anchor.  If it did, those two headings are free.  Before you reword any heading, look for its words outside `site/`:

```console
$ git grep -n -F '"<the heading>"' -- . ':!site' ':!docs/superpowers'
```

If a file quotes the heading, keep the heading.

## 7. The owner's reframings

Wherever a page of yours touches one of these, write it this way.

- **The outputs are listed with the default first**: `repository`, `methods`, `models`, `sql`.  This holds for a table, a list and a sentence, and for a part of the list: "`repository` or `methods`", "`models` or `sql`".  It holds on every page, `site/diagnostics.md` too.  Text that the build or the tool prints keeps the order in which it is printed: a message in a code span, a `console` block.
- **`repository` is the default.**  A page that shows one output shows this one, unless the section is about another.
- **`methods` is for your own code structures**, above all to control a transaction, or a timeout.  It is not a lower or a higher level.
- **`models` and `sql` are fallbacks**, mainly for a project where Dapper or your own ADO.NET code runs the SQL.
- **"Fallback" hides nothing.**  Some queries of a described project need `-- output: sql`: a statement that cannot be described, or a table-valued parameter on SQL Server.  DuckDB is read as `postgres` and needs `sql`.  Keep each sentence that says so.
- **No text says "choose a dialect"**, "pick" or "select" one.  You set the dialect to your database: `postgres` or `mssql`.  Another dialect matters only for the output `sql`.
- **The other dialect names stay** where a page documents them: `reference/dialects.md`, `reference/databases/other.md`, and a rule that is about `mysql` or `mariadb`.
- **No status language** (section 2).  A support fact is not status (section 6).
- **"Setting" has one meaning** (section 5 of the map).  A property of an attribute or of the project is a setting that has one place.  "Not a key of a settings file" is true of such a property, and stays.

## 8. A rule that another section owns

Many rules are stated on several pages.  The map gives each rule one owner (section 5, "One statement, several places").  A unit whose disposition is `restated-from <owner>` is cut to a restatement.  A restatement is one or two sentences that state the rule in its common form, with a link to the owner.

The owner's page is in another phase, which runs beside yours.  So:

1. Read the owner's section as it is at the commit you start from.
2. List each fact of the text that you cut.
3. Check that the owner's section holds each one.
4. **A fact that the owner lacks stays on your page**, in plain language.  You cannot add it to a page of another phase.  The fact is not lost: it stays where a reader finds it now.  Report it, as below.  If the owner is a page of your own phase, add the fact to the owner, and report nothing.
5. Check the restatement against the source, not against the owner's old sentence.

**The report of a fact with no owner** is written in two places, so that phase 25, which places such facts with their owners, finds each one:

- **In your ledger.**  The row of the unit that holds the fact has a note that opens with the words `no owner:`, then the owner as `<file>#<anchor>`, then the fact in a few words: `no owner: site/reference/<page>.md#<anchor>; <the fact>`.  The row keeps the disposition it would have without the note.
- **In the description of the pull request**, under the heading "Facts with no owner": one line for each fact, with the page and anchor where it stands, the owner, and the fact.  Write "none" when there is none.

`grep -n "no owner:" docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-*.tsv` then lists them all.  A diagnostics phase cuts no restatement, since a section stands alone.  If it sees that a section states a rule which the reference section of its `See:` line lacks, it leaves the section as it is and writes the same report.

A fact leaves a page in three ways only, and each has a row of the ledger:

- **It is said twice** on the page or in a merge pair, and you keep it once.
- **Another section owns it**, the owner holds it, and your page restates and links.
- **It is false**, and you correct it by the source, with a `corrected` row.  This is for a statement that you or a reviewer find false.  One that the map or a brief knows of was corrected by the move that carried it (section 1).

## 9. A merge

A move placed the texts of a merge pair side by side on the new page, each untouched under its own anchor.  The first text stands under the heading of the pair.  The second text keeps its own heading, one level down, with its own `<a id>` line above that heading.  A text that had no heading of its own, such as a paragraph of the README or a known-issue bullet, stands under the heading of the pair, in the order of the map's row.  The move phase started the worksheet `merges/MP-<nn>.md`.  Its part "The texts" holds each text, word for word, and says which heading the rewrite lifts.  The brief of phase 0 gives the form of the file.  You write its three other parts, and the merge.

1. **List the facts**, under "The facts".  One row for each fact of each text.  Mark where it comes from: the first text, the second, or both.
2. **Settle each conflict**, under "Conflicts".  For each pair of rows that disagree, open the source file that the map names (section 5).  Write what each text says, the file, the member or the line, and the ruling.  If there is none, write "None found."
3. **Write the merged text.**  It is the union: no fact of either text is dropped.  Put it on the page, in the shape of section 4 or 5.
4. **Lift the second heading.**  The merged text stands under one heading, the first.  Take out the heading of the second text, and move its `<a id>` line up, so that the one heading has both `<a id>` lines above it, the first text's first.  No `<a id>` line is added, removed or renamed, and every link to either anchor still opens the merged text.
5. **Where the map keeps two headings** (MP-06, MP-07, and the pairs of phase 12 that land on a page), both headings stay, each with its anchor.  Decide which heading owns each fact, and let the other restate and link.
6. **Finish the worksheet.**  "The merged text" says where the text stands, and holds it.  Each row of "The facts" says where its fact now stands, or which section owns it.
7. **Mark the ledger.**  Each unit of each text is `merge-pair`, with `MP-<nn>` in the column `arg`.  The heading that you lift is a unit too.  Write it as the brief of phase 0 writes a heading that goes: a `move-split` row with `-` in `to`, no part, and a note that names the heading it was lifted into.

A pair that the map lists with "No conflict known" can still hold one.  The list of facts finds it.

**MP-10 is the terms.**  Each term of the old `concepts.md` stands first in the section that defines it, above the section's own opening.  It has no heading and no anchor of its own, so nothing is lifted.  Merge the two into one definition, in the sentence after the sample.  The section wins where the two differ.  No anchor of `concepts.md` goes on the page.  `merges/MP-10.md` has one h2 for each of the 17 terms, and five phases write in it.  Write only under your own terms.

## 10. The procedure for one page

1. **Write the commit you start from** on the first line of your ledger: `# base <sha>`.  The checker reads "old" at that commit.  After a rebase, write the new one.
2. **Read the page, and its entry in the map.**  Section 3 has its headings and anchors.  Section 4 has the rows of its sources.  Section 5 has its merges and corrections.
3. **Find the old text of the page.**  Run `check.sh origin <page>` (the path below, in step 7).  It lists each unit of the page with the id of the baseline unit that has the same text, or `new`.  A unit with an id is old text: rewrite it.  A unit that is `new` stays as it is when a `new` or `corrected` row of an earlier phase's ledger accounts for it: the row's `to` is the section, and its note or its `words` says what the text is.  A unit that is `new` and that no ledger accounts for is text of feature work: rewrite it with the page.
4. **Read the old section where it helps**: `git show 28e20e3c:site/<old page>.md`.
5. **Rewrite one section at a time**, in this order:
   1. the shape;
   2. the merge, if the section has one;
   3. the restatements;
   4. each sentence, by the language rule;
   5. the reframings.
6. **Run the two searches** on the page: the `grep` of `site/AGENTS.md` for internals, and the search for status words (below).
7. **Run "check rewrite <nn>"**: `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh rewrite <nn>`, from the root of the repository.  The brief of phase 0 defines it, and "check coverage" and "check origin" are the commands `coverage` and `origin` of the same script.
8. **Account for every line that the checker prints**, in the `note` of a ledger row (below).
9. **Count the page.**  "check rewrite" prints the word count of each section and each page, old and new, and the sum of the phase.  Write the counts of the pages in a `#` comment line of the ledger, and in the pull request.
10. **Build the site** with `tools/build-site.sh`.  It fails on a link to an anchor that is gone.

**The rows of a rewrite's ledger.**  The brief of phase 0 gives the six columns: `unit`, `disposition`, `to`, `arg`, `words` and `note`.  A cell is never empty: `-` stands for nothing.  One row can hold a range of units.  A unit of the inventory is named by its file and line at the baseline, `site/settings.md:153`, which "check origin" prints.  Text that is not in the inventory is named by its place at your base, `@site/reference/settings.md:140`.  `to` is the page, or the page and the anchor of the section, where the unit's facts now stand.

| Disposition | In a rewrite it means |
|----|----|
| `move` | The facts of the unit stay on its page, in new words |
| `merge-pair` | The unit is part of a merged text.  `arg` holds `MP-<nn>` |
| `restated-from` | The unit was cut to a restatement.  `arg` holds the owner, `<file>#<anchor>` |
| `corrected` | The unit was false.  `words` holds the new words, and the note names the source |
| `new` | Text that you added: a part of a guide's shape, or a sample that your brief lists.  `unit` is `-`, and `to` says where the text is |

**What the checker prints.**  It compares by section: an h2 and all below it.  For each section that differs it prints:

- the code spans, the diagnostic ids, the numerals and the rows of each table whose count changed, old against new;
- each sentence that lost "not", "only", "never" or "unless";
- each fenced block that changed or went;
- the rows of your ledger that name the section;
- the word counts.

A difference is not a failure.  The checker fails in three cases: a section differs and no row names the section or a unit of it; a fenced block changed or went and no row names it; a unit of the old page is not on the new page word for word and has no row.  So every unit that you reword has a row.  Whether a note is true is for the reviewer to say.

| The checker prints | A note that accounts for it |
|----|----|
| A code span that went | "`sqlsource.json` 14 to 12: two restatements of `#settings-files` cut; the owner holds both facts" |
| A diagnostic id that went | "SQLSRC241 5 to 4: said twice in MP-06; kept once under `#running-in-the-build`" |
| A numeral that changed | "`512` 4 to 3: the padding sizes were listed twice" |
| A table row that changed | "Row `transaction`: the Effect cell became the text under `#gp-transaction`; the cell now links it" |
| A sentence that lost a limiter word | "Old: 'A `.env` in a folder above the project's is not looked for.'  New: 'The build ignores a `.env` file in a folder above the folder of the project.'  Same rule" |
| A fenced block that changed or went | Only a block that the brief lists, with a row that names it.  Any other is a mistake: put the block back |

**The checks of a rewrite.**  Each is a command, and "Done when" of each brief asks for all of them on the last commit.  `E` is `docs/superpowers/specs/2026-11-12-docs-overhaul`, and `<pages>` is the list of the pages of your brief.

| The check | The command | It passes when |
|----|----|----|
| The rewrite | `$E/check/check.sh rewrite <nn>` | It exits with 0 |
| The ledger | `$E/check/check.sh coverage` | It exits with 0 |
| Only your files | `git diff --name-only "${base}"` | It names your pages, `$E/ledger/phase-<nn>.tsv` and the worksheets of your pairs, and nothing else but the outline, if the plan changed |
| The anchors | The first loop below | It prints nothing |
| The fenced blocks | The second loop below | The two counts of each page are equal, but for a block that your brief lists |
| The site | `tools/build-site.sh` | It exits with 0 |
| The tests of the documents | `dotnet test --project tests/SqlSource.Repository.Tests` | It passes |
| The limits | The third loop below | The three counts of each page are equal: each limit has "What you see" and "What to do" |
| The worksheets | `grep -L '^## The merged text' $E/merges/MP-<nn>.md`, for your pairs | It prints nothing.  For `MP-10.md`, the command below prints the h2 of each of your terms |
| No internals | The `grep` of `site/AGENTS.md`, over `<pages>` | Each hit is text that the user sees anyway |
| No status | The first search below, over `<pages>` | Each hit is in a fenced block, or says nothing of what SqlSource can do now or later |
| The order of the outputs | The second search below, over `<pages>` | Each hit is text that the build or the tool prints (section 7) |
| Everything | `./pre-commit-validation.sh --full` | It passes, on the tree as pushed, after a clean `branch-review:review` |

The anchors: each page has the same `<a id>` lines as at the commit you started from.  Keep the braces in `${base}`: without them zsh reads `:s` as a modifier.

```bash
base=$(git merge-base origin/main HEAD)
for page in <pages>; do
  diff <(git show "${base}:${page}" | grep -o '<a id="[^"]*">' | sort) <(grep -o '<a id="[^"]*">' "${page}" | sort)
done
```

The fenced blocks: "check rewrite" lists a block that changed or went.  This loop shows a block that was added, as a count of fence lines before and after:

```bash
for page in <pages>; do
  echo "${page} $(git show "${base}:${page}" | grep -c '^ *```') $(grep -c '^ *```' "${page}")"
done
```

The limits, and the terms of `MP-10.md` whose merge is written:

```bash
for page in <pages>; do
  echo "${page} $(grep -c '<a id="limit-' "${page}") $(grep -c -F '**What you see.**' "${page}") $(grep -c -F '**What to do.**' "${page}")"
done
awk '/^## /{term = $0} /^### The merged text/{print term}' "$E/merges/MP-10.md"
```

The search for status words, and the search for a list of outputs that does not have `repository` first:

```bash
git grep -n -i -E "no effect yet|today|coming|later release|for now|currently|(^|[^[:alnum:]_])(yet|will)([^[:alnum:]_]|$)" -- <pages>
git grep -n -E '`(methods|models|sql)`(,| or| and) `repository`|`sql`(,| or| and) `(methods|models)`|`models`(,| or| and) `methods`' -- <pages>
```

The description of the pull request holds:

- the word count of each page, before and after, and each page over 3,500 words, for phase 25;
- each `corrected` row, with its source;
- "Facts with no owner" (section 8), or "none";
- each sentence that you left as it was because you could not check it.

## 11. The reviewer's kit

The two content reviewers of a rewrite get:

- each page as the phase found it and as it leaves it, side by side, unit by unit;
- the output of "check rewrite <nn>";
- `ledger/phase-<nn>.tsv`;
- the worksheets of the phase's merge pairs;
- the baseline pages, by `git show 28e20e3c:<path>`;
- the brief of the phase and this guide.

**The no-loss reviewer** reads old against new.  For each unit, the facts of the old unit are in the new one, or the ledger says where they went.  Each line of the checker's output has a note, and the note is true.  Each fact that was cut as a restatement is in the owner's section.  Each merged text holds every line of its worksheet's list of facts.

**The accuracy reviewer** checks the new text against the source, the tests and the example projects.  The brief names the files.  These are the places where a rewrite makes a false statement:

- a sentence with conditions that became a list: "all of these" against "one of these";
- a passive that became active: the actor;
- a pointer word that became a noun: what it pointed at;
- a list that changed its order, or a sentence that was cut in two: which id, number or limiter went with which half;
- a merged text: each conflict, by the source the worksheet names;
- a `corrected` row;
- a restatement: against the source;
- a limit: that "what to do" works.

Text in a tab is checked once for each database.

## 12. Size

- **A rewrite pull request holds at most about 12,000 words of old text.**  That is the most a reviewer can read old against new.
- **The count is of the text that the phase rewrites**, not of the pages after it.  Section 10 of the map gives both numbers for each phase: the sizes of the pages, and the text of today.  Phase 19 is 12,350 words by the first and 11,560 by the second, which the map accepts.  To count the old text of a page before you start, add up the `words` column of `inventory.tsv` for the units that "check origin" gives an id.  Plain language makes a page longer.  Report the new count.
- **A page aims at 600 to 3,500 words.**  Do not cut a fact to reach that.  Report a page that is over, and say where it has a clean cut.
- **Feature work can add text to your pages before you start.**  If they then hold more old text than the limit, make two pull requests, cut between two pages.  Say so in both.

## Calls made in this guide

The outline and the map are silent on these.  Each is what this guide chose and why.

- **The order of authority** is the outline, the map, the brief, this guide.  The brief is nearer to the pages than the guide is.
- **A rewrite adds no fenced block and no tab group unless its brief lists it.**  Both are new text that needs a source, and a tab hides its text from the browser's find.
- **A section with no sample opens with a concrete case in a sentence.**  The outline asks for an example first, and a rewrite cannot write a new sample.
- **A limit has three labelled parts**, "What you see", "Why" and "What to do".  Thirty-six limits are written in eight phases, and one form makes them read alike.
- **Any list of two or more outputs has `repository` first**, in the order `repository`, `methods`, `models`, `sql`.  The map asks for it "everywhere"; this guide reads that as each partial list too.
- **A fact that the owner lacks is reported in the ledger by a note that opens `no owner:`**, and under one heading of the pull request.  The outline says that the fact stays and that phase 25 places it; a fixed word lets phase 25 find each one with `grep`.
- **The table of terms in section 3.3.**  "One term for one thing" needs one choice for all thirteen rewrites.  Each choice is the word that the old pages use most.
- **A bullet has one rule.**  The outline's line speaks of a paragraph, and most rules of these pages stand in bullets.
- **A heading or a bold lead-in that a file outside `site/` quotes keeps its words**, and no rewrite changes the comment.  No test finds a quoted heading that went stale, and a rewrite that edits no example and no test cannot meet another rewrite in one.
- **A merge lifts the second heading and moves its `<a id>` line up.**  The outline says that the merged heading keeps both anchors; the brief of phase 6 says how the move leaves the two headings.
- **The checks of a rewrite are one table of commands** in section 10, which each brief's "Done when" names and does not repeat.
- **The checker lists four limiter words; the writer watches nine.**  "Alone", "at most", "at least", "except" and "whatever" carry rules on these pages as often as "only" does.
- **A code span counts as one word**, and a table cell is not held to the limit of 25 words.  A table cannot hold a list.
- **`merges/MP-10.md` is written by five phases.**  Each writes only under its own terms, so that the pull requests do not collide.
- **Text that feature work added to a page after its move** is rewritten with the page, and gets rows in the ledger of the rewrite.
- **A rewrite changes no h1 and no `toc.yml`.**  The map fixes the titles, and every rewrite would edit the same `toc.yml`.
