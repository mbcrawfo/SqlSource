# TD-0099 - The references of SQL Server's refusal of models have never run

## Problem

[`RealisticModelRefusalTests`](../../tests/DatabaseTests/SqlServer/Realistic/Generation/RealisticModelRefusalTests.cs) of SQL Server gives [`RealisticRefusalChecks`](../../tests/SqlSource.Tool.Testing/Realistic/RealisticRefusalChecks.cs) the reference `SqlClientReferences.SqlClient7`, the downloaded Microsoft.Data.SqlClient 7.1.1.  The check runs the generator through [`RealisticGeneration`](../../tests/SqlSource.Tool.Testing/Realistic/RealisticGeneration.cs) and `GeneratorHarness`, whose compilation already references every assembly of the test process, and the SQL Server test project references Microsoft.Data.SqlClient 7.1.1 itself.  So the run is given two files of one assembly identity.  What the compiler and the generator make of that pairing is not known: it has never run, because no SQL Server case of the realistic corpus has models the generator refuses, so every case of the test skips.

## Why it exists

The test was written for both engines alike before SQL Server had a case to run it, and the PostgreSQL test, which runs one case, gives no such second reference.  The plan of the realistic corpus's single claim (`docs/superpowers/plans/2026-11-03-realistic-corpus-single-claim.md`) found it, outside its scope, and the repository's rule records it.

## Impact

Low.  It runs no test today.  The first SQL Server case whose models the generator refuses may fail for the references, with an error of duplicate assemblies or a binding the generator does not expect, instead of the refusal its `case.json` names, and the failure would look like a fault of the case.

## Proposed fix

Give the SQL Server check no reference of its own, as the PostgreSQL check does, since the process's own Microsoft.Data.SqlClient is already in the run; or, if the check means to pin the driver's version, give the run the downloaded driver alone, with the harness's runtime references filtered of the process's copy.  Prove it with a case that runs, or with a unit test of `RealisticGeneration.RunAtModels` given `SqlClientReferences.SqlClient7` on the SQL Server corpus that asserts the run reports only what the query gives.

## Trigger

A SQL Server case of the realistic corpus whose `case.json` says the generator refuses its models, or a change to the references `GeneratorHarness` gives a run.
