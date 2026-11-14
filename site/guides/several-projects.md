# Use SqlSource in several projects

<a id="test-project-sees-internals"></a>

## Let a test project see the internals of the main project

Both projects can use SqlSource, and the attributes that each gets do not conflict.  The test project uses the main project's `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, so it catches what a method of either project throws by its type.  When the main project uses the exceptions of a shared project, the shared project grants `InternalsVisibleTo` to the test project too.  See [the recipe](#test-project-sees-internals), and [the other recipe](#share-the-exceptions) for an app that sees two data projects.

```xml
<!-- App.csproj -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

```csharp
// App.Tests, which references the SqlSource package too, with "output": "methods" in the sqlsource.json of
// Queries, since a static type has no repository methods
using SqlSource;

[SqlSourceGenerate(Source = "Queries")]
internal static partial class TestQueries;
```

```csharp
// App.Tests catches the exception that a method of App throws by its type
await Should.ThrowAsync<UnexpectedRowCountException>(() => users.GetActiveUser(new GetActiveUserParams(email)));
```

The test project uses App's `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, whether it uses SqlSource or not, and the attributes of each project do not conflict.  When App uses the exceptions of a [shared project](#share-the-exceptions), the shared project grants `InternalsVisibleTo` to App.Tests too.

Rules: [projects that share internals](../reference/exceptions.md#projects-that-share-internals).

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

Each data project uses the shared project's exceptions instead of writing its own, so the app sees one copy and catches what a method of either throws by its type.  Grant `InternalsVisibleTo` from Data.Shared to every project that catches the exceptions, or that uses SqlSource and sees a data project's internals, the data projects' test projects among them: one that does not see Data.Shared's copy gets its own, and its `catch` compiles but misses what a data project throws.  Without the shared project, an app that uses SqlSource and sees the internals of both gets [SQLSRC022](../diagnostics.md#sqlsrc022).  Data.Shared targets .NET 6 or later, or adds such a target to its `TargetFrameworks`, so that its `CollectionItemException` passes on `SqlState`, `IsTransient` and `BatchCommand`; on `netstandard2.0` or `netstandard2.1` alone, a data project gets [SQLSRC038](../diagnostics.md#sqlsrc038).

Rules: [projects that share internals](../reference/exceptions.md#projects-that-share-internals), [SQLSRC022](../diagnostics.md#sqlsrc022), [SQLSRC038](../diagnostics.md#sqlsrc038).
