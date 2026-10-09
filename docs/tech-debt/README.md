# Tech debt

Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `TD-0023`

## Active items

| ID | Status | Added | Impact | Description |
|----|----|----|----|----|
| [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Low | A few constructs are misread whatever the dialect: a MySQL versioned comment that holds `*/` in a string, a block comment of SQLite left open at the end of a file, a carriage return alone as a line end in MySQL, SQLite and CockroachDB |
| [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Low | Two projects that share internals and both use SqlSource each hold the generated attribute; the package hides the compiler's warning CS0436 for it and does not remove the conflict |
| [TD-0012](TD-0012-token-method-shapes-are-compiled-but-not-run.md) | Open | 2026-10-06 | Low | A token-only query and one with more than seven tokens are compiled in tests and never run, and awkward token names are compiled as C# 12 only |
| [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md) | Open | 2026-10-06 | Low | An edit to a `.sql` file makes the compiler read every attributed type again, because the attribute is added in a post-initialization step |
| [TD-0016](TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md) | Open | 2026-10-06 | Low | A `.sql` file that a target adds loses its dialect, and any other metadata the package reads, when the target hooks `GenerateMSBuildEditorConfigFileCore` and is declared after the package's targets |
| [TD-0017](TD-0017-value-written-over-several-lines-is-cut-at-its-first-line-break.md) | Open | 2026-10-08 | Medium | A property or metadata of the package whose value has a line break inside it, as a list of generator parameters written one word on each line has, reaches the generator cut at the line break, and nothing is reported |
| [TD-0018](TD-0018-token-and-parameter-lists-are-built-in-quadratic-time.md) | Open | 2026-10-08 | Low | The distinct count and the name lookup of a query's tokens, and the lookup of its parameters, are quadratic in their number: a query with 6,000 distinct tokens took about 0.45 s to parse |
| [TD-0019](TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md) | Open | 2026-10-08 | Low | A change to the set of claimed files, such as a file added to a type's folder or a changed `Path`, reads every claimed file again |
| [TD-0021](TD-0021-comments-and-token-defaults.md) | Open | 2026-10-08 | Low | A `--` comment inside an inline default swallows its closing braces, a `-- token:` default is compared as written while an inline one is compared without comments, and a comment can be scanned for tokens where no type keeps comments |
| [TD-0022](TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md) | Open | 2026-10-08 | Low | A query that keeps its comments has its SQL built and scanned for tokens twice, so its parse allocates and takes about twice as much |

## Columns

| Column | Contents |
|----|----|
| ID | `TD-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Impact | `Low` (cosmetic or local), `Medium` (slows work or risks defects), `High` (risks correctness, security or data) |
| Description | One line: what is wrong |
