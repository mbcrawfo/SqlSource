# AGENTS.md - docs/superpowers

Design and planning artifacts produced by the Superpowers brainstorming and writing-plans workflow.  Both folders use `YYYY-MM-DD-<topic>` filenames:

| Folder | Contents |
|----|----|
| `specs/` | Validated designs (`*-design.md`) - the approved approach agreed on before implementation |
| `plans/` | Implementation plans derived from a spec - the step-by-step breakdown of the work |

## Intent

The files capture the *reasoning and intent* behind a change at the time it was planned.  They are not a record of what shipped.

- Always commit them alongside the work in the same PR.
- **Expect drift**.  Problems discovered during implementation routinely change the approach, and these files are deliberately *not* updated to match the finished code.
- The code is the source of truth; the spec and plan are the starting intent.  Do not rewrite a spec to match the implementation, and do not treat a spec/code mismatch as a defect.
- Exception: an epic outline (`*-epic-design.md`) coordinates several phases and is kept current until its epic closes.  A phase that changes the plan updates the outline in the same PR.
- A spec committed by itself with no accompanying implementation is intended for future work.  Review it thoroughly - placeholders and TODOs, internal contradictions, ambiguous requirements, and whether the scope is right for a single implementation plan.
- A PR that includes a plan without an implementation should be treated with suspicion.  Plans carry detailed information about the current state of the code and will quickly become stale if not implemented immediately.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
