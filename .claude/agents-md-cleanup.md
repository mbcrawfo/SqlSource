# AGENTS.md cleanup rulings

Decisions the AGENTS.md cleanup keeps between runs.  Each keep is a statement to leave as it is.

## Keeps

- `AGENTS.md`, rule 5, "Pin every test database to its version": the full list of the places each image tag lives is the checklist an agent follows when it moves an image; keep it although the test readmes repeat it.
- `.github/AGENTS.md`, "`workflows/coverage-comment.yml` holds a write token": a security rule, kept whole beside the file's own comments.
- `docs/architecture/AGENTS.md`, "A new folder under `src/` gets its row in the same commit": stays here: rule 7 of the root guidelines keeps the root file from naming a child AGENTS.md.
- `docs/architecture/AGENTS.md`, "A page is coupled to every name it gives": guidance for page authors; the root's rule 4 covers the side of a change to code.
