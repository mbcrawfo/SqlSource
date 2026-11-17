# sqlfluff-templater-sqlsource

- `tests/corpus/` is written by `TemplatingCorpusTests` in C#; never edit it by hand, regenerate it there.
- Each module mirrors the C# file its docstring names; a change to that file changes the module.
- Check with `uv run --locked ruff check`, `uv run --locked ruff format` and `uv run --locked pytest`, which `pre-commit-validation.sh` and `format.sh` also run; `--locked` fails on a stale `uv.lock` instead of rewriting it, so run `uv lock` after a change to `pyproject.toml`'s dependencies.
- **CI also tests the lowest sqlfluff the project allows, 4.5.0, which `pre-commit-validation.sh` does not**, so after a change here run it too, from this folder; a floor that fails is raised in every place that `.github/AGENTS.md` lists:

  ```bash
  uv run --isolated --no-project --with-editable . --with sqlfluff==4.5.0 --with pytest==9.2.0 pytest
  ```

- Never edit `version` in `pyproject.toml`; CI sets it when it publishes.

> Maintenance: this file names specific files and commands.  If you rename, move, or remove them, update this file in the same commit.
