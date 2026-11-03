# TD-0098 - A settings file is not applied when the project's folder is reached through a symbolic link

## Problem

A build started in a folder whose path goes through a symbolic link gives the compiler two spellings of the project's folder.  [`SqlSourceFindSettingsFiles`](../../src/SqlSource/build/SqlSource.targets) adds each `sqlsource.json` it finds as an `AdditionalFiles` item by its full path, which MSBuild makes from the project's folder as the build was started, through the link.  The `.sql` files and the sidecars are items with paths relative to the project, which the compiler makes full against its own working folder, where the link is resolved.  The generator's chain of settings files ([`Generation/Chains/`](../../src/SqlSource/Generation/Chains/)) compares folders as text, so it finds no settings file above any `.sql` file, and every query is generated without the project's settings.

The review of sub-phase 9.0 ran it on macOS, where `/tmp` is a link to `/private/tmp`.  The spike's sample built from `/tmp/x` reported, at every query:

```text
/private/tmp/x/Queries/Count.sql(1,10): error SQLSRC209: The output 'models' needs each query described by its database, and the dialect 'ansi' has no database to ask.  ...
```

The compiler was given `/additionalfile:/tmp/x/sqlsource.json` beside `/additionalfile:Queries/Count.sql`.  The same folder built from `/private/tmp/x` reported only the error the sample expects.  [TD-0030](TD-0030-project-option-through-a-symbolic-link-is-not-found.md) is the same kind of problem in the tool's `--project` and `.sql` paths.

## Why it exists

No path of the package or the generator is resolved through links: the settings search, the manifest and the chain take paths as MSBuild and the compiler give them.  It was found by the review of the 9.0 spike, outside that change, and the owner chose on 2026-11-03 to record it.

## Impact

Low.  It takes a build started through a link, which is rare outside macOS's `/tmp` and a home folder or checkout reached through a link.  But nothing says why: the project's dialect, its type overrides and every other setting are not applied, so the queries report errors such as `SQLSRC209`, or are generated with defaults the user did not choose, with no error.

## Proposed fix

Give the compiler one spelling of each path.  Either the settings search adds each file by a path relative to the project when it is under the project's folder, as the `.sql` items are, or the generator resolves links on both sides before it compares folders, with `File.ResolveLinkTarget` for each folder, through the host the generator reads files with.  Add a test of each that builds a fixture project through a symbolic link to its folder.  Fix TD-0030 the same way if the generator's comparison is shared with the tool.

## Trigger

A user reports that a project's settings are ignored, or a build through a link that reports `SQLSRC209` or another error the settings would prevent.
