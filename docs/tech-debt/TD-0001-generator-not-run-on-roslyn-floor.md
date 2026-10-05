# TD-0001 - The generator is never run on its Roslyn floor

## Problem

The generator supports hosts back to Roslyn 4.8.0 (.NET 8 SDK, Visual Studio 2022 17.8), but no test runs it on that version.  [`SqlSource.Tests.csproj`](../../tests/SqlSource.Tests/SqlSource.Tests.csproj) overrides `Microsoft.CodeAnalysis.CSharp` to 5.9.0, so every driver test runs on a current compiler.  Compiling [`SqlSource.csproj`](../../src/SqlSource/SqlSource.csproj) against 4.8.0 guards the API surface only.

## Why it exists

The driver parses the source text that tests feed it.  On Roslyn 4.8.0 it cannot parse C# 13 or later, which rules out testing the generator against current syntax.  Most consumers also host the generator on a compiler newer than the floor, so the tests run where most users are.

## Impact

A behavioural difference between Roslyn 4.8 and the version the tests use would go unnoticed until a consumer on the .NET 8 SDK reports it.  The risk grows with the amount of syntax and semantic analysis the generator does.

## Proposed fix

Run the test suite a second time with the test project's `Microsoft.CodeAnalysis.CSharp` reference set to 4.8.0, for example through an MSBuild property that selects the version.  Tests whose input needs newer syntax are skipped in that run.

## Trigger

The generator gains its first real analysis logic, or a consumer reports a failure that occurs only on an older SDK.
