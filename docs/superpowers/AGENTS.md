# AGENTS.md - docs/superpowers

Design and planning artifacts produced by the Superpowers brainstorming and writing-plans workflow, named `YYYY-MM-DD-<topic>`: `specs/` holds the validated designs (`*-design.md`) and their appendices, and `plans/` the implementation plans derived from them.

## Intent

- Always commit them alongside the work in the same PR.
- **Expect drift**.  These files record the intent behind a change when it was planned, not what shipped: problems discovered during implementation routinely change the approach, and the files are deliberately *not* updated to match the finished code.  The code is the source of truth; do not rewrite a spec to match the implementation, and do not treat a spec/code mismatch as a defect.
- Exception: an epic outline (`*-epic-design.md`) coordinates several phases and is kept current until its epic closes.  A phase that changes the plan updates the outline in the same PR.
- A spec committed by itself with no accompanying implementation is intended for future work.  Review it thoroughly - placeholders and TODOs, internal contradictions, ambiguous requirements, and whether the scope is right for a single implementation plan.
- A PR that includes a plan without an implementation should be treated with suspicion.  Plans carry detailed information about the current state of the code and will quickly become stale if not implemented immediately.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
