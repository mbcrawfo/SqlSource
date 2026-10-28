# AGENTS.md - src/SqlSource/Generation/Models

- **`ModelCollector.FindDifference` compares every member of a model that changes the C# it emits, and not the documentation.**  A member it misses lets two queries that differ share one model.  `HasArrayColumn` is the one exception, on purpose: it adds a constructor and no property, so it is OR-ed over the queries that share a model, and a query whose column is an array shares a model with one whose column a type override gives the same C# array type.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
