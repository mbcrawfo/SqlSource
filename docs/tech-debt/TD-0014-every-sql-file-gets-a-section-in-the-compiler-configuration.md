# TD-0014 - Every `.sql` file gets a section in the compiler's configuration file

## Problem

[`build/SqlSource.props`](../../src/SqlSource/build/SqlSource.props) lists `SqlSourceDialect` as a `CompilerVisibleItemMetadata` of `AdditionalFiles`, so that a file can have its own dialect.  The .NET SDK then writes a section for every `AdditionalFiles` item into the configuration file it generates for the compiler, with an empty value where the metadata is not set.  That is every `.sql` file of the project, including the ones no type uses.

[`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) also updates every `AdditionalFiles` item, to trim the metadata.

## Why it exists

Item metadata is the only way the compiler hands a generator a value for one file.  The SDK decides what it writes, and it does not leave out an empty value.

The sections are also what makes the build after a `.sql` file is deleted, renamed or moved compile again.  A removed file has no timestamp to compare, and the SDK's own record of what the compiler was given, the `CoreCompileInputs.cache` file, does not hold `AdditionalFiles`.  The configuration file does, it is an input of the compiler, and it changes when a section goes.  Checked with the .NET SDK 8.0.409 and 10.0.401: without the declaration the build reports success and the assembly keeps the members of the file that is gone.

## Impact

A project with 5,000 `.sql` files that no type uses, measured with .NET SDK 10.0.401 on the day this was written: a build that compiles took 0.86 s before the declaration, 1.13 s with it, and 1.21 s with the trim as well.  The generated configuration file grew from 1 KB to 1.1 MB.  That is about 0.07 ms for each file.

A project of ordinary size does not notice.  One with thousands of migration scripts pays a third of a second on each build, and can avoid it by leaving the scripts out, as `README.md` already advises: `<AdditionalFiles Remove="Migrations/**/*.sql" />`.

## Proposed fix

None is known that keeps the dialect of a file in the project file.  If the cost is reported, document the `Remove` line next to the dialect metadata in `README.md`, and consider a property that turns the metadata off for a project that does not use it.

Any fix that removes the sections must give the compiler the list of `.sql` files some other way, or a removed file stops triggering a compile.  [`tools/check-package-install.sh`](../../tools/check-package-install.sh) fails when it does.  One way is a target that runs before the SDK's `_GenerateCompileDependencyCache` and adds the `.sql` `AdditionalFiles` to the `CoreCompileCache` item, so that the list becomes part of the hash in `CoreCompileInputs.cache`.  That hooks a target whose name starts with an underscore, which the SDK is free to rename, and it has not been tried.

## Trigger

A user reports slow builds in a project with many `.sql` files.
