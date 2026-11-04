# Sidecars in git Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tell users to mark sidecars `linguist-generated`, explain why they get no merge rule, and correct the advice for a sidecar with a merge conflict.

**Architecture:** Documentation only.  A new subsection of the manual's sidecar section holds the detail.  SQLSRC237, the README and the manual's entry page point to it.  This repository's `.gitattributes` follows the advice.

**Tech Stack:** Markdown, git attributes, the documentation tests in `tests/SqlSource.Tests/Documentation/`.

**Spec:** `docs/superpowers/specs/2026-11-04-sidecar-gitattributes-design.md`

## Global Constraints

- The attribute line is exactly `*.sql.json linguist-generated=true`.
- No `eol` rule and no merge rule (`-merge`, `binary`, a driver) is recommended to users.
- User docs describe effects, never internals: no sidecar field (no "hash"), no describer, no tech-debt or deferred id (`docs/manual/AGENTS.md`).
- `README.md` is packed: its link is absolute, `https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#sidecars-in-git`, and it holds no HTML.
- The manual links relatively, and a heading linked from elsewhere gets an `<a id>` line above it.
- The manual's prose uses two spaces after a full stop, as every page does.

## Review Focus

1. A user who has already resolved the conflict by taking one side: the docs must send them to `describe --force` on that `.sql` file, not plain `describe`.  Covered by the copy in Task 1, step 2.
2. A user without a database connection at merge time: the advice must say both commands need one.  Covered by the same copy.
3. A broken anchor from the README, the entry page or SQLSRC237 to the new subsection: `UserDocsLinksTests` catches it (Task 1, step 6).
4. An internal term slipping into the new copy: the grep in Task 1, step 7.
5. `src/SqlSource.Tool/README.md` is left unchanged on purpose: it mentions committing sidecars only in passing and links to the main README.  Builder's call, recorded here.

---

### Task 1: Document sidecars in git

**Files:**
- Modify: `docs/manual/tool.md` (sidecar section, lines 129-160)
- Modify: `docs/manual/diagnostics.md` (SQLSRC237, lines 1977-1989)
- Modify: `docs/manual/README.md` ("Find what you need", after line 31)
- Modify: `README.md` (line 132)
- Modify: `.gitattributes` (the `*.sql.json` line)
- Test: `tests/SqlSource.Tests/Documentation/` (existing tests; no new test)

**Interfaces:**
- Produces: the anchor `sidecars-in-git` in `docs/manual/tool.md`, linked by the other four places.

- [ ] **Step 1: Point the existing sentence in `tool.md` at the new subsection**

In the "Never edit a sidecar by hand" paragraph, replace `A sidecar with a merge conflict is no exception: run `describe` after the merge, and it writes the file again.` with `A sidecar with a merge conflict is no exception: see [sidecars in git](#sidecars-in-git).`

- [ ] **Step 2: Add the subsection to `tool.md`**

Insert it after the paragraph that ends with `A sidecar with a merge conflict in it is [SQLSRC237](diagnostics.md#sqlsrc237).` and before the paragraph about the JSON Schema:

````markdown
<a id="sidecars-in-git"></a>

### Sidecars in git

```text
# .gitattributes
*.sql.json linguist-generated=true
```

This line in the `.gitattributes` file at the root of your repository marks the sidecars as generated, so that GitHub collapses them in the diff of a pull request.  Give them no other rule.  Git merges two branches that changed one sidecar as it merges any text, and that is safe: the build knows which version of each query an entry was written for, so an entry that a merge leaves older than its query is [SQLSRC244](diagnostics.md#sqlsrc244) until `describe` writes it again.  A rule that stops git merging sidecars, such as `-merge` or `binary`, would turn every sidecar that both branches changed into a conflict, and would keep one side's file whole, losing what the other side's `describe --force` wrote after a schema change.

**After a merge conflict in a sidecar**, leave the conflict in the file and run `dotnet sqlsource describe`.  A sidecar with a conflict in it cannot be read, so the tool describes every query of its file again and writes the file whole: commit what it writes.  Until then the build reports [SQLSRC237](diagnostics.md#sqlsrc237).  If you have already taken one side of the conflict, run `dotnet sqlsource describe --force` on that `.sql` file instead, such as `dotnet sqlsource describe --force Queries/Users.sql`: the side you took may hold entries written before a schema change that the other side described, and plain `describe` keeps an entry whose query has not changed.  Both need a [connection](#connections) to the database.
````

- [ ] **Step 3: Correct SQLSRC237 in `diagnostics.md`**

Replace `After a merge conflict, take either side of the conflict or none: the tool writes the file from your queries and your database, not from what the file held.` with:

`After a merge conflict, leave the conflict in the file: the tool cannot read it, so it describes every query of the file again.  If you have already taken one side, run `dotnet sqlsource describe --force` on the `.sql` file instead, since the side you took may hold entries written before a schema change.`

Change the section's closing line to `See: [Sidecars in git](tool.md#sidecars-in-git)`.

- [ ] **Step 4: Add a row to "Find what you need" in `docs/manual/README.md`**

After the row `| Keep the sidecars current after a schema change | ... |`:

`| Set up git for the sidecars, or mend a merge conflict in one | [Sidecars in git](tool.md#sidecars-in-git) |`

- [ ] **Step 5: Add the sentence to `README.md` and the attribute to `.gitattributes`**

In `README.md` line 132, after `Commit the sidecars with the project: every build reads them, and a copy of the project without them reports an error for each query.`, insert:

`Add `*.sql.json linguist-generated=true` to your `.gitattributes` so that pull requests collapse them, and after a merge conflict in one, run `describe` again rather than merging it by hand: see [sidecars in git](https://github.com/mbcrawfo/SqlSource/blob/main/docs/manual/tool.md#sidecars-in-git).`

In `.gitattributes`, the sidecar rule becomes:

```text
# A sidecar is compared byte for byte with what the writer gives, which uses LF, and is collapsed in diffs as the manual advises
*.sql.json text eol=lf linguist-generated=true
```

- [ ] **Step 6: Run the documentation tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~DiagnosticsDocumentTests"`
Expected: PASS, including `RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist` and `RepositoryUrls_OfTheUserDocs_ReachAFileAndAnchorThatExist`.

- [ ] **Step 7: Grep the changed pages for internals**

Run: `grep -n -i -E "describer|EXPLAIN|heuristic|nullableSource|provenance|capture|tech-debt|superpowers|TD-0|D-0|hash" README.md docs/manual/tool.md docs/manual/README.md`
Expected: no hit in the new lines.  The `diagnostics.md` section changed only by the step 3 text, which contains none of these words.

- [ ] **Step 8: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: passes.

```bash
git add .gitattributes README.md docs/manual/tool.md docs/manual/diagnostics.md docs/manual/README.md docs/superpowers/specs/2026-11-04-sidecar-gitattributes-design.md docs/superpowers/plans/2026-11-04-sidecar-gitattributes.md
git commit -m "docs: sidecars in git, and the advice after a merge conflict in one"
```

### Task 2: Review and open the pull request

- [ ] **Step 1: Check the version**

Run `git fetch --tags origin`, then compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`.  If it is not greater, ask the user for the new version.

- [ ] **Step 2: Run `branch-review:review`** and address its findings until it comes back clean.

- [ ] **Step 3: Bring the branch up to date with `main`**, using `sync_with_base_branch`, and re-run `./pre-commit-validation.sh --full` on the tree as it will be pushed.
Expected: passes.

- [ ] **Step 4: Offer to open the pull request.**  On the user's yes, push, open it, bind it and turn on Auto-fix.
