# SqlSource.Tool

The `sqlsource` command of [SqlSource](https://github.com/mbcrawfo/SqlSource), a C# source generator for SQL queries.  It asks a database to describe the queries of a project, so that the generator can give them types.

This version finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It reports what would keep one from being described, and describes nothing yet.

```console
$ dotnet sqlsource describe
$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
$ dotnet sqlsource describe src/App/Queries/Users.sql
```

`describe` takes a `.sln`, `.slnx` or `.csproj` file, or a directory that holds exactly one, and the current directory when none is given.  In a solution it runs on the C# projects that use the SqlSource package; `--project`, which may be given several times, names the ones to run on.  A path that ends in `.sql` restricts the run to that file, and may be given several times.

The tool reads `[SqlSourceGenerate]` from the source without compiling it, so `Path` and `Output` must be written as literals: `Path = "Queries"`, `Output = GeneratorOutput.Models`.

The tool asks MSBuild about each project, so it needs the .NET SDK, and a project must have been restored: the tool does not restore, and reports a project that was not.  The SqlSource package of a project and the tool should be of one version.  See the [readme of the repository](https://github.com/mbcrawfo/SqlSource/blob/main/README.md) for what SqlSource does today.
