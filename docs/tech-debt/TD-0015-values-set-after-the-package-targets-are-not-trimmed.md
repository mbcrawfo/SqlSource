# TD-0015 - A value set after the package's targets is not trimmed

## Problem

The compiler reads `SqlSourceDialect` and `SqlSourceTokenValidation`, and the `SqlSourceDialect` metadata of an `AdditionalFiles` item, from a file that the build writes with one line for each value.  A value on a line of its own, as an element written over several lines has it, arrives empty there.  [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) trims each value so that this form works.

The trim runs where NuGet imports the package's targets.  It misses three cases:

- **A value set later in the evaluation.**  The .NET SDK imports the targets of packages before `Directory.Build.targets`.  A property set there, or an `AdditionalFiles` item declared or updated there, is not trimmed.
- **An item added inside a target.**  The trim of the metadata is an item update at evaluation time, and does not see an item that a target adds while the build runs.
- **Metadata that holds a single quote.**  The trim is `$([System.String]::Copy('%(SqlSourceDialect)').Trim())`.  A value such as `it's` ends the quoted argument early, and MSBuild then leaves the whole expression as the value.  `SQLSRC011` quotes the expression and not what the user wrote.

## Why it exists

A property function in the package's targets is the only place the package can change a value before the compiler reads it, and the SDK fixes where that file is imported.  The first two cases were known for `SqlSourceTokenValidation` and not written down; the review of the dialect setting found them again, and the third.

## Impact

Low.  In the first two cases a value written over several lines is taken as not set, with no diagnostic: the file is read by the project's dialect or as `ansi`, and token validation stays on.  A value written on one line is not affected, and that is the form `README.md` shows.  The third case is reported, as `SQLSRC011`, with a confusing value.

## Proposed fix

1. Move the trims into a target that runs before `GenerateMSBuildEditorConfigFileCore`, which is the target that writes the file the compiler reads.  That sees every property and every item, wherever it was set.  It has to be shown to run in a design-time build, which is how an IDE gets the values, before the evaluation-time trim is removed.
2. Until then, say in `README.md` that a value in `Directory.Build.targets` goes on one line.

## Trigger

A user reports a dialect or a validation setting that is ignored.  The trims are moved for another reason.
