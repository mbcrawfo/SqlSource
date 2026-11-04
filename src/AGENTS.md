# AGENTS.md - src

- **A switch over an enum names every member and ends in a discard, or a `default:` of its own, that throws `UnexpectedEnumValueException.Of(value)`**, so a new member, a `DatabaseEngine` above all ([adding an engine](../docs/architecture/generator.md#adding-a-database-engine)), fails the build at each switch: IDE0072 and IDE0010 report an unnamed member even beside a discard.  A `[Flags]` enum is tested by bit, and an enum of the compiler or the .NET runtime, which a newer one may extend, keeps a fallback.

> Maintenance: this file names specific files, types and members.  If you rename, move, or remove them, update this file in the same commit.
