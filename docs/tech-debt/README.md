# Tech debt

Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `TD-0009`

## Active items

| ID | Status | Added | Impact | Description |
|----|----|----|----|----|
| [TD-0002](TD-0002-no-coverage-comment-on-fork-pull-requests.md) | Open | 2026-10-05 | Low | Fork and Dependabot pull requests get no coverage comment |
| [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Medium | The SQL lexer misreads some MySQL, Oracle and SQL Server constructs, as an error or by stripping SQL |
| [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Medium | Two projects that share internals and both use SqlSource get warning CS0436 for the generated attribute |
| [TD-0007](TD-0007-sql-files-that-differ-only-by-case.md) | Open | 2026-10-05 | Low | Two `.sql` files whose paths differ only by case are treated as one, and the second is ignored |
| [TD-0008](TD-0008-package-is-not-installed-in-a-test.md) | Open | 2026-10-05 | Medium | No test installs the packed package into a project and builds it |

## Columns

| Column | Contents |
|----|----|
| ID | `TD-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Impact | `Low` (cosmetic or local), `Medium` (slows work or risks defects), `High` (risks correctness, security or data) |
| Description | One line: what is wrong |
