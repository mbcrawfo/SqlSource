# Docs overhaul phase 25: resize - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split each page of `site/` that is over 3,500 words and has a clean cut.  Record why each other page outside 600 to 3,500 words stays.  Place each fact that a rewrite reported with `no owner:` with its owner.  Write the section "Resize" of the outline.

**Architecture:** Each split is a verbatim move of whole `##` sections to a new page in the same folder.  It moves their `<a id>` lines and retargets every inbound link.  The new page gets a new `toc.yml` entry, and the old page gets one pointer sentence.  The facts are moved or copied word for word into the owner's section, or recorded in "Resize" when they do not fit.  `ledger/phase-25.tsv` records every unit, and `check.sh move 25` proves that nothing was lost.

**Tech Stack:** Markdown pages built with docfx 2.81; xUnit tests in `tests/SqlSource.Repository.Tests/Documentation/`; the epic's checker `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-25-resize-design.md`, under the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.

## Global Constraints

- The count is `find site -name '*.md' ! -name AGENTS.md -exec wc -w {} + | sort -n` on the branch after it merged `main` at `4cc1c24a`, the merge of phase 17.  That is after phase 24 merged.
- A split moves whole `##` sections with their `###` sections and `<a id>` lines.  No section is divided.
- Each of the two pages has at least 600 words, and the old page has at most 3,500.  No `setting-<name>` section leaves the page of its thing.  No anchor is renamed.
- Today's words do not change.  The only new text is each new page's title and opening sentence, the one pointer on the old page, and the old title losing the words that name what moved.  The ledger gives each of these a `new` row, or a part row for the shortened title.
- A same-page link that crosses the cut names the other page: `[x](#locations)` on `members.md` becomes `[x](member-placement.md#locations)`, and the reverse.
- Inbound links are retargeted everywhere outside `docs/superpowers`: `site/`, both packed readmes (absolute URLs `https://mbcrawfo.github.io/SqlSource/reference/<page>.html#<anchor>`), C# comments, `docs/architecture/`, `docs/tech-debt/`, `docs/deferred/`, `examples/` and every `AGENTS.md`.
- The retargeting regex never matches a longer file name.  It requires a character that is not `[a-z-]` before the page name, so that `generator-parameters.md` and the new `list-parameters.md` are not rewritten.
- A new page goes directly under the old page's entry in `site/reference/toc.yml`, in the same group.
- A fact in `site/diagnostics.md` is copied, never moved.  A fact that does not fit the owner's section word for word stays, with a line in "Resize".
- `src/SqlSource.Tool/AGENTS.md` is under 8 KB, and so is every other `AGENTS.md`.
- No page of `start/`, `index.md`, `diagnostics.md` or a database folder is split or merged.
- `sqlserver/query-methods.md` stays whole, as the one listed exception.
- Ledger base line: `# base 4cc1c24a<full sha>`, the `origin/main` that this branch merged.  A cell is never empty; `-` stands for nothing.

## The decisions

