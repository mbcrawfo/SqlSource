# Docs overhaul, phase 26: close - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The epic leaves nothing behind that only the epic needed, every rule and document that speaks of the site is true, the site is read once more whole, and the outline is closed.

**Architecture:** Documentation only.  Two read-only audits (the last read of the built site, and the deferred-work and debt audit) produce findings; fix tasks act on them; then the rules pass, the pass over the other documents, the last coverage run, the removal of the epic's instruments and the closing note.

**Tech Stack:** Markdown, docfx 2.81 (`tools/build-site.sh`), the epic's checker (`check/check.sh`), xUnit tests in `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-26-close-design.md`, read with the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.

## Where the merged code differs from the spec

- Phase 25 made pull request #310: the outline has its section "Resize", and `ledger/phase-25.tsv` exists.  The spec's "The counts, when phase 25 made no pull request" does not apply.
- `site/AGENTS.md` is 7,712 bytes, not the 7,047 the spec measured after phase 11.  The two diagnostic bullets it holds are 361 and 253 bytes.  The four bullets of the spec are 353, 193, 245 and 224 bytes: 8,113 bytes with all four, 79 under the limit.  The existing second bullet also names `#where-an-error-is-reported`, which the spec's replacement drops; that rule is kept, so the file needs trimming elsewhere (Task 5).
- `PagesNotYetMoved` is already gone (phase 8).
- Two owner rulings arrived with the gate (manager's message): mend `rewrite-guide.md` 3.6, "the file that `SqlSourceEnvFile` names" to "which file `SqlSourceEnvFile` names" (owner, p14 Q2, overriding the spec's call that the guide is not edited); and p24's shape rule, that a note directly after a bulleted Fix is a paragraph opening with a bold sentence, not a bullet, joins the diagnostic-section rule in `site/AGENTS.md`.

## Global Constraints

- Root `AGENTS.md` at most 16 KB, every other `AGENTS.md` at most 8 KB (8,192 bytes), a bullet at most 80 words: `AgentsFileTests`.
- `docs/architecture/` gets links and names, no new prose.
- The content map, `rewrite-guide.md`, `diagnostics-guide.md` and the 27 briefs are not edited, but for the owner's mend of 3.6.
- A false statement found is corrected by the source with a `corrected` row in `ledger/phase-26.tsv`.  A finding too large is a `docs/tech-debt` item named at the top of the PR.
- Do not run `docs.yml`.
- The quoted words "Give the method the transaction" and "A synchronous method needs a data source without multiplexing" still stand on their pages.
- Validation: `./format.sh`, then `./pre-commit-validation.sh` before each commit; `--full` before a push.

## Review Focus

- A link in a packed readme or an `AGENTS.md` that names a page phase 25 split: it must name the new page's anchor.
- A removed folder still named by a script, `.editorconfig`, a rule or `tests/README.md`.
- A deferred or debt item that duplicates an open one: search `docs/deferred` and `docs/tech-debt` before writing.
- A new limit without its row in `guides/troubleshooting.md` (`SiteStructureTests`).
- `site/AGENTS.md` over 8,192 bytes after the four bullets.

---

### Task 0: Ledger and the owner's mend (done by the controller)

- [x] `ledger/phase-26.tsv` with `# base e71155bf858cb746f7f3b3f293d9e08935c7b112` and the header line.
- [x] `rewrite-guide.md` line 185 mended.

### Task 1: The last read (read-only audit)

Follow the spec's "The last read" whole, on the built site (`tools/build-site.sh`, then read `_site` pages or serve with `dotnet docfx site/docfx.json --serve`).  Write to the scratchpad `last-read.md`:

- the landing page and Get started checklist, each item answered;
- for each of the sixteen rows, the three answers (no second click; whole sample; each rule link opens its owner);
- the three errors, SQLSRC241, 209 and 226;
- the search checks: the six words, the status grep, the internals grep, the sidebars;
- each finding with its file and line, its class (fix here / false statement with the source that proves it / too large), and the exact fix.

### Task 2: The deferred-work and debt audit (read-only audit)

Read, for phases 0 to 25: each PR's description (`gh pr view <n>` for #283, #285 to #310), the notes of each ledger file, `git log -p` of the outline, and its "Resize".  Compare with `docs/deferred/README.md` and `docs/tech-debt/README.md` and their `AGENTS.md`.  Write to the scratchpad `debt-audit.md` each candidate: its source, whether an open item already holds it, and if not, the item to write (deferred or debt, title, body points, and the limit when a user can see it).  The spec's "These are not items" list is excluded.

### Task 3: Fix the last read's findings

Apply each finding of Task 1: small fixes in place; false statements with a `corrected` row (`unit` by `@<file>:<line>` at the base, `to` the file and anchor, `words` the new words, a note naming the source); too-large findings as `docs/tech-debt` items.  Run `tools/build-site.sh` and the `SqlSource.Repository.Tests` project.  Commit.

### Task 4: Write the missing deferred and debt items

Write each item of Task 2 by `docs/deferred/AGENTS.md` and `docs/tech-debt/AGENTS.md`, with its index row, and with its limit and troubleshooting row where a user can see it.  Commit.

### Task 5: The rules in the `AGENTS.md` files

- Replace the two `- A new diagnostic` bullets of `site/AGENTS.md` with the spec's two, keeping `#where-an-error-is-reported` in the second, and add the p24 shape rule to the first.
- Add the outputs and the words bullets under "Writing", if they fit; otherwise in the spec's order.
- Check each row of the spec's table "The rules that must be in an `AGENTS.md` by now" against its file; add what is missing in the fewest words.
- After each edit: `wc -c`.  Then `AgentsFileTests`.  Commit.

### Task 6: The last pass over the other documents

- Each `AGENTS.md` from `git grep -l -E "site/|mbcrawfo\.github\.io/SqlSource|the manual|diagnostics\.md" -- '*AGENTS.md'`: pages, anchors and tests exist; the site's shape is true; no rule of the old site; maintenance footer; under the limit.
- The spec's table of files: `CONTRIBUTING.md`, `docs/publishing.md` (remove the "One exception holds until the documentation overhaul closes" paragraph), `tests/README.md`, `tools/README.md`, `.github/workflows/README.md`, `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`, `docs/architecture/**`, `examples/AGENTS.md`, the example READMEs, `.github/ISSUE_TEMPLATE/bug-report.yml`, both readmes.
- The search for the words of the old site: read each hit.
- Commit.

### Task 7: Coverage, removal, closing note (controller)

- [ ] `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh coverage`; exit 0; save the whole output for the PR.
- [ ] Read every ledger file's notes before removal (Task 2 did; re-check `phase-26.tsv`).
- [ ] Remove `inventory.tsv`, `ledger/`, `merges/`, `check/`; the epic bullet of `docs/superpowers/AGENTS.md`; the `.tsv` pattern of `pre-commit-validation.sh`; the `.editorconfig` section and comment.
- [ ] `git grep -n "docs-overhaul" -- . ':!docs/superpowers/specs'` names the outline at most.
- [ ] The closing note under the outline's title, by the spec's "The outline, closed", with the last commit that holds the instruments.
- [ ] Commit.

### Task 8: Reviews and validation

Content reviewers (no-loss and accuracy), then `/branch-review:review` to clean, then `./pre-commit-validation.sh --full`.  A fix reruns the later steps.
