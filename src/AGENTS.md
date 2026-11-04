# AGENTS.md - src

- **A switch over an enum names every member and ends in a discard, or a `default:` of its own, that throws `UnexpectedEnumValueException.Of(value)`**, so a new member, a `DatabaseEngine` above all ([adding an engine](../docs/architecture/generator.md#adding-a-database-engine)), fails the build at each switch: IDE0072 and IDE0010 report an unnamed member even beside a discard.  A `[Flags]` enum is tested by bit, and an enum of the compiler or the .NET runtime, which a newer one may extend, keeps a fallback.
- **A value of each engine goes in a `PerEngine<T>`, never in fields named for engines.**  A value every engine must decide is built by `PerEngine.Of` with a switch, so a new engine is IDE0072 there; `default`, `Only` and `With` leave the other slots `default(T)`, only where that means "says nothing".  Values whose type differs by engine keep named fields, each question asked of them a switch ([adding an engine](../docs/architecture/generator.md#adding-a-database-engine)).
- **No bool names an engine**: pass the `DatabaseEngine` and switch over it.  A bool may name one driver's or library's capability, as `MethodDrivers.PostgresAtLeast10` does.

> Maintenance: this file names specific files, types and members.  If you rename, move, or remove them, update this file in the same commit.
