# TD-0119 Static Access in Full Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A static connection source or static factory holder is reached through the repository's type in full, so a base type's static member with the repository's name compiles.

**Architecture:** `ConnectionResolver.Resolving.AccessTextOf` writes `global::`, the namespace, and each containing type with its type parameters, then the member.  Nothing can hide that text, so `LeadingNameOf` names only a parameter, and `SQLSRC275` no longer refuses a token with the repository's name.

**Tech Stack:** C#, Roslyn source generator, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0119-static-member-of-the-repositorys-name.md`

## Global Constraints

- Every static access takes the full form, as the brief prefers, unless the golden corpus shows ordinary repositories made unreadable.
- `AccessText` keeps a keyword's `@` in the namespace, each type and the member.
- A primary-constructor parameter keeps its leading name and its `SQLSRC275`.
- Resolve the item as `docs/tech-debt/AGENTS.md` says: delete its file and row, every reference to its id, its limit and its troubleshooting row.

## Review Focus

- A repository in the global namespace is reached as `global::Sample.X`, with no empty namespace segment.
- A repository nested in a generic type carries each level's type parameters.
- A keyword used as a namespace or a type name keeps its `@`.
- A namespace with the repository's name, `namespace Sample { class Sample : Base }`, still reaches the member.
- A token with the repository's name compiles beside a static source and a static holder.  A struct has no base type, so it cannot inherit a member of its name.

---

### Task 1: Write static access in full

**Files:**
- Modify: `src/SqlSource/Generation/Repositories/ConnectionResolver.Resolving.cs`
- Modify: `src/SqlSource/Generation/Repositories/ConnectionSource.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`HiddenSourceReason`)
- Test: `tests/SqlSource.Tests/Generator/MethodCompileTests.cs`, `tests/SqlSource.Tests/Generator/SharedConnectionMethodTests.cs`, `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`, `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTierTests.cs`, golden files

**Interfaces:**
- Produces: `ConnectionSource.AccessText` of `global::App.Repo<T>.X` for a static member; `LeadingName` null for every static member and static holder.

- [ ] **Step 1: Write the failing compile tests** in `MethodCompileTests`: `Run_StaticMemberWithTheRepositorysName_Compiles` and `Run_StaticHolderWithTheRepositorysName_Compiles`, each over the Review Focus shapes, and a token with the repository's name.
- [ ] **Step 2: Run them.**  Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.MethodCompileTests"`.  Expected: FAIL with CS1061.
- [ ] **Step 3: Implement.**  `AccessTextOf` uses the type in full; `LeadingNameOf(string name, CandidateMember member)` returns the name for a parameter, else null; `HiddenSourceReason` drops its arm for the type.
- [ ] **Step 4: Update the resolver tests** to the new `AccessText` and null `LeadingName`, and turn `Run_TokenNamedLikeAStaticHolderOfTheTypesName_ReportsSqlsrc275ForTheType` into a test that the token is accepted.
- [ ] **Step 5: Update the golden corpus** with `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone, run it again without the variable, and read the diff.
- [ ] **Step 6: Run `tests/SqlSource.Tests`.**  Expected: PASS.

### Task 2: Documents and the item

**Files:**
- Modify: `site/reference/repository-connections.md`, `site/guides/troubleshooting.md`, `site/reference/query-methods.md`, `site/reference/repositories.md`, `site/diagnostics.md`, `docs/architecture/generator.md`, `docs/tech-debt/README.md`
- Delete: `docs/tech-debt/TD-0119-static-member-of-the-repositorys-name.md`

- [ ] **Step 1: Remove the limit** and its troubleshooting row, and the page's `Limits` section, which holds nothing else.
- [ ] **Step 2: Remove the statement** that a token cannot have the repository's name from the three site pages, and describe the new access in `generator.md`.
- [ ] **Step 3: Delete the item** and its row, and confirm `git grep TD-0119` finds only this plan.
- [ ] **Step 4: Format and validate.**  Run: `./format.sh`, then `./pre-commit-validation.sh`.  Expected: pass.
- [ ] **Step 5: Commit.**
