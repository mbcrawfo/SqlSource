# Tech debt

Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `TD-0013`

## Active items

| ID | Status | Added | Impact | Description |
|----|----|----|----|----|
| [TD-0002](TD-0002-no-coverage-comment-on-fork-pull-requests.md) | Open | 2026-10-05 | Low | Fork and Dependabot pull requests get no coverage comment |
| [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Medium | The SQL lexer misreads some MySQL, Oracle and SQL Server constructs, as an error or by stripping SQL |
| [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Medium | Two projects that share internals and both use SqlSource get warning CS0436 for the generated attribute |
| [TD-0007](TD-0007-sql-files-that-differ-only-by-case.md) | Open | 2026-10-05 | Low | Two `.sql` files whose paths differ only by case are treated as one, and the second is ignored |
| [TD-0008](TD-0008-package-is-not-installed-in-a-test.md) | Open | 2026-10-05 | Medium | No test installs the packed package into a project and builds it |
| [TD-0009](TD-0009-removed-sql-file-does-not-trigger-a-rebuild.md) | Open | 2026-10-06 | Medium | Deleting or renaming a `.sql` file does not trigger an incremental rebuild, so the old members stay until a full build |
| [TD-0010](TD-0010-path-resolution-scales-with-types-times-files.md) | Open | 2026-10-06 | Low | Resolving paths costs time and memory in proportion to the number of types times the number of `.sql` files |
| [TD-0011](TD-0011-language-version-is-not-checked.md) | Open | 2026-10-06 | Low | A project that sets `LangVersion` below 12 gets compiler errors in generated code, not a diagnostic |
| [TD-0012](TD-0012-token-method-shapes-are-compiled-but-not-run.md) | Open | 2026-10-06 | Low | A token-only query and one with more than seven tokens are compiled in tests and never run, and awkward token names are compiled as C# 12 only |

## Columns

| Column | Contents |
|----|----|
| ID | `TD-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Impact | `Low` (cosmetic or local), `Medium` (slows work or risks defects), `High` (risks correctness, security or data) |
| Description | One line: what is wrong |
