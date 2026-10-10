# Tech debt

Problems that were identified and not resolved, and intentional choices known to be sub-optimal.  Each active item is fully documented in its own file, linked from the ID column.  When an item is resolved, its row is removed and its file deleted in the same PR as the fix.

Next id: `TD-0036`

## Active items

| ID | Status | Added | Impact | Description |
|----|----|----|----|----|
| [TD-0003](TD-0003-run-number-limited-by-assembly-version.md) | Open | 2026-10-05 | Low | A run number above 65534 fails the build, because it is a part of the assembly version |
| [TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) | Open | 2026-10-05 | Low | A few constructs are misread whatever the dialect: a MySQL versioned comment that holds `*/` in a string, a block comment of SQLite left open at the end of a file, a carriage return alone as a line end in MySQL, SQLite and CockroachDB, a user variable of MySQL or MariaDB read as a parameter |
| [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) | Open | 2026-10-05 | Low | Two projects that share internals and both use SqlSource each hold the generated attribute; the package hides the compiler's warning CS0436 for it and does not remove the conflict |
| [TD-0012](TD-0012-token-method-shapes-are-compiled-but-not-run.md) | Open | 2026-10-06 | Low | A token-only query and one with more than seven tokens are compiled in tests and never run, and awkward token names are compiled as C# 12 only |
| [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md) | Open | 2026-10-06 | Low | An edit to a `.sql` file makes the compiler read every attributed type again, because the attribute is added in a post-initialization step |
| [TD-0016](TD-0016-dialect-of-a-file-added-by-a-late-target-is-lost.md) | Open | 2026-10-06 | Low | A `.sql` file that a target adds loses its dialect, and any other metadata the package reads, when the target hooks `GenerateMSBuildEditorConfigFileCore` and is declared after the package's targets |
| [TD-0018](TD-0018-token-and-parameter-lists-are-built-in-quadratic-time.md) | Open | 2026-10-08 | Low | The distinct count and the name lookup of a query's tokens, and the lookup of its parameters, are quadratic in their number: a query with 6,000 distinct tokens took about 0.45 s to parse |
| [TD-0019](TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md) | Open | 2026-10-08 | Low | A change to the set of claimed files, such as a file added to a type's folder or a changed `Path`, reads every claimed file again |
| [TD-0021](TD-0021-comments-and-token-defaults.md) | Open | 2026-10-08 | Low | A `--` comment inside an inline default swallows its closing braces, a `-- token:` default is compared as written while an inline one is compared without comments, and a comment can be scanned for tokens where no type keeps comments |
| [TD-0022](TD-0022-query-that-keeps-its-comments-is-built-and-scanned-twice.md) | Open | 2026-10-08 | Low | A query that keeps its comments has its SQL built and scanned for tokens twice, so its parse allocates and takes about twice as much |
| [TD-0023](TD-0023-unknown-option-and-path-are-printed-as-given.md) | Open | 2026-10-09 | Low | The `sqlsource` tool prints the name of an unknown option, the path of `describe`, a `.sql` path of it, a path of `--project` and a name of `--database` that no query has as they were typed, so a secret typed as any of them reaches its output, and so does the start of a `--connection` value typed without a name, which `SQLSRC213` prints as a name that no database has |
| [TD-0024](TD-0024-no-test-reaches-the-line-that-a-command-line-is-not-valid.md) | Open | 2026-10-09 | Low | No test reaches the line `sqlsource: the command line is not valid`, which the `sqlsource` tool writes when System.CommandLine and its own check of the command line disagree; it waits for an option that may be given only once |
| [TD-0025](TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md) | Open | 2026-10-09 | Low | The `sqlsource` tool does not see a `.sql` file, or a C# file, that a target adds from a hook of the build: only a target that hooks `SqlSourceTrimMetadataOfFiles` runs before the project manifest is written |
| [TD-0026](TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md) | Open | 2026-10-09 | Low | For a project with several target frameworks the `sqlsource` tool reads the first one alone: a file, an attribute or a reference to SqlSource that only another framework has is not seen |
| [TD-0027](TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md) | Open | 2026-10-09 | Low | The target that writes the project manifest depends on `AddImplicitDefineConstants`, a target of the SDK whose name is no contract |
| [TD-0028](TD-0028-msbuild-runs-of-the-tool-are-not-verified-on-windows.md) | Open | 2026-10-10 | Low | The `sqlsource` tool's runs of `dotnet msbuild` were never run on Windows: a path outside ASCII in MSBuild's output, a path with characters that MSBuild reads, a `SolutionDir` that ends with a backslash, the exit code `-1`, and the kill of a process tree |
| [TD-0029](TD-0029-run-ended-from-outside-leaves-processes-and-a-folder.md) | Open | 2026-10-10 | Low | A `sqlsource` run that is ended with `SIGTERM` leaves its `dotnet msbuild` processes running and its temporary folder behind, a kill that fails on Ctrl+C would be reported as `SQLSRC200`, and a run killed while it writes a sidecar leaves a `.tmp` file beside the `.sql` file |
| [TD-0030](TD-0030-project-option-through-a-symbolic-link-is-not-found.md) | Open | 2026-10-10 | Low | A full path given to `--project` through a symbolic link to the working directory is `SQLSRC207`, and a `.sql` path given the same way is `SQLSRC212`: paths are compared as text |
| [TD-0031](TD-0031-attribute-reader-of-the-tool-is-syntax-only.md) | Open | 2026-10-10 | Low | The `sqlsource` tool reads `[SqlSourceGenerate]` as syntax: an alias for the attribute is not seen, a type of the same name is taken for it, and an attribute inside `#if` is read under the configuration of the manifest alone |
| [TD-0032](TD-0032-sql-file-that-is-not-utf-8-is-hashed-differently.md) | Open | 2026-10-10 | Low | A `.sql` file that is not UTF-8 is read with replacement characters by the `sqlsource` tool and in a fallback code page by the compiler, so the hash of a query that holds such a character differs between the two |
| [TD-0033](TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md) | Open | 2026-10-10 | Low | A `.sql` file that two projects list is planned under the first that claims it, so the second's dialect and database are not used, and a run on one of the two sees the needs of that one alone |
| [TD-0034](TD-0034-redaction-of-a-describers-text-has-holes.md) | Open | 2026-10-10 | Low | The `sqlsource` tool replaces the whole connection value in the text a describer gave, and nothing else: not the descriptor of a failure, a value split over lines or changed, or one keyword of it; an entry is searched for the whole value alone before it is written to a sidecar |
| [TD-0035](TD-0035-three-small-gaps-of-describe.md) | Open | 2026-10-10 | Low | Three small gaps of `describe`: a database that exists only through a file that cannot be described counts as a selected database, the connection variables reach every `dotnet msbuild` the tool starts, and a sidecar that the tool cannot open stays beside a file that needs no entry, with nothing said |

## Columns

| Column | Contents |
|----|----|
| ID | `TD-NNNN`, linked to the item's file in this folder |
| Status | `Open` (nobody is working on it), `In progress`, or `Blocked` (waiting on something named in the item file) |
| Added | Date the item was recorded, `YYYY-MM-DD` |
| Impact | `Low` (cosmetic or local), `Medium` (slows work or risks defects), `High` (risks correctness, security or data) |
| Description | One line: what is wrong |
