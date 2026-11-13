# Docs overhaul, phase 25: resize

Phase 25 of the [docs overhaul](2026-11-12-docs-overhaul-epic-design.md).  Read the outline, the [content map](2026-11-12-docs-overhaul/content-map.md) (sections 1 to 3, 6 and 8) and [`rewrite-guide.md`](2026-11-12-docs-overhaul/rewrite-guide.md) before this brief.

- Done by: an Opus agent.
- Needs: phases 12 to 24, all merged.
- Runs beside: nothing.  Phase 26 waits for it.

## Goal

After the rewrites, pages are longer.  This phase counts every page of the site again.  It splits each page over 3,500 words that has a clean cut, and it writes down why each other page over the limit stays whole.  It looks at each page under 600 words too.  It also places each fact that a rewrite reported as having no owner.  If no page needs a split or a merge and no fact needs a place, the phase makes no pull request, no commit and no push, and phase 26 records the counts.

## The counts

- Each rewrite phase, 12 to 24, reported the word count of each of its pages, before and after: in its pull request's description, and in a `#` comment line of its ledger file, `ledger/phase-<nn>.tsv`.  Read them: they say where a page grew.
- Those counts are a guide.  The count that decides is yours, made on `main` after phase 24 merged:

```bash
find site -name '*.md' ! -name AGENTS.md -exec wc -w {} + | sort -n
```

- The count is `wc -w` over the whole file, fenced blocks and tables included.  It is the rule of the content map.
- Make one table of all pages: the page, the words in section 1 of the map, the words now.  The table goes into the outline, as "The record in the outline" says.

## The rule

A page over 3,500 words is split where it has a clean cut.  A cut is clean when all of these are true:

- It runs between two `##` sections.  Whole sections move, each with its `###` sections and its `<a id>` lines.  No section is divided.
- Each of the two pages is 600 words or more, and the page that was too long is now at most 3,500.
- Each of the two pages answers one question, and a sidebar title can say that question.
- No `setting-<name>` section leaves the page of the thing that it changes.
- No anchor changes its name.

These pages are never split:

- `site/diagnostics.md`, whatever its count.  It is decision 7 of the outline, and every help link names the page.
- `site/index.md`, and each page of `start/`.  The path of Get started is the nine pages of the outline.
- A page of a database folder, unless the same cut is clean in every database folder.  `SiteStructureTests` fails when the folders have different file names.  So a split there is made in each folder, with one new file name, or not at all.
- `reference/databases/sqlserver/query-methods.md`.  The map rules on it: it has no clean cut, no sixth file name is added for it, and it stays the one listed exception.  Phase 20 reported its count.

A page over 3,500 words with no clean cut stays whole.  Its name, its count and the reason go into the outline.

## The splits that the map foresaw

Section 1 of the map names one split and five more pages close to the limit.  The map gives the cut for the first.  The cuts for the others are calls of this brief, measured on the baseline.  The briefs of phases 13 to 17 name the same cuts for all but `models.md`, and each of those phases reports whether its page passed the limit.

| Page | Words in the map | The cut, if the page is over 3,500 | New page |
|----|----|----|----|
| `reference/members.md` | 3,850 | The placement sections: `#locations` with `#setting-member-location`, `#placement-for-a-file`, `#setting-visibility`, `#setting-nested-class` | `reference/member-placement.md` |
| `reference/parameters.md` | 3,350 | `#list-parameters` with `#list-padding` and `#list-limit`, 1,370 words at the baseline | `reference/list-parameters.md` |
| `reference/models.md` | 3,350 | `#table-models` and `#shared-models`, 1,050 words at the baseline | `reference/shared-models.md` |
| `reference/msbuild.md` | 3,200 | `#running-in-the-build`, `#online-build` and `#setting-env-file`, 2,320 words at the baseline, before the merge of MP-06 | `reference/online-build.md` |
| `reference/repositories.md` | 3,150 | `#repository-connection`, 1,200 words at the baseline | `reference/repository-connections.md` |
| `reference/type-mapping.md` | 3,400 | None.  `#setting-type-overrides` is one setting's section of 2,600 words.  `#property-types` alone is 420 words, under 600 | Stays whole |

