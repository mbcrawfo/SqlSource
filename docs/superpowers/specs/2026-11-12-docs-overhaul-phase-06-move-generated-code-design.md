# Docs overhaul, phase 06: move-generated-code

The outline is [`2026-11-12-docs-overhaul-epic-design.md`](2026-11-12-docs-overhaul-epic-design.md), and the content map is [`2026-11-12-docs-overhaul/content-map.md`](2026-11-12-docs-overhaul/content-map.md).  Both are read in full before this brief, and the outline wins where they differ.
Done by: an Opus agent.  Needs: phases 0 to 5.  Runs alone: phases 0 to 11 run one after another.

## Goal

`site/generator.md` is gone.  Its text, and the sections of `site/settings.md` on models, members, collections, interfaces and exceptions, stand word for word on ten pages of `site/reference/`: `members.md`, `models.md`, `model-properties.md`, `query-methods.md`, `return-types.md`, `repositories.md`, `connection-methods.md`, `collection-methods.md`, `exceptions.md` and `attributes.md`.  Every link, in the repository and in the two readmes, names the new page.  No fact is lost and no word of a moved unit changes, but for one corrected sentence and the words that the move makes wrong.

## Where things stand

Checked at the baseline, `28e20e3c`.  Read a baseline page with `git show 28e20e3c:<path>`.

- `site/generator.md`: 1,190 lines, 18,751 words, 46 `<a id>` lines (45 kept, and `known-issues`, which retires).  Its ranges in the map's table, "Map: `site/generator.md`", match the headings of the file line for line.
- `site/settings.md`: 1,087 lines, 20,330 words, 31 `<a id>` lines.  This phase takes 12 of them.
- Links at the baseline: 232 from other pages of `site/` to `generator.md`, 114 inside it, 79 from it to `settings.md` and 55 back.  Outside `site/`: 32 references to `site/generator.md` in 7 files, and 15 links to `generator.html` in `README.md`.
- Phases 0 to 5 leave this.  The tests read `site/` in folders, and `InboundSiteReferenceTests` checks every reference to a site page from any tracked file.  The inventory, the ledger and the checker exist.  `site/reference/` holds `sql-files.md`, `parameters.md`, `tokens.md`, `dialects.md`, `errors.md`, `cli.md`, `describing.md`, `connections.md`, `sidecars.md`, `bundles.md`, `msbuild.md` and the folder `databases/`.  `site/guides/troubleshooting.md` exists, with its six h2 headings.  `site/AGENTS.md` opens with the table "Where a new thing goes", whose rows name today's page until the new page exists.
- `SiteStructureTests` holds the list of the pages not yet moved: `concepts.md`, `generator.md`, `recipes.md` and `settings.md` when this phase starts.  Each stands in the sidebar it will join, as `../<page>.md`.

**The cut of `site/settings.md`**, in lines of the baseline.  A range starts at the `<a id>` line of its first heading, two lines above the heading, and ends at the blank line before the next range.

| Phase | Lines | Sections |
|----|----|----|
| 6 | 255-394 | The four model settings, "Collection type", "Streaming rows" |
| 6 | 570-620 | "Column types" |
| 6 | 714-819 | "Where the members go", "Visibility", "Name of the nested class" |
| 6 | 912-956 | "Public exceptions", "Repository interfaces" |
| 7 | 1-254, 395-569, 621-713, 820-911, 957-1087 | All the rest, with "Known issues" |

Between phases 6 and 7, `site/settings.md` stays at the root of `site/`, with the sections of phase 7 in today's order.  It stays in the Settings group of `site/reference/toc.yml` as `../settings.md`, and in the list of `SiteStructureTests`.  "The JSON opt-in" (h3, line 623) then follows "A class in a JSON column" and so stands under "Type overrides" for one phase; its words do not change.

## In scope / Not in scope

In scope:

