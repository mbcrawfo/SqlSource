# TD-0020 - The `Settings` and `Parsing` folders depend on each other

## Problem

[`SettingValue`](../../src/SqlSource/Settings/SettingValue.cs) in `Settings/` calls `SqlIdentifier.IsUsableName` of `SqlSource.Parsing` to check the name of a namespace, while `Parsing/` uses `Settings/` for the levels, the kinds and the values that a marker gives.  The two folders therefore use each other.

[`src/SqlSource/AGENTS.md`](../../src/SqlSource/AGENTS.md) describes `Settings/` as plain data and readers of values that `Parsing/` and `Generation/` both use, and says that the command-line tool will use it too.  A tool that takes `Settings/` takes `SqlIdentifier` with it.

## Why it exists

What makes a name an identifier is one rule, and `SqlIdentifier` is where it lives.  `SettingValue` needed the same rule for a namespace, and `SqlIdentifier` was already there to use.

## Impact

None today: both folders are in one assembly and nothing is built from `Settings/` alone.  It matters when the tool is added, which takes `Settings/` and would take `SqlIdentifier` with it, and in the reading of the code, where the direction of the dependencies is not what the folders suggest.

## Proposed fix

Move `SqlIdentifier` to a place both folders may use, for example a `Text/` folder or `Settings/` itself, and let `Parsing/` use it from there; then `Settings/` uses nothing from `Parsing/`, and the section of `src/SqlSource/AGENTS.md` can say so without a qualification.

## Trigger

The command-line tool starts to use `Settings/`.  Another type of `Settings/` needs something from `Parsing/`.
