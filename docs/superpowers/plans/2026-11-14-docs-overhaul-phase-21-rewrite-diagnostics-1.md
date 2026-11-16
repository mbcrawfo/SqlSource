# Docs overhaul, phase 21: rewrite-diagnostics-1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The sections `SQLSRC001` to `SQLSRC121` of `site/diagnostics.md` are in plain language and in the one shape of the diagnostics guide, with every fact of today's sections still there.

**Architecture:** Six slices of the range, each a run of ids rewritten in place, each with its rows appended to `ledger/phase-21.tsv` and checked by `check.sh rewrite 21`.  A last task recounts the range, runs every check of the brief and drafts the pull request's description.

**Tech Stack:** Markdown, docfx (`tools/build-site.sh`), the epic's checker (`docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh`), xUnit tests of `tests/SqlSource.Repository.Tests`.

**Spec:** `docs/superpowers/specs/2026-11-12-docs-overhaul-phase-21-rewrite-diagnostics-1-design.md`.  Read with it, in this order of authority: the outline `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`, the content map, the spec, `docs/superpowers/specs/2026-11-12-docs-overhaul/diagnostics-guide.md`, `docs/superpowers/specs/2026-11-12-docs-overhaul/rewrite-guide.md` (sections 1 to 3, 6, 7, 10 to 12, and the report of 8).

## Where the code differs from the spec's assumptions

