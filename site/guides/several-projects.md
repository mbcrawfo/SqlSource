# Use SqlSource in several projects

Use `InternalsVisibleTo` between a test project, class libraries and an app that use SqlSource, and catch the exception of a method by its type.

**Use this when** more than one project of a solution uses SqlSource, such as a main project and its test project.  Use it too when two projects need the queries of one `.sql` file.

<a id="test-project-sees-internals"></a>

## Let a test project see the internals of the main project

The main project grants `InternalsVisibleTo` to its test project:

```xml
<!-- App.csproj -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

The test project can use SqlSource too:

```csharp
// App.Tests, which references the SqlSource package too, with "output": "methods" in the sqlsource.json of
// Queries, since a static type has no repository methods
using SqlSource;

[SqlSourceGenerate(Source = "Queries")]
internal static partial class TestQueries;
```

It catches by its type the exception that a method of App throws:

```csharp
// App.Tests catches the exception that a method of App throws by its type
await Should.ThrowAsync<UnexpectedRowCountException>(() => users.GetActiveUser(new GetActiveUserParams(email)));
```

**What you get.**  The test project uses App's `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, whether it uses SqlSource or not.  So it catches by its type what a method of either project throws.  The attributes that each project gets do not conflict.

**The rules you need.**

- **A project that sees the exceptions in a project it references gets none of its own.**  Its query methods throw the exceptions of the other project.  See [projects that share internals](../reference/exceptions.md#projects-that-share-internals).
- **If App uses the exceptions of a shared project, the shared project grants `InternalsVisibleTo` to App.Tests too.**  See [the shared project of an app that sees two data projects](#share-the-exceptions).

<a id="share-the-exceptions"></a>

## Share the exceptions between two data projects and an app

```xml
<!-- Data.Shared.csproj: references the SqlSource package, and has no query -->
<PropertyGroup>
    <!-- .NET 6 or later, alone or beside an older target such as netstandard2.0 -->
    <TargetFramework>net8.0</TargetFramework>
</PropertyGroup>
<ItemGroup>
    <InternalsVisibleTo Include="Data.Orders" />
    <InternalsVisibleTo Include="Data.Users" />
    <InternalsVisibleTo Include="App" />
    <!-- and each test project of a data project -->
    <InternalsVisibleTo Include="Data.Orders.Tests" />
    <InternalsVisibleTo Include="Data.Users.Tests" />
</ItemGroup>
```

```xml
<!-- Data.Orders.csproj and Data.Users.csproj: each references the SqlSource package and has its queries -->
<ItemGroup>
    <ProjectReference Include="../Data.Shared/Data.Shared.csproj" />
    <InternalsVisibleTo Include="App" />
</ItemGroup>
```

```csharp
// App, which sees the internals of all three
catch (UnexpectedRowCountException exception)
{
    // thrown by a method of Data.Orders or of Data.Users
}
```

**What you get.**  Each data project uses the exceptions of Data.Shared instead of writing its own.  So the app sees one copy, and catches by its type what a method of either data project throws.

**The rules you need.**

- **Grant `InternalsVisibleTo` from Data.Shared to every project that catches the exceptions.**
- **Grant `InternalsVisibleTo` from Data.Shared to every project that uses SqlSource and sees the internals of a data project.**  The test projects of the data projects are among them.
- **A project that uses SqlSource and does not see the copy of Data.Shared gets its own copy.**  Its `catch` compiles, but misses what a data project throws.
- **Without the shared project, an app that uses SqlSource and sees the internals of both data projects gets [SQLSRC022](../diagnostics.md#sqlsrc022).**  See [projects that share internals](../reference/exceptions.md#projects-that-share-internals).
- **Data.Shared must target .NET 6 or later, or add such a target to its `TargetFrameworks`.**  On `netstandard2.0` or `netstandard2.1` alone, a data project gets [SQLSRC038](../diagnostics.md#sqlsrc038).
- **On .NET 6 or later, the `CollectionItemException` of Data.Shared passes on `SqlState`, `IsTransient` and `BatchCommand`.**

<a id="one-file-two-projects"></a>

## One `.sql` file in two projects

If two projects must use the same queries, keep the `.sql` file in one project and list it in the other:

```text
App.slnx
App/
    App.csproj
    sqlsource.json                "dialect": "postgres"
    Queries/
        UserRepository.cs
        UserRepository.sql        used by App and by Reports
        UserRepository.sql.json   one sidecar for both projects
Reports/
    Reports.csproj
    UserRepository.cs
```

```xml
<!-- Reports/Reports.csproj: references the SqlSource package -->
<ItemGroup>
    <AdditionalFiles Include="../App/Queries/UserRepository.sql" />
    <AdditionalFiles Include="../App/Queries/UserRepository.sql.json" />
</ItemGroup>
```

```csharp
// Reports/UserRepository.cs
using Npgsql;
using SqlSource;

namespace Reports;

[SqlSourceGenerate(Source = "../App/Queries/UserRepository.sql")]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

```console
$ dotnet sqlsource describe App.slnx
```

**What you get.**  Each project gets its own `UserRepository`, with a method and models for each query of the file.  The file has one sidecar, and the run on the solution describes its queries for both projects.

**The rules you need.**

- A `.sql` file outside the project's folder is not included.  List it and its sidecar as `AdditionalFiles` items.  See [Which `.sql` files are included](../reference/msbuild.md#included-files).
- `Source` is relative to the folder of the C# file, never to the project.  See [Source](../reference/attributes.md#setting-source).
- Both projects read the settings of the file from its markers and the settings files of its own folder.  So both projects need the same entries in its sidecar.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).
- If one project finds a settings file that the other does not, a run on that project alone, or its build, can leave out or remove entries that the other needs.  Name every settings file `sqlsource.json`, and describe the solution, with `--project` if you want one project.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).

## If it does not work

- **SQLSRC004 in the second project.**  The project does not list the `.sql` file.  Add it as an `AdditionalFiles` item.  See [SQLSRC004](../diagnostics.md#sqlsrc004).
- **SQLSRC241 in the second project, and the sidecar has the entry.**  The project does not list the sidecar.  Add it as an `AdditionalFiles` item.  See [SQLSRC241](../diagnostics.md#sqlsrc241).
- **SQLSRC241 in one project after a run on the other.**  The two projects read different settings for the file.  Describe the solution, not the other project alone.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).
- **SQLSRC022 in the app.**  Put the exceptions in a shared project, as [Share the exceptions between two data projects and an app](#share-the-exceptions) shows.  See [SQLSRC022](../diagnostics.md#sqlsrc022).
- **SQLSRC038 in a data project.**  Add a target of .NET 6 or later to Data.Shared.  See [SQLSRC038](../diagnostics.md#sqlsrc038).
- **A `catch` compiles, but misses what a data project throws.**  Grant the project `InternalsVisibleTo` from Data.Shared.  See [Share the exceptions between two data projects and an app](#share-the-exceptions).
