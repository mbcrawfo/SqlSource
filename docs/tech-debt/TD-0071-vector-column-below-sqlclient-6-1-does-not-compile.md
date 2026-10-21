# TD-0071 - A `vector` column below Microsoft.Data.SqlClient 6.1 is typed by a type that version lacks

## Problem

A project that references a Microsoft.Data.SqlClient older than 6.1 is `SQLSRC267`, an error, and its queries are still typed by the 6.1 column of [`SqlServerTypeMap`](../../src/SqlSource/Generation/Types/SqlServerTypeMap.cs), as [`SqlClientProbe.Column`](../../src/SqlSource/Generation/Types/SqlClientProbe.cs) chooses.  For a `vector` column that type is `global::Microsoft.Data.SqlTypes.SqlVector<float>`, which Microsoft.Data.SqlClient first defines in 6.1, so the generated model does not compile: the user gets the compiler's error for the missing type beside `SQLSRC267`.  `SQLSRC269` does not apply, because it says that the project does not reference the driver, which is false here.

## Why it exists

The spec for the SQL Server map decides that every project is typed by the column of 6.1 or later, and that a project below 6.1 is told to update the package.  Updating the package clears both errors, and a message that is true for a version below the floor needs a new diagnostic id, which the sub-phase that added the map may not take.

## Impact

Low.  The build fails already, on `SQLSRC267`, and the one fix, updating Microsoft.Data.SqlClient to 6.1 or later, clears the compiler error too.  The cost is a second error whose cause is not obvious from its text.  The manual's known issues for SQL Server say what to do.

## Proposed fix

Give a project below the floor a column of its own, or have `SqlClientProbe.Column` return `None` for it, so that a column whose type needs the driver (`vector`, today) is not typed by a type that version lacks.  Report it with a message that is true for it, either a new id such as "the column has the type 'vector(3)', whose C# type needs Microsoft.Data.SqlClient 6.1 or later, and this project references 6.0.5" or `SQLSRC269` reworded to cover a version below the floor as well as no reference.  A new id is a new diagnostic: add it to `SqlDiagnostics`, `AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, and cases in `SqlClientProbeTests` that run a `vector` column against `SqlClientReferences.SqlClient60`.  Then remove the known issue from `docs/manual/sqlserver.md`.

## Trigger

A user's report of the compiler error beside `SQLSRC267`, or the next change to the diagnostics of the SQL Server map.