- A page of this table that is at 3,500 words or under is not split.
- If a rewrite changed a page so that its cut is no longer clean, look for another by the rule.  If there is none, the page stays whole.
- Any other page over 3,500 words gets the same treatment: find a clean cut, or record why there is none.
- Give a new page a sidebar title that names its thing, as the other titles of `reference/` do.  If the old title names what moved, as "(`@name`, `-- param:`, IN lists)" does, take those words out of the old title.

## What a split is

A split is a move.  Today's words do not change.  One pull request can hold several splits.

1. **The new page**, in the folder of the old one.  It has its title, one opening sentence that says what the page holds, and the sections that moved, in their order.  Give it whatever every page of that folder has at its top.
2. **The anchors move by name.**  Each `<a id>` line moves with its heading.  A heading that keeps two `<a id>` lines from a merge keeps both.
3. **One pointer on the old page**, where the sections stood: one sentence with a link to the new page.  The opening sentence and the pointer are the only new text, and they are in plain language.
4. **`## Limits`.**  Each limit goes with the page that owns the behaviour.  If both pages then have limits, both have the heading.  `guides/troubleshooting.md` links each limit, so retarget its rows: `SiteStructureTests` fails on a page with `## Limits` that it does not link.
5. **The `toc.yml` of the folder.**  The new entry stands directly under the old page's entry, in the same group.  `SiteTocTests` fails on a page that is not in exactly one `toc.yml`.
6. **Every inbound link.**  Search for each anchor that moved, by name:

```bash
git grep -n -E "members\.(md|html)#(locations|setting-member-location|placement-for-a-file|setting-visibility|setting-nested-class)"
```

   - Inside `site/`: links from other pages, and the links of the two pages to each other, which were `#anchor` and now name a page.
   - In `README.md` and `src/SqlSource.Tool/README.md`: absolute links, `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html#<anchor>`.
   - Elsewhere: comments in C# files, `docs/architecture/`, `docs/tech-debt/`, `docs/deferred/`, the example projects, every `AGENTS.md`.  `InboundSiteReferenceTests` fails on each one that you miss.
   - A link to the old page with no anchor stays, unless its sentence is about what moved.
7. **The rules that name the page.**  Search every `AGENTS.md`: `git grep -n "<page>.md" -- "*AGENTS.md"`.  The table "Where a new thing goes" of `site/AGENTS.md` names `reference/msbuild.md`, among others.  Change each row in the same commit, and check the 8 KB limit: `AgentsFileTests` holds it.
8. **The twins of section 8 of the map.**  If a page with a mirrored sample splits, and the sample moves, change the path or the words in its twin in the same commit.
9. **The outline.**  Change its tree and its count of pages.  The content map does not change: it is the record of the plan.
10. **The ledger.**  `ledger/phase-25.tsv` has a `move` row for each section that changed its page, and a `new` row for each opening sentence and each pointer.  After the rewrites almost no unit of a page has the text of its baseline unit, so a row names its units by their place at your base, as a range: `@site/reference/members.md:120-310`.  Its `to` is the new page and the anchor of the section.

Then run "check move 25": `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh move 25`.  The checker reads "old" at the commit of the `# base` line of `ledger/phase-25.tsv`.  It reduces each link to its text, drops `<a id>` lines, and matches the units of the old pages against the pages of the working tree.  Its lists "Not at its place" and "Left over" must be empty, and its list "Added" must hold only the text of the `new` rows.  Section 6 of the [brief of phase 0](2026-11-12-docs-overhaul-phase-00-guard-rails-design.md) describes the lists.  For a quick look of your own at one split:

```bash
base=$(git merge-base origin/main HEAD)
strip() { sed -E 's/\]\([^)]*\)/]()/g; /^<a id=/d' "$@" | sort; }
diff <(git show "${base}:site/reference/members.md" | strip) <(strip site/reference/members.md site/reference/member-placement.md)
```

