# TD-0096 - No check keeps a catch-all arm out of a switch over an enum

## Problem

Every switch expression over an enum in [`src/SqlSource`](../../src/SqlSource) names each member and has no `_ =>` arm, so that a new member of an enum, a `DatabaseEngine` above all, fails the build with CS8509 at each switch that must decide something for it.  To allow that, the `[src/SqlSource/**.cs]` section of [`.editorconfig`](../../.editorconfig) turns off CS8524 and IDE0072, which ask every such switch for a discard arm.

No rule of the compiler or the analyzers asks for the opposite.  IDE0072 wants every member named and a discard too, and CS8509 fires only when a switch has no discard.  So a `_ =>` arm written back into a switch over an enum builds, and the next member added to that enum falls into it without a word.  Only the rule in [`src/SqlSource/AGENTS.md`](../../src/SqlSource/AGENTS.md) stands against it, and only an agent reads that.

[`src/SqlSource.Tool`](../../src/SqlSource.Tool) still has IDE0072 on, so a new member fails its build at each switch expression over the enum, but those switches keep a discard that a value the code never makes can reach, and the two projects then follow two rules.  Its 65 `_ =>` and `default:` arms have not been sorted into those over enums and those over strings and types, which cannot name every value.  Switch statements over an enum are guarded in both projects by IDE0010, which asks for each member a statement does not name.

## Why it exists

The closed set of engines, PR 2 of the [query-method emission refactor](../superpowers/specs/2026-10-30-query-method-emission-refactor-design.md), turned the two rules off to give every engine decision a compile-time check.  Enforcing that no switch over an enum has a discard needs an analyzer of the repository's own, which touches the build of every project, so the owner chose to record it here rather than grow that refactor.

## Impact

Low today: no switch over an enum in `src/SqlSource` has a discard.  As the code grows, one written back by someone who has not read the rule makes the next member of its enum silently take the discard's behaviour, which is how a new engine would behave as another.  The tool's database code, which a later cleanup is expected to reorganise by engine, follows the other rule until then.

## Proposed fix

Add an analyzer project to the repository, referenced by `src/SqlSource` and `src/SqlSource.Tool` as an analyzer only (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`), and never packed.  It reports an error at a switch expression or statement whose governing type is an enum and that has a discard arm or a `default:` label, unless the enum is `[Flags]`.  Give it tests, and a line in `CONTRIBUTING.md`.  Then:

- Turn IDE0072 off for `src/SqlSource.Tool` as for `src/SqlSource`, drop the discards of its switch expressions over enums, and sort its `default:` labels the same way.
- Replace the rule in `src/SqlSource/AGENTS.md` with a pointer to the analyzer, update the `.editorconfig` comment and `docs/architecture/generator.md`'s "Extending query methods", and resolve this item.

## Trigger

The cleanup of the `sqlsource` tool's code by database engine, or the next engine.
