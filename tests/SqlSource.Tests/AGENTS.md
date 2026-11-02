# AGENTS.md - tests/SqlSource.Tests

- **A hand-built entry lists its query's parameters as the tool writes them**: each as the SQL spells it, in the order of the SQL, numbered from 0.  `Sidecars.Of` of `tests/SqlSource.Testing/Generator/` fails a current entry that does not; a test of a sidecar a person edited writes it with `Sidecars.HandEdited` and says why in a comment.  A test that hands an entry straight to a step, as `Generation/Types/QueryTyperTests.cs` does, writes its SQL to match.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
