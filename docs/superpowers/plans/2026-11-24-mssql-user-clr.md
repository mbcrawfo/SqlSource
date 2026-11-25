# SQL Server CLR type of a user's own Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A query that returns or takes a column of a CLR type of a user's own assembly (`CREATE TYPE ... EXTERNAL NAME`) is described and gets a sidecar, on the floor and default SQL Server images, where today the whole run ends with `SQLSRC200`.

**Architecture:** `SqlServerTypes.Build` names a row with no system type name, but a user type with an assembly-qualified name, `schema.name` by its user type.  The generator already maps such a type through the type override and binds it as a `Udt` of its user type, and the CLR nullability rule of `td-0071-clr-setop` already treats a row with no type name as a CLR type.

**Tech Stack:** C#, xUnit, Shouldly, SQL Server 2017 and 2025 in containers, a net48 build of a test CLR type.

**Spec:** the unit `mssql-user-clr` of the tech-debt run, which resolves TD-0127.

## Global Constraints

- A false non-null is the worst outcome: the rule of `td-0071-clr-setop` stays as it is.
- No other type's answer changes: the fixtures and the end-to-end sidecars re-record with no diff.
- `README.md` is not edited.

## Measurements

On 2017 (CU31-GDR19) and 2025 (CU9-GDR1), with a `SAFE` assembly of a `Format.Native` type.

| Fact | Result |
|----|----|
| `sys.dm_exec_describe_first_result_set` of a column of the type | `system_type_name` `NULL`; `user_type_schema`, `user_type_name`, `assembly_qualified_type_name` and every facet given |
| `sp_describe_undeclared_parameters` of `WHERE t = @t` and of an `INSERT` | `suggested_system_type_name` `NULL`, the user type and the facets given |
| `=` on a type that is not byte ordered | Error 403, so the parameter is not inferred (11507) |
| The assembly-qualified name | Names version `0.0.0.0`, whatever the build's |
| `sqlsource describe` before the fix | `SQLSRC200`, and no sidecar for any query of the run |
| After the fix, with `dbo.Tag=Tag` | Generated methods read and send the type through SqlClient 7.1.1 on .NET 10 |

### Task 1: The type

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Types/SqlServerTypesTests.cs`

- [ ] A row with no system type name, a user type's name and an assembly-qualified name is `schema.name`; any other row with no name, or a row without a facet, still throws.

### Task 2: The server's tests

**Files:**
- Create: `tests/DatabaseTests/SqlServer.ClrType/`, `tests/DatabaseTests/SqlServer/UserClrDatabase.cs`, `tests/DatabaseTests/SqlServer/Describer/UserClrTypeTests.cs`
- Modify: `tests/DatabaseTests/SqlServer/SqlServer.csproj`, `SqlServerDatabases.cs`, the solution, `tools/test-scopes.txt`

- [ ] A net48 and net10.0 build of `Grade`; the test project embeds the net48 build and loads it into `user_clr` in `BuiltAsync`.
- [ ] A column, an inferred and a declared parameter, a set operator, a view and expressions, each on both images, and the whole `describe` command.

### Task 3: The documents

- [ ] The manual's type table, a section with the mapping, `SQLSRC246`'s fixes, the query methods' page; the limit and its troubleshooting row removed.
- [ ] TD-0127 removed; the architecture page, `CONTRIBUTING.md` and the test readmes.
- [ ] The example, as the owner decides.
