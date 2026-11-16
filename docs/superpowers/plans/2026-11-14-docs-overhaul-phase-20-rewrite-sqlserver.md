# Docs overhaul, phase 20: rewrite-sqlserver - plan

Spec: [`2026-11-12-docs-overhaul-phase-20-rewrite-sqlserver-design.md`](../specs/2026-11-12-docs-overhaul-phase-20-rewrite-sqlserver-design.md).  The rewrite guide, section 10, is the procedure; this plan holds only the order and the decisions.

**Base:** `db3285d4` (`main` after phase 11, #296).

## Tasks

1. **`query-methods.md`.**  Cut each sentence that restates a rule of every database to a restatement and link, by the owner table of the brief, after checking the owner at the base.  Split `#errors-and-rollbacks` into one rule a bullet.  Keep each fact of SQL Server.  Write the four limits in three parts.  Fold a fact said twice on the page into one place: the `NOCOUNT` rule of `#rowcount` and its limit, the `ANSI_NULLS` rule of `#list-parameters` and its limit, the SQLSRC127 rule of `#list-parameters` and `#limit-list-in-an-aggregate`.
2. **`nullability.md`.**  One case to a list item; the long sentences become lists.  The two limits in three parts.
3. **`index.md`, `types.md`, `libraries.md`.**  Plain language, sentence by sentence; tables of facts stay row for row.  The five other limits in three parts.
4. **Ledger.**  `ledger/phase-20.tsv`: `# base`, then one row for each unit, `restated-from` for each cut with its owner in `arg`, and `no owner:` for each fact the owner lacks.  Run `check.sh rewrite 20` and `coverage` until both pass.
5. **Checks.**  The loops and searches of section 10, `tools/build-site.sh`, the repository tests.
6. **Reviews.**  The two Opus content reviewers, then `branch-review:review` to clean, then `./pre-commit-validation.sh --full`.

## Decisions

- **The plan is committed** beside the pages, as `docs/superpowers/AGENTS.md` asks of every plan.  It is the one file outside the guide's list of "Only your files".
- **No fenced block changes**, and none is added: the brief lists none.
