# MSBuild properties and the build

<a id="properties"></a>

## The properties

SqlSource has five MSBuild properties that a project can set.  You can write each property in one of these places:

- the project file;
- a `Directory.Build.props` above the project file;
- the command line, as `-p:`;
- a variable of the environment.

This table gives each property, what it sets, its default and the section that documents it.

| Property | What it sets | Default | Section |
|----|----|----|----|
| `SqlSourceIncludeFiles` | Whether the package gives the compiler every `.sql` file and sidecar under the project's folder.  `false` turns this off | On | [Which `.sql` files are included](#included-files) |
| `SqlSourceOnline` | Whether a build runs `dotnet sqlsource describe` before it compiles: `auto`, `true` or `false` | `auto` | [Running the tool in the build](online-build.md#online-build) |
| `SqlSourceEnvFile` | The `.env` file that a build reads its connections from: a path, or `none` | The `.env` in the project's folder | [The build's `.env` file](online-build.md#setting-env-file) |
| `SqlSourcePublicExceptions` | Whether the exceptions that the query methods throw are `public`.  `true` makes them `public` | Off: they are `internal` | [Public exceptions](exceptions.md#public-exceptions) |
| `SqlSourceRepositoryInterfaces` | Whether the project's repositories get their interfaces.  `false` turns them off | On | [Repository interfaces](repositories.md#repository-interfaces) |

<a id="build-requirements"></a>

## What the build needs

**Supported environments.**  A project that uses SqlSource targets .NET 8 or later, and compiles C# 12 or later.  C# 12 is the default for .NET 8.  The project builds in one of these:

- the .NET SDK 9.0.300 or later;
- Visual Studio 2022 17.14 or later.

The .NET 8 SDK and Visual Studio before 17.14 are not supported.  A build on one of them stops with [SQLSRC021](../diagnostics.md#sqlsrc021).

<a id="included-files"></a>

## Which `.sql` files are included

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

The package gives the compiler every `.sql` file under the project's folder, as an `AdditionalFiles` item.  This is how a source generator sees a file that is not C#.  The sample above takes the `.sql` files of the folder `Migrations`, and of its subfolders, out of that list.

- **The package leaves some folders out.**  It leaves out the output and intermediate folders, and each folder whose name starts with a dot.  The SDK leaves the same folders out of your C# files.
- **The `sqlsource` tool reads the same list of `.sql` files.**
- **A `.sql` file outside the project's folder is not included.**  List it and its sidecar yourself.
- **The package gives the compiler the [sidecar](sidecars.md#sidecar-files) of each `.sql` file too**, by the same rules.  The sidecar of `Users.sql` is the file `Users.sql.json` beside it.
- **The build reads the sidecar of each `.sql` file that a type claims.**  It reports each query that must be described and has no current entry in the sidecar: see [Output](settings.md#setting-output).
- **An included file is only handed to the compiler.**  The [`Source`](attributes.md#setting-source) of a type says which of the files the type claims.  SqlSource ignores a file that no type claims.

To turn the default off and list the files yourself:

```xml
<PropertyGroup>
    <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Include="Queries/**/*.sql" />
    <AdditionalFiles Include="Queries/**/*.sql.json" />
</ItemGroup>
```

- **`false` turns the default off for sidecars too.**  So list the sidecars as well, as the sample does.
- **A build that [runs the tool](online-build.md#running-in-the-build) compiles a sidecar that it has just written for the first time.**  It compiles that sidecar whether you list it or not.
- **Every later build compiles only the sidecars that you list.**  This is true of a build offline and of a build in CI too.

[Running in the build](online-build.md), where `dotnet build` describes your queries, is a page of its own.

<a id="limits"></a>

## Limits

<a id="limit-file-added-by-a-target"></a>

### SQLSRC241 for a `.sql` file that a build target of your own adds

**What you see.**  A target in your project adds a `.sql` file to the `AdditionalFiles` items while the build runs.  For example, the target writes the file or copies it in.  The build compiles the file, but it reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query of the file that must be described.

**Why.**  `sqlsource` leaves out the queries of such a file, so it describes none of them.  In a project with one target framework, a build that [runs the tool](online-build.md#running-in-the-build) describes the file even without the `BeforeTargets` line.  In a project with several, the run of the tool in the build leaves the file out, as a `sqlsource describe` that you run yourself does.  A project that is not of the .NET SDK can have one more effect.  A project that sets `AfterMicrosoftNETSdkTargets` without keeping its value can have it too.  In either project, a target that runs later also leaves the file without some settings.  These are the settings of a `sqlsource.json` that only the file reaches, such as the one beside it.  No error says so.

**What to do.**  Tell your target to run before the package lists the files of the project.  This works wherever the target is declared:

```xml
<Target Name="AddGeneratedSql" BeforeTargets="SqlSourceListFiles">
    <ItemGroup>
        <AdditionalFiles Include="obj/generated/*.sql" />
    </ItemGroup>
</Target>
```

Change the name and the path to your own.  `BeforeTargets="SqlSourceListFiles"` is the line that matters.  The same line also gives the file the settings that it was without.