- **Size.**  The range is 11,466 words at the base `db3285d4`, not 10,928: phase 2's lines and the move phases' links.  Under the 12,000 of the outline, so one pull request.
- **Phase 2 wrote a second line under "Reported by" in 13 sections**, which the "after" samples of the diagnostics guide do not show: `SQLSRC021` ("The error comes before the build compiles anything."), `SQLSRC024`, `025`, `027` ("The error is at the project file."), `SQLSRC026` (where the warning is, and `MSBuildWarningsAsMessages`), and `SQLSRC028` to `030`, `032` to `034` and `036` ("A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models."), `SQLSRC031` (the warning's form).  They are phase 2's text: keep them word for word directly under the "Reported by" line, and delete from the section's own text a statement they already make (diagnostics guide, the paragraph after "Rules for the parts").
- **The order of the outputs.**  Neither message prints the values of `output`: `SettingsFileDiagnostics.HelpOf` gives `output` an empty end, and `SqlMarkerScope.AddInvalid` gives the `-- output:` marker none.  So both "`output` takes `sql`, `models`, `methods` or `repository`" (`SQLSRC019`) and "`-- output:` takes ..." (`SQLSRC111`) become "`repository`, `methods`, `models` or `sql`", and the two "`sql` or `models`" of `SQLSRC009` become "`models` or `sql`".  No other list of values of the two sections changes its order.

## Global Constraints

- Edit only the lines from `## SQLSRC001` to the line before `## SQLSRC122` of `site/diagnostics.md`, and `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-21.tsv`.  No other file of `site/`, not `site/AGENTS.md`.
- Never change a `## SQLSRCnnn` heading, a bold title line, a "Reported by" line or the phase 2 line under it, the order of the sections, or the bytes or info string of a fenced block.  No line starts with `## ` inside a section; a section holds no heading.
- The shape: title, "Reported by" (and phase 2's line), what happened, (why), (sample of the mistake), `**Fix:**` with numbered steps, or `**Fix:** do one of these.` with bullets for alternatives, (sample of the fix), (notes, each a bullet opening with a bold sentence), (the severity paragraph), `See:` last.
- No fenced block inside a list.  No new fenced block.  No new or removed link target; no `See:` line changes but the three new ones of Task 1 and Task 2.
- Language: one instruction in a sentence; at most 20 words in a step and 25 in a description (a code span is one word); active voice, present tense, no "will"; the condition first; "must" only for what an error holds; a paragraph of at most six sentences.  Keep every limiter word: "not", "only", "never", "unless", "alone", "at most", "at least", "except", "whatever".
- Terms: a type *claims* a file; a value *can be null*; a sidecar entry is *current* or *out of date*; the tool *describes* a query; a *settings file*; a *folder*.  Say "the build" or "the tool", never a bare "is reported" where the actor is known.
- The outputs in a sentence of the section's own: `repository`, `methods`, `models`, `sql`.  A message, a code span that quotes one, or a `console` block keeps its order.
- `SQLSRC019` and `SQLSRC111` keep their lists; never replace a list by a link.  `SQLSRC109` keeps the twelve words of the generator parameters in backticks.  `SQLSRC116` keeps its 17 rows, cell for cell.
- Each section has one ledger row: `site/diagnostics.md:<first>-<last>` (the baseline lines below), `move`, `site/diagnostics.md#sqlsrcnnn`, `-`, `-`, and a note that accounts for every line `check.sh rewrite 21` prints for the section.  A new `See:` line is a `new` row.  A statement found false is a `corrected` row with its source.  A rule that the section states and its `See:` target lacks gets `no owner: <file>#<anchor>; <the fact>` at the start of the note, and the section keeps it.

Baseline lines of each section (`git show 28e20e3c:site/diagnostics.md`):

`001` 347-361, `002` 362-376, `003` 377-384, `004` 385-399, `005` 400-413, `007` 414-437, `008` 438-447, `009` 448-463, `012` 464-480, `013` 481-502, `015` 503-520, `016` 521-537, `017` 538-555, `018` 556-577, `019` 578-626, `020` 627-652, `021` 653-660, `022` 661-674, `023` 675-688, `024` 689-702, `025` 703-716, `026` 717-730, `027` 731-744, `028` 745-772, `029` 773-811, `030` 812-834, `031` 835-868, `032` 869-891, `033` 892-910, `034` 911-929, `035` 930-955, `036` 956-974, `037` 975-993, `038` 994-1007, `101` 1008-1021, `102` 1022-1038, `103` 1039-1055, `104` 1056-1065, `105` 1066-1080, `106` 1081-1097, `107` 1098-1113, `108` 1114-1131, `109` 1132-1145, `110` 1146-1155, `111` 1156-1205, `112` 1206-1233, `113` 1234-1248, `114` 1249-1264, `115` 1265-1291, `116` 1292-1330, `117` 1331-1357, `118` 1358-1372, `119` 1373-1388, `120` 1389-1423, `121` 1424-1457.

## Review Focus

- A value dropped from a list of `SQLSRC016`, `018`, `019`, `109`, `111` or `112` when a sentence becomes bullets: count the code spans of the section before and after.
- A condition list whose join changed: "all of these" against "one of these", above all in `SQLSRC028`, `029`, `032`, `035` and `112`.
- What an error costs, moved to the wrong cause or the wrong half of a split sentence: `SQLSRC007`, `008`, `009`, `013`, `114`, `120`.
- A `sql error` block re-marked, indented into a list or moved past the paragraph it shows.
- A line outside the range touched by the editor: a reflow, a trailing space, a changed line end.

---

### Task 0: The pilot (done)

`SQLSRC104` is rewritten as the diagnostics guide's example, and `ledger/phase-21.tsv` holds its `# base db3285d4adf7542f6e80e96d7f0b8182cd091a9f` line, the header and the row of `SQLSRC104`.

### Tasks 1 to 6: one slice each

| Task | Ids | Words at base | Its own calls |
|----|----|----|----|
| 1 | `001` to `018` | 1,960 | `SQLSRC003` and `012` get `See: [What the build needs](reference/msbuild.md#build-requirements)`, each a `new` row.  `007`: the fix is alternatives.  `008` and `104` stay different.  `009`: "`models` or `sql`" twice.  `013`: two causes, each with its own fix; the `xml` block belongs to the second.  `016`: the 18 keys. |
| 2 | `019` to `021` | 1,382 | `019`: the lists stay, "`output` takes `repository`, `methods`, `models` or `sql`", no other list changes order.  `021` gets the same `See:` line as `003`, a `new` row. |
| 3 | `022` to `027` | 1,252 | `022`: the fix is a procedure in order; the guide link stays in the text, `See:` unchanged.  `026`: do not copy the `NoWarn` paragraph of `031`; keep phase 2's line. |
| 4 | `028` to `038` | 2,498 | `028`: the five bullets of the fix keep each its situation.  `029`: the second `csharp` block shows what is allowed.  `031`: the severity paragraph is last before `See:`.  `035`, `037`: the error costs the interface only.  `038`: the guide link stays. |
| 5 | `101` to `111`, without `104` | 2,033 | `109`: the twelve words.  `111`: twelve bullets and five paragraphs keep every value; the `sql error` block between the third and fourth bullets stands after a list, never inside one; "`-- output:` takes `repository`, `methods`, `models` or `sql`". |
| 6 | `112` to `121` | 2,331 | `112`: ten kinds of conflict, and the last paragraph's list of what is not one.  `116`: the table unchanged.  `120`: three causes as bold-led paragraphs, each with its block, in today's order.  `121`: the severity paragraph and its two blocks last before `See:`. |

**Files:**
- Modify: `site/diagnostics.md`, the slice's sections only.
- Modify: `docs/superpowers/specs/2026-11-12-docs-overhaul/ledger/phase-21.tsv`, append the slice's rows.

- [ ] **Step 1: Count the code spans of each section of the slice** (`grep -o '`[^`]*`'` over the section) and keep the counts.
- [ ] **Step 2: Rewrite each section** to the shape and the language of the constraints, reading the source the spec names for each fact that the rewording touches.
- [ ] **Step 3: Append one ledger row for each section**, and a `new` row for each new `See:` line.
- [ ] **Step 4: Run `docs/superpowers/specs/2026-11-12-docs-overhaul/check/check.sh rewrite 21`**: exit 0, and for each printed difference of the slice a true note in its row.
- [ ] **Step 5: Run the check of lines outside the range** (diagnostics guide, "One file, four phases", with `## SQLSRC001` and `## SQLSRC122`): prints nothing.
- [ ] **Step 6: Run the two searches over the range** (the internals grep of `site/AGENTS.md`, the output-order search of the rewrite guide): each hit is text the build or the tool prints, or a message.
- [ ] **Step 7: Run `./pre-commit-validation.sh`**: passes.
- [ ] **Step 8: Commit** `site/diagnostics.md` and the ledger, "docs: rewrite SQLSRCaaa to SQLSRCbbb in plain language".

### Task 7: Close the phase

- [ ] **Step 1:** Count the range before (at the base) and after, and write both in a `#` comment line of the ledger, under the `# base` line.
- [ ] **Step 2:** Run the "Done when" checks of the spec: 55 headings and 55 `See:` lines over the range; the outside check; `git diff --stat` against the merge base names only `site/diagnostics.md`, the ledger and this plan; `check.sh rewrite 21` and `check.sh coverage` exit 0; `tools/build-site.sh` with no warning; the internals, status and output-order searches; `dotnet test --project tests/SqlSource.Repository.Tests`.
- [ ] **Step 3:** Draft the pull request's description: the goal, the word counts, each `corrected` row with its source, "Facts with no owner" (or "none"), each sentence left as it was because it could not be checked, and lines outside the range that need a fix (reported, not made).
- [ ] **Step 4:** Content reviews (no-loss, accuracy), then `branch-review:review`, then `./pre-commit-validation.sh --full`.
