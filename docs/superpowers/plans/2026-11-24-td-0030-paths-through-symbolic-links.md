# TD-0030: Paths through symbolic links Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `--project` path or a `.sql` path of `describe` (and the `--query` file of `diagnose`) that reaches its file through a symbolic link names the same file as its real path, so it is no longer `SQLSRC207` or `SQLSRC212`.

**Architecture:** One helper, `RealPath.Of`, replaces every link among a full path's folders and the file itself with what it links to.  `RunProjects.Restrict` and `RunFilters.Create` keep the text comparison they have, and only when it finds nothing compare the links-resolved path with the links-resolved paths of the other side, so the unit's own spelling of a project or `.sql` file is what the rest of the run sees.  Messages keep naming the path as it was typed.

**Tech Stack:** .NET, `FileSystemInfo.ResolveLinkTarget`, xUnit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0030-project-option-through-a-symbolic-link-is-not-found.md`

## Global Constraints

- Paths are still compared ignoring case (`RunProjects.PathComparer`, `SqlPath.Comparer`).
- A message names a path as it was typed, by `UsageValue.NamedAsPath`; `PathToken` is unchanged.
- Resolve links on both sides: a unit given through a link has projects and manifest paths through it.
- Cover a link at the working directory and at a folder above it (the brief's scope).
- Resolution touches the disk only when the text comparison finds no match.

## Where the code differs from the spec

- The spec proposes resolving `.sql` paths in `RunFilters.Create`.  The planned files' normalised paths have no root, so they cannot be resolved; `Create` takes the manifests' full `.sql` paths instead and binds a filter to the listed file's normalised path.  `RunPlanner`, `PlanSteps.FilesNotNamed` and `DiagnoseRun` then compare as they do now.

## Review Focus

- A link whose target is relative (`link -> ../real`): resolved against the link's folder.
- A link whose target's own folders hold a link (macOS `/var -> /private/var`): resolved again.
- A path whose tail does not exist: the existing part is resolved, the rest kept as text, and it is still reported.
- A loop of links: no hang; the path is kept as given.
- Two filters naming one file, one through the link: kept once, by the first.

---

### Task 1: `RealPath.Of`

**Files:**
- Create: `src/SqlSource.Tool/RealPath.cs`
- Modify: `tests/SqlSource.Tool.Testing/OnlineBuilds.cs` (its private `RealPath` calls the tool's)
- Test: `tests/SqlSource.Tool.Tests/RealPathTests.cs`

**Interfaces:**
- Produces: `internal static class RealPath { public static string Of(string fullPath); }`

- [ ] Tests (skipped on Windows, which needs a privilege to make a link): a path with no link is returned as given; a link to a folder at the start, at a folder above, and as the file itself; a relative target; a target under a link; a missing tail kept as text after the resolved part; a loop returns the path as given.
- [ ] Implement: walk the segments from the root; for a segment that exists, `File.ResolveLinkTarget(path, returnFinalTarget: false)`; on a link, restart the walk from the target's full path joined with the remaining segments, at most 40 links, else return the input; stop at the first segment that does not exist and join the rest as text.  `IOException` and `UnauthorizedAccessException` stop the walk the same way.
- [ ] Run `dotnet test` filtered to `RealPathTests`, then commit.

### Task 2: `--project` through a link

**Files:**
- Modify: `src/SqlSource.Tool/Projects/RunProjects.cs` (`Restrict`)
- Test: `tests/SqlSource.Tool.Tests/Projects/RunProjectsTests.cs`

**Interfaces:**
- Consumes: `RealPath.Of`

- [ ] Tests: a solution in a real folder, the project named by its full path through a link to that folder, and through a link to a folder above it, is selected and evaluated by the solution's spelling; a solution named through a link with the project named by its real path is selected; a missing project through the link is still `SQLSRC207` naming the path as typed.
- [ ] Implement: in `Restrict`, when the text match fails, build once a map from `RealPath.Of(project)` to the project, and look up `RealPath.Of(path)`.
- [ ] Run the `RunProjectsTests`, then commit.

### Task 3: `.sql` paths through a link, and the docs

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunFilters.cs` (`Create` gains `IEnumerable<string> listedFiles`)
- Modify: `src/SqlSource.Tool/PlanSteps.cs`, `src/SqlSource.Tool/Bundles/DiagnoseRun.cs` (pass `manifests.SelectMany(m => m.Files)`)
- Test: `tests/SqlSource.Tool.Tests/Planning/RunFiltersTests.cs`, and a CLI-level test of `describe` with a `.sql` path through a link if a fixture allows
- Docs: delete the TD-0030 file and row; remove `site/reference/cli.md`'s `## Limits` and its troubleshooting row; say in `site/reference/cli.md#arguments` and `docs/architecture/tool.md` that a path through a link names its real file.

- [ ] Tests: a `.sql` path through a link at the working directory and above it binds to the listed path's normalised form; a listed path through a link and a real filter path bind too; two filters for one file through link and real path keep the first; a path not listed keeps its own normalised path.
- [ ] Implement: when `Normalize(full)` is not among the listed files' normalised paths, compare `Normalize(RealPath.Of(full))` with a map, built once, from `Normalize(RealPath.Of(listed))` to `Normalize(listed)`.
- [ ] Format, validate, commit.
