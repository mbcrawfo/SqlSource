# TD-0100 - A merge can keep an older tool's entry under a new tool's version

## Problem

A sidecar records the version of the tool that wrote it once, in its `toolVersion`, for the whole file.  The tool trusts an entry when the file's version is its own and the entry's `hash` and `database` match the query ([`SidecarOnDisk.Usable`](../../src/SqlSource.Tool/Describing/SidecarOnDisk.cs), [`SidecarEntry.IsCurrentFor`](../../src/SqlSource/Snapshot/SidecarEntry.cs)), and the build warns of another tool's sidecar from the file's version alone ([`SidecarFileChecks`](../../src/SqlSource/Generation/Sidecars/SidecarFileChecks.cs), SQLSRC240).

Git breaks the rule that [`docs/architecture/sidecar-format.md`](../architecture/sidecar-format.md) states, that an entry from an older tool never stands beside a new one.  Branch A updates the tool, and its `describe` rewrites every sidecar, changing `toolVersion`.  Branch B, still on the older tool, changes one query and describes it, changing only that entry.  Git merges the two without a conflict: the file carries A's version and B's entry, whose hash matches the merged query.  The build reports neither SQLSRC240 nor SQLSRC244, and `describe` skips the entry.  Only `describe --check`, which describes every query, finds a difference.  The branch review of the change that added "Sidecars in git" to `docs/manual/tool.md` reproduced it on `postgres:18.6`.

## Why it exists

The format stamps the file, not the entry, and was designed for a tool that writes the whole file at once.  A merge that combines two tools' writes was not considered.  The change that found it was documentation only, so it tells users to run `describe --force` after merging a tool update and records the gap here, rather than changing the sidecar format.

## Impact

Medium.  When the newer tool describes the query differently, such as a nullability fix between two preview releases, the generated models of that query are wrong and nothing but `--check` reports it.  It needs a tool update on one branch while another branch describes a query, and a project that does not run `--check` in CI depends on its developers following the manual's advice.

## Proposed fix

Give each entry the version of the tool that wrote it, beside `hash` and `database`, in a new format version.  `IsCurrentFor` then also compares the entry's version with the tool's, so `describe` describes such an entry again, and the build's SQLSRC240 check looks at every entry's version, not the file's alone.  The file-level `toolVersion` can stay as the version of the last write.  Update `schemas/`, the architecture page, the manual's "Sidecars in git" and its known issue in `docs/manual/tool.md`, and resolve this item.

## Trigger

The next change to the sidecar format, or a report of models that disagree with `describe --check` after a merge.
