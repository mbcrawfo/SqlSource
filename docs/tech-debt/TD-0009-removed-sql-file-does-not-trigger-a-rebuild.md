# TD-0009 - Deleting or renaming a `.sql` file does not trigger an incremental rebuild

## Problem

[`build/SqlSource.props`](../../src/SqlSource/build/SqlSource.props) hands a project's `.sql` files to the compiler as `AdditionalFiles`.  When one is deleted, renamed or moved, an incremental `dotnet build` does not run the compiler again: it reports success, and the assembly keeps the members of the file that is gone.  `dotnet build --no-incremental` then reports the real errors.  Editing a file is not affected, because its timestamp changes.

The cause is in the SDK.  The `_GenerateCompileDependencyCache` target hashes the lists of `Compile` items and references into a file that is an input of the compiler, so that a removed source file forces a recompile.  `AdditionalFiles` are not part of that hash.

Found in the review of phase 2, with the .NET SDK 10.0.401, by deleting a `.sql` file in a project that used its query.

## Why it exists

The fix hooks a target whose name starts with an underscore, which the SDK is free to rename.  It was not verified in the review, and nothing installed the package then, so it could not be regression-tested.  [`tools/check-package-install.sh`](../../tools/check-package-install.sh) installs it now, and is where that test goes.

## Impact

A developer who deletes or renames a `.sql` file sees a green local build with stale members, and then a failing clean build in CI.  Visual Studio's own up-to-date check was not tested.  `README.md` describes the behaviour and the workaround.

## Proposed fix

Add a target to [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) that runs before `_GenerateCompileDependencyCache` and adds the `.sql` `AdditionalFiles` to the `CoreCompileCache` item, so that the list of files becomes part of the hash:

```xml
<Target Name="SqlSourceTrackSqlFiles" BeforeTargets="_GenerateCompileDependencyCache">
    <ItemGroup>
        <CoreCompileCache Include="@(AdditionalFiles)" Condition="'%(Extension)' == '.sql'" />
    </ItemGroup>
</Target>
```

Check it against the oldest supported SDK, 8.0, and add the case to `tools/check-package-install.sh`: after the first build, delete a `.sql` file whose query the project uses, build again without `--no-incremental`, and expect the build to fail.  Remove the paragraph about it from `README.md`.

## Trigger

Before the first release to nuget.org.
