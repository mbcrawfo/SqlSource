# TD-0027 - The manifest target depends on a target of the SDK by name

## Problem

`SqlSourceWriteManifest` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) depends on `AddImplicitDefineConstants` when the project has a `TargetFramework`.  That is the target of the .NET SDK that adds the constants of the framework, `NET10_0` and `NET8_0_OR_GREATER` among them, to `DefineConstants`.  The SDK runs it on the way to a compile; run by name, a project has `TRACE;DEBUG` alone, and the tool would not find an attribute under `#if NET8_0_OR_GREATER`.

The name is the SDK's own and no contract.  An SDK that renames the target fails every run of the tool with `MSB4057`, which the tool reports as `SQLSRC205`.  A project that sets `DisableImplicitFrameworkDefines` gets no such constants, in the manifest as in its build.

## Why it exists

It is the one way to get the constants as the compiler gets them, with what a project adds or removes.  The target has had this name since the .NET 5 SDK.

## Impact

Low today.  A rename breaks the tool for every project until the package is updated, and the failure names the missing target.

## Proposed fix

The tool works the constants out itself from the manifest's `TargetFramework`, as Roslyn's own workspace does, and the target stops depending on the SDK's.  That loses a constant a project's own target adds, which the manifest does not hold today either.

## Trigger

An SDK in which `dotnet msbuild -t:SqlSourceWriteManifest` fails with `MSB4057` for `AddImplicitDefineConstants`.  `tests/SqlSource.Tool.Tests/ManifestTargetTests.cs` fails on that SDK.