**A tenth entry.**  A sidebar group can hold a tenth entry in this phase.  The group "Generated code" has nine today, and three pages of the table above are in it.  If a group would hold eleven, divide the group in `reference/toc.yml` into two groups.  For "Generated code", cut it as phases 16 and 17 did: what is generated (members, models, model properties, return types, and their new pages), and the methods (query methods, repositories, connection methods, collection methods, exceptions, and their new pages).  A group exists only in `toc.yml`, so this moves no file and changes no link.

## Pages under 600 words

- The map allows three, each with its reason: `index.md`, `start/testing.md` and `reference/databases/other.md`.  Leave them.
- For any other page under 600 words, decide: merge it, or leave it with a reason.
- Leave it when its title carries a word that a reader searches for and no other title has, or when it is the one door for a group of readers.
- Never merge a page of `start/`, a page of a database folder, or a page that a row of the landing page names as its answer.
- A merge is a move in the other direction: the sections and anchors go to the page that takes them, the page and its `toc.yml` entry go, and every inbound link is retargeted, as above.  A guide group must not fall to one page.
- Write each decision into the table of counts.

## Facts with no owner

A rewrite cuts a rule that another section owns down to a restatement.  If the owner's section lacks a fact, the rewrite leaves the fact on its own page and reports it (section 8 of the rewrite guide).  The rewrites ran at the same time, so none could add to the page of another.  This phase can, and the outline gives it the work.

- Find the reports.  Each is a row of a rewrite's ledger whose note opens with `no owner:`, then the owner's `<file>#<anchor>`, then the fact.  This lists them all:

```bash
grep -n "no owner:" docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-1[2-9].tsv docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-2[0-4].tsv
```

- Compare with the heading "Facts with no owner" in the description of each of the thirteen pull requests: `gh pr view <number> --json body`.  A fact that is in one place and not in the other is still a fact to place.
- For each fact, read the owner's section as it is now.  If it holds the fact by now, there is nothing to do.
- If it does not, move the sentence to the owner's section, word for word, to the place where it fits.  It is a `move` row of the ledger, with the sentence named by its place at your base.
- On the page that had it, the restatement and its link stay.  If the sentence was the whole restatement, leave one sentence that links the owner.  It is a `new` row.
- A fact that a section of `site/diagnostics.md` states is copied, not moved: a section stands alone and keeps its text.  The copy in the owner's section is a `new` row, and its note names the section it came from.
- If the sentence does not fit the owner's section without a change of words, leave it where it is.  Nothing is lost.  Record it in the section "Resize" of the outline.
- "check move 25" covers these moves too.

## The record in the outline

When this phase makes a pull request, the outline gets a section "Resize" at its end, in that pull request.  When it makes none, phase 26 writes the section in its own pull request, from its own count: its brief says so.  The section holds:

- the date;
- the table of counts;
- each split and each merge that was made;
- each fact with no owner, and where it is now;
- each page that is over 3,500 or under 600 words and stays, with its reason.

Section 1 of the map holds the exceptions of the plan.  This section holds what was found.

## If nothing needs a split, a merge or a place

- Open no pull request.  Make no commit, and push nothing: no change reaches `main`, or any branch of the remote, outside a pull request.
- Say in your report that no page needed a split or a merge and no fact a place.  Give the table of counts, and for each page over 3,500 or under 600 words the reason why it stays.
- Phase 26 runs the count again and writes the section "Resize" of the outline in its own pull request.  The reasons are the rules of this brief, so that phase can give them without your report.

## Done when

If the phase has nothing to do, it is done when its report is given: the first item below, and no commit.  Otherwise:

- The table of counts covers every page that `find site -name '*.md' ! -name AGENTS.md` prints, and each page over 3,500 or under 600 words has a split, a merge or a written reason.
- `wc -w` of each page that was split prints at most 3,500, and of each new page 600 or more.
- "check move 25" exits with 0: its lists "Not at its place" and "Left over" are empty, and its list "Added" holds only the text of the `new` rows of `ledger/phase-25.tsv`.  "check coverage" exits with 0.
- Each `no owner:` note of the ledgers of phases 12 to 24 has a row in `ledger/phase-25.tsv` or a line in the section "Resize".
- No anchor changed its name: every anchor that the old pages had is on one page of the site.  This prints nothing:

