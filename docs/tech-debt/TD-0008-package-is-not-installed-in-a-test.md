# TD-0008 - No test installs the packed package

## Problem

Two things check the package, and neither uses it the way a consumer does:

- [`tools/check-package.sh`](../../tools/check-package.sh) lists the files in the `.nupkg` and checks that `build/SqlSource.props`, the analyzer assembly and the readme are there.
- [`SqlSource.Tests.csproj`](../../tests/SqlSource.Tests/SqlSource.Tests.csproj) imports `src/SqlSource/build/SqlSource.props` by path and gets the generator through a project reference.

Nothing restores the `.nupkg` into a project and builds it.

## Why it exists

Such a test needs a local package feed, a scratch project and a second restore and build, in `pre-commit-validation.sh` and in CI.  The phase 2 design left it out of scope.

## Impact

A mistake that only shows when NuGet consumes the package would reach a release: a file in the wrong package folder that the script does not look for, a `DevelopmentDependency` setting that stops `build/` assets from flowing, or a generator dependency missing from `analyzers/`.  The first sign would be a consumer whose attributed types report SQLSRC005 for a folder that has `.sql` files.

## Proposed fix

Add `tools/check-package-install.sh`: pack into a temporary folder, create a console project there with a `nuget.config` that adds the folder as a source, add the package, a `.sql` file and an attributed type that prints a constant, and assert on the output of `dotnet run`.  Run it in `pre-commit-validation.sh` and in `workflows/build.yml`.

## Trigger

Before the first release to nuget.org.  A change to the pack items in `SqlSource.csproj`.