- The rows of the map's "Map: `site/generator.md`", all of them, and the rows of "Map: `site/settings.md`" for the lines of phase 6 above.
- The 8 limits of `generator.md` that the map gives phase 6, and the second text of MP-09.
- The pages this phase makes for a later phase: `reference/attributes.md`, `reference/type-mapping.md`, `guides/dapper.md` (the map's "Units that land apart from the rest of their page").
- Correction C-02 at `settings.md` 953.
- Every link to `generator.md`, to `generator.html`, and to the 12 anchors of `settings.md` that move.

Not in scope:

- Any other line of `settings.md`.  Phase 7 moves it, writes the matrix and makes C-01 and the rest of C-02.
- A change of words in a moved unit: a heading reworded, a table reordered, a merge.  Phases 16 and 17 do that.  The one exception is the words that the move makes wrong (step 7).
- The terms of `concepts.md` (phase 8), the walkthrough of `guides/dapper.md` (phases 8 and 9), `#what-the-package-adds` (phase 8).
- `docs/architecture/generator.md`.  It is another file of the same name, and nothing in it changes but its links to the site.

## The work

How a phase is done, the ledger, the reviews and publishing are in the outline's "Delivery".  The steps below are what is specific to this phase.

### 1. Read what changed since the baseline

Run `git diff 28e20e3c -- site/generator.md site/settings.md`.  Each difference is a row of an earlier phase's ledger (a status sentence of phase 1, a sentence that phase 2 placed in `#converters`, a link that phases 3 to 5 retargeted) or text of feature work, which this phase adds to the inventory for the ranges it moves.  Line numbers in this brief are the baseline's; in the working tree find a section by its `<a id>`.

### 2. Make the pages

Each page has an h1 that is its sidebar title in the map's section 1, in the form of the pages that phases 3 to 5 made.  A heading that the map's section 3 lists as h2 is `##` on the new page, whatever its level today; a heading listed after "with" is `###`.  The words of a heading do not change.  `g` is `generator.md` and `s` is `settings.md`.

| Page of `site/` | Sections in order: anchor (lines) |
|----|----|
| `reference/members.md` | New h2 "What the generator writes", `generator` (g 3-32); `locations` (g 81-116) with `placement-for-a-file` (g 120-156) and `setting-member-location` (s 716-755); `setting-visibility` (s 759-781); `setting-nested-class` (s 785-818); `constants` (g 160-175); `token-methods` (g 179-188); `intellisense` (g 192-231) |
| `reference/models.md` | `models` (g 235-268); `which-models` (g 272-287); `model-names` (g 291-306) with `setting-model-suffix` (s 257-273) and `setting-model-name` (s 317-331); `model-namespace` (g 310-312) with `setting-model-namespace` (s 277-294); `model-accessibility` (g 316-322); `model-shape` (g 326-352) with `setting-model-type` (s 298-313); `model-documentation` (g 454-460); `table-models` (g 464-492); `shared-models` (g 496-514); `unusable-model-names` (g 518-527) |
| `reference/model-properties.md` | One new sentence under the h1; `property-order` (g 356-360); `property-names` (g 364-381); `model-nullability` (g 413-426) with `column-marker` (g 430-450); `setting-column-types` (s 572-619) |
| `reference/query-methods.md` | `query-methods` (g 531-556); `which-methods` (g 560-571); `synchronous-methods` (g 939-1000); `reading-rows` (g 1058-1066); `method-parameters` (g 1070-1076); `limits` with `limit-wrapped-connection` (g 1186) |
| `reference/return-types.md` | `method-shapes` (g 832-852); `setting-collection-type` (s 335-362); `streaming-rows` (s 366-393) |
| `reference/repositories.md` | `repository-methods` (g 575-613); `repository-signature` (g 617-639); `repository-connection` (g 643-704); `repository-interface` (g 708-731) with `repository-interfaces` (s 930-955); `repository-transactions` (g 735-739); `synchronous-repository-methods` (g 1004-1054) |
| `reference/connection-methods.md` | `methods-that-take-a-connection` (g 743-774); `method-location` (g 778-791); `method-signature` (g 795-820); `method-connection` (g 824-828) |
| `reference/collection-methods.md` | `collection-methods` (g 856-902); `collection-transactions` (g 906-926); `collection-failures` (g 930-935); `limits` with `limit-wrapped-connection` (g 1187) |
| `reference/exceptions.md` | `unexpected-row-count` (g 1080-1097); `transaction-rolled-back` (g 1101-1121); `collection-item-exception` (g 1125-1144); `public-exceptions` (s 914-926); `projects-that-share-internals` (g 1148-1176); `limits` with `limit-caught-by-type` (g 1184) and `limit-cref` (g 1185) |
| `reference/attributes.md`, in the Settings group | New h2 "The three attributes", `attributes`; `attribute` (g 36-57); `files-of-a-type` (g 61-77); `limits` with `limit-source-written-as-text` (g 1182) and `limit-embedded-attribute` (g 1190).  Phase 7 adds `setting-source` before `limits`, and phase 8 `what-the-package-adds` |
| `reference/type-mapping.md`, in the Types group | `property-types` (g 385-409), and nothing else.  Phase 7 fills the page |
| `guides/dapper.md` | `limits` with `limit-parameter-names` (g 1183), and nothing else.  Phases 8 and 9 fill the page |
| `reference/databases/postgresql/query-methods.md`, which exists | g 1188, as a second text under `limit-connection-string-repository` |
| `reference/databases/sqlserver/query-methods.md`, which exists | A new h3, `limit-rowcount-after-nocount` (g 1189), under its `## Limits` |

Totals: 45 anchors of `generator.md` and 12 of `settings.md` keep their names; 15 `<a id>` lines are new (`generator`, `attributes`, five `limits`, eight `limit-` lines).  The words moved are in the map's section 10, "Phase 6": about 24,000.

### 3. Place the merge pairs

Both texts of a pair go on the page untouched, in the order of the map's table "Merge pairs".  The first text's heading is the pair's heading.  The second text keeps its own heading, one level below, and its own `<a id>` line above that heading.  So each anchor still leads to its own text, and no heading is lost in a move.  For each pair, write `merges/MP-<nn>.md` in the form of the phase 0 brief, section 7, which `merges/MP-08.md` of phase 3 shows: both texts with their baseline file and lines, the anchor each stands under now, the conflicts of the map's row, and the source files of the map's row.  The merged text is left empty for the rewrite phase.  Each worksheet says that the merge removes the second heading and lifts its `<a id>` line to stand above the pair's heading.

| Pair | On | Texts, in order | Worksheet |
|----|----|----|----|
| MP-01 | `reference/members.md` | g 81-116, g 120-156, s 716-755.  Two headings read "Where the members go": the h2 with `locations`, and the h3 with `setting-member-location`, after the h3 `placement-for-a-file` | New, complete |
| MP-02 | `reference/models.md` | g 291-306, s 257-273, s 317-331 | New, complete |
| MP-03 | `reference/models.md` | g 310-312, s 277-294 | New, complete |
| MP-04 | `reference/models.md` | g 326-352, s 298-313 | New, complete |
| MP-05 | `reference/repositories.md` | g 708-731, s 930-955 with line 953 corrected | New, complete; it names C-02 |
| MP-07 | `reference/attributes.md` | g 61-77 under `files-of-a-type`.  Phase 7 adds s 822-847 as a second h2 | New, with the first text |
| MP-09 | `reference/databases/postgresql/query-methods.md` | g 1188 after the text of `postgresql.md` 528, which phase 4 placed | Add to it, or make it if phase 4 did not |
| MP-10 | Four pages | The openings that phase 8 pairs with a term: g 3 (`members.md#generator`), g 179-188 (`#token-methods`), g 531-556 (`query-methods.md#query-methods`), g 575-613 (`repositories.md#repository-methods`) | Add the four openings |
| MP-12 | `guides/dapper.md` | g 1183 under `limit-parameter-names` | New, with this text |

### 4. Place the limits

The map's section "The 46 known-issue bullets become 36 limits" gives the rule and the words of what the reader sees.  Each bullet goes under `## Limits` (`limits`) of its page, as an h3 with its anchor, in the form phases 3 to 5 used; the bold sentence that opens the bullet stays.  The h3's words are new text.  Each limit gets one entry in `guides/troubleshooting.md`, one or two sentences and a link to the limit's anchor.

| Limit | Bullet | Entry under, in `troubleshooting.md` |
|----|----|----|
| `reference/attributes.md#limit-source-written-as-text` | g 1182 | `not-described` |
| `guides/dapper.md#limit-parameter-names` | g 1183 | `wrong-result` |
| `reference/exceptions.md#limit-caught-by-type` | g 1184 | `at-run-time` |
| `reference/exceptions.md#limit-cref` | g 1185 | `wrong-model`, as the nearest heading for a compiler warning |
| `reference/query-methods.md#limit-wrapped-connection` | g 1186 | `at-run-time` |
| `reference/collection-methods.md#limit-wrapped-connection` | g 1187 | `at-run-time` |
| `reference/databases/sqlserver/query-methods.md#limit-rowcount-after-nocount` | g 1189 | `wrong-result` |
| `reference/attributes.md#limit-embedded-attribute` | g 1190 | `wrong-model`, for the same reason |

`generator.md` 1188 is no new limit: it joins the limit that phase 4 made, and that limit's entry stays as it is.  The heading `## Known issues` (g 1180) goes, and its anchor retires.

### 5. Make the correction

- **C-02, at `settings.md` 953.**  "The two properties of the attribute are not settings either: they belong to the type." becomes "The two properties of the attribute cannot be written in a settings file or a marker: they belong to the type."  The first sentence of the bullet, and line 924, "not a setting of a settings file", are true and stay.
- **C-01 has no sentence in this phase.**  `generator.md` 687, "Several databases", is the true statement and moves as it is.  The false sentence is `settings.md` 153, which phase 7 carries.

### 6. Write the new text

In plain language, by the outline's rule for authors.  Each piece gets the accuracy review.

- The h1 of each of the 12 new pages.
- `reference/members.md`: the heading "What the generator writes" and `generator`.
- `reference/attributes.md`: the heading "The three attributes", `attributes`, and a table of three columns: attribute, property, section.  Its rows: `[SqlSourceGenerate]` with `Source`, `GenerateInterface` and `InterfaceName`; `[SqlSourceConnection]` with `Database`; `[SqlSourceConverter]`, which has no property.  The links: `#attribute`; `../settings.md#setting-source` until phase 7; `repositories.md#repository-interfaces`; `repositories.md#repository-connection`; `../settings.md#converters` until phase 7.  The table states no value and no default.
- `reference/model-properties.md`: one sentence under the h1 that links `type-mapping.md#property-types` for the order in which a C# type is chosen.
- Five `## Limits` headings, eight limit headings, eight entries of `guides/troubleshooting.md`.

### 7. Retarget the links

The rule is the map's section 6: an anchor keeps its name and only the page changes, so every link is retargeted by page and anchor together, never by anchor alone.  A link to a section that phase 7 moves names `../settings.md#<anchor>` from a page of `reference/`.  The same holds for `concepts.md` and `recipes.md`, which are still at the root.

Links whose anchor changes (the map's 6.1, 6.2 and 6.4):

| Link | New target |
|----|----|
| g 38, `#known-issues` | `attributes.md#limit-embedded-attribute` |
| g 352, `settings.md#known-issues` | `../settings.md#known-issues`, from `reference/models.md`.  Phase 7 makes the limit and retargets it |
| g 702 | `databases/postgresql/query-methods.md#limit-connection-string-repository`.  Phase 4 gave it this target, so here it is re-based alone |
| g 850 | `databases/sqlserver/query-methods.md#limit-rowcount-after-nocount` |
| g 1097, 1121, 1144 | `exceptions.md#limit-caught-by-type` |
| `diagnostics.md`, the `See:` line of SQLSRC208 | `reference/attributes.md#limit-source-written-as-text` |
| `tool.md` 48, now in `reference/describing.md` | `attributes.md#limit-source-written-as-text` |
| `postgresql.md` 493; `sqlserver.md` 312 and 488, now under `reference/databases/` | `reference/query-methods.md#limit-wrapped-connection` |
| `sqlserver.md` 485, now in `reference/databases/sqlserver/query-methods.md` | `#limit-rowcount-after-nocount`, on its own page |
| `recipes.md` 410 | No change: phase 4 gave it `reference/databases/postgresql/query-methods.md#limit-connection-string-repository` |
| `recipes.md` 702 | `guides/dapper.md#limit-parameter-names` |
| `guides/troubleshooting.md`, the sentence that links the known issues of the pages not yet moved | Its link to `../generator.md#known-issues` goes.  The sentence then links `../settings.md#known-issues` alone |
| `README.md`, two links to `generator.html#known-issues` | `reference/query-methods.html#limit-wrapped-connection` |
| Bare `generator.md`: `parameters-and-tokens.md` 177, now in `reference/tokens.md` | `members.md#token-methods` |
| Bare `generator.md`: `concepts.md` 127; the link of `index.md` that came from `site/README.md` 114 | `reference/members.md#generator`; `reference/members.md` |

**Words that the move makes wrong.**  Seven link texts of the moved ranges of `settings.md` say "the generator page" (lines 273, 294, 313, 617, 755, 776 and 818).  So do four units outside those ranges: `parameters-and-tokens.md` 177, now on `reference/tokens.md`; `postgresql.md` 433 and `sqlserver.md` 421, now under `reference/databases/`; and `settings.md` 847, which waits at the root for phase 7.  Two more units say it in plain words, with no link: `postgresql.md` 397 and `sqlserver.md` 386, "by the rules of the generator page", each now on the `index.md` of its database's folder.  No such page exists after this phase, so all thirteen are mended here.  A section that leaves its parent can also say "above" or "below" of text that is now on another page: g 1187 says "as for the query method above" of g 1186, which is now on `reference/query-methods.md`.  Find them with `git grep -n -i "generator page" -- site` and `grep -n -i -E "this page|above|below" site/reference/*.md site/guides/dapper.md`.  Change the fewest words that make each phrase true: the page's name of today in place of "the generator page", or "this page" where the link now stays on its own page.  Give the unit's row the note `position`, and change no other word.  "The recipe", in six link texts of `generator.md`, stays: it names a section, which keeps its text until phase 8, and not a page.

**Interim links this phase retargets:**

| Link | Left by | Parked at | Becomes |
|----|----|----|----|
| `postgresql.md` 493; `sqlserver.md` 312 and 488 | Phase 4 | `../../../generator.md#known-issues` | `../../query-methods.md#limit-wrapped-connection` |
| `sqlserver.md` 485 | Phase 4 | `../../../generator.md#known-issues` | `#limit-rowcount-after-nocount`, on its own page |
| `tool.md` 48, on `reference/describing.md` | Phase 5 | `../generator.md#known-issues` | `attributes.md#limit-source-written-as-text` |
| `parameters-and-tokens.md` 177, on `reference/tokens.md` | Phase 3 | `../generator.md#token-methods` | `members.md#token-methods` |
| One link of the one sentence of `guides/troubleshooting.md` | Phase 3 | `../generator.md#known-issues` | It goes |

**Interim links this phase leaves:**

| Link | Now on | Parked at | The map's target | Phase |
|----|----|----|----|----|
| g 352 | `reference/models.md` | `../settings.md#known-issues` | `R/type-mapping.md#limit-nullable-type-argument` | 7 |
| Two links of the new table of `#attributes` | `reference/attributes.md` | `../settings.md#setting-source`, `../settings.md#converters` | `#setting-source`, on its own page; `R/converters.md#converters` | 7 |
| `docs/architecture/sidecar-format.md` 52, the slug `#type-overrides` | The same file | `site/settings.md#type-overrides` | `site/reference/type-mapping.md#setting-type-overrides` | 7 |
| `concepts.md` 127 | `concepts.md`, at the root | `reference/members.md#generator` | None: the closing sentence goes | 8 |
| The link of `index.md` that came from `site/README.md` 114 | `index.md` | `reference/members.md` | None: the landing page is written new | 10 |

Find the rest by search, and retarget each by its anchor:

- Inside `site/`: `git grep -n -E "generator\.md" -- site`, and `git grep -n -E "settings\.md#(setting-model-(suffix|namespace|type|name)|setting-collection-type|streaming-rows|setting-column-types|setting-member-location|setting-visibility|setting-nested-class|public-exceptions|repository-interfaces)" -- site`.  `diagnostics.md` alone held 51 and 80 links to the two pages at the baseline.
- In `README.md`: 15 links to `generator.html` and 4 to `settings.html` (`#repository-interfaces`, `#setting-model-namespace`, `#setting-column-types` twice).  The form is `https://mbcrawfo.github.io/SqlSource/reference/<page>.html#<anchor>`.  `src/SqlSource.Tool/README.md` has none.
- Elsewhere: `git grep -n -E "site/(generator|settings)\.md" -- ':!site' ':!docs/superpowers'`.  For this phase:

| File | What changes |
|----|----|
| `docs/architecture/generator.md` 3, 316, 570, 576, 607 | `site/reference/members.md`; `models.md#models`; `members.md#constants`; `members.md#locations`; `query-methods.md#query-methods` |
| `docs/architecture/sidecar-format.md` 52 | `site/settings.md#column-types` is the slug of the heading, not its `<a id>`: it becomes `site/reference/model-properties.md#setting-column-types`.  Its neighbour, `#type-overrides`, is phase 7's |
| `docs/deferred/D-0002-describing-call-and-data-definition.md` 9 | `site/reference/query-methods.md#which-methods` |
| `examples/PostgresExamples/README.md`, `examples/SqlServerExamples/README.md` | Eleven links to `generator.md` each, and the links to the 12 anchors of `settings.md`: by anchor.  The bare link of lines 102 and 107 becomes `site/reference/members.md` |
| `examples/*/Methods/OrderLineQueries.cs` 12 | `site/reference/query-methods.md, "Query methods"` |
| `examples/*/Methods/OrderQueries.cs` 12 | `site/reference/members.md#setting-member-location`.  Line 13 is phase 7's |
| `tests/DatabaseTests/SqlServer.SyncAsync/KnownDifferences.cs` 12 and 22 | `site/reference/connection-methods.md`, in the comment and in the message.  The quoted words, "Give the method the transaction", stay: they move word for word with g 828 |

No `AGENTS.md` under `src/`, `tests/` or `examples/` names `site/generator.md`.

### 8. Tests, `AGENTS.md` and the sidebars

- **`SiteStructureTests`**: `generator.md` leaves the list of the pages not yet moved, which is then `concepts.md`, `recipes.md` and `settings.md`.  No test is new.
- **What fails a mistake**: `SiteTocTests`, a new page with no entry; `UserDocsLinksTests` and the docfx build, a dead link or anchor in `site/` or a readme; `InboundSiteReferenceTests`, a reference to `site/generator.md` anywhere; `SiteStructureTests`, a page with `## Limits` that `guides/troubleshooting.md` does not link; `UserDocsSamplesTests`, a `sql` block that lost its info string.
- **`site/AGENTS.md`**: the row of the table reads `| Attribute, or a property of one | reference/attributes.md | A reviewer |`, with the page in code font.  The bullet "Pages not yet moved" loses `generator.md`.  Run `grep -n "generator\.md" site/AGENTS.md site/*/AGENTS.md site/reference/databases/AGENTS.md` and rewrite each hit to the page that now holds the thing.  Check the size: `AgentsFileTests` holds 8 KB and 80 words.
- **`site/reference/toc.yml`**: the entry `../generator.md` goes.  The Generated code group gets nine entries, in the map's order and with the map's titles: `members.md`, `models.md`, `model-properties.md`, `query-methods.md`, `return-types.md`, `repositories.md`, `connection-methods.md`, `collection-methods.md`, `exceptions.md`.  `attributes.md` joins the Settings group, after `../settings.md`.  `type-mapping.md` is the one entry of a group "Types", after "Generated code".
- **`site/guides/toc.yml`**: `dapper.md`, under "Other ways to use your SQL".

## The ledger

`ledger/phase-06.tsv` has a row for every unit of the baseline `generator.md`, and for every unit of `settings.md` in the four ranges of phase 6.  "check coverage" must pass with them.

- `move`: every unit that no line below names.
- `move-split`: g 1, the h1, whose words go; g 1180, the heading "Known issues".
- `merge-pair`: the units of the table in step 3, each with its pair's id.
- `corrected`: the bullet of s 953, with C-02 and both texts.
- `new`: each piece of step 6, with its page.
- In the note column: `level` for a heading whose level changed; `position`, with the old and the new words, for a unit whose words step 7 mended.

"check move 06" finds every `move` and `merge-pair` unit at its new place.  What it prints as left over must be exactly the `move-split` and `corrected` rows and the rows with the note `position`.

## Done when

- [ ] `test ! -e site/generator.md` succeeds.
- [ ] `git grep -n -E "generator\.(md|html)" -- site README.md src/SqlSource.Tool/README.md` prints nothing.
- [ ] `git grep -n -E "site/generator\.md" -- ':!docs/superpowers'` prints nothing.
- [ ] `git grep -n "known-issues" -- 'site/*.md'` prints only the anchor of `site/settings.md` and links to it: one in `site/settings.md`, one in `site/reference/models.md`, one in `site/reference/sidecars.md` and one in `site/guides/troubleshooting.md`.
- [ ] `git grep -n -i "generator page" -- site` prints nothing.
- [ ] `grep -c "<a id=" site/settings.md` prints 19, and each of the 12 moved anchors is gone from it.
- [ ] This prints nothing: `for f in $(git ls-files 'site/*.md'); do grep -o '<a id="[^"]*"' "$f" | sort | uniq -d | sed "s|^|$f: |"; done`.
- [ ] For each page and anchor of the table in step 2, `grep -c '<a id="<anchor>"></a>' site/<page>` prints 1.
- [ ] `grep -c "not settings either" site/reference/repositories.md` prints 0, and `grep -c "cannot be written in a settings file or a marker" site/reference/repositories.md` prints 1.
- [ ] `grep -rn "Give the method the transaction" site tests/DatabaseTests/SqlServer.SyncAsync` finds the bold words in `site/reference/connection-methods.md` and both lines of `KnownDifferences.cs`, which name that page.
- [ ] `ls docs/superpowers/specs/2026-11-12-docs-overhaul/merges/` lists `MP-01.md` to `MP-05.md`, `MP-07.md`, `MP-09.md`, `MP-10.md` and `MP-12.md`.
- [ ] "check coverage" passes, and "check move 06" leaves over only the rows the ledger names.
- [ ] `tools/build-site.sh` ends with no docfx warning.
- [ ] `./pre-commit-validation.sh --full` passes on the tree as pushed, after a clean `branch-review:review`.
- [ ] The pull request says that the site is published by hand after the merge (the outline's "Publishing").

## For the reviewers

The no-loss reviewer, beyond the general kit:

- **Bullets that hold a fenced block**: the two of g 643-704, whose fences are at lines 674 and 689.  The block's indent must be the bullet's, or the list breaks and docfx still builds.
- **Sections that leave their parent**: g 1004-1054 moves away from "Synchronous methods"; g 385-409 leaves the models for `type-mapping.md`; s 759-781, "Visibility", becomes an h2.  Read each for a sentence that says "above" or "below" of text that is now on another page: each must be mended, with the note `position`.
- **The nine bullets**: each whole, with its bold opening sentence, and g 1188 after phase 4's text, not in place of it.  The last sentence of g 1187 says "as for the query method above", and that bullet, g 1186, is now on `reference/query-methods.md`: the words are mended, with the note `position`.
- **Each unit with the note `position`**: the fewest words changed, the phrase is now true, and the link under the words goes where they say.
- **The two limits named `limit-wrapped-connection`**: every link to the query method's limit (from the database pages and the README) goes to `reference/query-methods.md`, and none to `reference/collection-methods.md`.
- **The 114 links that were inside `generator.md`**: a sample of 20, each opened on the built site.
- **`settings.md` after the cut**: the 19 anchors of phase 7 and their text are untouched; `git diff` of the file shows only removed ranges and retargeted links.

The accuracy reviewer:

- **The table of `#attributes`** against `src/SqlSource/Generation/AttributeSource.cs`, lines 68 to 127 at the baseline: three attributes; `Database` on `SqlSourceConnectionAttribute`; `Source`, `GenerateInterface` and `InterfaceName` on `SqlSourceGenerateAttribute`; no property on `SqlSourceConverterAttribute`.
- **C-02** against `src/SqlSource/Generation/Repositories/RepositoryInterfaces.cs` and lines 47 to 55 of `src/SqlSource/build/SqlSource.props`: the project's property and the attribute's two properties are the only places.
- **Each limit's heading and each entry of `troubleshooting.md`** against its bullet: the symptom named is the one the bullet gives, and the entry promises no fix the bullet lacks.
- **The sentence of `model-properties.md`** against `generator.md` 385-409: it says only that the order is there.

## Traps

- **Seventeen anchor ids stand on more than one page at the baseline.**  Four are on `generator.md` and on both database pages: `models`, `query-methods`, `method-parameters`, `collection-methods`.  Phase 4 put the database pages' copies on `databases/*/index.md` and `databases/*/query-methods.md`.  No two of them meet on one page in this phase.  A scripted rewrite that keys on the anchor alone sends a link to the wrong page, so key on page and anchor.  If two equal ids would stand on one page, the placement is wrong: stop and read the map again.
- **`known-issues` is the fifth.**  The anchor of `generator.md` retires here; that of `settings.md` lives one more phase.
- **A link to a heading's slug.**  The tests take a heading's slug as an anchor.  `docs/architecture/sidecar-format.md` 52 uses one.  Use the `<a id>` in every retargeted link, since phase 16 rewords headings.
- **The checker and heading levels.**  The checker compares a heading without its `#` marks, so a heading whose level changed is in no list.  If one is left over, its words changed: put them back.
- **YAML.**  The title "Model properties: names, column types, nullable" holds a colon and a space, so its `name:` is quoted in `toc.yml`.
- **`docs/architecture/generator.md`** is linked from many `AGENTS.md` files as `generator.md#...`.  A search for `generator.md` alone finds them.  They do not change.
- **A fenced block is byte for byte the same.**  The `sql` info strings go with their blocks.
- **`reference/members.md` is about 3,850 words.**  The map allows it, and phase 25 splits it if phase 16 leaves it over 3,500.  Do not split it here.

## Left to the agent

- The words of each new heading, of the table of `#attributes`, of the eight entries of `troubleshooting.md`, and of the sentence of `model-properties.md`.
- The heading of `troubleshooting.md` for an entry, where the earlier phases put a like symptom elsewhere: follow them, and keep the six headings.
- Whether the pages are made by `git mv` and cuts or written new.
- The order of the commits.

## Calls made in this brief

- **A pair's second text keeps its heading, one level down, with its own anchor.**  The outline places the two texts "side by side on the new page, each untouched under its own anchor", and the map's section 3 says the same.  The worksheet tells the rewrite which heading to remove and which `<a id>` line to lift.
- **`settings.md` line 3 is left alone.**  "Every setting is defined here" is not true of twelve sections between phases 6 and 7.  The sentence is C-02's, phase 7 rewrites it, and the table under it links each moved section at its new page.
- **Words that name a page of today are mended in the move**, in the fewest words, each with the note `position`, as the outline rules.  "The generator page" is such a name, in thirteen places, eleven link texts and two phrases in plain words, six of them on pages that this phase does not move.  "The recipe" is not: it names a section that keeps its text, so the six link texts of `generator.md` that say it stay, and phase 8 only retargets them.
- **The h1 of a new page is its sidebar title.**
- **Two groups of one entry** stand for a short time: "Types" in `reference/toc.yml` until phase 7, and "Other ways to use your SQL" in `guides/toc.yml` until phase 8.
- **A compiler warning or error has no heading of its own in `troubleshooting.md`.**  The entries of `limit-cref` and `limit-embedded-attribute` go under `wrong-model`, since the map fixes the six headings.
- **`merges/MP-10.md` gets the four openings now**, so that phase 8 only adds the terms.
