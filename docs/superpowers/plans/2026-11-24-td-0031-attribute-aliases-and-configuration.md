# TD-0031: attribute aliases and configuration

**Goal:** the `sqlsource` tool reads `[SqlSourceGenerate]` through a `using` alias, in the file or among the project's global usings, and `describe` takes the configuration that MSBuild evaluates, as `dotnet build -c` does.  TD-0031 is rewritten to what stays: a type of the same name, which needs symbols.

**Spec:** `docs/tech-debt/TD-0031-attribute-reader-of-the-tool-is-syntax-only.md`, "Proposed fix".

## Departures from the spec

- The project's global usings come from two places.  A `global using` directive in a `Compile` file is in the manifest already.  A `<Using Include="..." Alias="..." />` item becomes a `global using` only in a file that the SDK's `GenerateGlobalUsings` writes under `obj` during a build, which the tool never runs.  So the manifest gains a line, `UsingAlias=<alias>=<type>`, for each `Using` item with an `Alias`.  It is an addition, and the format keeps version 2.
- The owner answered Q1 to Q3 on 2026-11-25: fix the partial-declarations case here, keeping the first claim of each type's full name in the order of the files; `describe` and `diagnose` both take `--configuration`, from `SharedOptions`; and the option has no `-c`.  TD-0031 keeps the same-named type alone.

## Tasks

1. **Manifest.**  `SqlSourceWriteManifest` writes `UsingAlias=%(Alias)=%(Identity)` for each `Using` item with an `Alias`, after the `Compile` lines.  `ManifestReader` reads it into `ProjectManifest.UsingAliases` and passes over a value with no `=`.  Tests: `BuildFileTests` (lines and order, item types), `ManifestReaderTests`, `ManifestTargetTests` (a real `dotnet msbuild`).
2. **Alias reading.**  `AttributeReader` keeps an alias whose target's last identifier is `SqlSourceGenerateAttribute`.  Scopes: the manifest's and every file's `global using` aliases for the project, the compilation unit's for its file, and a namespace's for its body, with the inner alias winning.  `[N]` is a claim when `N` or `NAttribute` is such an alias.  A file is parsed when it holds the word `SqlSourceGenerate` or, once global aliases are known, the name of one.  Tests in `AttributeReaderTests`, plus a `GeneratorParityTests` case if one fits.
3. **`--configuration`.**  `describe --configuration <name>` passes `-p:Configuration=<name>` to every `dotnet msbuild` of the run, a watch's too.  It is refused empty, given twice, or beside `--manifest`.  It goes through `UsageCheck`, `DescribeCommand.ReadsValuesAs`, the vocabulary of `UsageCheckTests`, `site/reference/cli.md`, and tests of the evaluator's arguments.
4. **Docs.**  TD-0031 rewritten, with its row; `docs/architecture/tool.md`, `docs/architecture/msbuild-and-manifest.md`, `Planning/AGENTS.md`, `site/reference/describing.md` and `site/reference/cli.md`; a limit for the same-named type in `site/reference/attributes.md` with its row in `site/guides/troubleshooting.md`.  The examples need no change: no example uses an alias or another configuration.
5. **Validate and review.**  `./format.sh`, `./pre-commit-validation.sh`, then `/branch-review:review --base origin/main`.
