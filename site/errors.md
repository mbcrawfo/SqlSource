# How errors are reported

This page tells you how SqlSource reports a problem: how severe it is, where you see it, what it costs and what reports it.  Each error, with its cause and its fix, is on the page [Diagnostics](diagnostics.md).

<a id="severity"></a>

## Errors, warnings and information

Every problem that SqlSource finds is an error.  You cannot turn an error off or make it a warning.  Eight ids are the exceptions.

These six are warnings:

- [SQLSRC026](diagnostics.md#sqlsrc026): the build cannot start the `sqlsource` tool, and goes on with the sidecars it has.
- [SQLSRC031](diagnostics.md#sqlsrc031): a connection is marked for a logical database that no repository query uses.
- [SQLSRC121](diagnostics.md#sqlsrc121): a parameter of a SQL Server query is written in two spellings.
- [SQLSRC240](diagnostics.md#sqlsrc240): another version of the tool wrote a sidecar, or an entry of one.
- [SQLSRC245](diagnostics.md#sqlsrc245): an entry of a sidecar names no query that needs it.
- [SQLSRC284](diagnostics.md#sqlsrc284): a short name of a C# type that you chose finds a type of a package that the project does not reference itself.

These two are information, about the version of a database driver:

- [SQLSRC248](diagnostics.md#sqlsrc248), for Npgsql.
- [SQLSRC268](diagnostics.md#sqlsrc268), for Microsoft.Data.SqlClient.

The section of each of the eight says how a project can configure it.

Information is not in the output of `dotnet build`.  An IDE that lists information shows it, and the build log has it at the detailed verbosity, as [SQLSRC248](diagnostics.md#sqlsrc248) says.

<a id="where-an-error-is-reported"></a>

## Where an error is reported

The build reports its errors and warnings in its output, and your IDE lists them with the compiler's.  Each one names the file, the line and the column of the problem, where it has them:

- **A `.sql` file.**  An error in a `.sql` file is at its line and column in that file.  An error about a query is at the query's name, or at the marker that the error is about.
- **A sidecar.**  An error in a sidecar is at its line and column in the sidecar.
- **A settings file.**  An error in a settings file, `sqlsource.json`, is at its line and column in that file.
- **A C# file.**  An error about a type that carries `[SqlSourceGenerate]` is at the type's name, at its attribute, or at the member that the error names.
- **The project file.**  The errors of a build that runs the tool, [SQLSRC024](diagnostics.md#sqlsrc024) to [SQLSRC027](diagnostics.md#sqlsrc027), are at the project file.

Some errors are about the project's references, and have no file and line.  In the build's output, such an error starts with `CSC` in place of a path.  These are reported once for the project:

- [SQLSRC022](diagnostics.md#sqlsrc022), [SQLSRC023](diagnostics.md#sqlsrc023) and [SQLSRC038](diagnostics.md#sqlsrc038), about SqlSource's exceptions in the projects that the project references.
- [SQLSRC247](diagnostics.md#sqlsrc247) and [SQLSRC248](diagnostics.md#sqlsrc248), about the version of Npgsql.
- [SQLSRC267](diagnostics.md#sqlsrc267) and [SQLSRC268](diagnostics.md#sqlsrc268), about the version of Microsoft.Data.SqlClient.

Two more have no file and line, and the project can get more than one of each:

- [SQLSRC273](diagnostics.md#sqlsrc273), a driver that the project does not reference, is reported once for each driver that is missing, Npgsql or Microsoft.Data.SqlClient.
- [SQLSRC282](diagnostics.md#sqlsrc282), a library that is too old, is reported once for each package that is too old.

[SQLSRC021](diagnostics.md#sqlsrc021), a compiler that is too old, comes before the build compiles anything.

<a id="what-an-error-costs"></a>

## What an error costs

Most errors cost part of what SqlSource generates, until you fix them.  What an error costs depends on what it is about.

- **How a `.sql` file is written.**  An error from [SQLSRC101](diagnostics.md#sqlsrc101) to [SQLSRC120](diagnostics.md#sqlsrc120), or [SQLSRC126](diagnostics.md#sqlsrc126), costs the file all its members and models.  The other files of the type keep theirs.  The section of [SQLSRC120](diagnostics.md#sqlsrc120) says when it costs only a type that keeps comments.
- **Where a query's members go.**  An error from [SQLSRC122](diagnostics.md#sqlsrc122) to [SQLSRC125](diagnostics.md#sqlsrc125) costs the file its members and models for the type that the error is reported for.  Another type that claims the file keeps them.  The section of [SQLSRC123](diagnostics.md#sqlsrc123) says when it costs one method alone.
- **An empty list.**  [SQLSRC127](diagnostics.md#sqlsrc127) is reported only for a query whose output is `methods` or `repository`.  It costs that query its method, and nothing else.
- **Describing a query, or its sidecar.**  An error such as [SQLSRC241](diagnostics.md#sqlsrc241) costs the query that it is about its models.  [SQLSRC209](diagnostics.md#sqlsrc209) costs its models to each query of the file that needs an entry.  The other members of the file stay in place.
- **A repository.**  Each of [SQLSRC028](diagnostics.md#sqlsrc028) to [SQLSRC034](diagnostics.md#sqlsrc034), and [SQLSRC036](diagnostics.md#sqlsrc036), costs a method to each repository query of the logical databases that it is about.  The warning [SQLSRC031](diagnostics.md#sqlsrc031) is not one of them.  The models of those queries are still generated.

A query can get several errors at once.  If a query has an error about describing it, its sidecar, its models or its method, the query's output decides what it keeps:

- If the output is `models`, the query keeps its constant or its token method.
- If the output is `methods` or `repository`, the query gets no method until you fix every error.

The section of an error says when it costs less.  [SQLSRC297](diagnostics.md#sqlsrc297) costs only the collection method, [SQLSRC299](diagnostics.md#sqlsrc299) only the synchronous methods, and [SQLSRC281](diagnostics.md#sqlsrc281) costs nothing.

While a `.sql` file has an error of its own, the build checks less of its sidecar: see [sidecar files](tool.md#sidecar-files).

<a id="who-reports"></a>

## Who reports an error: the build, the tool, or both

Two things report errors: the build of your project, and the `sqlsource` tool, such as `dotnet sqlsource describe`.  Each section of [Diagnostics](diagnostics.md) has a line under its title that says which one reports its error.

| Reported by | Ids | What you see |
|----|----|----|
| The build alone | `SQLSRC001` to `SQLSRC013`, `SQLSRC020` to `SQLSRC038`, `SQLSRC121` to `SQLSRC125`, `SQLSRC127`, `SQLSRC237` to `SQLSRC261`, `SQLSRC267` to `SQLSRC270`, `SQLSRC272` to `SQLSRC284`, and `SQLSRC297` to `SQLSRC299` | The build reports them, and the tool never does.  [SQLSRC020](diagnostics.md#sqlsrc020), a key that a settings file repeats, is the build's alone.  [SQLSRC122](diagnostics.md#sqlsrc122) to [SQLSRC125](diagnostics.md#sqlsrc125) depend on the type that claims the file, and [SQLSRC127](diagnostics.md#sqlsrc127) on the query's output |
| The tool alone | `SQLSRC200` to `SQLSRC208`, `SQLSRC211` to `SQLSRC236`, `SQLSRC262` to `SQLSRC266`, `SQLSRC271`, and `SQLSRC285` to `SQLSRC296` | The tool reports them.  A build shows them only when it runs the tool, as [running in the build](tool.md#running-in-the-build) says |
| The build and the tool: how a `.sql` file is written | `SQLSRC101` to `SQLSRC120`, and `SQLSRC126` | The tool reports them with the same id as the build.  The problem is the same, and so is the fix |
| The build and the tool: a settings file | `SQLSRC015` to `SQLSRC019` | The tool reports them at the same line and column as the build.  It reports them only for a settings file that applies to a `.sql` file or a C# file of a project it runs on.  It reports each once in a run, however many of those projects the file applies to.  The tool reports [SQLSRC019](diagnostics.md#sqlsrc019) for `dialect`, `output` and `database` alone.  The build reports it for every key but `database`, whose value the tool alone checks |
| The build and the tool: a query that cannot be described | `SQLSRC209` and `SQLSRC210` | Each reports them at the same place: the query that must be described and cannot be |

In a build that runs the tool, the build lists each error that both report once: see [running in the build](tool.md#running-in-the-build).

<a id="the-tools-errors"></a>

## The tool's errors

```console
$ dotnet sqlsource describe --check
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': the query has changed since it was described
    help: run 'dotnet sqlsource describe' and commit the sidecars
    see: https://mbcrawfo.github.io/SqlSource/diagnostics.html#sqlsrc219
```

The tool prints its errors itself, in your terminal.  Each error has the format of a build error: its place, `error`, the id and the message.  The place is the file, with the line and column when the error has them.  An error with no file starts with `sqlsource` in place of a path.

Lines under the error tell you more.  A `help:` line, when the error has one, says what to do.  The last line, `see:`, links to the error's section of [Diagnostics](diagnostics.md).

The tool's exit code says whether it reported an error: see [exit codes](tool.md#exit-codes).  In a build that runs the tool, each of its errors is an error of the build: see [running in the build](tool.md#running-in-the-build).

<a id="errors-by-area"></a>

## Errors by area

Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 to 127 are about the contents of a `.sql` file.

| Area | Ids | Reported by | Where |
|----|----|----|----|
| Project and attribute | [SQLSRC001](diagnostics.md#sqlsrc001), [SQLSRC002](diagnostics.md#sqlsrc002), [SQLSRC003](diagnostics.md#sqlsrc003), [SQLSRC004](diagnostics.md#sqlsrc004), [SQLSRC005](diagnostics.md#sqlsrc005), [SQLSRC007](diagnostics.md#sqlsrc007), [SQLSRC008](diagnostics.md#sqlsrc008), [SQLSRC009](diagnostics.md#sqlsrc009), [SQLSRC012](diagnostics.md#sqlsrc012), [SQLSRC013](diagnostics.md#sqlsrc013) | The build | In the C# file, at the type's name or its `[SqlSourceGenerate]`.  `SQLSRC008`, `SQLSRC009` and `SQLSRC013` are in the `.sql` file |
| Settings files | [SQLSRC015](diagnostics.md#sqlsrc015), [SQLSRC016](diagnostics.md#sqlsrc016), [SQLSRC017](diagnostics.md#sqlsrc017), [SQLSRC018](diagnostics.md#sqlsrc018), [SQLSRC019](diagnostics.md#sqlsrc019), [SQLSRC020](diagnostics.md#sqlsrc020) | The build and the tool, but `SQLSRC020` the build alone | At the line and column in the settings file |
| The compiler | [SQLSRC021](diagnostics.md#sqlsrc021) | The build | Before the build compiles anything |
| Projects that share SqlSource's exceptions | [SQLSRC022](diagnostics.md#sqlsrc022), [SQLSRC023](diagnostics.md#sqlsrc023) | The build | No file and line, once for the project |
| A build that runs the tool | [SQLSRC024](diagnostics.md#sqlsrc024), [SQLSRC025](diagnostics.md#sqlsrc025), [SQLSRC026](diagnostics.md#sqlsrc026), [SQLSRC027](diagnostics.md#sqlsrc027) | The build | At the project file |
| Repositories | [SQLSRC028](diagnostics.md#sqlsrc028), [SQLSRC029](diagnostics.md#sqlsrc029), [SQLSRC030](diagnostics.md#sqlsrc030), [SQLSRC031](diagnostics.md#sqlsrc031), [SQLSRC032](diagnostics.md#sqlsrc032), [SQLSRC033](diagnostics.md#sqlsrc033), [SQLSRC034](diagnostics.md#sqlsrc034), [SQLSRC035](diagnostics.md#sqlsrc035), [SQLSRC036](diagnostics.md#sqlsrc036), [SQLSRC037](diagnostics.md#sqlsrc037) | The build | In the C# file, at the type's `[SqlSourceGenerate]` or at the member that the error names.  Only for a type that claims a query whose output is `repository` |
| Projects that share SqlSource's exceptions across frameworks | [SQLSRC038](diagnostics.md#sqlsrc038) | The build | No file and line, once for the project |
| `.sql` files | [SQLSRC101](diagnostics.md#sqlsrc101), [SQLSRC102](diagnostics.md#sqlsrc102), [SQLSRC103](diagnostics.md#sqlsrc103), [SQLSRC104](diagnostics.md#sqlsrc104), [SQLSRC105](diagnostics.md#sqlsrc105), [SQLSRC106](diagnostics.md#sqlsrc106), [SQLSRC107](diagnostics.md#sqlsrc107), [SQLSRC108](diagnostics.md#sqlsrc108), [SQLSRC109](diagnostics.md#sqlsrc109), [SQLSRC110](diagnostics.md#sqlsrc110), [SQLSRC111](diagnostics.md#sqlsrc111), [SQLSRC112](diagnostics.md#sqlsrc112), [SQLSRC113](diagnostics.md#sqlsrc113), [SQLSRC114](diagnostics.md#sqlsrc114), [SQLSRC115](diagnostics.md#sqlsrc115), [SQLSRC116](diagnostics.md#sqlsrc116), [SQLSRC117](diagnostics.md#sqlsrc117), [SQLSRC118](diagnostics.md#sqlsrc118), [SQLSRC119](diagnostics.md#sqlsrc119), [SQLSRC120](diagnostics.md#sqlsrc120), [SQLSRC121](diagnostics.md#sqlsrc121), [SQLSRC122](diagnostics.md#sqlsrc122), [SQLSRC123](diagnostics.md#sqlsrc123), [SQLSRC124](diagnostics.md#sqlsrc124), [SQLSRC125](diagnostics.md#sqlsrc125), [SQLSRC126](diagnostics.md#sqlsrc126), [SQLSRC127](diagnostics.md#sqlsrc127) | The build and the tool, but `SQLSRC121` to `SQLSRC125` and `SQLSRC127` the build alone | At the line and column in the `.sql` file |
| The tool | [SQLSRC200](diagnostics.md#sqlsrc200), [SQLSRC201](diagnostics.md#sqlsrc201), [SQLSRC202](diagnostics.md#sqlsrc202), [SQLSRC203](diagnostics.md#sqlsrc203), [SQLSRC204](diagnostics.md#sqlsrc204), [SQLSRC205](diagnostics.md#sqlsrc205), [SQLSRC206](diagnostics.md#sqlsrc206), [SQLSRC207](diagnostics.md#sqlsrc207), [SQLSRC208](diagnostics.md#sqlsrc208), [SQLSRC209](diagnostics.md#sqlsrc209), [SQLSRC210](diagnostics.md#sqlsrc210), [SQLSRC211](diagnostics.md#sqlsrc211), [SQLSRC212](diagnostics.md#sqlsrc212), [SQLSRC213](diagnostics.md#sqlsrc213), [SQLSRC214](diagnostics.md#sqlsrc214), [SQLSRC215](diagnostics.md#sqlsrc215), [SQLSRC216](diagnostics.md#sqlsrc216), [SQLSRC217](diagnostics.md#sqlsrc217), [SQLSRC218](diagnostics.md#sqlsrc218), [SQLSRC219](diagnostics.md#sqlsrc219), [SQLSRC220](diagnostics.md#sqlsrc220), [SQLSRC221](diagnostics.md#sqlsrc221), [SQLSRC222](diagnostics.md#sqlsrc222), [SQLSRC223](diagnostics.md#sqlsrc223) | The tool, and `SQLSRC209` and `SQLSRC210` the build too | At the query, the file, the project or the solution that the error is about.  Some have no file |
| Describing a query | [SQLSRC224](diagnostics.md#sqlsrc224), [SQLSRC225](diagnostics.md#sqlsrc225), [SQLSRC226](diagnostics.md#sqlsrc226), [SQLSRC227](diagnostics.md#sqlsrc227), [SQLSRC228](diagnostics.md#sqlsrc228), [SQLSRC229](diagnostics.md#sqlsrc229), [SQLSRC230](diagnostics.md#sqlsrc230), [SQLSRC231](diagnostics.md#sqlsrc231), [SQLSRC232](diagnostics.md#sqlsrc232), [SQLSRC233](diagnostics.md#sqlsrc233), [SQLSRC234](diagnostics.md#sqlsrc234), [SQLSRC235](diagnostics.md#sqlsrc235), [SQLSRC236](diagnostics.md#sqlsrc236) | The tool | In the `.sql` file, at the query's name.  An error about the database is at its first query |
| The sidecar of a `.sql` file | [SQLSRC237](diagnostics.md#sqlsrc237), [SQLSRC238](diagnostics.md#sqlsrc238), [SQLSRC239](diagnostics.md#sqlsrc239), [SQLSRC240](diagnostics.md#sqlsrc240), [SQLSRC241](diagnostics.md#sqlsrc241), [SQLSRC242](diagnostics.md#sqlsrc242), [SQLSRC243](diagnostics.md#sqlsrc243), [SQLSRC244](diagnostics.md#sqlsrc244), [SQLSRC245](diagnostics.md#sqlsrc245) | The build | In the sidecar of a `.sql` file that a type uses, at the line and column of the problem.  `SQLSRC241` to `SQLSRC244` are at the query in the `.sql` file |
| The C# types of a query | [SQLSRC246](diagnostics.md#sqlsrc246), [SQLSRC247](diagnostics.md#sqlsrc247), [SQLSRC248](diagnostics.md#sqlsrc248), [SQLSRC249](diagnostics.md#sqlsrc249) | The build | At the query's name.  `SQLSRC247` and `SQLSRC248` have no file and line, and are reported once for the project |
| A C# type of your choice | [SQLSRC250](diagnostics.md#sqlsrc250), [SQLSRC251](diagnostics.md#sqlsrc251), [SQLSRC252](diagnostics.md#sqlsrc252) | The build | At the name of each query that has a column or a parameter of the type |
| The models of a query | [SQLSRC253](diagnostics.md#sqlsrc253), [SQLSRC254](diagnostics.md#sqlsrc254), [SQLSRC255](diagnostics.md#sqlsrc255), [SQLSRC256](diagnostics.md#sqlsrc256), [SQLSRC257](diagnostics.md#sqlsrc257), [SQLSRC258](diagnostics.md#sqlsrc258), [SQLSRC259](diagnostics.md#sqlsrc259), [SQLSRC260](diagnostics.md#sqlsrc260) | The build | At the query's name, or at the marker that the error is about |
| An entry of the type override that maps nothing | [SQLSRC261](diagnostics.md#sqlsrc261) | The build | At the query's name |
| The parameters of a SQL Server query | [SQLSRC262](diagnostics.md#sqlsrc262) | The tool | At the query's name |
| The columns of a SQL Server query | [SQLSRC263](diagnostics.md#sqlsrc263), [SQLSRC264](diagnostics.md#sqlsrc264), [SQLSRC265](diagnostics.md#sqlsrc265), [SQLSRC266](diagnostics.md#sqlsrc266) | The tool | At the query's name |
| The C# types of a SQL Server query | [SQLSRC267](diagnostics.md#sqlsrc267), [SQLSRC268](diagnostics.md#sqlsrc268), [SQLSRC269](diagnostics.md#sqlsrc269), [SQLSRC270](diagnostics.md#sqlsrc270) | The build | At the query's name.  `SQLSRC267` and `SQLSRC268` have no file and line, and are reported once for the project |
| The encoding of a file | [SQLSRC271](diagnostics.md#sqlsrc271) | The tool | At the `.sql` file, with no line and column.  For a C# file, at its `Source` or at the type's name |
| The methods of a query | [SQLSRC272](diagnostics.md#sqlsrc272), [SQLSRC273](diagnostics.md#sqlsrc273), [SQLSRC274](diagnostics.md#sqlsrc274), [SQLSRC275](diagnostics.md#sqlsrc275), [SQLSRC276](diagnostics.md#sqlsrc276) | The build | At the query's name.  `SQLSRC273` has no file and line.  `SQLSRC276` is at the type's `[SqlSourceGenerate]` |
| Converters | [SQLSRC277](diagnostics.md#sqlsrc277), [SQLSRC278](diagnostics.md#sqlsrc278), [SQLSRC279](diagnostics.md#sqlsrc279), [SQLSRC280](diagnostics.md#sqlsrc280) | The build | At the converter's name in the C# file, or at the query's name |
| The libraries of a query | [SQLSRC281](diagnostics.md#sqlsrc281), [SQLSRC282](diagnostics.md#sqlsrc282) | The build | `SQLSRC281` is at the entry of the settings file or the marker that turns the library on.  `SQLSRC282` has no file and line |
| The JSON opt-in | [SQLSRC283](diagnostics.md#sqlsrc283) | The build | At the query's name |
| A short name of your choice | [SQLSRC284](diagnostics.md#sqlsrc284) | The build | At the query's name |
| A `.env` file | [SQLSRC285](diagnostics.md#sqlsrc285), [SQLSRC286](diagnostics.md#sqlsrc286), [SQLSRC287](diagnostics.md#sqlsrc287) | The tool | At the `.env` file, or at the line and column of a variable in it.  `SQLSRC287` is at the folder that is watched |
| A bug-report bundle | [SQLSRC288](diagnostics.md#sqlsrc288), [SQLSRC289](diagnostics.md#sqlsrc289), [SQLSRC290](diagnostics.md#sqlsrc290), [SQLSRC291](diagnostics.md#sqlsrc291) | The tool | At the bundle |
| Making a bug report with `diagnose` | [SQLSRC292](diagnostics.md#sqlsrc292), [SQLSRC293](diagnostics.md#sqlsrc293), [SQLSRC294](diagnostics.md#sqlsrc294), [SQLSRC295](diagnostics.md#sqlsrc295), [SQLSRC296](diagnostics.md#sqlsrc296) | The tool | At the bundle.  `SQLSRC292` is at the `.sql` file |
| The collection method of a query | [SQLSRC297](diagnostics.md#sqlsrc297) | The build | At the query's name |
| A query's synchronous method | [SQLSRC298](diagnostics.md#sqlsrc298), [SQLSRC299](diagnostics.md#sqlsrc299) | The build | At the query's name |

[SQLSRC224](diagnostics.md#sqlsrc224) to [SQLSRC236](diagnostics.md#sqlsrc236), and [SQLSRC262](diagnostics.md#sqlsrc262) to [SQLSRC266](diagnostics.md#sqlsrc266), are the errors of describing a query against its database, PostgreSQL or SQL Server.  The section of each names the database that gives it.

The build checks the models of a query, [SQLSRC253](diagnostics.md#sqlsrc253) to [SQLSRC260](diagnostics.md#sqlsrc260), only while the query's entry in its sidecar is current.  For [SQLSRC253](diagnostics.md#sqlsrc253) to [SQLSRC258](diagnostics.md#sqlsrc258), each column and parameter of the query must also have a C# type.
