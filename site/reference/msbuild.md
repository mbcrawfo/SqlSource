# MSBuild properties and the build

<a id="properties"></a>

## The properties

A project can set five MSBuild properties of SqlSource.  Write each in the project file, in a `Directory.Build.props` above it, as `-p:` on the command line, or as a variable of the environment.

| Property | What it sets | Default | Section |
|----|----|----|----|
| `SqlSourceIncludeFiles` | Whether the package gives the compiler every `.sql` file and sidecar under the project's folder.  `false` turns this off | On | [Which `.sql` files are included](#included-files) |
| `SqlSourceOnline` | Whether a build runs `dotnet sqlsource describe` before it compiles: `auto`, `true` or `false` | `auto` | [Running the tool in the build](#online-build) |
| `SqlSourceEnvFile` | The `.env` file that a build reads its connections from: a path, or `none` | The `.env` in the project's folder | [The build's `.env` file](#setting-env-file) |
| `SqlSourcePublicExceptions` | Whether the exceptions that the query methods throw are `public`.  `true` makes them `public` | Off: they are `internal` | [Public exceptions](exceptions.md#public-exceptions) |
| `SqlSourceRepositoryInterfaces` | Whether the project's repositories get their interfaces.  `false` turns them off | On | [Repository interfaces](repositories.md#repository-interfaces) |

<a id="included-files"></a>

## Which `.sql` files are included

The package hands every `.sql` file under the project's folder to the compiler, as an `AdditionalFiles` item: that is how a source generator sees a file that is not C#.  The output and intermediate folders are left out, and so are folders whose names start with a dot, as the SDK leaves them out of your C# files.  The `sqlsource` tool reads the same list of `.sql` files.  A `.sql` file outside the project's folder is not included; list it, and its sidecar, yourself.

The package hands the [sidecar](../concepts.md#sidecar) beside a `.sql` file to the compiler too, the file `Users.sql.json` for `Users.sql`, under the same rules.  The build reads the sidecar of each `.sql` file that a type claims, and reports a query that must be described and has no current entry in it: see [Output](settings.md#setting-output).

To leave some files out:

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

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

Turning the default off turns it off for sidecars too, so list them as well, as the example does.  A build that [runs the tool](#running-in-the-build) compiles a sidecar it has just written for the first time, listed or not; every later build, like a build offline or in CI, compiles only the sidecars you list.  A file the project includes is only handed to the compiler.  Which of them belong to a type is the type's [`Source`](attributes.md#setting-source), and a file no type claims is ignored.

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

With a [`.env` file](connections.md#env-file) like the one above, `dotnet build` runs `dotnet sqlsource describe` on the project before it compiles.  So you do not run `describe` after you change a query: the build describes it, compiles the models and methods of what your database says, and reports a query that the database rejects as a build error at the query, here a table named `user` where the schema has `users`.  A build with no connection, a build in CI, and a build of a project that turns it off build from the committed sidecars, as a build without the tool does.  Commit the sidecars that a build writes, as you commit those that `describe` writes.

- **What turns it on.**  By default a build runs the tool when it finds a connection: the variable `SQLSOURCE_CONNECTION` in the environment, or the `.env` file in the project's folder, or the one [`SqlSourceEnvFile`](#setting-env-file) names.  A `.env` in a folder above the project's does not count, and is not read.  A build in CI never runs it.  [`SqlSourceOnline`](#online-build), a property of the project, turns it on whatever the build finds, or off.
- **Which builds.**  `dotnet build`, `dotnet run`, `dotnet publish`, a rebuild, and the build command of an IDE.  An IDE that loads the project, or reads it again after you save a file, does not run the tool, and nor does a build of the `Compile` target alone.  This was checked in Rider on macOS; Visual Studio, Visual Studio Code, and Rider on another system have not been checked, and may differ.
- **Only when something changed.**  A build runs the tool only when something it reads has changed since the tool last succeeded: a `.sql` file, a C# file, a settings file, a sidecar, changed or deleted, the `.env` file, the tool manifest, which updating the tool rewrites, the version of the `SqlSource` package, the value of `SqlSourceOnline`, or which file [`SqlSourceEnvFile`](#setting-env-file) names.  A build after which nothing changed does not start it.  A change to the schema changes none of them: run `describe --force`, as [after a schema change](describing.md#after-a-schema-change) says.
- **It describes the whole project**, as `describe` with no path does: it writes the sidecar of each file whose queries changed, deletes a sidecar that no query needs any more, and the same build compiles with what it wrote, a sidecar it wrote for the first time too.  Otherwise it compiles the sidecars your project lists, as a build offline or in CI does: a project that [lists its own files](#included-files), or a `.sql` file from outside its folder, lists their sidecars too, or the next build reports [SQLSRC241](../diagnostics.md#sqlsrc241) for the queries of a sidecar it left out, as CI would.  It reads the `.env` in the project's folder, or the one `SqlSourceEnvFile` names, in a build of a solution too, and never one in a folder above it.  It gets the build's environment, so a connection variable that your IDE sets reaches it.
- **The tool must be installed** in the repository's tool manifest and restored, as [install the tool](cli.md#install-the-tool) says, at the version of the `SqlSource` package.  A build that cannot start it is [SQLSRC026](../diagnostics.md#sqlsrc026), a warning, and the build goes on with the committed sidecars; under `SqlSourceOnline` `true` it is the error [SQLSRC025](../diagnostics.md#sqlsrc025).  A tool older than the package, which does not know what the build asks of it, is [SQLSRC027](../diagnostics.md#sqlsrc027).

**Offline.**  A `.env` file or a variable that gives no connection for any database with a query to describe, such as your application's own `.env`, starts the tool, which then says it is offline and changes nothing; the build goes on with the committed sidecars, and the next build tries again.  The generator still reports each query whose entry is missing or out of date, as [sidecar files](sidecars.md#sidecar-files) says.  When every entry is current the tool asks no database anything, and needs no connection.  A connection for some databases and not for others is [SQLSRC213](../diagnostics.md#sqlsrc213) at the queries of the others, and fails the build, as do a variable that two databases share, when it is set, [SQLSRC214](../diagnostics.md#sqlsrc214), and `SQLSOURCE_CONNECTION` set for several databases, [SQLSRC215](../diagnostics.md#sqlsrc215).

**Errors.**  Each error of the tool is an error of the build, at the `.sql` file and the place of the query, in the compiler's format, so your IDE lists it with the build's other errors.  The lines that `describe` writes under an error, such as `help:` and `see:`, follow it on the same line, each after `; `.

- **One failing query holds back its file.**  A sidecar is written whole or not at all, as [describing again](sidecars.md#describing-again) says, so the file keeps its old sidecar, and the generator then reports the failing query too, as having no entry ([SQLSRC241](../diagnostics.md#sqlsrc241)) or an entry older than the query ([SQLSRC244](../diagnostics.md#sqlsrc244)), and so each other query of the file whose entry was to change.  The tool's error says why, and the generator's what follows from it.  Fix the query and build again.
- **An error that the build's compiler reports too is listed once**, by the compiler, at its place in the file: a problem in a settings file ([SQLSRC015](../diagnostics.md#sqlsrc015) to [SQLSRC019](../diagnostics.md#sqlsrc019)), a `.sql` file that does not parse, a token with no default ([SQLSRC210](../diagnostics.md#sqlsrc210)) and an output that the dialect cannot describe ([SQLSRC209](../diagnostics.md#sqlsrc209)).  The exception is [SQLSRC019](../diagnostics.md#sqlsrc019) for a `database` that is not a valid name: only the tool checks that value, so the tool writes the error and the build lists it.  The build still fails on each of them.  While one is in the project the tool deletes no sidecar, and writes none for a `.sql` file that does not parse, so the generator may follow with the queries of that file as having no entry, [SQLSRC241](../diagnostics.md#sqlsrc241).  `describe` run by hand lists these errors itself.
- **A database that cannot be opened** is [SQLSRC224](../diagnostics.md#sqlsrc224), and fails the build.  Its `help:` line says how to build without the database: remove its connection, or set `SqlSourceOnline` to `false`.

**Several target frameworks.**  The tool runs once in a build, for the first framework that `TargetFrameworks` lists, which is the framework `describe` reads, and every framework compiles with the sidecars it wrote.  A build of another framework alone, with `-f` or through a reference from a project of that framework, still runs it, for the first framework.  A query that only another framework claims is not described: see [a limit](describing.md#limit-several-target-frameworks).

**In CI**, a build never runs the tool, whatever `SqlSourceOnline` says, so that a job checks the sidecars that were committed and does not write its own.  Describe in a step of its own, with `describe --check`: see [check in CI](describing.md#check-in-ci).

**What the build prints.**  The build shows the tool's errors at every verbosity, and nothing else that the tool prints.  The tool's [summary](cli.md#summary), each line of it after `sqlsource: summary: `, or its line that says it is offline, is a message of the detailed verbosity, which `dotnet build -tl:off -v:d` shows.  `dotnet build` in an interactive terminal shows no such message at any verbosity unless `-tl:off` turns its live display off; a binary log, `-bl`, holds them too.

**Logs.**  `--log` cannot be given to the build's run, and a failed build does not suggest it.  Set the variable `SQLSOURCE_LOG`, to any value, and each build that runs the tool writes the log of its run to a file of the project and framework in the intermediate folder, such as `obj/Debug/net10.0/App.csproj.SqlSource.log`; the variable's value is not used as a path, since projects built at once would write one file.  [Read a log before you share it](cli.md#logs).

**Your connection in the build's logs.**  The build itself reads only whether `SQLSOURCE_CONNECTION` is set, never its value nor what a `.env` file says, so neither reaches its logs, at any verbosity or in a binary log.  What the tool prints is in them, and the tool hides a connection there as it does in your terminal ([a limit](connections.md#limit-short-password)).  `MSBUILDLOGALLENVIRONMENTVARIABLES=1`, a setting of MSBuild, writes every variable of the environment into the build's logs, every connection variable among them: leave it unset on a machine whose environment holds a connection, or search such a log for your password before you share it.


<a id="online-build"></a>

## Running the tool in the build

```xml
<PropertyGroup>
    <SqlSourceOnline>false</SqlSourceOnline>
</PropertyGroup>
```

`SqlSourceOnline` says whether a build of the project runs `dotnet sqlsource describe` on it before it compiles, as [running in the build](#running-in-the-build) describes.  `false` above keeps the build on the committed sidecars on every machine.

| Value | The build runs the tool |
|----|----|
| `auto`, empty, or not set | When it finds a connection: the environment variable `SQLSOURCE_CONNECTION`, set and not blank, or the build's `.env` file, the one in the project's folder or the one [`SqlSourceEnvFile`](#setting-env-file) names, when it is there.  A `.env` in a folder above the project's is not looked for.  When the tool then finds no connection for any database with a query to describe, it says the build is offline, and the build goes on with the committed sidecars |
| `true` | Whether it finds a connection or not.  A database with a query to describe and no connection is then [SQLSRC213](../diagnostics.md#sqlsrc213), and a tool that cannot be started [SQLSRC025](../diagnostics.md#sqlsrc025) |
| `false` | Never |

- **The value is matched ignoring case** and the white space around it, so ` True ` is `true`.  Any other value, such as `yes`, is [SQLSRC024](../diagnostics.md#sqlsrc024), and the build stops.
- **A build in CI never runs the tool**, whatever the value, so that a job builds from the sidecars that were committed.  A build is in CI when the variable `CI` is `true`, ignoring case, `TF_BUILD` is `True`, or `TEAMCITY_VERSION` or `JENKINS_URL` is set: GitHub Actions, GitLab, Buildkite, Azure Pipelines, TeamCity and Jenkins each set one.  `CI=1` is not recognised.
- **A connection in a variable of a database's name alone**, such as `SQLSOURCE_CONNECTION_BILLING` with no `.env` file, is not found under `auto`.  Set `SqlSourceOnline` to `true` in your own environment, which MSBuild reads as a property of every project, rather than in the project, so that a machine with no database still builds.
- **The build's `.env` file turns `auto` on whatever it holds**, one that holds only your application's settings too: the build looks only whether it is there.  The tool then finds no connection and the build goes on offline, and each build starts the tool again while it finds none.  Set `false` to stop that, or `SqlSourceEnvFile` to `none`.  So does a symbolic link that leads to no file, as [the build's `.env` file](#setting-env-file) says.
- **A `.env` above the project's folder**, at the root of your solution or repository, or in your home folder, does not turn `auto` on, and the build says nothing of it: it builds from the committed sidecars.  To use such a file, name it with [`SqlSourceEnvFile`](#setting-env-file).
- **Where it is written.**  In the project file, in a `Directory.Build.props` above it for every project there, on the command line as `-p:SqlSourceOnline=false`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.

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

`SqlSourceEnvFile` names the [`.env` file](connections.md#env-file) that a build of the project reads its connections from, in place of the `.env` in the project's folder.  The `Directory.Build.props` above gives every project below it the one `.env` beside it, at the root of the repository: `$(MSBuildThisFileDirectory)` is the folder of the file that writes it.

| Value | The build reads |
|----|----|
| Empty, or not set | The `.env` in the project's folder, and never one in a folder above it |
| A path | That file.  A relative path is from the project's folder, so `../.env` is the `.env` of the folder above it |
| `none` | No `.env` file: the connections come from the environment alone |

- **The value is trimmed**, and `none` is matched ignoring case.  To name a file called `none`, write `./none`.
- **It decides whether `auto` runs the tool**, as [running the tool in the build](#online-build) says: the file named turns it on when it is there.  A path where there is no file, or a folder, turns nothing on and is no error.  Under `none` only `SQLSOURCE_CONNECTION` in the environment turns `auto` on.  A symbolic link that leads to no file is the exception: the build counts it as there and the tool reads nothing from it, so each build starts the tool, which goes on offline.  Remove the link, or set `SqlSourceEnvFile` to `none`.
- **A change to the file** runs the tool again at the next build, as a change to the project's own `.env` does.
- **It reaches only the build's run.**  A `describe` or `describe --watch` that you run yourself reads the `.env` in the folder of the project or solution it runs on, and `--env-file` names another, as [the `.env` file](connections.md#env-file) says: with the example above, `dotnet sqlsource describe src/App/App.csproj --env-file .env` at the root of the repository reads the file the build reads.
- **Where it is written.**  In the project file, in a `Directory.Build.props` above it for every project there, on the command line as `-p:SqlSourceEnvFile=none`, or as a variable of the environment.  It is a property of the build, not a key of a settings file.

<a id="limits"></a>

## Limits

<a id="limit-file-added-by-a-target"></a>

### SQLSRC241 for a `.sql` file that a build target of your own adds

- **A `.sql` file that one of your own build targets adds is not described.**  This is about a target in your project that adds a `.sql` file to the `AdditionalFiles` items while the build runs, such as one that writes a file or copies it in.  The build compiles the file, but `sqlsource` leaves out the file's queries, so it describes none of them, and the build reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each that must be described.  To make it work, tell your target to run before the package lists the project's files, wherever the target is declared:

  ```xml
  <Target Name="AddGeneratedSql" BeforeTargets="SqlSourceListFiles">
      <ItemGroup>
          <AdditionalFiles Include="obj/generated/*.sql" />
      </ItemGroup>
  </Target>
  ```

  Change the name and the path to your own.  `BeforeTargets="SqlSourceListFiles"` is the line that matters.  A build that [runs the tool](#running-in-the-build) describes such a file without it in a project with one target framework, and leaves it out, as `sqlsource` does, in a project with several.  In a project that is not of the .NET SDK, or one that sets `AfterMicrosoftNETSdkTargets` without keeping its value, a target that runs later also leaves the file without the settings of a `sqlsource.json` that only it reaches, such as the one beside it, with no error; the same line fixes that too.