```bash
base=$(git merge-base origin/main HEAD)
diff <(git grep -h -o '<a id="[^"]*">' "${base}" -- site | sort) <(grep -r -h -o '<a id="[^"]*">' site | sort)
```

- `tools/build-site.sh` gives no warning.
- These tests pass: `SiteTocTests`, `SiteStructureTests`, `InboundSiteReferenceTests`, `UserDocsLinksTests`, `UserDocsSamplesTests`, `SettingsReferenceTests`, `CliReferenceTests`, `SupportedVersionsTests`, `ReadmeTests`, `DiagnosticsDocumentTests` and `AgentsFileTests`.  No test was weakened to pass.
- Every `AGENTS.md` that names a page that split names the right page, and is under its limit.
- The outline's tree, its count of pages and its section "Resize" are true.
- The reviews of the outline are done, in their order: content review, `branch-review:review`, `./pre-commit-validation.sh --full`.
- The pull request says whether a link of a packed readme changed.  If one did, it tells the owner to publish the site by hand after the merge: `gh workflow run docs.yml --ref main`.

## For the reviewers

**The no-loss reviewer:**

- Run "check move 25", or the comparison by hand, for each split.  Read what is left over: it is the opening sentence, the pointer, and nothing else.
- Check that the sections of each new page stand in the order they had.
- Open three inbound links of each split in the built site, and one from outside `site/`.

**The accuracy reviewer:**

- Read each opening sentence and each pointer: it says what the page holds, and states no rule.
- Check each cut against the rule: one question on each page, and no `setting-<name>` section away from its thing.
- Check each reason for a page left whole.  "No clean cut" names the sections and their sizes.
- Check the `toc.yml`: the order, the group, and no group over ten entries.

## Traps

- **Do not reword while you split.**  A sentence that reads badly after the split is a finding for the pull request, not an edit.  The two new sentences are the exception.
- **A same-page link becomes a cross-page link.**  `[placement](#locations)` on `members.md` is dead after the split, and docfx fails on it.  The links of the moved sections back to the old page need its name too.
- **A tab can hold a link, and never a heading or an anchor.**  Do not cut a page inside a tab group.
- **`SettingsReferenceTests` reads the matrix of `reference/settings.md`.**  Each row links a `setting-` anchor.  When that anchor moves to a new page, change the link of the row.  The test fails on a `setting-` anchor with no row or with two.
- **`CliReferenceTests` reads `reference/cli.md`, and `SupportedVersionsTests` reads `reference/databases/index.md`.**  Neither page is in the table above.  If one of them must split, the rows that its test reads stay on the page that the test names.
- **`EditorRecipeTests` reads `site/guides/describe-on-save.md`.**  Its VS Code task stays on that page.
- **`src/SqlSource.Tool/AGENTS.md` has almost no room.**  A longer page name can push it over 8 KB.
- **Do not split to make the numbers look even.**  A page at 3,480 words stays.

## Calls made in this brief

- **The cuts of `parameters.md`, `models.md`, `msbuild.md` and `repositories.md`, and "no clean cut" for `type-mapping.md`.**  The map says that phase 25 splits these pages if they pass the limit, and names no cut.
- **The names of the new pages.**  The map names `member-placement.md` alone.
- **A group that would hold eleven entries is divided in `toc.yml`.**  The outline allows a tenth entry and says nothing of two splits in one group.
- **A page under 600 words is merged only in `guides/` and in `reference/` outside the database folders.**  The outline says that such pages are looked at, and gives no rule.
- **How a fact with no owner is found and placed.**  The outline gives this phase the placement.  This brief finds each one by the note `no owner:` that the rewrite guide asks for, moves the sentence word for word, and copies a fact of a diagnostic section, which must keep it.
- **With nothing to do, this phase leaves no commit.**  The outline says that there is then no pull request and that phase 26 records the counts.  Phase 26 counts again, so nothing has to pass from this phase to that one but the report.
- **The content map is not edited.**  The outline takes the new tree, and the map stays the record of the plan, as the outline says of it.