| Page | Words | Decision |
|----|----|----|
| `reference/members.md` | 4,266 | Split.  `#locations` (with `#setting-member-location` and `#placement-for-a-file`), `#setting-visibility` and `#setting-nested-class` (2,965 words) go to `reference/member-placement.md`, "Where the members go".  The old title loses "and where it goes" |
| `reference/repositories.md` | 3,794 | Split.  `#repository-connection` (1,422) goes to `reference/repository-connections.md`, "Where a repository gets its connection" |
| `reference/type-mapping.md` | 3,722 | Stays whole.  `#property-types` is 501 words and the page's top 12, under 600.  `#setting-type-overrides` is one setting's section of 2,995 words, which cannot be divided |
| `reference/parameters.md` | 3,710 | Split.  `#list-parameters` (1,505 words, with `#list-padding` and `#list-limit`) goes to `reference/list-parameters.md`, "List parameters (IN lists)".  The old title loses ", IN lists" |
| `reference/models.md` | 3,659 | Split.  `#table-models` and `#shared-models` (1,181) go to `reference/shared-models.md`, "Table models and shared models" |
| `reference/settings.md` | 3,598 | Owner question Q1.  The default is to keep it whole: the outline's "Where rules live" keeps the places, the matrix, precedence and the three settings on this page |
| `reference/query-methods.md` | 3,561 | Split.  `#synchronous-methods` (1,082) goes to `reference/synchronous-methods.md`, "Synchronous methods" |
| `reference/msbuild.md` | 3,507 | Split.  `#running-in-the-build`, `#online-build` and `#setting-env-file` (2,644) go to `reference/online-build.md`, "Running the tool in the build".  `## Limits` stays: its one limit is about the files the build includes |
| `reference/databases/sqlserver/query-methods.md` | 4,442 | Stays: the map's one listed exception |
| `diagnostics.md` | 49,774 | Stays: decision 7 |
| `reference/databases/other.md`, `start/testing.md` | 300, 546 | Stay: allowed by the map |
| `reference/databases/index.md` | 474 | Stays: a page of the databases folder, the one home of the supported versions, read by `SupportedVersionsTests` |
| `start/how-it-works.md`, `start/install.md`, `start/call-queries.md` | 484, 568, 586 | Stay: pages of Get started are never merged |

The group "Generated code" would hold 13 entries.  It divides in `reference/toc.yml` into "Generated code": members, member-placement, models, shared-models, model-properties, return-types.  The second group is "Generated methods": query-methods, synchronous-methods, repositories, repository-connections, connection-methods, collection-methods, exceptions.

## Review Focus

- A link inside a moved section to an anchor that stays on the old page, or the reverse, left as `#anchor`.  docfx fails on it, so `tools/build-site.sh` must give no warning.
- A link that names a page by a longer file name (`generator-parameters.md#list-parameters`) rewritten by mistake.  `git diff` holds no such change: check it with a grep of the diff for `generator-list-parameters` and `-list-parameters.md` preceded by a letter.
- A packed readme's absolute URL, which `UserDocsLinksTests` checks against the site's pages.  The PR says whether one changed.
- A matrix row of `reference/settings.md` that links a moved `setting-` anchor, which `SettingsReferenceTests` reads.
- An `AGENTS.md` that names a moved section by page, such as `site/AGENTS.md`'s table or `src/SqlSource.Tool/AGENTS.md`.

---

### Task 1: The six splits

**Files:**
- Create: `site/reference/member-placement.md`, `repository-connections.md`, `list-parameters.md`, `shared-models.md`, `synchronous-methods.md`, `online-build.md`
- Modify: the six old pages, `site/reference/toc.yml`, every file that links a moved anchor, any `AGENTS.md` that names a moved section
- Create: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-25.tsv`

**Interfaces:**
- Produces: the anchors' new pages, listed in "The decisions", which Task 2 uses as the owners' places.

- [ ] **Step 1: Write the ledger's head**

The ledger starts with `# base <full sha of origin/main>`, then a `#` comment with the counts of each split page, old > new.  Then the header `unit	disposition	to	arg	words	note`.

- [ ] **Step 2: Move the sections**

For each split, cut the line range from the old section's first `<a id>` line to the line before the next kept `<a id>` line.  Put it on the new page after this top:

```markdown
# <Title>

<One opening sentence that says what the page holds, with a link back to the old page.>

```

In the old page's place, leave one pointer paragraph: one sentence with a link to the new page.

- [ ] **Step 3: Fix the links that cross the cut**

On each pair of pages, `](#a)` becomes `](<other>.md#a)` when `a` is now on the other page.

- [ ] **Step 4: Retarget the inbound links**

Use one Perl substitution per split, over `git grep -l` outside `docs/superpowers`:

```bash
perl -pi -e 's{(?<![a-z-])members\.(md|html)#(locations|setting-member-location|placement-for-a-file|setting-visibility|setting-nested-class)(?![a-z-])}{member-placement.$1#$2}g'
```

The anchor sets are:

