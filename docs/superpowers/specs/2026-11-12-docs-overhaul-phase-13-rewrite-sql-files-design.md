# Docs overhaul, phase 13: rewrite-sql-files

- The outline: [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md).
- The content map: [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).
- The rewrite guide: [`2026-11-12-docs-overhaul/rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md).  Read it first.  This brief holds only what is special to phase 13.
- Done by: an Opus agent.  Needs: phase 11.  Runs beside: the other phases of 12 to 24.

## Goal

The four pages of the group ".sql files" are in plain language and in the shape of a reference page.  Five terms are defined once, each in its section.  The two reframings that the map gives this phase are made: the outputs with `repository` first, and no text that says "choose a dialect".

## The pages

All are in `site/reference/`.  "Words" is the size of each page in section 1 of the map: 8,750 in all.  The old text is 8,870 words (section 10 of the map).

| Page | Words | Sections that need most work | Merge | Limits |
|----|----|----|----|----|
| `sql-files.md` | 2,500 | `marker-index`, `queries-and-names`, `markers-in-the-wrong-place` | MP-10: query, marker, file header | None |
| `parameters.md` | 3,350 | `query-parameters`, `param-marker`, `list-parameters` | MP-10: query parameter | `limit-parameter-taken-for-a-variable` |
| `tokens.md` | 1,450 | `tokens`, `token-defaults` | MP-10: token | None |
| `dialects.md` | 1,450 | The opening, `choosing-a-dialect` | None | None |

The three hardest passages:

- **`parameters.md#list-parameters`.**  961 words in eleven bullets, seven sentences over 40 words.  One bullet holds five rules; section 3.7 of the rewrite guide shows it, before and after.
- **`parameters.md#query-parameters`**, the sentence on a name under `mssql`.  It lists seven kinds of character in 68 words, with one exception inside brackets: the zero-width space ends the name.
- **`tokens.md#token-defaults`**, the paragraph on comments inside a default.  It has seven sentences.  Four of them give SQLSRC120 and its cases, each with its own reason.

## Specific to this phase

### The terms of MP-10

Phase 8 put each term of the old `concepts.md` first in its section, above the section's own opening.  Merge the two into one definition, in the sentence after the sample.  The section wins where they differ.  Write in `merges/MP-10.md` under these five terms only.

| Term | Section | Where the two texts differ |
|----|----|----|
| Query | `sql-files.md#queries-and-names` | The term said that each query "becomes one member".  That is false: a query has two methods under `method-type=both`, and a collection method beside its method.  It is correction C-08 of the map, and the sentence arrives corrected by phase 8: "Each query becomes one or more members of the generated code, named after the query."  Check it against `src/SqlSource/Settings/MethodType.cs` and `src/SqlSource/Generation/Methods/MethodNames.cs`, and keep it true |
| Marker | `sql-files.md#marker` | The term writes `-- word: value`, the section `-- word: rest`.  Keep the section's form, and its three examples: `-- name: GetUser`, `--NAME:GetUser`, and `-- name : GetUser`, which is a comment |
| File header | `sql-files.md#the-file-header` | None known |
| Query parameter | `parameters.md#query-parameters` | None known.  The term's last sentence, on `-- param:`, restates `#param-marker` on the same page |
| Token | `tokens.md#tokens` | None known.  The term says "plain text substitution", the section "string concatenation".  Use one |

`dialects.md` defines the dialect in its opening, a third time.  The term's home is `settings.md#setting-dialect`, in phase 14.  Here the sentence is a restatement: keep it to one sentence, and keep its link.

### The reframings on these pages

- **The `-- output:` row of the marker index** lists the four values with `repository` first, and still says which is the default.
- **The `-- dialect:` row of the marker index** says: the dialect of the file, which you set to your database, `postgres` or `mssql`.  It then links the dialects page for the other names, the options and the reading rules.
- **`dialects.md#choosing-a-dialect`** gets the heading "Dialect names".  The anchor stays.
- **"Choose the dialect named for your database"** becomes "Set the dialect named for your database".  The opening's "how to choose one" becomes "the dialect names".
- **The first paragraph of `dialects.md`** is new text of phase 3.  It tells a reader with PostgreSQL or SQL Server to stop reading.  Leave it, and make the next paragraph follow from it.
- **Every list of outputs** on the four pages has `repository` first: "whose output is `repository` or `methods`" in `sql-files.md#result-shapes`, "under `repository`, `methods` and `models`" in `parameters.md`.

### Rules that another section owns

Cut each of these to a restatement and a link, by section 8 of the rewrite guide.

| On this page | The rule | Its owner |
|----|----|----|
| `sql-files.md#markers-in-the-wrong-place`, last paragraph | What an error costs a file or a query | `errors.md#what-an-error-costs` |
| `sql-files.md#result-shapes`, the table | What a method returns for each shape.  The five words and their errors are owned here, so the table stays, short | `return-types.md#method-shapes` |
| `parameters.md#param-marker` | The order in which a C# type is chosen | `type-mapping.md#property-types` |
| `parameters.md#param-marker`, last paragraph | When an entry of a sidecar stops being current | `sidecars.md#describing-again` |
| `tokens.md#tokens` | Where a token method goes | `members.md#token-methods` |

`parameters.md#list-parameters` is itself an owner.  Its last bullet, on the outputs `models` and `sql` and on Dapper, stays whole: `guides/dapper.md` restates it.

### Facts most at risk

| Fact | Page | Check against |
|----|----|----|
| Which markers stand in the file header and which inside a query | `sql-files.md#marker-index` | `src/SqlSource/Parsing/Markers/SqlMarkerKind.cs`, `SqlMarkerScope.cs`; `SettingsReferenceTests` reads the rows |
| What is done to the SQL that reaches the generated code | `sql-files.md#generated-sql` | `src/SqlSource/Parsing/SqlTextBuilder.cs`; `tests/SqlSource.Tests/Parsing/SqlTextBuilderTests.cs` |
| The characters of a parameter's name under `mssql`; which uses of `@` are not parameters | `parameters.md#query-parameters` | `src/SqlSource/Parsing/SqlParameterName.cs`, `SqlParameterList.cs`; `tests/SqlSource.Tests/Parsing/SqlParameterNameTests.cs` |
| Where a list can stand; `position(a IN @p)` is "the one place" | `parameters.md#list-parameters` | `src/SqlSource/Parsing/SqlListUses.cs` and its two partial files; `SqlListUsesTests.cs`, `SqlListPlacesTests.cs` |
| The sizes of padding: 1, 2, 4 and so on to 512, then each multiple of 512; "3 values are sent as 4, and 600 as 1,024" | `parameters.md#list-padding` | `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs`; `tests/SqlSource.Tests/Generator/ListPaddingParameterTests.cs` |
| 65,535 parameters for PostgreSQL; 2,098 for SQL Server, which is 2,100 less two | `parameters.md#list-limit` | `ListWriter.cs`, the two `EngineLimit` lines |
| A variable declared with `DECLARE` | `parameters.md#declared-variables` | `src/SqlSource/Parsing/SqlDeclaredVariables.cs`; `SqlDeclaredVariablesTests.cs` |
| What is and is not a token; a comment in a default | `tokens.md` | `src/SqlSource/Parsing/Tokens/TokenScanner.cs`; `SqlFileParserTokenTests.cs` |
| The dialect names, the names "also accepted", and each Yes and No of the two tables | `dialects.md` | `src/SqlSource/Parsing/SqlDialectName.cs`, `SqlDialectRules.cs`; `SqlDialectNameTests.cs`, `SqlDialectRulesTests.cs` |

### Samples and tables

- **No sample of these pages has a twin in a test**, but `UserDocsSamplesTests` parses every `sql` block.  Each keeps its info string: `sql`, `sql postgres`, `sql mysql`, `sql fragment`.
- **The marker index, the table of result shapes and the tables of `dialects.md` are tables of facts.**  Change only the two rows that the reframings name.
- **The comparison table of `tokens.md#token-or-parameter`** was the opening of the old page.  Its four rows stay.

### Sizes

No page is over 3,500 words today.  `parameters.md` is at 3,350, and plain language makes it longer.  Do not cut a fact to hold it.  Report the count.  If it passes 3,500, say that its clean cut is the three sections on lists: `list-parameters`, `list-padding` and `list-limit`.

## Done when

- Each check of "The checks of a rewrite", in section 10 of the rewrite guide, passes on the last commit.  "check rewrite 13" exits with 0, so each unit has a row in `ledger/phase-13.tsv`, and it lists no fenced block as changed or gone.
- The command of that section for `merges/MP-10.md` prints the h2 of Query, Marker, File header, Query parameter and Token.  Each of the five terms is defined once on its page.
- `grep -c '^## Dialect names' site/reference/dialects.md` prints 1, and `git grep -n -i -E "choos(e|ing) (a|one|the) dialect|how to choose" -- site/reference/sql-files.md site/reference/parameters.md site/reference/tokens.md site/reference/dialects.md` prints nothing.
- Each section opens with its example and states one rule in each bullet: the reviewers read this.
- The description of the pull request holds what section 10 of the rewrite guide asks: the word count of each page, and "Facts with no owner", written as its section 8 says.

## For the reviewers

- **No-loss.**  Read `parameters.md` first: it is the densest page, and its bullets were split.  The old `#list-parameters` has eleven bullets and the old `#param-marker` nine.  Read them old against new, rule by rule.
- **Accuracy.**  For each split bullet, check which half each diagnostic id went to.  The ids of `#param-marker` are SQLSRC111, SQLSRC112, SQLSRC116 to SQLSRC118 and SQLSRC226 to SQLSRC228.
- **The tables of `dialects.md`** must be equal to the old ones, cell by cell.

## Traps

- **`IN @ids` and `IN (@ids)` are two things.**  The first is a list, the second is one value.  Never tidy the parentheses in a sentence or a code span.
- **Some code spans hold characters that you cannot see**: a zero-width non-joiner, `Ⅰ`, `‿`.  Copy them.  Do not type them again.
- **"Parameter" has three meanings.**  On `parameters.md` say *query parameter* in each heading and each first mention.  Say *generator parameter* for `keep-comments`.
- **The two halves of the old page now link each other across pages.**  Phase 3 mended each "above" and "below" of `tokens.md` and `parameters.md` that the split made wrong, with the note `position` in its ledger.  Check each one that is left, and name the section.
- **The warning of `tokens.md#tokens`** is the first text after the sample, and stays there.  It has four facts: string concatenation; nothing escaped, quoted or checked; a value a user can influence is a SQL injection; a token is for text your own code chooses.
- **"A default is a sample of what the caller will pass."**  The "will" goes.  The rule stays: the generated method does not use the default.
- **`dialects.md` is for every reader in part.**  The table of `what-a-dialect-changes` has `mssql` and `postgres` columns.  Do not word the page as if only other databases need it.

## Calls made in this brief

- **The term "Query" is not corrected here.**  Its sentence on "one member" is false today, so the move that carries it, phase 8, corrects it (C-08).  This phase checks it.
- **The words of the `-- dialect:` row.**  The map says the row is reworded for decision 5, and gives no words.
- **The sentence of `dialects.md` that defines the dialect is a restatement.**  The map lists it under MP-10, and the term's home is on a page of phase 14.
- **A table of facts changes only where a reframing names the row.**  The checker compares rows, and a reworded cell cannot be told from a lost one.
- **The clean cut of `parameters.md`** is named for phase 25.  The map lists the page as close to the limit and names no cut.
