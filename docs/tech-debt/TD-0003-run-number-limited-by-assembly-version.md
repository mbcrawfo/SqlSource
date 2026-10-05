# TD-0003: The run number is limited by the assembly version format

## Problem

[`Directory.Build.props`](../../Directory.Build.props) sets `AssemblyVersion` and `FileVersion` to `$(VersionPrefix).$(BuildNumber)`, and the workflows set `BuildNumber` to `github.run_number`.  Each part of an assembly version is limited to 65534.  A build with `BuildNumber` 65535 or higher fails with `CS7034`.

## Why it exists

The version scheme puts the run number in the DLL so that a build can be traced to the run that produced it.  The fourth part of the assembly version is the conventional place for it, and the limit is far away: each workflow counts its own runs from 1.

## Impact

Low.  Nothing is wrong today.  When the run number of `ci.yml` or `publish.yml` reaches 65535, every run of that workflow fails at the build step until the scheme changes.

## Proposed fix

Stop using the raw run number as a version part.  Either leave the fourth part at `0` and rely on the informational version, which already carries the package version and the commit, or pass a bounded value such as the run number modulo 65535.  The package version is not affected: a prerelease label has no such limit.

## Trigger

The run number of either workflow passing 60000.
