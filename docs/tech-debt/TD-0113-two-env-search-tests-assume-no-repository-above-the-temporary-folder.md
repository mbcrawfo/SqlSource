# TD-0113 - Two `.env` search tests assume no repository above the temporary folder

## Problem

Found by the maintainability review of 2026-11-06.  [`DotEnvSearchTests`](../../tests/SqlSource.Tool.Tests/DotEnv/DotEnvSearchTests.cs) `Read_NoEnvAnywhere_GivesNoFile` and `Read_OutsideARepository_ReadsOnlyTheUnitFolder` read through the real `ToolHost.ReadText` and assume that no folder above the system's temporary folder holds a `.git`.  [`DotEnvSearch.FoldersOf`](../../src/SqlSource.Tool/DotEnv/DotEnvSearch.cs) climbs to the nearest such folder, so when the temporary folder is inside a git work tree, as with `TMPDIR` pointed into a checkout or a Windows home folder kept in git:

- `Read_OutsideARepository_ReadsOnlyTheUnitFolder` finds the `.env` it wrote itself above the unit folder and fails every time;
- `Read_NoEnvAnywhere_GivesNoFile` fails when a `.env` sits between the temporary folder and that `.git`.

The class's other tests make their own `.git` and do not depend on it.

## Why it exists

The two tests take the usual machine for granted, and none of their peers needed a guard.

## Impact

Low: a false failure on a machine set up that way, which the developer cannot fix from the test's message.

## Proposed fix

Skip both with `Assert.SkipWhen(DotEnvSearch.IsInRepository(_folder.Path), "The temporary folder is inside a git work tree.")`.

## Trigger

A report of either test failing on a developer's machine, or the next change to the class.
