# AGENTS.md - docs/architecture

`README.md` is the overview: the components, the flow, where shared code lives, the glossary and the reading guide.

## Which page covers which code

Each page is in this folder.

| Folder | Page | Sections |
|----|----|----|
| `src/SqlSource`, its root | `generator.md` | `## The pipeline` |
| `src/SqlSource/Generation`, with `Converters/`, `Models/`, `Paths/`, `Sidecars/` and `Types/`, with `Types/Drivers/`, `Types/Maps/` and `Types/MappedTypes/` | `generator.md` | `## The pipeline`; `### Converters` for `Converters/` |
| `src/SqlSource/Generation/Chains` | `generator.md` | `#### The chains` and `#### Which files are checked`, under `### Settings files` |
| `src/SqlSource/Generation/Emitting` | `generator.md` | `## Emitting` |
| `src/SqlSource/Generation/Methods` | `generator.md` | `### Query methods`, under `## Emitting` |
| `src/SqlSource/Settings`, with `ClrTypes/`, `ColumnTypes/`, `Libraries/` and `TypeOverrides/` | `generator.md` | `## Settings` |
| `src/SqlSource/SettingsFiles` | `generator.md` | `### Settings files`, under `## Settings` |
| `src/SqlSource/Parsing`, with `Markers/`, `Quoting/` and `Tokens/` | `generator.md` | `## Parsing` |
| `src/SqlSource/Diagnostics` | `generator.md` | `## Diagnostics` |
| `src/SqlSource/Snapshot`, with `Reading/` | `generator.md`, `sidecar-format.md` | `## Snapshot`; every section of `sidecar-format.md` |
| `src/SqlSource/Polyfills` | None | It holds the types that `netstandard2.0` lacks, and no design |
| `src/SqlSource/build` | `msbuild-and-manifest.md` | `## What the props and targets do`, `## The manifest`, `## Known limits` |
| `src/SqlSource.Tool`, its root | `tool.md` | `## The command line`, `## A describe run` |
| `src/SqlSource.Tool/Projects` | `tool.md` | `## A describe run`, `## Folders`; the manifest's reader in `msbuild-and-manifest.md`, `## The manifest` |
| `src/SqlSource.Tool/Planning` | `tool.md` | `## A describe run`, `## Folders` |
| `src/SqlSource.Tool/Processes` | `tool.md` | `## Folders` |
| `src/SqlSource.Tool/Describing`, its root | `tool.md` | `## A describe run`, `## Folders` |
| `src/SqlSource.Tool/Logging` and `src/SqlSource.Tool/Reporting` | `tool.md` | `## Logging and reporting` |
| `src/SqlSource.Tool/Describing/Describers` | `postgres-describer.md` | `## The describer contract` |
| `src/SqlSource.Tool/Describing/Capture` | `postgres-describer.md` | `## Capture and replay` |
| `src/SqlSource.Tool/Describing/Postgres`, with `Columns/`, `Statements/` and `Types/` | `postgres-describer.md`, `postgres-nullability.md` | `## One describe, step by step`, `## Building parameters and columns`, `## Matching a table`, `## Errors`, `## Why a query cannot run a statement`; `## Nullability` in `postgres-nullability.md` |
| `src/SqlSource.Tool/Describing/Postgres/Walk` | `postgres-nullability.md` | `## The plan walk` |
| `src/SqlSource.Tool/Describing/SqlServer`, with `Columns/`, `Statements/` and `Types/` | `sqlserver-describer.md` | Every section |
| `src/SqlSource.Tool/Describing/SqlServer/Proofs` | `sqlserver-describer.md` | `## Proofs of not null` |

A new page gets its rows here and its line in the reading guide of `README.md`, in the commit that adds it.  A new folder under `src/` gets its row in the same commit.

## Keeping the pages true

- **A page is coupled to every name it gives.**  When a change renames, moves or removes a file, folder, type or member that a page names, the same commit updates the page, as root `AGENTS.md` asks of an `AGENTS.md`.
- **Check a claim against the code before you write it.**  When the code and an older document disagree, the page says what the code does.  When the code itself is the problem, it is recorded in `docs/tech-debt`.
- **Use the glossary's terms in its sense.**  A term that a second page needs goes in the glossary of `README.md`, and is not defined again on a page.

## Placeholders

A part of query generation that is not built yet has a placeholder under `## Not built yet` of the page it will extend.  It is exactly this: a heading, one sentence on what will go there, and the phase with a link to the epic.  Nothing else.

```markdown
### Build integration

> **Not built yet.**  How a target of the build runs the tool before the compile in the online mode.  Delivered by phase 9 of the [query generation epic](../superpowers/specs/2026-10-07-query-generation-epic-design.md).
```

- **A placeholder is never filled with design from a spec.**  It holds no design and no types until the code exists.
- **Never describe the future as present.**  A part that is not built is a placeholder or nothing.

## What belongs elsewhere

- Behaviour a user sees goes in `docs/manual/`.  Where a page touches it, such as exit codes or which columns can be null, the page links to the manual and explains only how the code produces it.
- Decisions and their history go in `docs/superpowers/`.
- What has been delivered is recorded in "What works today" in `docs/manual/README.md`.  A page holds no status table.
- The sidecar's shape and the meaning of each key are defined by `schemas/sidecar-v1.schema.json`, and its rules by `sidecar-format.md`.  Link to them; never restate them.

## Writing a page

- **Link to source; never copy it.**  A short snippet that shows a shape is fine.  No line-by-line walkthroughs, and no list of type names that does not say what each does.
- **Diagrams are Mermaid**, in a ` ```mermaid ` fence.  Draw one where a flow is easier to see than read.
- **Write in the repository's voice**: plain declarative sentences, two spaces after a full stop.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
