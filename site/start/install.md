# Install

You use Dapper: start with [Use Dapper](../guides/dapper.md).  Your database is not PostgreSQL or SQL Server: start with [another database](../guides/other-databases.md).

At the end of this page, your project has the SqlSource NuGet package, and your repository has the tool, on a supported .NET version.  Your project also has the driver package of your database.

<a id="requirements"></a>

## Requirements

- Your project targets .NET 8 or later, and C# 12 or later.
- You build it with the .NET SDK 9.0.300 or later, or with Visual Studio 2022 17.14 or later.
- Your database is PostgreSQL 14 or later, or SQL Server 2017 or later, as [supported versions](../reference/databases/index.md#supported-versions) lists.
- The `sqlsource` tool needs the .NET SDK, not only the .NET runtime.

If your .NET SDK or Visual Studio is older, the build stops with [SQLSRC021](../diagnostics.md#sqlsrc021).  Update the .NET SDK to 9.0.300 or later, or Visual Studio to 2022 17.14 or later.  If a `global.json` file pins an older SDK, change the version it pins.  Your project can still target .NET 8.

<a id="add-the-package"></a>

## Add the package

In the folder of each project that has queries, add the `SqlSource` package:

```console
$ dotnet add package SqlSource
```

Then add the driver package of your database to the same project:

# [PostgreSQL](#tab/postgresql)

```console
$ dotnet add package Npgsql
```

# [SQL Server](#tab/sqlserver)

```console
$ dotnet add package Microsoft.Data.SqlClient
```

If your project uses EF Core 8 or 9 and does not reference Microsoft.Data.SqlClient itself, it gets 5.1, and the build reports [SQLSRC267](../diagnostics.md#sqlsrc267).  Reference version 6.1 or later in your project file:

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.7" />
</ItemGroup>
```

---

Each command adds a `PackageReference` to your project file.

<a id="install-the-tool-step"></a>

## Install the tool

Do this once, in the folder at the root of your repository:

```console
$ dotnet new tool-manifest
$ dotnet tool install SqlSource.Tool
```

If your repository already has a tool manifest, leave out the first command.  The second command adds the tool to the manifest, `dotnet-tools.json`, and prints that the tool was installed.

- Commit `dotnet-tools.json`.  A colleague who clones the repository runs `dotnet tool restore` to get the same tool.
- Keep the tool at the version of the `SqlSource` package.  When you update the package, run `dotnet tool update SqlSource.Tool`.

If the tool and the package have different versions, the tool can report [SQLSRC206](../diagnostics.md#sqlsrc206).  Update the older of the two.

<a id="packages"></a>

## What each package adds to your project

- **`SqlSource`** runs inside the compiler when you build.  It adds no runtime dependency: no SqlSource assembly ships with your application.  Your project compiles only the code it generates, with the attributes you write and the exceptions its methods throw.
- **`SqlSource.Tool`** adds the `sqlsource` command to your repository.  It describes your queries, as the next page shows.

## If it does not work

- [SQLSRC021](../diagnostics.md#sqlsrc021): your .NET SDK or Visual Studio is too old.  Update it, as [Requirements](#requirements) says.
- [SQLSRC267](../diagnostics.md#sqlsrc267): on SQL Server, your project references Microsoft.Data.SqlClient older than 6.1.  Reference 6.1 or later.
- [SQLSRC206](../diagnostics.md#sqlsrc206): the tool and the package have different versions.  Update the older one.

## What you have now

A project that references `SqlSource` and your driver, and a repository whose tool manifest has the `sqlsource` tool.

Rules: [what the build needs](../reference/msbuild.md#build-requirements), [supported versions](../reference/databases/index.md#supported-versions), [install the tool](../reference/cli.md#install-the-tool), [what the tool needs](../reference/cli.md#requirements), [what the package adds](../reference/attributes.md#what-the-package-adds), [SQLSRC267 with EF Core](../reference/databases/sqlserver/types.md#limit-ef-core-sqlclient).

Next: [Your first query](first-query.md)
