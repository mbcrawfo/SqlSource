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
| `SqlSourceOnline` | Whether a build runs `dotnet sqlsource describe` before it compiles: `auto`, `true` or `false` | `auto` | [Running the tool in the build](#online-build) |
| `SqlSourceEnvFile` | The `.env` file that a build reads its connections from: a path, or `none` | The `.env` in the project's folder | [The build's `.env` file](#setting-env-file) |
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
- **A build that [runs the tool](#running-in-the-build) compiles a sidecar that it has just written for the first time.**  It compiles that sidecar whether you list it or not.
- **Every later build compiles only the sidecars that you list.**  This is true of a build offline and of a build in CI too.

<a id="running-in-the-build"></a>

## Running in the build

```dotenv
SQLSOURCE_CONNECTION_POSTGRES="Host=localhost;Database=app;Username=app;Password=secret"
```

```console
$ dotnet build -tl:off
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist; query: GetUser, database postgres, postgres 18.6, Npgsql 10.0.3; step: describe parameters; server: 42P01: relation "user" does not exist; server: at character 15; see: https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc225 [/work/App/App.csproj]
/work/App/Queries/Users.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'methods' needs one.  Run 'sqlsource describe'. [/work/App/App.csproj]
```

With a [`.env` file](connections.md#env-file) like the one above, `dotnet build` runs `dotnet sqlsource describe` on the project before it compiles.  So you do not run `describe` after you change a query.  The build describes the query, and compiles the models and methods from what your database says.  If the database rejects a query, the build reports a build error at the query.  In the sample, the query names a table `user`, and the schema has `users`.

Some builds use the committed sidecars, as a build without the tool does.  These are a build that finds no connection, a build in CI, and a build of a project that turns the tool off.  Commit the sidecars that a build writes, as you commit the sidecars that `describe` writes.

- **What turns it on.**  By default, a build runs the tool when it finds `SQLSOURCE_CONNECTION` in the environment, or its `.env` file.  [`SqlSourceOnline`](#online-build) gives the rules: what the build looks for, the builds in CI, and how to turn the tool on or off.
- **Which builds.**  `dotnet build`, `dotnet run`, `dotnet publish`, a rebuild, and the build command of an IDE run the tool.  An IDE that loads the project, or reads it again after you save a file, does not run the tool.  A build of the `Compile` target alone does not run it either.  This was checked in Rider on macOS.  Visual Studio, Visual Studio Code, and Rider on another system have not been checked, and can differ.
- **Only when something changed.**  A build runs the tool only when one of these changed after the tool last succeeded:
  - a `.sql` file, a C# file, a settings file or a sidecar, changed or deleted;
  - the `.env` file;
  - the tool manifest, which an update of the tool rewrites;
  - the version of the `SqlSource` package;
  - the value of `SqlSourceOnline`;
  - which file [`SqlSourceEnvFile`](#setting-env-file) names.

  If nothing changed after the last run, the build does not start the tool.  A change to the schema changes none of these: run `describe --force`, as [after a schema change](describing.md#after-a-schema-change) says.
- **The tool describes the whole project**, as `describe` with no path does.  It writes the sidecar of each file whose queries changed, and deletes a sidecar that no query needs any more.  The same build compiles with what the tool wrote, and this includes a sidecar that the tool wrote for the first time.
- **Apart from a sidecar written for the first time, a build compiles only the sidecars that your project lists.**  A build offline or in CI does the same.  So a project that [lists its own files](#included-files) must list their sidecars too, and so must a project with a `.sql` file from outside its folder.  If the project leaves out a sidecar, the next build reports [SQLSRC241](../diagnostics.md#sqlsrc241) for the queries of that sidecar, as CI would.
- **The tool reads the `.env` in the project's folder, or the one that `SqlSourceEnvFile` names.**  This is true in a build of a solution too.  Unless `SqlSourceEnvFile` names one, the tool never reads a `.env` in a folder above the project's folder.
- **The tool gets the environment of the build.**  So a connection variable that your IDE sets reaches the tool.
- **The tool must be installed** in the tool manifest of the repository and restored, at the version of the `SqlSource` package.  [Install the tool](cli.md#install-the-tool) says how.
- **If the build cannot start the tool, it reports [SQLSRC026](../diagnostics.md#sqlsrc026), a warning.**  The build goes on with the committed sidecars.  Under `SqlSourceOnline` `true`, it reports the error [SQLSRC025](../diagnostics.md#sqlsrc025) instead.
- **A tool older than the package is [SQLSRC027](../diagnostics.md#sqlsrc027).**  Such a tool does not know what the build asks of it.

**Offline.**  A `.env` file or a variable can start the tool and give no connection for any database with a query to describe.  Your application's own `.env` file is an example.  Then the tool says that it is offline, and changes nothing.

- **The build goes on with the committed sidecars**, and the next build tries again.
- **The generator still reports each query whose entry is missing or out of date**, as [sidecar files](sidecars.md#sidecar-files) says.
- **If every entry is current, the tool asks no database anything**, and needs no connection.
- **A connection for some databases and not for others is [SQLSRC213](../diagnostics.md#sqlsrc213)** at the queries of the others, and fails the build.
- **A variable that two databases share fails the build when it is set**: [SQLSRC214](../diagnostics.md#sqlsrc214).
- **`SQLSOURCE_CONNECTION` set for several databases fails the build**: [SQLSRC215](../diagnostics.md#sqlsrc215).

**Errors.**  Each error of the tool is an error of the build, at the `.sql` file and the place of the query.  It is in the format of the compiler, so your IDE lists it with the other errors of the build.  `describe` writes lines under an error, such as `help:` and `see:`.  In the build, each of these lines follows the error on the same line, after `; `.

- **One failing query holds back its file.**  The tool writes a sidecar whole or not at all, as [describing again](sidecars.md#describing-again) says, so the file keeps its old sidecar.  The generator then reports the failing query too, as having no entry ([SQLSRC241](../diagnostics.md#sqlsrc241)) or an entry older than the query ([SQLSRC244](../diagnostics.md#sqlsrc244)).  It reports each other query of the file whose entry was to change in the same way.  The error of the tool says why, and the error of the generator says what follows from it.  Fix the query and build again.
- **An error that the compiler of the build reports too is listed once**, by the compiler, at its place in the file.  These errors are:
  - a problem in a settings file, [SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC019](../diagnostics.md#sqlsrc019);
  - a `.sql` file that does not parse;
  - a token with no default, [SQLSRC210](../diagnostics.md#sqlsrc210);
  - an output that the dialect cannot describe, [SQLSRC209](../diagnostics.md#sqlsrc209).
- **The exception is [SQLSRC019](../diagnostics.md#sqlsrc019) for a `database` that is not a valid name**: only the tool checks that value, so the tool writes the error and the build lists it.
- **The build still fails on each of these errors.**
- **`describe` that you run by hand lists these errors itself.**
- **While one of these errors is in the project, the tool deletes no sidecar.**  The tool also writes no sidecar for a `.sql` file that does not parse.  So the generator can then report the queries of that file as having no entry, [SQLSRC241](../diagnostics.md#sqlsrc241).
- **A database that cannot be opened is [SQLSRC224](../diagnostics.md#sqlsrc224)**, and fails the build.  Its `help:` line says how to build without the database: remove its connection, or set `SqlSourceOnline` to `false`.

**Several target frameworks.**  The tool runs once in a build, for the first framework that `TargetFrameworks` lists.  This is the framework that `describe` reads.  Every framework compiles with the sidecars that the tool wrote.  A build of another framework alone still runs the tool, for the first framework: a build with `-f`, or a build through a reference from a project of that framework.  A query that only another framework claims is not described: see [a limit](describing.md#limit-several-target-frameworks).

**In CI**, a build never runs the tool, whatever `SqlSourceOnline` says: see [running the tool in the build](#online-build).  Describe in a step of its own, with `describe --check`: see [check in CI](describing.md#check-in-ci).

**What the build prints.**  The build shows the errors of the tool at every verbosity, and nothing else that the tool prints.  The [summary](cli.md#summary) of the tool is a message of the detailed verbosity, each line of it after `sqlsource: summary: `.  So is the line of the tool that says it is offline.  `dotnet build -tl:off -v:d` shows these messages.  In an interactive terminal, `dotnet build` shows no such message at any verbosity unless `-tl:off` turns its live display off.  A binary log, `-bl`, holds them too.

**Logs.**  You cannot give `--log` to the run of the build, and a failed build does not suggest it.  To get a log, set the variable `SQLSOURCE_LOG` to any value.  Then each build that runs the tool writes the log of its run to a file of the project and framework in the intermediate folder, such as `obj/Debug/net10.0/App.csproj.SqlSource.log`.  The build does not use the value of the variable as a path, since projects built at once would write one file.  [Read a log before you share it](cli.md#logs).

**Your connection in the build's logs.**  The build itself reads only whether `SQLSOURCE_CONNECTION` is set.  It never reads the value of the variable or what a `.env` file says, so neither reaches the logs of the build, at any verbosity or in a binary log.  What the tool prints is in the logs, and the tool hides a connection there as it does in your terminal ([a limit](connections.md#limit-short-password)).  `MSBUILDLOGALLENVIRONMENTVARIABLES=1`, a setting of MSBuild, writes every variable of the environment into the logs of the build, every connection variable among them.  On a machine whose environment holds a connection, do one of these:

- Leave `MSBUILDLOGALLENVIRONMENTVARIABLES` unset.
- Search such a log for your password before you share it.

<a id="online-build"></a>

## Running the tool in the build

```xml
<PropertyGroup>
    <SqlSourceOnline>false</SqlSourceOnline>
</PropertyGroup>
```

`SqlSourceOnline` says whether a build of the project runs `dotnet sqlsource describe` on it before it compiles.  [Running in the build](#running-in-the-build) says what the build then does.  The `false` of the sample keeps the build on the committed sidecars on every machine.

| Value | The build runs the tool |
|----|----|
| `auto`, empty, or not set | When it finds a connection: the environment variable `SQLSOURCE_CONNECTION`, set and not blank, or the build's `.env` file, the one in the project's folder or the one [`SqlSourceEnvFile`](#setting-env-file) names, when it is there.  A `.env` in a folder above the project's is not looked for.  When the tool then finds no connection for any database with a query to describe, it says the build is offline, and the build goes on with the committed sidecars |
| `true` | Whether it finds a connection or not.  A database with a query to describe and no connection is then [SQLSRC213](../diagnostics.md#sqlsrc213), and a tool that cannot be started [SQLSRC025](../diagnostics.md#sqlsrc025) |
| `false` | Never |

- **The build ignores case and the white space around the value.**  So ` True ` is `true`.
- **Under `auto`, the build does not find a connection in a variable of a database's name alone.**  An example is `SQLSOURCE_CONNECTION_BILLING` with no `.env` file.  To run the tool with such a variable, set `SqlSourceOnline` to `true` in your own environment.  Do not set it in the project, so that a machine with no database still builds.  MSBuild reads a variable of the environment as a property of every project.
- **The build's `.env` file turns `auto` on whatever it holds.**  The build looks only at whether the file is there.  So a file that holds only the settings of your application turns `auto` on too.  The tool then finds no connection and the build goes on offline, and each build starts the tool again while it finds none.  To stop that, set `SqlSourceOnline` to `false`, or `SqlSourceEnvFile` to `none`.
- **A symbolic link that leads to no file turns `auto` on too.**  The build counts it as there, as [the build's `.env` file](#setting-env-file) says.
- **A `.env` above the project's folder does not turn `auto` on.**  This is true of a `.env` at the root of your solution or repository, or in your home folder.  The build says nothing of such a file, and builds from the committed sidecars.  To use such a file, name it with [`SqlSourceEnvFile`](#setting-env-file).
- **Where it is written.**  You can write `SqlSourceOnline` in the project file, or in a `Directory.Build.props` above it for every project there.  You can also give it on the command line as `-p:SqlSourceOnline=false`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.
- **A build in CI never runs the tool**, whatever the value.  So a job in CI builds from and checks the sidecars that were committed, and does not write its own.  A build is in CI when one of these is true:
  - `CI` is `true`, `yes` or `on`, ignoring case;
  - `TF_BUILD` is `true`, `yes` or `on`, ignoring case;
  - `TEAMCITY_VERSION` is set and not empty;
  - `JENKINS_URL` is set and not empty.

  MSBuild reads `!false`, `!off` and `!no` as `true` too.  It reads each of the four as a property, so a property of that name counts as the variable does, such as `-p:CI=true`.  GitHub Actions, GitLab, Buildkite, Azure Pipelines, TeamCity and Jenkins each set one of them.  `CI=1` is not recognised.
- **Any value that the table does not list, such as `yes`, is [SQLSRC024](../diagnostics.md#sqlsrc024)**, and the build stops.

<a id="setting-env-file"></a>

## The build's `.env` file

```xml
<!-- Directory.Build.props, at the root of the repository -->
<Project>
    <PropertyGroup>
        <SqlSourceEnvFile>$(MSBuildThisFileDirectory).env</SqlSourceEnvFile>
    </PropertyGroup>
</Project>
```

`SqlSourceEnvFile` names the [`.env` file](connections.md#env-file) that a build of the project reads its connections from, in place of the `.env` in the project's folder.  The `Directory.Build.props` of the sample gives every project below it the one `.env` beside it, at the root of the repository.  `$(MSBuildThisFileDirectory)` is the folder of the file that writes the property.

| Value | The build reads |
|----|----|
| Empty, or not set | The `.env` in the project's folder, and never one in a folder above it |
| A path | That file.  A relative path is from the project's folder, so `../.env` is the `.env` of the folder above it |
| `none` | No `.env` file: the connections come from the environment alone |

- **The build trims the value, and matches `none` ignoring case.**  To name a file called `none`, write `./none`.
- **The file decides whether `auto` runs the tool**, as [running the tool in the build](#online-build) says.  If the named file is there, it turns `auto` on.  A path where there is no file, or a path to a folder, turns nothing on and is no error.
- **Under `none`, only `SQLSOURCE_CONNECTION` in the environment turns `auto` on.**
- **A symbolic link that leads to no file is the exception.**  The build counts it as there, and the tool reads nothing from it.  So each build starts the tool, which goes on offline.  To stop that, remove the link, or set `SqlSourceEnvFile` to `none`.
- **A change to the file runs the tool again at the next build**, as a change to the `.env` in the project's folder does.
- **It reaches only the run of the tool in the build.**  A `describe` or `describe --watch` that you run yourself reads the `.env` in the folder of the project or solution that it runs on.  `--env-file` names another file, as [the `.env` file](connections.md#env-file) says.  With the sample, `dotnet sqlsource describe src/App/App.csproj --env-file .env` at the root of the repository reads the file that the build reads.
- **Where it is written.**  You can write `SqlSourceEnvFile` in the project file, or in a `Directory.Build.props` above it for every project there.  You can also give it on the command line as `-p:SqlSourceEnvFile=none`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.

<a id="limits"></a>

## Limits

<a id="limit-file-added-by-a-target"></a>

### SQLSRC241 for a `.sql` file that a build target of your own adds

**What you see.**  A target in your project adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as a target that writes the file or copies it in.  The build compiles the file, but it reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query of the file that must be described.

**Why.**  `sqlsource` leaves out the queries of such a file, so it describes none of them.  In a project with one target framework, a build that [runs the tool](#running-in-the-build) describes the file even without the `BeforeTargets` line.  In a project with several, the run of the tool in the build leaves the file out, as a `sqlsource describe` that you run yourself does.  A project that is not of the .NET SDK can have one more effect, and so can a project that sets `AfterMicrosoftNETSdkTargets` without keeping its value.  There, a target that runs later also leaves the file without some settings.  These are the settings of a `sqlsource.json` that only the file reaches, such as the one beside it.  No error says so.

**What to do.**  Tell your target to run before the package lists the files of the project.  This works wherever the target is declared:

```xml
<Target Name="AddGeneratedSql" BeforeTargets="SqlSourceListFiles">
    <ItemGroup>
        <AdditionalFiles Include="obj/generated/*.sql" />
    </ItemGroup>
</Target>
```

Change the name and the path to your own.  `BeforeTargets="SqlSourceListFiles"` is the line that matters.  The same line also gives the file the settings that it was without.
