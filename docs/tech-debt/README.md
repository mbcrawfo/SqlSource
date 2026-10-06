# Tech debt

Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `TD-0016`

## Active items

| ID | Status | Added | Impact | Description |
|----|----|----|----|----|
| [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Low | A few constructs are misread whatever the dialect: MySQL's SQL modes, a versioned comment that holds `*/` in a string, a comment inside a continued PostgreSQL string |
| [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Low | Two projects that share internals and both use SqlSource each hold the generated attribute; the package hides the compiler's warning CS0436 for it and does not remove the conflict |
| [TD-0007](TD-0007-sql-files-that-differ-only-by-case.md) | Open | 2026-10-05 | Low | Two `.sql` files whose paths differ only by case are treated as one, and the second is ignored |
| [TD-0012](TD-0012-token-method-shapes-are-compiled-but-not-run.md) | Open | 2026-10-06 | Low | A token-only query and one with more than seven tokens are compiled in tests and never run, and awkward token names are compiled as C# 12 only |
| [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md) | Open | 2026-10-06 | Low | An edit to a `.sql` file makes the compiler read every attributed type again, because the attribute is added in a post-initialization step |
| [TD-0014](TD-0014-every-sql-file-gets-a-section-in-the-compiler-configuration.md) | Open | 2026-10-06 | Low | Every `.sql` file of a project, used or not, adds a section to the configuration file the SDK writes for the compiler |
| [TD-0015](TD-0015-values-set-after-the-package-targets-are-not-trimmed.md) | Open | 2026-10-06 | Low | A dialect or validation setting written over several lines in `Directory.Build.targets`, or on an item a target adds, reaches the compiler empty and is ignored |

## Columns

| Column | Contents |
|----|----|
| ID | `TD-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Impact | `Low` (cosmetic or local), `Medium` (slows work or risks defects), `High` (risks correctness, security or data) |
| Description | One line: what is wrong |