- repositories: `repository-connection`.
- parameters: `list-parameters|list-padding|list-limit`.
- models: `table-models|shared-models`.
- query-methods: `synchronous-methods`.  The match must not reach `databases/*/query-methods.md`: check that no database page has the anchor.
- msbuild: `running-in-the-build|online-build|setting-env-file`.

- [ ] **Step 5: The toc and the titles**

Add each entry under its old page, with the new group as above.  Sidebar titles:

- "Where the members go (location, visibility, nested class)";
- "Where a repository gets its connection";
- "List parameters (IN lists)";
- "Table models and shared models";
- "Synchronous methods";
- "Running the tool in the build (online build)".

The old sidebar titles lose the words of what moved:

- "What is generated";
- "Query parameters (@name, -- param:)".

- [ ] **Step 6: Ledger rows**

Each moved `##` section gets one `move` row: `@site/reference/<old>.md:<first>-<last>` at the base, `to` `site/reference/<new>.md#<anchor>`.

Each opening sentence and each pointer gets a `new` row with `to` its page.  Each changed title gets a `move-split` row with one part, `<unit>/1`, holding the words that stay.  The new h1 that gets this treatment is also counted in a `new` row of that page.

- [ ] **Step 7: Verify**

Run, and expect:

- `check.sh move 25`: lists 1 and 2 hold nothing unexplained, and list 3 holds only the new rows' text.
- The brief's anchor diff prints nothing.
- `wc -w` of each split page is at most 3,500, and of each new page at least 600.
- `tools/build-site.sh` gives no warning.
- `dotnet test tests/SqlSource.Repository.Tests` passes.

- [ ] **Step 8: Commit** after `./format.sh` and `./pre-commit-validation.sh`.

### Task 2: The facts with no owner

**Files:**
- Modify: the owners' pages: `msbuild.md`, `attributes.md`, `member-placement.md`, `settings-file.md`, `exceptions.md`, `repositories.md`, `repository-connections.md`, `dialects.md`, `generator-parameters.md`, `sql-files.md`, `parameters.md`, `models.md`, `settings.md`, `type-mapping.md`, `list-parameters.md`, `databases/sqlserver/query-methods.md`
- Modify: `ledger/phase-25.tsv`

**Interfaces:**
- Consumes: the new pages of Task 1, as the owners' places.  An owner anchor that moved is looked up on its new page.
- Produces: one disposition for each of the 36 `no owner:` notes: a ledger row, or a line for "Resize" in the hand-back.

- [ ] **Step 1: List the 36 notes**

```bash
grep -n "no owner:" docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-1[2-9].tsv docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-2[0-4].tsv
```

They are 1 of p13, 1 of p15, 2 of p16, 4 of p17 and 28 of p21.  The PR descriptions agree.

- [ ] **Step 2: For each note, read the owner's section as it is now**

If it holds the fact, the disposition is "held", and nothing changes.

- [ ] **Step 3: Otherwise, place the fact**

From a page, the sentence moves word for word to the owner's section; this is a `move` row.  If the sentence was the whole restatement, one sentence linking the owner stays, with a `new` row.

From `diagnostics.md`, the sentence is copied; this is a `new` row whose note names the SQLSRC section.

A sentence that does not fit without a change of words stays, and is recorded for "Resize".

- [ ] **Step 4: Verify**

Run `check.sh move 25`, `tools/build-site.sh` and the documentation tests.

- [ ] **Step 5: Commit**

### Task 3: The record

**Files:**
- Modify: `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`: the tree, "That is 70 pages", the Constraints line on `src/SqlSource.Tool/AGENTS.md` if its size changed, and a new section `## Resize` at the end.

- [ ] **Step 1: Write "Resize"**

It holds:

- the date;
- the table of counts: every page, the map's words (`-` for a page the map has no count for) and the words now;
- each split;
- each fact and where it is now;
- each page that stays over 3,500 or under 600, with its reason.

- [ ] **Step 2: Verify**

Run `check.sh coverage` and `./pre-commit-validation.sh`.

- [ ] **Step 3: Commit**
