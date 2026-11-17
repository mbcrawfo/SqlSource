# sqlfluff-templater-sqlsource

- `tests/corpus/` is written by `TemplatingCorpusTests` in C#; never edit it by hand, regenerate it there.
- Each module mirrors the C# file its docstring names; a change to that file changes the module.
- Check with `uv run ruff check`, `uv run ruff format` and `uv run pytest`, which `pre-commit-validation.sh` and `format.sh` also run.
- **CI also tests the lowest sqlfluff the project allows, 4.5.0, which `pre-commit-validation.sh` does not**, so after a change here run it too, from this folder; a floor that fails is raised in every place that `.github/AGENTS.md` lists:

  ```bash
  uv run --isolated --no-project --with-editable . --with sqlfluff==4.5.0 --with pytest==9.2.0 pytest
  ```

- Never edit `version` in `pyproject.toml`; CI sets it when it publishes.

> Maintenance: this file names specific files and commands.  If you rename, move, or remove them, update this file in the same commit.
