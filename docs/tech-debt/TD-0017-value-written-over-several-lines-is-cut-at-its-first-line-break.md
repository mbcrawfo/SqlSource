# TD-0017 - A value with a line break inside it is cut at the line break

## Problem

The compiler reads the properties and the metadata that [`build/SqlSource.props`](../../src/SqlSource/build/SqlSource.props) lists from a file the build writes with one line for each value.  The targets of [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) trim each value, so that a value on a line of its own arrives.  They remove the white space around a value and leave the white space inside it.

A list of generator parameters that is written with one word on each line therefore keeps its line breaks:

```xml
<PropertyGroup>
    <SqlSourceGeneratorParameters>
        keep-comments
        no-token-validation
    </SqlSourceGeneratorParameters>
</PropertyGroup>
```

The build writes `build_property.SqlSourceGeneratorParameters = keep-comments` and then a line that holds only `no-token-validation`, which the compiler ignores.  The generator sees `keep-comments` alone.  Nothing is reported: the methods of the project check their arguments.  The same happens to the metadata of an `AdditionalFiles` item, and to `SqlSourceDialect` when an option is on a line after the name of the dialect: with the comma left on the first line the value is cut to `mysql,`, which is reported as [SQLSRC011](../diagnostics.md#sqlsrc011), and it is silent only when the comma starts the second line, where the value is cut to `mysql`.  A value of `SqlSourceOutput`, such as `code` and `gen` on two lines, or of `SqlSourceDatabase`, `SqlSourceInputModelSuffix`, `SqlSourceOutputModelSuffix`, `SqlSourceModelNamespace`, `SqlSourceInputModelType`, `SqlSourceOutputModelType` or `SqlSourceCollectionType`, is cut the same way: `sealed` and `record` on two lines are `sealed`, which is no value of `SqlSourceInputModelType`.

## Why it exists

The trim was written for `SqlSourceTokenValidation` and `SqlSourceDialect`, whose values were one word, or a name and its options that are naturally written on one line.  `SqlSourceGeneratorParameters`, which replaced the first of them, is a list of words, and a list is what a person writes over several lines.  It was found while the targets were generalised for that property, by building a project that writes its list that way, and changing what the trim does to a value was outside that change.

## Impact

Medium.  A project that writes a list over several lines gets the first word and silently loses the others.  `README.md` says to write the words on one line, in "MSBuild", "Generator parameters".

## Proposed fix

Make each trim replace every run of white space inside the value with one space, as well as removing the white space around it.  For a property that is `$([System.Text.RegularExpressions.Regex]::Replace($(SqlSourceGeneratorParameters), '\s+', ' ').Trim())`; for the metadata of an item the same on the `AsWritten` property of the trim target, which keeps the metadata out of the argument of a property function.  `tests/SqlSource.Tests/Package/BuildFileTests.cs` pins the text of each trim and changes with it.  Add a value written with one word on each line to `tests/SqlSource.Tests/SqlSource.Tests.csproj` and to `tools/package-install`, so that the end-to-end tests and the check of the installed package prove it.

## Trigger

A user reports that only the first of several generator parameters applies, or that an option of a dialect is ignored.  A setting that is added with a value of several words.
