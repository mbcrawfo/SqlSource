# Docs overhaul phase 17, rewrite-generated-code-2: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The five pages `query-methods.md`, `repositories.md`, `connection-methods.md`, `collection-methods.md` and `exceptions.md` of `site/reference/` are in plain language and in the shape of a reference page; MP-05 is merged, and the terms "Query method" and "Repository" of MP-10 are each defined once.

**Architecture:** A rewrite of words only, one page at a time, by the procedure of section 10 of the rewrite guide.  Each task rewrites one page, appends its rows to the one ledger `ledger/phase-17.tsv`, and leaves `check.sh rewrite 17` passing for the pages done so far.  No anchor, link target, fenced block, heading order, `toc.yml` or other page changes.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), xUnit v3 (`tests/SqlSource.Repository.Tests`), the epic's checker (`docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh`).

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-17-rewrite-generated-code-2-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the map `docs/superpowers/specs/2026-11-12-docs-overhaul/content-map.md` and the rewrite guide `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md`.

## Global Constraints

- `E` = `docs/superpowers/specs/2026-11-12-docs-overhaul`.  Base = `db3285d4adf7542f6e80e96d7f0b8182cd091a9f` (merge of p11, #296); after a sync with `main`, the new merge-base.
- Files this phase changes: the five pages, `$E/ledger/phase-17.tsv`, `$E/merges/MP-05.md`, `$E/merges/MP-10.md` (only under `## Query method` and `## Repository`), and this plan.  Nothing else.
- Every `<a id>` line stays, once; MP-05 is the only one that moves (up beside `repository-interface`).  No new `<a id>`, no new heading, no new fenced block, no new tab group.  Every fenced block byte-identical.
- Kept words: the heading "Query methods" (`query-methods.md#query-methods`); the bold lead-in "**Give the method the transaction.**" (`connection-methods.md#method-connection`); "cannot be written in a settings file or a marker: they belong to the type" and "It is a property of the project file, and not a setting of a settings file" (MP-05); every message letter for letter; the `### ` limit headings.
- Outputs listed with `repository` first: `repository`, `methods`, `models`, `sql`.
- No status words; no internals (`site/AGENTS.md` grep).  Errors linked as `[SQLSRCnnn](../diagnostics.md#sqlsrcnnn)`.
- Language: one instruction per sentence; at most 25 words in a description; a paragraph at most six sentences; condition first; "must" only with its error; a bullet has one rule with a bold lead-in.  Limiter words ("not", "only", "never", "unless", "alone", "at most", "at least", "except", "whatever") stay.
- A fact that another section owns: cut to one or two sentences and a link only when the owner holds every fact; else it stays here and gets a `no owner:` note.
- Owner ruling (p09 Q1): the bold heading "A borrowed connection runs the command in no transaction." of `repositories.md#repository-connection` is false on PostgreSQL (Npgsql runs the command in the connection's transaction).  Correct it, with a `corrected` row.

## Where the tree differs from the spec

- `check.sh origin` finds text with no baseline id and no earlier ledger row (feature work after the baseline): `query-methods.md` line 32 (the paragraph "Either method sends each parameter…", 176 words), `repositories.md` lines 134 and 252, `collection-methods.md` line 94 (the limit's body), and `exceptions.md` lines 22, 46 and 69.  Each is rewritten with its page and named in the ledger by `@site/reference/<page>.md:<line>`.  `repositories.md` 190 is C-02, a `corrected` row of phase 6, and stays as words.
- Page sizes at the base: `query-methods.md` 2,924, `repositories.md` 3,211, `connection-methods.md` 1,280, `collection-methods.md` 1,722, `exceptions.md` 1,793 words (`wc -w`).

## Review Focus

- A sentence split in two where an id, number or limiter word went with the wrong half (`#which-methods`, `#repository-connection`, `#reading-rows`).
- "All of these" against "one of these" in a list made from a sentence with "and", "or", "when", "unless".
- The exception of SQLSRC299 kept beside the rule that a failing query gets neither its methods nor a constant.
- The two `limit-wrapped-connection` limits keeping their own symptoms: `InvalidCastException` (query method with parameters) and `NotSupportedException` (collection method).
- PostgreSQL and SQL Server each stated in `#method-connection`, `#collection-transactions` and `exceptions.md#transaction-rolled-back`; `TransactionRolledBackException` is SQL Server's alone.

---

### Per-page procedure (every task)

For the page `P` of the task:

1. Read `P`, its entry in section 3 of the map, its old text (`git show 28e20e3c:site/generator.md` / `site/settings.md` at the lines the map names) and the "Facts most at risk" sources of the spec.
2. Rewrite one h2 section at a time: shape (section 4 of the guide: sample first, term next, bulleted rules with bold lead-ins, `See:` line where the old text lists errors), merge, restatements, sentences, reframings.
3. Append the page's rows to `$E/ledger/phase-17.tsv`: one row per reworded unit (`move`, `site/reference/P.md#<anchor>`, `arg` `-`), or a range row; `merge-pair` with `MP-<nn>`; `restated-from` with the owner; `corrected` with new words and the source; `@` names for text with no inventory id.  A note on every row that accounts for a line the checker prints.
4. Run `zsh -lc "$E/check/check.sh rewrite 17"`; account for every printed difference in a note; it exits 0.
5. Run the internals grep of `site/AGENTS.md`, the status grep and the output-order grep of guide section 10 over `P`; fix each hit.
6. Anchor loop and fence-count loop of guide section 10 over `P`: no anchor diff (but MP-05's move), equal fence counts.
7. `./format.sh`, `./pre-commit-validation.sh`, commit `docs(site): rewrite reference/P.md in plain language (phase 17)`.

### Task 1: `connection-methods.md` and the ledger header

**Files:** Modify `site/reference/connection-methods.md`; Create `$E/ledger/phase-17.tsv` with `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f`, a `#` line reserved for the word counts, and the header `unit	disposition	to	arg	words	note`.

- [ ] The procedure above.
- [ ] `#methods-that-take-a-connection` says what `methods` is for: your own code structures, above all a transaction or a timeout that your code controls.
- [ ] `#method-connection` keeps "**Give the method the transaction.**" as the whole bold lead-in, and states what Microsoft.Data.SqlClient and what Npgsql each do with a command not given the transaction (check `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs`).  The rule "opens the connection when its `State` is `Closed`, and closes it again" keeps its second half: an open connection is left as it was.
- [ ] `grep -c -F "**Give the method the transaction" site/reference/connection-methods.md` prints 1.

### Task 2: `exceptions.md`

**Files:** Modify `site/reference/exceptions.md`; append to the ledger.

- [ ] The procedure above.
- [ ] `#transaction-rolled-back` says that `TransactionRolledBackException` is SQL Server's alone, and what PostgreSQL does instead.  Members `QueryName`, `ItemIndex`, `InnerException` checked against `src/SqlSource/Generation/Exceptions/ExceptionSource.cs`.
- [ ] `#limit-cref`'s one sentence of 92 words becomes a list of its four conditions, with CS0419 for the exception itself and CS1574 for a member (check `tests/SqlSource.Tests/Generator/ExceptionReuseTests.cs`).  Both limits have "What you see", "Why", "What to do".

### Task 3: `collection-methods.md`

**Files:** Modify `site/reference/collection-methods.md`; append to the ledger.

- [ ] The procedure above.
- [ ] `#collection-transactions` restates the values of the generator parameter `transaction` and links `generator-parameters.md#gp-transaction`, keeping any fact the owner lacks with a `no owner:` note.  It says what happens on PostgreSQL and on SQL Server.  1,000 items to a round trip, when a transaction begins, SQLSRC297: check `BatchWriter.cs`, `TransactionMode.cs`, `CollectionMethodTests.cs`.
- [ ] `#limit-wrapped-connection` keeps its own symptom, `NotSupportedException`, in three labelled parts.

### Task 4: `query-methods.md` and the term "Query method"

**Files:** Modify `site/reference/query-methods.md`; append to the ledger; write `### The facts`, `### Conflicts`, `### The merged text` under `## Query method` of `$E/merges/MP-10.md`.

- [ ] The procedure above.
- [ ] `#query-methods`: the heading "Query methods" stays; both fenced blocks stay (the C# is mirrored by `examples/*/Methods/OrderLineQueries.cs`); the merged definition, in the sentence after the sample: the generated method that runs a query; a repository method or a method that takes a connection; a token method is not one.  The `concepts.md` term's text and the section's opening merge into it; its units are `merge-pair` `MP-10`.
- [ ] `#which-methods`: a list of the six conditions, each with its own errors as links, then a list of what each kind of error costs ("gets neither its methods nor a constant", "its models are still generated", "leaves the query's whole file", "only its synchronous methods", with the SQLSRC299 exception beside the rule).  Check against `MethodPlanner.cs`, `MethodConflicts.cs`, `MethodDiagnosticsTests.cs`, `MethodConflictTests.cs`.  The last condition keeps "Npgsql 8.0 or later" beside SQLSRC247 and "Microsoft.Data.SqlClient 6.1 or later" beside SQLSRC267, and links `databases/index.md#supported-versions` around the words of the two minimums (check both numbers against that table).  PostgreSQL's `CALL` (SQLSRC233) and a dialect that is not described both want `-- output: sql`; SQLSRC209 is restated from `settings.md#setting-output` and `databases/other.md#not-described`.
- [ ] `#synchronous-methods`: the three cases of the `Async` suffix; `Finalize`; SQLSRC298, SQLSRC299 (check `MethodSynchrony.cs`, `MethodNames.cs`, `SynchronousMethodTests.cs`).
- [ ] `#reading-rows`, `#method-parameters`: keep what holds for every database; what a driver reads or sends for its own types is restated from each database's `query-methods.md#method-results` / `#method-parameters`.  The bullet "A JSON `null` is not a null" becomes a list of its C# types and results.  Messages such as "Column 'metadata' holds a JSON null." stay letter for letter (`RowReaderWriter.cs`).
- [ ] `#limit-wrapped-connection` keeps its own symptom, `InvalidCastException`, in three labelled parts.
- [ ] `grep -c -E "^#+ Query methods$" site/reference/query-methods.md` ≥ 1; `grep -c -F "index.md#supported-versions" site/reference/query-methods.md` ≥ 1.

### Task 5: `repositories.md`, MP-05 and the term "Repository"

**Files:** Modify `site/reference/repositories.md`; append to the ledger; finish `$E/merges/MP-05.md`; write under `## Repository` of `$E/merges/MP-10.md`.

- [ ] The procedure above.
- [ ] `#repository-methods`: the merged definition of "Repository": a type whose queries have the output `repository`; its methods are instance methods that get their own connection; it is the default.
- [ ] `#repository-connection`: which member gives the connection, a pair of members, `[SqlSourceConnection]`, several databases (what a logical database is restated from `settings.md#setting-database`); SQLSRC028 to SQLSRC033 and SQLSRC036 each with its own case, checked against `ConnectionResolver.cs`, `ConnectionCandidateReader.cs`, `RepositoryDiagnosticsTests.cs`.  The owner's correction of the bold heading "A borrowed connection runs the command in no transaction." with a `corrected` row.
- [ ] `#repository-signature`: names a repository method cannot have; SQLSRC274, SQLSRC275 (`MethodConflicts.cs`, `RepositoryMethodTests.cs`).
- [ ] MP-05: one h2 "The interface" with `<a id="repository-interface"></a>` then `<a id="repository-interfaces"></a>` above it; the h3 "Repository interfaces" removed (`move-split` row, `to` `-`, note naming the heading it was lifted into).  Order: both samples (the setting's `PropertyGroup` and attribute first), what the interface holds, the three things that set it, the default, SQLSRC035 and SQLSRC037.  Conflicts settled by `RepositoryInterfaces.cs`, `RepositoryShapeReader.cs`, `SqlSource.props`; `InterfaceSignature.cs` and `RepositoryInterfaceTests.cs` for the members (an `internal` method is left out).  Worksheet parts "The facts", "Conflicts", "The merged text" written.
- [ ] `#repository-transactions` stays whole, with its second paragraph on the collection method and an ambient transaction, sends a query that needs a transaction to `methods`, and links `../start/transactions.md#queries-in-a-transaction`.
- [ ] `grep -B6 -E "^#+ The interface$" site/reference/repositories.md | grep -c '<a id="repository-interface'` prints 2.  Report the page's word count; if over 3,500, its clean cut is `repository-connection`.

### Task 6: Done when (manager, not an implementer)

- [ ] `check.sh rewrite 17` and `check.sh coverage` exit 0; anchors, fences, limits loops; `grep -L '^## The merged text' $E/merges/MP-05.md` prints nothing; the MP-10 awk prints "## Query method" and "## Repository" among its lines.
- [ ] Word counts in the ledger's `#` line; `tools/build-site.sh`; `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] Content reviews (no-loss, accuracy), then `/branch-review:review`, then `./pre-commit-validation.sh --full`.
