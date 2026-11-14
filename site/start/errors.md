# When something goes wrong

At the end of this page, you can read a SQLSRC build error, find its fix, and know where to look when a method throws an exception at run time.

<a id="read-an-error"></a>

## Read an SQLSRC error

This is one error line of `dotnet build`:

```console
/work/App/Queries/UserRepository.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'repository' needs one.  Run 'sqlsource describe'. (https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc241) [/work/App/App.csproj]
```

- `Queries/UserRepository.sql(1,10)` is the file, the line and the column.  An error about a query is at the query's name.
- `SQLSRC241` is the id of the error.
- The message says what is wrong, and often what to run.
- The link opens the section of the error in [the list of errors](../diagnostics.md), which says why it happens and how to fix it.
- The project file in brackets is the project that the build was building.

An error of the build that has no file, such as one about a package version, starts with `CSC` in place of the file.

<a id="what-an-error-costs"></a>

## What an error costs you

- **An error in how a `.sql` file is written** costs the whole file its code.  No query of the file gets its models or its method until you fix it.
- **An error about describing a query** costs that query its models and its method.  The other queries of the file keep theirs.
- **A query that your database rejects** holds back each other new or changed query of its file, with SQLSRC241 or SQLSRC244.  Fix the query, and the others are described too.

<a id="first-errors"></a>

## The errors you meet first

- [SQLSRC241](../diagnostics.md#sqlsrc241): the query has no entry in the sidecar.  Describe it: build with your `.env` file in place, or run `dotnet sqlsource describe`.  Then commit the sidecar.
- [SQLSRC244](../diagnostics.md#sqlsrc244): the entry is stale, since the query changed after it was described.  Describe it again, as for SQLSRC241.
- [SQLSRC209](../diagnostics.md#sqlsrc209): the query needs a dialect that can be described.  Name your database in `sqlsource.json`, as [Your first query](first-query.md#name-your-database) shows.
- [SQLSRC213](../diagnostics.md#sqlsrc213): your database has no connection.  Put the `.env` file in the folder of your project, not at the root of your repository.
- [SQLSRC220](../diagnostics.md#sqlsrc220): the project was not restored.  Run `dotnet restore`, then run the tool again.
- [SQLSRC224](../diagnostics.md#sqlsrc224): your database could not be opened.  Check that it runs, and check the connection string and the password.  For SQL Server in a local container, add `TrustServerCertificate=True`.
- [SQLSRC226](../diagnostics.md#sqlsrc226) and [SQLSRC227](../diagnostics.md#sqlsrc227): the type of a parameter could not be determined, or its first use does not fix it.  Add a `-- param:` line with the type, such as `-- param: @name text null`.
- [SQLSRC021](../diagnostics.md#sqlsrc021): the compiler is too old.  Update the .NET SDK to 9.0.300 or later, or Visual Studio to 2022 17.14 or later.
- [SQLSRC267](../diagnostics.md#sqlsrc267): Microsoft.Data.SqlClient is older than 6.1, often because EF Core 8 or 9 brings 5.1.  Reference Microsoft.Data.SqlClient 6.1 or later in your project file.
- [SQLSRC206](../diagnostics.md#sqlsrc206): the project manifest cannot be read, because the tool and the `SqlSource` package have different versions.  Update the older one.

<a id="at-run-time"></a>

## It built, but it fails when it runs

Some problems show only when a method runs, such as an `InvalidCastException`, or a null in a column that you marked never null.  [Troubleshooting](../guides/troubleshooting.md#at-run-time) lists them by what you see, each with its fix.

## What you have now

A way to read any SQLSRC error, and the fixes of the errors you meet first.

Rules: [where an error is reported](../reference/errors.md#where-an-error-is-reported), [what an error costs](../reference/errors.md#what-an-error-costs), [running in the build](../reference/msbuild.md#running-in-the-build), [the list of errors](../diagnostics.md).

Next: [What do you want to do?](../index.md#find-what-you-need)
