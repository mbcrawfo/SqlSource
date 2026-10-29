# Diagnostics

Every problem SqlSource finds is an error, and none can be turned off or made a warning, except two warnings about a sidecar that a project can configure, [SQLSRC240](#sqlsrc240) and [SQLSRC245](#sqlsrc245), a warning about a short name of a C# type that you chose, [SQLSRC284](#sqlsrc284), and two messages of information about the version of a database driver, [SQLSRC248](#sqlsrc248) for Npgsql and [SQLSRC268](#sqlsrc268) for Microsoft.Data.SqlClient.  The generator reports its errors and warnings in the build's output.  Its information is not in the output of `dotnet build`: an IDE that lists information shows it, and the build log has it at the detailed verbosity, as [SQLSRC248](#sqlsrc248) and [SQLSRC268](#sqlsrc268) say.  An error in a `.sql` file is reported at its line and column in that file.  An error in how a `.sql` file is written, an id from 101 to 120, costs the file its members until it is fixed.  An error about describing a query or about its sidecar, such as [SQLSRC209](#sqlsrc209) or [SQLSRC241](#sqlsrc241), leaves them in place: a query whose output is `models` still gets its constant or token method, and one whose output is `codegen` gets no method until the error is fixed.  An error in a sidecar is reported at its line and column in the sidecar.  An error in a settings file, `sqlsource.json`, is reported at its line and column in that file.  The `sqlsource` command-line tool prints its errors itself, in the format of a build error, and ends each with a `see:` line that links to its section here; the samples on this page leave that line out.

**Project and attribute**

| Id | Title |
|----|----|
| [SQLSRC001](#sqlsrc001) | Type must be partial |
| [SQLSRC002](#sqlsrc002) | Type is file-local |
| [SQLSRC003](#sqlsrc003) | Target framework is not supported |
| [SQLSRC004](#sqlsrc004) | Path matches no SQL file |
| [SQLSRC005](#sqlsrc005) | Folder has no SQL file |
| [SQLSRC006](#sqlsrc006) | Attribute value is not valid |
| [SQLSRC007](#sqlsrc007) | Type has a member named like its nested class |
| [SQLSRC008](#sqlsrc008) | Query name is used in two files |
| [SQLSRC009](#sqlsrc009) | Query is named like its containing type |
| [SQLSRC012](#sqlsrc012) | Language version is not supported |
| [SQLSRC013](#sqlsrc013) | SQL file paths differ only by case |

**Settings files**

| Id | Title |
|----|----|
| [SQLSRC015](#sqlsrc015) | Settings file is not a JSON object |
| [SQLSRC016](#sqlsrc016) | Settings file key is not known |
| [SQLSRC017](#sqlsrc017) | Settings file key is written twice |
| [SQLSRC018](#sqlsrc018) | Settings file value has the wrong type |
| [SQLSRC019](#sqlsrc019) | Settings file value is not valid |
| [SQLSRC020](#sqlsrc020) | Settings file entry repeats a key |

**`.sql` files**

| Id | Title |
|----|----|
| [SQLSRC101](#sqlsrc101) | Quote is not closed |
| [SQLSRC102](#sqlsrc102) | Comment is not closed |
| [SQLSRC103](#sqlsrc103) | Query name is not valid |
| [SQLSRC104](#sqlsrc104) | Query name is used twice |
| [SQLSRC105](#sqlsrc105) | File name is not a valid query name |
| [SQLSRC106](#sqlsrc106) | SQL before the first name |
| [SQLSRC107](#sqlsrc107) | Summary before the first name |
| [SQLSRC108](#sqlsrc108) | Marker has no SQL after it |
| [SQLSRC109](#sqlsrc109) | Generator parameter is not known |
| [SQLSRC110](#sqlsrc110) | Generator parameter is missing |
| [SQLSRC111](#sqlsrc111) | Marker value is not valid |
| [SQLSRC112](#sqlsrc112) | Settings conflict |
| [SQLSRC113](#sqlsrc113) | Query has no SQL |
| [SQLSRC114](#sqlsrc114) | Token name is a keyword |
| [SQLSRC115](#sqlsrc115) | Dialect marker is misplaced |
| [SQLSRC116](#sqlsrc116) | Marker is not allowed here |
| [SQLSRC117](#sqlsrc117) | Parameter has no type |
| [SQLSRC118](#sqlsrc118) | Parameter is not declared |
| [SQLSRC119](#sqlsrc119) | Query has no parameters |
| [SQLSRC120](#sqlsrc120) | Token crosses a comment |

**The tool**

| Id | Title |
|----|----|
| [SQLSRC200](#sqlsrc200) | The tool failed unexpectedly |
| [SQLSRC201](#sqlsrc201) | No project or solution found |
| [SQLSRC202](#sqlsrc202) | More than one project or solution found |
| [SQLSRC203](#sqlsrc203) | Path is not a project or a solution |
| [SQLSRC204](#sqlsrc204) | Project does not use SqlSource |
| [SQLSRC205](#sqlsrc205) | Project could not be evaluated |
| [SQLSRC206](#sqlsrc206) | Project manifest cannot be read |
| [SQLSRC207](#sqlsrc207) | Project is not in the run |
| [SQLSRC208](#sqlsrc208) | Attribute argument is not a literal |
| [SQLSRC209](#sqlsrc209) | Output needs a dialect that can be described |
| [SQLSRC210](#sqlsrc210) | Token has no default |
| [SQLSRC211](#sqlsrc211) | Database has two dialects |
| [SQLSRC212](#sqlsrc212) | File is not in the run |
| [SQLSRC213](#sqlsrc213) | Database has no connection |
| [SQLSRC214](#sqlsrc214) | Two databases share a connection variable |
| [SQLSRC215](#sqlsrc215) | Connection names no database |
| [SQLSRC216](#sqlsrc216) | No describer for the dialect |
| [SQLSRC217](#sqlsrc217) | Sidecar was not written |
| [SQLSRC218](#sqlsrc218) | File could not be changed |
| [SQLSRC219](#sqlsrc219) | Sidecar is out of date |
| [SQLSRC220](#sqlsrc220) | Project was not restored |
| [SQLSRC221](#sqlsrc221) | Sidecar was written by a newer tool |
| [SQLSRC222](#sqlsrc222) | Directory cannot be read |
| [SQLSRC223](#sqlsrc223) | Solution cannot be read |

**Describing a query**

| Id | Title |
|----|----|
| [SQLSRC224](#sqlsrc224) | Database could not be opened |
| [SQLSRC225](#sqlsrc225) | Server rejected the query |
| [SQLSRC226](#sqlsrc226) | Parameter type could not be determined |
| [SQLSRC227](#sqlsrc227) | Parameter's first use does not fix its type |
| [SQLSRC228](#sqlsrc228) | Declared type is not valid |
| [SQLSRC229](#sqlsrc229) | Column type is not supported |
| [SQLSRC230](#sqlsrc230) | Query holds more than one statement |
| [SQLSRC231](#sqlsrc231) | Parameters are read differently |
| [SQLSRC232](#sqlsrc232) | Type cannot be bound by Npgsql |
| [SQLSRC233](#sqlsrc233) | Statement cannot be described |
| [SQLSRC234](#sqlsrc234) | Query writes a positional parameter |
| [SQLSRC235](#sqlsrc235) | Server version is not supported |
| [SQLSRC236](#sqlsrc236) | Connection does not keep a session |

**The sidecar of a `.sql` file**

| Id | Title |
|----|----|
| [SQLSRC237](#sqlsrc237) | Sidecar cannot be read |
| [SQLSRC238](#sqlsrc238) | Sidecar has a newer format |
| [SQLSRC239](#sqlsrc239) | Sidecar has an older format |
| [SQLSRC240](#sqlsrc240) | Sidecar was written by another version of the tool |
| [SQLSRC241](#sqlsrc241) | Query has no entry in the sidecar |
| [SQLSRC242](#sqlsrc242) | Entry is of another database |
| [SQLSRC243](#sqlsrc243) | No type map for the dialect |
| [SQLSRC244](#sqlsrc244) | Entry is stale |
| [SQLSRC245](#sqlsrc245) | Entry names no query |

**The C# types of a query**

| Id | Title |
|----|----|
| [SQLSRC246](#sqlsrc246) | Type has no C# type |
| [SQLSRC247](#sqlsrc247) | Npgsql is older than 8.0 |
| [SQLSRC248](#sqlsrc248) | Npgsql is newer than SqlSource knows |
| [SQLSRC249](#sqlsrc249) | Type needs Npgsql |

**A C# type of your choice**

| Id | Title |
|----|----|
| [SQLSRC250](#sqlsrc250) | Mapped type is not found |
| [SQLSRC251](#sqlsrc251) | Mapped type is not accessible |
| [SQLSRC252](#sqlsrc252) | Mapped type cannot be a property's type |

**The models of a query**

| Id | Title |
|----|----|
| [SQLSRC253](#sqlsrc253) | Column has no name |
| [SQLSRC254](#sqlsrc254) | Name is not an identifier after PascalCase |
| [SQLSRC255](#sqlsrc255) | Two names give one property name |
| [SQLSRC256](#sqlsrc256) | Property name is taken |
| [SQLSRC257](#sqlsrc257) | Two models of one name differ |
| [SQLSRC258](#sqlsrc258) | Model name cannot be used |
| [SQLSRC259](#sqlsrc259) | Output model on a query without rows |
| [SQLSRC260](#sqlsrc260) | Column marker names no usable column |

**An entry of the type override that maps nothing**

| Id | Title |
|----|----|
| [SQLSRC261](#sqlsrc261) | Entry names an array as the catalog does |

**The parameters of a SQL Server query**

| Id | Title |
|----|----|
| [SQLSRC262](#sqlsrc262) | Copies of a parameter have different types |

**The columns of a SQL Server query**

| Id | Title |
|----|----|
| [SQLSRC263](#sqlsrc263) | Query returns rows of different shapes |
| [SQLSRC264](#sqlsrc264) | Query runs SQL the server cannot read |
| [SQLSRC265](#sqlsrc265) | Query uses a temporary table in a batch |
| [SQLSRC266](#sqlsrc266) | Column has no name |

**The C# types of a SQL Server query**

| Id | Title |
|----|----|
| [SQLSRC267](#sqlsrc267) | Microsoft.Data.SqlClient is older than 6.1 |
| [SQLSRC268](#sqlsrc268) | Microsoft.Data.SqlClient is newer than SqlSource knows |
| [SQLSRC269](#sqlsrc269) | Type needs Microsoft.Data.SqlClient |
| [SQLSRC270](#sqlsrc270) | Type needs a newer Microsoft.Data.SqlClient |

**The encoding of a file**

| Id | Title |
|----|----|
| [SQLSRC271](#sqlsrc271) | File is not saved as UTF-8 |

**The methods of a query**

| Id | Title |
|----|----|
| [SQLSRC272](#sqlsrc272) | Result shape needs rows |
| [SQLSRC273](#sqlsrc273) | Driver is not referenced |
| [SQLSRC274](#sqlsrc274) | Method name is taken |
| [SQLSRC275](#sqlsrc275) | Token is named as an argument of the method |
| [SQLSRC276](#sqlsrc276) | Extension class name is taken |

**Converters**

| Id | Title |
|----|----|
| [SQLSRC277](#sqlsrc277) | Method marked as a converter is not one |
| [SQLSRC278](#sqlsrc278) | Two converters convert between the same types |
| [SQLSRC279](#sqlsrc279) | Converters do not convert from the database type |
| [SQLSRC280](#sqlsrc280) | Converter is missing for one direction |

**A short name of your choice**

| Id | Title |
|----|----|
| [SQLSRC284](#sqlsrc284) | Short name finds a type that only a package brings |

Ids below 100 are about the type that carries `[SqlSourceGenerate]`, or about the project.  Ids from 101 to 120 are about the contents of a `.sql` file.  Ids from 200 to 236, 262 to 266, and 271 are the errors of the `sqlsource` tool: it prints each with a line under it that starts with `see:` and links to its section here, and exits with the code 1, or with 2 when it reported nothing but `SQLSRC219`.  The build reports two of them as well, at the same places: [SQLSRC209](#sqlsrc209) and [SQLSRC210](#sqlsrc210), for a query that must be described and cannot be.  The tool also reports the errors of a `.sql` file, the ids from 101 to 120, under the generator's ids: what is wrong is the same, and so is the fix.  It reports the errors of a settings file, [SQLSRC015](#sqlsrc015) to [SQLSRC019](#sqlsrc019), at the same line and column as the build, for a settings file that applies to a `.sql` file or a C# file of the project, and once in a run however many of its projects it applies to.  It reports `SQLSRC019` for `dialect`, `output` and `database` alone, and the build for every key but `database`.  [SQLSRC020](#sqlsrc020), a key repeated in `typeOverrides` or `columnTypes`, is the build's alone.

The ids from `SQLSRC224` to `SQLSRC236`, and `SQLSRC262` to `SQLSRC266`, are the errors of describing a query against its database, PostgreSQL or SQL Server.  A section that names a database says which gives it and for what, and a section that names none is PostgreSQL's.  This version of the tool describes the `postgres` and `mssql` dialects.

The ids from `SQLSRC237` to `SQLSRC245` are reported by the build, about the sidecar of a `.sql` file that a type uses.  A query whose output is `models` or `codegen` needs a current entry in its file's sidecar, which `dotnet sqlsource describe` writes; one whose output is `sql` needs none.  The default output is `codegen`, so under the `postgres` and `mssql` dialects every query needs an entry unless the project, the type or the file sets the output to `sql`.  A query that no entry can be written for gets [SQLSRC209](#sqlsrc209) or [SQLSRC210](#sqlsrc210) instead.  A sidecar that exists is checked even when no query of its file needs an entry.  While a `.sql` file has an error of its own, an id from 101 to 120, it has no queries to check, so the build checks only the sidecar itself, [SQLSRC237](#sqlsrc237) to [SQLSRC240](#sqlsrc240), and reports no entry of it as one that names no query.

The ids `SQLSRC246` and `SQLSRC249` are reported by the build, about the C# types of a query whose output is `models` or `codegen` and whose entry in its sidecar is current: `SQLSRC246` under the `postgres` or the `mssql` dialect, and `SQLSRC249` under the `postgres` dialect.  Each column and parameter of the entry gets the C# type that the database's driver reads it as, Npgsql for `postgres` and Microsoft.Data.SqlClient for `mssql`, which can depend on the driver that the project references, or the type you chose for it, by the type override, the column types or a marker.  Each is reported at the query's name, once for a query: for its first column or parameter that has no C# type, parameters before columns.

The ids `SQLSRC247` and `SQLSRC248` are reported by the build, about the version of the `Npgsql` package that the project references, directly or through another package.  The C# types of a PostgreSQL query depend on it.  Each is reported once for the project, with no file and line, and only when a query needs an entry in its sidecar under the `postgres` dialect.

The ids `SQLSRC267` and `SQLSRC268` are reported by the build, about the version of Microsoft.Data.SqlClient that the project references, whenever a query of the `mssql` dialect has the output `models` or `codegen`.  They have no file and line, and each is reported once for the project, however many of its queries read the driver.

`SQLSRC269` and `SQLSRC270` are reported by the build at a query of the `mssql` dialect whose output is `models` or `codegen` and whose entry in its sidecar is current, as `SQLSRC249` is under the `postgres` dialect: at the query's name, once for the query, for its first column or parameter that has no C# type, parameters before columns.

The ids from `SQLSRC250` to `SQLSRC252` are about a C# type that you chose: one that the type override gives a database type, that the column types give a column, or that a `-- param:` or `-- column:` marker gives after `->`.  Each is reported at a query that has a column or a parameter the type is chosen for, and names the column or parameter, what is mapped, the C# type and the place that chose it.  An entry that no query uses is never an error, so a project can set its mappings before it has the queries.

`SQLSRC261` is reported at a query that has a column or a parameter of an array of a type outside `pg_catalog`, or of a domain over such an array, when the type override has an entry for the array's name in the database.  It names the column or parameter, the entry, the place that wrote it and the key to write.  It is reported once for the query, for its first column or parameter that has no C# type, as `SQLSRC246` and `SQLSRC249` to `SQLSRC252` are, so the query has no models until it is fixed.

The ids from `SQLSRC253` to `SQLSRC260` are reported by the build, about the [models](generator.md#models) of a query whose entry in its sidecar is current and whose columns and parameters all have a C# type, except `SQLSRC259` and `SQLSRC260`, which a query with a current entry gets whatever its C# types.  Each property of a model is named by the PascalCase of its column or parameter: `created_at` gives `CreatedAt`.  `SQLSRC253` to `SQLSRC255`, `SQLSRC256` for a name that a record or `object` has, `SQLSRC259` and `SQLSRC260` are about the query's own columns, parameters and markers: each is reported once for the query, however many types claim its file, at the query's name, or at its marker for `SQLSRC259` and `SQLSRC260`, and the query gets no models until it is fixed.  `SQLSRC257` and `SQLSRC258`, `SQLSRC256` for a property named as its model, and `SQLSRC251` for each property whose type is not public in a public model, are about a model, whose name and accessibility come from the types that claim its queries: `SQLSRC257` is reported at the later of two queries whose models differ, at its marker when it has one, and the others at the name of each query of the model, which is not generated until it is fixed.  A query gets every error it has; under the output `models` its constant or token method is still generated, and under `codegen` it gets no method until all are fixed.

The ids from `SQLSRC272` to `SQLSRC276` are reported by the build, about the [method](generator.md#query-methods) of a query whose output is `codegen`.  `SQLSRC272`, `SQLSRC274` and `SQLSRC275` are reported at the query's name, once however many types claim its file, except a `SQLSRC274` whose cause is of one type, which each such type reports, and one for the methods of two types that clash, which each of the two queries gets.  A query of a file that two types of one namespace claim gives each type a method of one signature, so it gets one `SQLSRC274` for each type, each naming the other.  A query gets every one of them that it has, and no method until all are fixed; its models are still generated.  `SQLSRC273` has no file and line, and is reported once for the project and database.  `SQLSRC276` is about the extension class that holds a type's methods, and is reported at the type's `[SqlSourceGenerate]`, once for each type whose methods would go into the class.

The ids from `SQLSRC277` to `SQLSRC280` are reported by the build, about converters, the methods marked `[SqlSourceConverter]`.  `SQLSRC277` and `SQLSRC278` are about the methods of the project's own code: at the method's name, whatever the output of the project, its types and its files, and whether a query uses the method or not.  A converter of a type that a type override, a column type or a `-- param:` marker names, declared in a referenced assembly, has no place in the project, so `SQLSRC277` and `SQLSRC278` are reported for it at the name of each query whose output is `codegen` and whose method would use it.  `SQLSRC279` and `SQLSRC280` are about a column or a parameter of a query whose output is `codegen`, whose C# type a type override, a column type or a `-- param:` marker chose, and whose type has converters that do not serve it.  Each of these at a query is reported at the query's name, and the query gets no method until it is fixed; its models are still generated.  A project whose output is `models` uses no converter, and gets none of them at a query, and neither does a query whose database's driver the project does not reference, which [SQLSRC273](#sqlsrc273) is about.

`SQLSRC284` is reported by the build, as `SQLSRC250` to `SQLSRC252` are, at the name of a query whose output is `models` or `codegen`, once for each column and parameter whose C# type you chose by a short name that it is about.  It is a warning, and the query keeps the type: it gets its models, and under `codegen` its method.

`SQLSRC901` is not in this list because it is not a problem.  It is the id under which SqlSource turns off the compiler's warning CS0436 for the types it adds to every project, in a project that sees the internals of another one that uses SqlSource; see [Projects that share internals](generator.md#projects-that-share-internals).

## SQLSRC001

**Type must be partial**

SqlSource adds members through a second declaration of the type, which C# allows only when every declaration is `partial`.  The same holds for each type the type is nested in.

```csharp
[SqlSourceGenerate]
public class UserRepository { }
```

Add `partial` to the type named in the message.

See: [The attribute](generator.md#attribute)

## SQLSRC002

**Type is file-local**

A type declared with the `file` modifier exists only in its own source file, so generated code cannot add to it.  The same holds for a type nested in a file-local type.

```csharp
[SqlSourceGenerate]
file partial class UserRepository { }
```

Remove the `file` modifier, or move the attribute to a type that is not file-local.

See: [The attribute](generator.md#attribute)

## SQLSRC003

**Target framework is not supported**

The generated code uses API that first appeared in .NET 8.

Target `net8.0` or later.  For a project that targets several frameworks, put the attributed types under a condition that excludes the older ones.

## SQLSRC004

**Path matches no SQL file**

`Path` is relative to the folder of the source file that carries the attribute.  A value that ends in `.sql` names one file.  Any other value names a folder, and the type gets the `.sql` files directly in it; subfolders are not searched.

```csharp
[SqlSourceGenerate(Path = "Queries/User.sql")] // The file is Queries/Users.sql.
public partial class UserRepository { }
```

Check the spelling, and that the path starts from the folder of this source file and not from the project.  If the file exists, check that the build sees it.  The package registers the `.sql` files under the project's own folder, so a file outside it, such as a linked file, must be listed as an `AdditionalFiles` item.  So must every `.sql` file of a project that sets `SqlSourceIncludeFiles` to `false`.

See: [Path](settings.md#setting-path)

## SQLSRC005

**Folder has no SQL file**

Without `Path`, the type gets every `.sql` file in the folder of the source file that carries the attribute.  That folder has none.

Add a `.sql` file next to the source file, or set `Path` to the folder or file that holds the queries.  If a file is there, check that the build sees it, as described for [SQLSRC004](#sqlsrc004).

See: [Path](settings.md#setting-path)

## SQLSRC006

**Attribute value is not valid**

A property of `[SqlSourceGenerate]` has a value it does not take.  The message names the property and the value: a number that is no member of the property's enum, or a word in `Parameters` that is not a generator parameter.

```csharp
[SqlSourceGenerate(SqlLocation = (SqlLocation)5, Parameters = "keep-coments")]
public partial class UserRepository { }
```

Use a member of the enum, and for `Parameters` the words that [SQLSRC109](#sqlsrc109) lists.  `Output` takes `GeneratorOutput.Sql`, `GeneratorOutput.Models` or `GeneratorOutput.CodeGen`.  `InputModelType` and `OutputModelType` take `GeneratorModelType.Record`, `SealedRecord`, `Class` or `SealedClass`; `CollectionType` takes a member of `GeneratorCollectionType`; `MethodLocation` takes a member of `MethodLocation`.  `InputModelSuffix` and `OutputModelSuffix` take characters that can be part of an identifier, `ModelNamespace` takes a namespace such as `App.Models`, and `NestedClass` takes one identifier that is not a keyword; a value that is empty or only white space is not set.  The type gets no members until the value is fixed.

See: [All settings](settings.md#all-settings)

## SQLSRC007

**Type has a member named like its nested class**

With `SqlLocation.Nested` the queries go in a nested class, named `Sql` unless [the nested class](settings.md#setting-nested-class) names it otherwise, and the type already has a member with that name.  The message names it.  The same error is reported when the type itself, or one of its type parameters, has the name: a nested class cannot share either name.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    private string Sql { get; }
}
```

Rename the member, the type or the type parameter, give the nested class another name with `NestedClass` on the attribute or `nestedClass` in a settings file, or set `SqlLocation = SqlLocation.Direct` so that the queries become members of the type itself.

See: [Where the members go](generator.md#locations)

## SQLSRC008

**Query name is used in two files**

Two `.sql` files of one type each have a query with the same name, and a type cannot have two members with one name.  The error is at the second query, and the message names the file that holds the first.  The file that holds the second query produces no members for that type.

Rename one of the queries.

See: [Which files belong to a type](generator.md#files-of-a-type)

## SQLSRC009

**Query is named like its containing type**

A member cannot have the name of the type that contains it.  This applies to a query whose output is `sql` or `models`, whose constant or token method is a member of that type.  With `SqlLocation.Nested` that type is the generated class, so no such query can have its name, `Sql` unless [the nested class](settings.md#setting-nested-class) is named otherwise.  With `SqlLocation.Direct` it is the attributed type, so no such query can have that type's name.

```sql
-- name: Sql
-- output: sql
SELECT 1;
```

Rename the query.  A query whose output is `codegen` has a method in place of the member, and may have either name when its method can: what its method cannot be named is [SQLSRC274](#sqlsrc274).

See: [Where the members go](generator.md#locations)

## SQLSRC012

**Language version is not supported**

The code SqlSource generates for a type is C# 12, the default language version of a project that targets .NET 8, and the project sets `LangVersion` to an older one.  The message quotes the version the project uses.

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion>
</PropertyGroup>
```

Remove `LangVersion` to get the default of the target framework, or set it to `12` or later.  It can be set in the project file, in `Directory.Build.props`, and by a `-p:` argument of the build command.

The error is reported at each `[SqlSourceGenerate]` attribute, and no type gets members until it is fixed.  A project that targets a framework older than .NET 8 has an older language version by default; it gets [SQLSRC003](#sqlsrc003) and not this error, because targeting .NET 8 fixes both.

## SQLSRC013

**SQL file paths differ only by case**

Two `.sql` files of the project have paths that differ only by upper and lower case: `Users.sql` and `users.sql` in one folder, or a file of the same name in `Queries` and in `queries`.  SqlSource compares paths ignoring case, so that a project builds the same on every operating system, and the two are one file to it.  It would use the one the project lists first and ignore the other.  The error is at the start of the file that would be ignored, and the message names the other one.

Rename one of the files, or one of the folders.  Such a pair can exist only on a file system that tells upper case from lower, and already breaks a checkout of the repository on Windows and on macOS.

A project that lists one file twice, with spellings that differ by case, gets the same error, because SqlSource may not ask the file system whether two paths are one file.  That takes an `AdditionalFiles` item written by hand for a file that the package already includes, here `Queries/Users.sql`:

```xml
<ItemGroup>
    <AdditionalFiles Include="queries/users.sql" />
</ItemGroup>
```

Remove the item, since the package already includes the file.  To change the item the package adds, write `Update` in place of `Include`, and spell the path as it is on disk.

The error is reported only for a `.sql` file that a type uses.

See: [Path](settings.md#setting-path)

## SQLSRC015

**Settings file is not a JSON object**

A settings file, `sqlsource.json`, holds one JSON object, and this one does not: its text is not JSON, or it is JSON whose top level is an array, a string or another value that is not an object.  The message says which.  The JSON of a settings file may have `//` and `/* */` comments, and a comma after the last member of an object or the last element of an array; anything else that JSON does not allow is this error, a missing comma or a key without quotes among them, and so is a value nested more than 64 levels deep.  The error is at the first character that breaks the JSON, or at the end of a file that is cut short; for a top level that is not an object, it is at the whole value.

```jsonc
{
    // A comma is missing after "models".
    "output": "models"
    "dialect": "postgres",
}
```

Correct the JSON.  While it is wrong the file gives nothing: none of its settings applies, and neither does its `root`.

See: [Settings files](settings.md#settings-files)

## SQLSRC016

**Settings file key is not known**

A key of the top-level object of a settings file is not one of its keys.  Keys are matched exactly, case included, so `Dialect` is reported.  The message quotes the key, and the error is at it.

```jsonc
{
    "Dialect": "postgres",
    "outptu": "models",
}
```

The keys are `$schema`, `root`, `dialect`, `database`, `output`, `generatorParameters`, `inputModelSuffix`, `outputModelSuffix`, `modelNamespace`, `inputModelType`, `outputModelType`, `collectionType`, `typeOverrides`, `columnTypes` and `nestedClass`.  Correct the key, or remove the member.  While it is wrong its value is not read, and the other members still apply.

See: [Settings files](settings.md#settings-files)

## SQLSRC017

**Settings file key is written twice**

A key is written twice in the top-level object of a settings file.  The first member applies, and each later one is reported at its key, whatever its value.

```jsonc
{
    "output": "models",
    "dialect": "postgres",
    "output": "codegen",
}
```

Remove one of the two members, and keep the value you want.

See: [Settings files](settings.md#settings-files)

## SQLSRC018

**Settings file value has the wrong type**

A value of a settings file is not of the JSON type its key takes.  `root` takes `true` or `false`, `generatorParameters` an array of strings, `typeOverrides` and `columnTypes` an object whose values are strings, and every other key a string.  `null` is of the wrong type for every key.  The message names the key and what it takes, and the error is at the value.

```jsonc
{
    "root": "true",
    "output": null,
    "generatorParameters": ["keep-comments", 1],
    "typeOverrides": {
        "int4": "int",
        "int8": false,
    },
}
```

Write the value as its key takes it: `"root": true`, `"generatorParameters": ["keep-comments"]`.  While a value is of the wrong type its setting is not set by the file.  An element of `generatorParameters`, or a member of `typeOverrides` or `columnTypes`, of the wrong type is reported on its own and left out, and the others still apply; an array that has elements, none of them a string, sets nothing, while `[]` is the empty list.  To leave a setting unset, remove its member: an empty string, or one of white space, also sets nothing for a key that takes a string, and is not an error.

See: [Settings files](settings.md#settings-files)

## SQLSRC019

**Settings file value is not valid**

A value of a settings file is of the JSON type its key takes, but its setting does not take it.  The message quotes the value, trimmed, and names the key; for a member of `typeOverrides` or `columnTypes` it quotes the member as `key: type`.  The error is at the string, or for a member from its key to its value.

```jsonc
{
    "dialect": "pgsql",
    "output": "model",
    "generatorParameters": ["keep-comments", "keep-coments"],
    "typeOverrides": {
        "[dbo].[Email]": "MyApp.Email",
        "int4": "int?",
    },
    "columnTypes": {
        "id": "MyApp.UserId",
    },
}
```

Correct the value, or remove it.  While it is wrong the setting is not set by this file, so a farther settings file or the default applies.  In `generatorParameters`, `typeOverrides` and `columnTypes` each element or member that is not valid is reported on its own, and the others still apply, with one exception: `default` beside another generator parameter makes the whole list not set, and each `default` is reported.  For a key that takes a string, a value that is empty or white space sets nothing and is not an error, and such an element of `generatorParameters` is skipped; a member of `typeOverrides` or `columnTypes` whose key or type is empty or white space is reported.

`dialect` takes a dialect's name, `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` or `oracle`, in any case.  `sqlserver` and `tsql` also mean `mssql`, `postgresql` also means `postgres`, and `cockroach` also means `cockroachdb`.  After `mysql` or `mariadb` the value may name options, each after a comma, as in `"mysql,ansi-quotes"`.  The options are `ansi-quotes` and `no-backslash-escapes`, also written `ansi_quotes` and `no_backslash_escapes`.  An option that does not exist, an option after any other dialect, and a comma with nothing before it or after it each make the whole value wrong.

`output` takes `sql`, `models` or `codegen`, in any case and with or without hyphens and spaces.  `inputModelType` and `outputModelType` take `record`, `sealed record`, `class` or `sealed class`, and `collectionType` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` or `IAsyncEnumerable`, all matched in the same way.  `inputModelSuffix` and `outputModelSuffix` take characters that can be part of an identifier, `modelNamespace` takes a namespace such as `App.Models`, and `nestedClass` takes one identifier that is not a keyword.  `database` takes one word of letters, digits, `-`, `_` and `.`.

`generatorParameters` takes one generator parameter in each element: `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix`, `no-array-pool` or `default`, in any case.  `[]` is the empty list, which gives every default, and so is `["default"]`; beside another word `default` is reported, and the message says to write `[]`.

`typeOverrides` takes members `"key": "type"`, and the key and the type of each are trimmed:

- **The key is a type's one-word name**, `timestamptz` and not `timestamp with time zone`, `float8` and not `double precision`; every built-in type has one.  A type outside `pg_catalog`, or a SQL Server type outside `sys`, is written with its schema, `public.user_status` or `dbo.Email`: a name without one must be a type SqlSource knows, so `user_status` and the misspelling `timestampz` are reported.  A value serves PostgreSQL and SQL Server alike, so a name of either one's types, `int4` or `nvarchar`, is not reported.  A schema or a name may be in double quotes, each written `\"` in JSON, as in `"\"my schema\".\"order-status\""`, and a part may be in `U&"..."` with escapes, an escape that is not valid making the member not valid.
- **A key is not written in brackets.**  `"[dbo].[Email]"` is reported, and the message shows the forms that are read: `dbo.Email`, which matches the type's name in any case, and `"dbo"."Email"`, which matches its case exactly and is written `"\"dbo\".\"Email\""` as a JSON key.  A key without a schema has the plain form alone, `[INT]` is `INT`, since a quoted name without a schema must be in lower case.  A part that holds anything but letters, digits, `_` and `$`, such as `[my schema]`, has the quoted form alone, `"my schema"`.
- **The C# type is a keyword, a full name or a short name**, `System.DateTimeOffset` or `DateTimeOffset`, with no `?` at the end: `"int4": "int"` and not `"int4": "int?"`.  It may also be `default`.

The rules are in [type overrides](settings.md#setting-type-overrides).

`columnTypes` takes members `"key": "type"`, trimmed in the same way.  Its key is the column a value is read from, `table.column` or `schema.table.column`, each part plain or in double quotes, so `id`, of one part, is reported, and so are a key of four parts, a key in brackets and a key with `[]`.  Its C# type is read as a type override's is, or is `default`.  The rules are in [column types](settings.md#setting-column-types).

See: [Settings files](settings.md#settings-files)

## SQLSRC020

**Settings file entry repeats a key**

Two members of `typeOverrides`, or two of `columnTypes`, have one key.  Two keys are one when they are the same once their quotes and escapes are read and their aliases resolved: `integer` and `int4` name one PostgreSQL type, `"users".id` and `users.id` one column, and an unqualified built-in type name is one in any case, so `timestamptz` and `TIMESTAMPTZ` are one key.  Keys that differ only in the case of a plain part of a qualified name are two keys and nothing is reported: `users.id` and `Users.ID`, or `dbo.Email` and `DBO.EMAIL`, both apply, and a type or column whose name matches one of them exactly takes that one.  The first member of two that are one applies, and the second is reported at its key; the message quotes both keys and names the setting.

```jsonc
{
    "typeOverrides": {
        "integer": "long",
        "int4": "int",
    },
    "columnTypes": {
        "users.id": "MyApp.UserId",
        "\"users\".id": "UserId",
    },
}
```

Remove one of the two members, and keep the C# type you want.

See: [Settings files](settings.md#settings-files)

## SQLSRC101

**Quote is not closed**

A string or a quoted identifier starts and never ends.  The error is at the opening quote or bracket.

```sql error
SELECT 'unfinished FROM users;
```

Close the quote.  If the SQL is valid for your database, SqlSource is reading it by the rules of another one: `'it\'s'` is one string in MySQL and an unclosed one elsewhere.  Set the dialect of the file.  A few constructs are read differently from the database whatever the dialect: see [what is still read differently](dialects.md#still-read-differently).

See: [What each dialect changes](dialects.md#what-a-dialect-changes)

## SQLSRC102

**Comment is not closed**

A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.

```sql error
/* outer /* inner */
SELECT 1;
```

Close the comment.  Whether a `/*` inside a comment needs its own `*/` depends on the dialect: it does in the default dialect, in SQL Server and in PostgreSQL, and it does not in MySQL, MariaDB, SQLite and Oracle.  If the SQL is valid for your database, set the dialect of the file.

SQLite accepts a comment that is still open at the end of the file.  SqlSource does not, in any dialect, because the comment would take every later query of the file with it.

See: [What each dialect changes](dialects.md#what-a-dialect-changes)

## SQLSRC103

**Query name is not valid**

The value of a `-- name:` marker becomes the name of a C# member, so it must be a C# identifier and must not be a reserved keyword such as `class`.

```sql error
-- name: get-user
SELECT 1;
```

Rename the query, for example to `GetUser`.

The name ends at `->`, when the marker has one.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC104

**Query name is used twice**

Two `-- name:` markers in one file have the same name.  The error is at the second.

Rename one of the queries.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC105

**File name is not a valid query name**

A file without a `-- name:` marker is one query, named after the file without its extension.  This file's name cannot be the name of a C# member.

```text
001_init.sql
get-user.sql
```

Add a `-- name:` marker at the top of the file, or rename the file.  If the file is not meant to be a query of this type, move it out of the type's folder or point the type's `Path` somewhere else.

See: [A file with no `-- name:`](sql-files.md#a-file-with-no-name)

## SQLSRC106

**SQL before the first name**

In a file that has `-- name:` markers, only comments and the markers that describe the whole file may come before the first one: the table of [SQLSRC116](#sqlsrc116) has the list.  SQL there would belong to no query.

```sql error
SET search_path TO app;

-- name: GetUser
SELECT 1;
```

Move the SQL below a `-- name:` marker, or delete it.

See: [The file header](sql-files.md#the-file-header)

## SQLSRC107

**Summary before the first name**

A `-- summary:` marker describes the query it is in.  One in the file header, before the first `-- name:` line, is in no query.

```sql error
-- summary: Loads one user.
-- name: GetUser
SELECT 1;
```

Move the summary below the `-- name:` marker.

See: [Summaries](sql-files.md#summaries)

## SQLSRC108

**Marker has no SQL after it**

A marker comes before the SQL it describes.  This one stands after the last SQL of its query, where a reader would take it to belong to the next one.  That usually means it was written above the next `-- name:` marker and was meant for that query.

```sql error
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

Move the marker below the `-- name:` marker of the query it describes, or delete it.

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC109

**Generator parameter is not known**

A `-- generator:` marker holds a word that is not a generator parameter.  The generator parameters are `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models`, `async-method-suffix`, `no-array-pool` and `default`.  If you used `token-validation` or `token-ignore` as generator parameters in an earlier preview: leave out the first, since validation is the default, and write the second as a marker inside the query, `-- token-ignore: name`.

```sql error
-- generator: keep-comment
```

Correct the parameter.  A dialect is not a generator parameter: it has a marker of its own, `-- dialect: name`.

See: [Generator parameters](settings.md#setting-generator-parameters)

## SQLSRC110

**Generator parameter is missing**

A `-- generator:` marker has nothing after the colon.

Add a generator parameter, or delete the line.

See: [Generator parameters](settings.md#setting-generator-parameters)

## SQLSRC111

**Marker value is not valid**

A generator parameter has a value it does not take; what follows a name in a `-- name:` marker is not a shape; a `-- dialect:` marker does not name a dialect; a marker that holds a setting, such as `-- output:`, `-- database:`, `-- input-model-type:`, `-- collection-type:` or `-- type-override:`, has a value that setting does not take; or a `-- token:`, `-- token-ignore:`, `-- param:` or `-- column:` marker does not hold what it needs.

- No generator parameter takes a value: `keep-comments=1` is this error.
- What follows the name in a `-- name:` marker must be `->` and one of `many`, `one`, `one-optional`, `none` and `rowcount`, as in `-- name: GetUser -> one`.
- `-- dialect:` needs the name of a dialect, as in `-- dialect: postgres`, with any options after it, as in `-- dialect: mysql, ansi-quotes`.  The names and the options are those of a settings file's `dialect`, in [SQLSRC019](#sqlsrc019).  The value is the rest of the line, so nothing else may follow it.

```sql error
-- generator: keep-comments=true
-- dialect: pgsql
-- dialect: mysql keep-comments
```

- `-- output:` takes `sql`, `models` or `codegen`.  `-- database:` takes one word of letters, digits, `-`, `_` and `.`.
- `-- input-model-type:` and `-- output-model-type:` take `record`, `sealed record`, `class` or `sealed class`.  `-- collection-type:` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` or `IAsyncEnumerable`.
- `-- input-model-suffix:` and `-- output-model-suffix:` take characters that can be part of an identifier, and `-- model-namespace:` takes a namespace such as `App.Models`.
- `-- input-model:` and `-- output-model:` take the name of a type, or its full name with its namespace: `UserRow` or `App.Models.UserRow`.  Neither is a reserved keyword, and no generic type is named.
- `-- type-override:` takes one entry, `key = type` or `key = default`.  The key is a type's one-word name, `timestamptz` and not `timestamp with time zone`, and a type outside `pg_catalog`, or a SQL Server type outside `sys`, is written with its schema, `public.user_status` or `dbo.Email`.  A schema or a name may be in double quotes, with a quote inside it doubled: `public."Status"`, `"my schema"."order-status"`; or in `U&"..."` with escapes, `public.U&"Order\000AItem"`, where an escape that is not `\\`, `\` and four hex digits, or `\+` and six, for a code from 1 to 10FFFF (a character beyond FFFF is one `\+` escape or the escapes of its two surrogates), is not valid.  The type is a keyword, a full name or a short name, `System.DateTimeOffset` or `DateTimeOffset`, with no `?` at the end.  See [type overrides](settings.md#setting-type-overrides).
- The key of a `-- type-override:` marker is read by its file's dialect.  In an `mssql` file it is a SQL Server type's name, such as `int`, `nvarchar` or `sys.int`, and a name SQL Server does not have, such as `int4` or `pg_catalog.text`, is this error.  In a file of any other dialect a name that SQL Server alone has, such as `nvarchar` or `rowversion`, is this error.  A type of a schema of your own, `dbo.Email`, is read in either.
- A key is not written in brackets: `-- type-override: [dbo].[Email] = MyApp.Email` is this error, and the message shows the forms that are read, `dbo.Email`, which matches the type's name in any case, and `"dbo"."Email"`, which matches its case exactly.  A key without a schema has the plain form alone, `[INT]` is `INT`, since a quoted name without a schema must be in lower case.  A part that holds anything but letters, digits, `_` and `$`, such as `[my schema]`, has the quoted form alone, `"my schema"`.

A `-- token-ignore:` marker holds one name, a C# identifier, and nothing else.

A `-- token:` marker holds exactly one token with a default, `{{name:default}}`, and nothing else; and a quote or a block comment inside the default must close there.

A `-- param:` marker starts with the parameter as the SQL writes it, `@name`, alone or followed by a space or `->`.  What follows its `->` is a C# type, written as a type override's is, and never `default`, since nothing below a query maps a parameter: `-- param: @author -> default` is this error.

Inside a query, a `-- column:` marker holds a column's name, then `null` or `not null`, `->` and a C# type or `default`, or both in that order, as in `-- column: total not null` or `-- column: note null -> NoteText`.  A name with white space or a `.`, or one that is the word `null` or `not`, is written in double quotes, with a quote inside it doubled: `-- column: "total due" not null`.  A name with a `.` outside quotes, `-- column: users.id -> UserId`, is this error, whose message says that a column's source is mapped in the file header.

In the file header, a `-- column:` marker holds a column's source, `table.column` or `schema.table.column`, then `->` and a C# type or `default`, as in `-- column: users.id -> UserId`.  A marker without `->`, one that says `null` or `not null`, and one whose key has one part are this error.

```sql error
-- column: users.id not null
-- column: id -> UserId

-- name: ListUsers
-- column: users.id -> UserId
-- param: @team -> default
SELECT id FROM users WHERE team_id = @team;
```

Add the missing value, correct the one that is wrong, or remove the one that does not belong.

See: [Marker index](sql-files.md#marker-index)

## SQLSRC112

**Settings conflict**

Two settings contradict each other.  The error is at the second.

- `default` beside another generator parameter in the file header, or inside one query: `default` is the empty list, and a list that holds something is not empty.
- Two `-- dialect:` markers name different dialects, or one dialect with different options.  A file has one dialect.
- Two defaults for one token of a query that differ: two `{{name:default}}` in its SQL, two `-- token:` markers, or one of each.
- Two `-- column:` markers inside one query for one column, its name compared ignoring case, of which one says `null` and the other `not null`, or which give it two C# types, `default` among them.  One may give the nullability and the other the type.
- Two `-- column:` markers in the file header that give one key two C# types.  The key is compared as [column types](settings.md#setting-column-types) compares it, so `users.id` and `"users".id` are one key, and `users.id` and `public.users.id` two.
- Two `-- param:` markers for one parameter that give different types, different nullability or different C# types after `->`.  `not null` counts as different from saying nothing: `-- param: @a int` and `-- param: @a int not null` conflict.  Write both the same.
- Two `-- output:` markers that give different values, or two `-- database:` markers that name different databases (`billing` and `Billing` are different), in the file header or inside one query.
- Two markers of any other setting of the models and the collection type that give different values in the file header or inside one query: two `-- input-model:` markers that name different types, two `-- model-namespace:` markers, and so on.
- Two `-- type-override:` markers that give one database type different C# types, in the file header or inside one query.  The key is compared as [type overrides](settings.md#setting-type-overrides) compares it, so `integer` and `int4` are one type, `public.status` and `public."status"` are one type, and `public.Status` and `public.status` are two; and the C# types are compared by how they are named, so `int` and `System.Int32` are two, and so are `UserId` and `MyApp.UserId`.  The same holds for the C# types of `-- column:` and `-- param:` markers.

```sql error
-- name: ListUsers
-- output: sql
-- output: models
SELECT id, name FROM users;
```

Remove one of the two.  A query's `-- generator:` list replaces the file header's list, and a query's `-- type-override:` for a type replaces the file header's for that type: neither is a conflict, and nor is a query's `-- column:` for a column whose source the file header maps.  The same dialect given twice with the same options is not a conflict either.

See: [A value that is wrong](settings.md#a-value-that-is-wrong)

## SQLSRC113

**Query has no SQL**

A query, or a whole file without `-- name:` markers, holds nothing but whitespace, comments and markers.  A file that cannot be read is reported the same way, at its first line.

```sql error
-- name: GetUser
-- TODO: write this query
```

Write the SQL, or delete the query.

See: [Queries and names](sql-files.md#queries-and-names)

## SQLSRC114

**Token name is a keyword**

A token `{{name}}` becomes a parameter of a generated method, so its name must not be a reserved C# keyword.

```sql error
SELECT * FROM {{class}};
```

The same holds for the name in a `-- token:` marker, and for a token in a comment of a query that keeps its comments.  A token in a comment is an error only for a type that keeps the comments, and the file that holds it then produces no members for that type.

Rename the token.  If the braces are literal text and not a token, add `-- token-ignore: class` to the query.

See: [Tokens](parameters-and-tokens.md#tokens)

## SQLSRC115

**Dialect marker is misplaced**

A `-- dialect:` marker sets the dialect of a whole file, and it changes how the text after it is read.  So it must stand in the file header, before the file's first `-- name:` line and before its first SQL.  This one is inside a named query, or after SQL.

```sql error
-- name: GetUser
-- dialect: mysql
SELECT 1;
```

A marker after the last SQL of its query is reported twice: as this error, and as [SQLSRC108](#sqlsrc108).

Move the marker to the top of the file.  Comments may come before it, such as a licence header.  In a file with no `-- name:` marker, which is one query, put it above the query's SQL.

```sql
-- dialect: mysql

-- name: GetUser
SELECT 1;
```

A file cannot mix dialects.  Put the queries for another database in a file of their own.

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC116

**Marker is not allowed here**

A marker stands in the wrong part of its file.  Some markers describe one query and go inside it, after its `-- name:` line; some describe the whole file and go before the first `-- name:` line.  The message says which this one is.

```sql error
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

Move the marker to where the message says.  A file with no `-- name:` line is one query, and takes every marker.

| Marker | File header | Inside a query |
|----|----|----|
| `-- name:` | no | starts one |
| `-- summary:` | no | yes |
| `-- dialect:` | yes | no |
| `-- generator:` | yes | yes |
| `-- param:` | no | yes |
| `-- token:` | no | yes |
| `-- token-ignore:` | no | yes |
| `-- database:` | yes | yes |
| `-- output:` | yes | yes |
| `-- input-model-suffix:`, `-- output-model-suffix:` | yes | no |
| `-- model-namespace:` | yes | no |
| `-- input-model-type:`, `-- output-model-type:` | yes | yes |
| `-- input-model:`, `-- output-model:` | no | yes |
| `-- collection-type:` | yes | yes |
| `-- type-override:` | yes | yes |
| `-- column:` | yes | yes |

The table is this error's, with two exceptions.  A `-- summary:` in the file header is [SQLSRC107](#sqlsrc107), and a `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

See: [Markers in the wrong place](sql-files.md#markers-in-the-wrong-place)

## SQLSRC117

**Parameter has no type**

A `-- param:` marker without a type names a parameter that does not stand as a parameter in the query's SQL: it appears only in a token's default, or, under `mssql`, only where SqlSource reads it as a variable declared with `DECLARE`, or nowhere, as when its name is misspelt.  Nothing but the marker can then say its type.

```sql error
-- name: ListUsers
-- param: @page
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

```sql error
-- dialect: mssql

-- name: FromStart
-- param: @n
DECLARE @n int = 1;
SELECT * FROM events WHERE id > @n;
```

A marker that gives only a C# type, `-- param: @page -> PageNumber`, has no type either: the C# type is not the database's.

Give the type, in the database's own words: `-- param: @page int`, or `-- param: @page int -> PageNumber`.  Under `mssql`, remove the marker when the name is a variable that the query declares, and give the type when it is a parameter that SqlSource took for a variable: see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  Correct a misspelt name.

See: [Giving a parameter a type: `-- param:`](parameters-and-tokens.md#param-marker)

## SQLSRC118

**Parameter is not declared**

A parameter appears in the default of a token, in the SQL or in a `-- token:` marker, and nowhere else in the query.  A default is a sample, and a type taken from a sample alone would be a guess.

```sql error
-- name: ListUsers
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

Declare the parameter with its type: `-- param: @page int`.

See: [Defaults](parameters-and-tokens.md#token-defaults)

## SQLSRC119

**Query has no parameters**

An `-- input-model:` marker names the type of a query's parameters, and this query has none: no `@name` in its SQL and no `-- param:` marker.  Under the `mssql` dialect a name that the query declares with `DECLARE` is a local variable, and is not a parameter.

```sql error
-- name: CountUsers
-- input-model: CountArgs
SELECT COUNT(*) FROM users;
```

Remove the marker.

See: [Name of one query's model](settings.md#setting-model-name)

## SQLSRC120

**Token crosses a comment**

A token and a comment overlap: the comment is not wholly inside the token's default, nor outside the token.  Comments are removed before tokens are found, and a query that keeps its comments (the `keep-comments` [generator parameter](settings.md#setting-generator-parameters)) has its tokens found in the SQL with them as well, so the two would read different tokens.  It happens three ways.

A comment inside a default holds `}}`.  Without the comment those braces are gone, so the default runs on to the next `}}`, and a token written before that `}}` is lost.  A `--` comment runs to the end of its line, so the `}}` written after one on that line is part of the comment.  The comment is an error wherever the file is used.

```sql error
-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter:AND active -- only live rows}}
ORDER BY {{orderBy:name}};
```

A comment opens a default, `{{name:`, and the SQL after the comment closes it.  Without comments the query is as you wrote it, but where comments are kept the default runs from the comment to the next `}}`, and the token after it is lost.  A token written whole inside a comment, such as `-- see {{note:x}}`, is fine.

```sql error
-- name: ListUsers
-- generator: keep-comments
SELECT id -- e.g. {{note: here
FROM users WHERE {{filter}};
```

A comment stands inside a token's braces before its default: between `{{` and the name, or between the name and the `:` or the closing `}}`.  Without comments the token is read, but where comments are kept the braces stay in the SQL as written, and the method has no argument for the token.

```sql error
-- name: ListFrom
-- generator: keep-comments
SELECT id FROM {{table/* users or archive */}};
```

The second and third are errors only for a type that keeps the query's comments, and that type gets no members from the file until it is fixed.  All three hold for a name in a `-- token-ignore:` marker too, whose text runs to its closing `}}` as a token's does, so a comment there changes which tokens the rest of the query has.  Keep a comment wholly inside the token's default, or outside the token: move it onto a line of its own, or after the token's `}}`.

See: [Defaults](parameters-and-tokens.md#token-defaults)

## SQLSRC200

**The tool failed unexpectedly**

`sqlsource` stopped on an exception that it has no error of its own for.  The message holds the type of the exception and its message.

```console
$ dotnet sqlsource describe
sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.IO.FileNotFoundException: Unable to find the specified file.
```

Here the current folder was deleted while a terminal was still in it.  When the message names such a condition of your machine, mend that.  Otherwise it is a bug in the tool: set the environment variable `SQLSOURCE_DEBUG` to any value, run the command again, and [report it](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.

See: [Environment variables](tool.md#environment-variables)

## SQLSRC201

**No project or solution found**

`sqlsource describe` was given a folder, or none and so the current one, that holds no `.sln`, `.slnx` or `.csproj` file.  The tool looks in that folder alone, not in the folders below it.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.

Run the command from the folder of the project or the solution, or name the one to run on:

```console
$ dotnet sqlsource describe src/App/App.csproj
```

See: [Arguments](tool.md#arguments)

## SQLSRC202

**More than one project or solution found**

`sqlsource describe` was given a folder, or none and so the current one, that holds more than one `.sln`, `.slnx` or `.csproj` file.  The tool does not choose between them, as `dotnet build` does not.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.

Name the one to run on:

```console
$ dotnet sqlsource describe App.slnx
```

See: [Arguments](tool.md#arguments)

## SQLSRC203

**Path is not a project or a solution**

The path given to `sqlsource describe` does not exist, or is a file that is not a `.sln`, `.slnx` or `.csproj` file.  A solution filter, `.slnf`, and a project of another language are not read.  The message holds the full path the tool looked at: a relative path is resolved against the current folder.  An empty path is this error too, which is what a variable that is not set gives in a shell.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.

Give the path of a solution, of a C# project, or of a folder that holds exactly one of them.

See: [Arguments](tool.md#arguments)

## SQLSRC204

**Project does not use SqlSource**

The project given to `sqlsource describe`, or named with `--project`, was restored and does not use the SqlSource package.  A project of a solution that was not named is not an error: it is left out, and nothing is said.

```console
$ dotnet sqlsource describe src/Tools/Tools.csproj
/work/src/Tools/Tools.csproj : error SQLSRC204: '/work/src/Tools/Tools.csproj' does not use SqlSource
    help: add the SqlSource package to the project
```

Add the package with `dotnet add package SqlSource --prerelease` and restore, or run the command on the project that has the queries.  A project with several target frameworks is read by the first of them: one that references SqlSource for a later framework alone is this error.

See: [Arguments](tool.md#arguments)

## SQLSRC205

**Project could not be evaluated**

`sqlsource` asks MSBuild about each project with `dotnet msbuild`: first what the project is, then which files the compiler is given.  One of those runs failed.  The lines under the error give the reason, and then the first twenty lines that MSBuild wrote.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC205: MSBuild could not evaluate '/work/App/App.csproj'
    reason: dotnet msbuild ended with the exit code 1
    msbuild: /work/App/App.csproj(4,5): error MSB4019: The imported project "/work/Shared.props" was not found.
```

Mend what MSBuild reports; `dotnet build` of the project reports the same.  When MSBuild says that the target `SqlSourceWriteManifest` does not exist, the SqlSource package of the project is older than the tool: update the package.  When the reason is that the project file does not exist, the solution lists a project that is not on the disk: restore the file, or take the project out of the solution.  When the reason is that `dotnet` could not be started, the .NET SDK is not installed or not on the path: the tool needs it, and not only the runtime it runs on.

See: [Requirements](tool.md#requirements)

## SQLSRC206

**Project manifest cannot be read**

For each project, `sqlsource` has MSBuild write a file that lists what the compiler is given: the project manifest, which the SqlSource package writes.  The tool could not read it.  The message ends with what is wrong.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC206: The project manifest of '/work/App/App.csproj' cannot be read: it has version '2' of the format and this tool reads version 1.  The SqlSource package and the sqlsource tool are out of step: update the older one
```

The SqlSource package of the project and the `sqlsource` tool are of different versions.  Update the older of the two: the package in the project file, or the tool with `dotnet tool update SqlSource.Tool --prerelease`.  The two are released together under one version number.

Any other reason, one that names a line for one, means that the package wrote a file the tool does not expect of its own version.  That is a bug in SqlSource: [report it](https://github.com/mbcrawfo/SqlSource/issues) with the message.

See: [Install the tool](tool.md#install-the-tool)

## SQLSRC207

**Project is not in the run**

A path given with `--project` is not a C# project of the solution that the run is on.  When the run is on a project, the option may name that project and no other.  The message holds the full path the tool looked at: a relative path is resolved against the current folder, not against the folder of the solution.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.

```console
$ dotnet sqlsource describe App.slnx --project Tools.csproj
sqlsource : error SQLSRC207: '/work/Tools.csproj' is not a project of '/work/App.slnx'
```

Give the path of a `.csproj` file that the solution lists.  `dotnet sln list` shows them.

See: [Options](tool.md#options)

## SQLSRC208

**Attribute argument is not a literal**

The `sqlsource` tool finds `[SqlSourceGenerate]` by reading the C# files of a project as text.  It does not compile them, so it cannot follow a name to its value.  `Path` and `Output` decide which queries are described, and the tool reads those two alone: `Path` must be a string literal, and `Output` a member of `GeneratorOutput` written out.

```csharp
using SqlSource;
internal static class Folders { public const string Queries = "Queries"; }
[SqlSourceGenerate(Path = Folders.Queries)]
internal static partial class Queries;
```

```console
/work/App/Queries.cs(3,27): error SQLSRC208: 'Path' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here
```

Write the value where the attribute is: `Path = "Queries"`, `Output = GeneratorOutput.Models`.  A constant, `nameof`, a concatenation, an interpolated string and a cast of a number are all this error, though the compiler accepts them.  The tool describes nothing for the type until it is mended, so the build reports [SQLSRC241](#sqlsrc241) for each of the type's queries that needs an entry in its sidecar and has none.  Under a dialect that has no database to ask, such as `ansi`, the build reports [SQLSRC209](#sqlsrc209) instead.

See: [Known issues](generator.md#known-issues)

## SQLSRC209

**Output needs a dialect that can be described**

A query whose output is `models` or `codegen` is described by a database, and the dialects the tool can describe are `postgres` and `mssql`.  This file has a query with such an output and another dialect.  The build reports this error, and so does the tool.  The default output is `codegen` and the default dialect is `ansi`, so a project that sets neither gets this error for every file.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC209: The output 'codegen' needs each query described by its database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.
```

For PostgreSQL or SQL Server files, set the dialect, `postgres` or `mssql`: in the [settings file](settings.md#settings-files) of the project's folder for every file under it, in that of the files' own folder for those files alone, or with `-- dialect: postgres` or `-- dialect: mssql` at the top of the file:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

For any other database, and for queries that only want their SQL, set the output to `sql`: `"output": "sql"` in a settings file, `Output = GeneratorOutput.Sql` on the attribute, or `-- output: sql` in the file.  The error is reported once for a file, at the first query that needs types.  Until it is mended, no query of the file is described, and none gets an error about its entry in the sidecar; under the output `models` the file's constants and token methods are generated as before, and under `codegen` its queries get no method.

See: [Database support](dialects.md#database-support)

## SQLSRC210

**Token has no default**

A query with a `{{token}}` cannot be sent to a database as it is written.  To describe it, the tool puts a sample in the token's place: the token's default.  This query's output is `models` or `codegen`, so it must be described, and the token the message names has none.  The build reports this error, and so does the tool, which prints the `help:` line under it.

```sql
-- name: FindUsers
SELECT id, name FROM users {{where}};
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC210: The token 'where' has no default, which a query whose output is 'codegen' needs.  Give it a default, or set the output to 'sql'.
    help: write the token with a default, {{where:default}}, or give one in a marker of the query: -- token: {{where:default}}
```

Give the token a default where it stands, `{{where:WHERE deleted_at IS NULL}}`, or once for the query with a marker, `-- token: {{where:WHERE deleted_at IS NULL}}`.  An empty default, `{{where:}}`, describes the query with nothing there.  The default is a sample for describing and nothing else: the generated method still takes the token as an argument.  A query that only wants its SQL can instead have the output `sql`, which needs no default.  The error is reported once for each token without a default, at the name of its query.  Until it is mended, the query is not described and gets no error about its entry in the sidecar; under the output `models` its constant or token method is generated as before, and under `codegen` it gets no method.

See: [Defaults](parameters-and-tokens.md#token-defaults)

## SQLSRC211

**Database has two dialects**

Each query that is described belongs to one logical database: the one its `-- database:` marker names, or else the `database` of the nearest [settings file](settings.md#settings-files) of its file's folder, or of a folder above it, that sets one, or else the name of its file's dialect.  A name is one database across the whole run, with one connection, and the dialect picks the driver of that connection.  Here two files give one name two dialects.  Names are compared ignoring case.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC211: The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Queries/Users.sql'
```

Give the queries of one of the two files another database, or the dialect of the other file.  The database keeps the dialect of the first file that names it, in the order of the projects and then of the files' paths.  The error is reported once for each other file, at its first query of that database, and no query of that database in that file is described until it is mended.  A query whose output is `sql` belongs to no database and is not counted, and neither is a file that already has [SQLSRC209](#sqlsrc209).

See: [Database](settings.md#setting-database)

## SQLSRC212

**File is not in the run**

A path given to `sqlsource describe` that ends in `.sql` restricts the run to that file.  The run holds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects it is on, and this path is none of them.  The message holds the full path the tool looked at: a relative path is resolved against the current folder.  On Linux and macOS, a path written with backslashes whose `..` go above the root of the file system is reported here too, with its `..` as typed.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.

```console
$ dotnet sqlsource describe Queries/User.sql
sqlsource : error SQLSRC212: '/work/App/Queries/User.sql' is not a .sql file that a type of the run claims
```

Check the spelling of the path.  If the file exists, it is either not a file of the project, as when `SqlSourceIncludeFiles` is off and the project does not list it, or no type claims it: a type claims the `.sql` files in the folder of its own source file, or the ones its `Path` names.  With `--project`, the file must be claimed by a type of one of the projects named.  The rest of the run goes on.

See: [Arguments](tool.md#arguments)

## SQLSRC213

**Database has no connection**

A query that must be described belongs to a database that the run has no connection for.  A connection is given for the name of a database, and never in the project, since it holds credentials.  On the command line it is `--connection billing=<connection string>`.  In the environment it is the variable of the name: `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`, so `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  `SQLSOURCE_CONNECTION`, with no name, serves a run that has exactly one database that can be described: a database that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), does not count.  The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  A variable that is empty or holds only white space is not set.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC213: No connection is given for the database 'postgres'
    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES
```

Give the connection in one of the two ways the `help:` line names, or leave the database out of the run with `--database`.  When `--connection` was given for a name that no database of the run has, the `help:` line lists the names that are close to a database of the run, so that a misspelt one is seen, and counts any other by the position of its value on the command line, as `The value of --connection at position 3 names no database of this run`: a name that is far from every database of the run, such as the start of a connection string typed without its `name=`, is not printed.  The error is reported once for a database, at its first query that must be described, and those queries count as failed.  A database whose queries all have a current entry needs no connection, and its summary line says `no connection`; under `--force` every query is described, so every database of the run needs one.

See: [Connections](tool.md#connections)

## SQLSRC214

**Two databases share a connection variable**

The variable of a database's name is the name in upper case with every character that is not a letter or a digit written as `_`.  Two databases of this run have names that give one variable, as `app-v2` and `app_v2` do, so the variable cannot say which of them it is for.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2
```

Give both databases their connection on the command line, `--connection app-v2=<connection string> --connection app_v2=<connection string>`, or rename one of them.  A connection on the command line for one of the two is not enough: the variable would then be read for the other alone, and a reader of the command could not tell.  The error is reported once for each of the two databases that has a query to describe, at its first such query, and those queries count as failed.

See: [Connections](tool.md#connections)

## SQLSRC215

**Connection names no database**

`SQLSOURCE_CONNECTION` is set, and it is the connection of a run with exactly one database.  This run has more, and the database of this query has no connection of its own, so nothing says that the variable is for it.  The message counts and names the databases that can be described: one that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), needs no connection and is left out.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: postgres, reports
    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_REPORTS for 'reports', or pass --connection <name>=<connection string> for each
```

Set the variable of each database, as the `help:` line names them, or give each with `--connection`, or restrict the run to one database with `--database`.  The error stands in the place of [SQLSRC213](#sqlsrc213) and is reported as that one is: once for a database, at its first query that must be described.

See: [Connections](tool.md#connections)

## SQLSRC216

**No describer for the dialect**

No dialect of this release gets this error: the tool describes `postgres` and `mssql`, the two dialects that pass [SQLSRC209](#sqlsrc209).  It is kept for a database whose dialect passes that check before the tool can describe it, where it would be reported at each query that the run would have described, and each would count as failed.  A dialect that the tool does not describe at all is SQLSRC209, every row marked No in [database support](dialects.md#database-support).

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC216: This version of sqlsource cannot describe 'mssql'
```

A tool of an earlier release reported it so for `mssql`, which that release could not describe.  Update the tool, together with the `SqlSource` package, as [SQLSRC206](#sqlsrc206) asks.

See: [Database support](dialects.md#database-support)

## SQLSRC217

**Sidecar was not written**

A sidecar is written whole or not at all: when every query of its `.sql` file that needs an entry was described in this run or already has a current one.  Here the run described a query of the file and could not write the result, because another query of the file has neither.  The lines under the message name each such query and say why: it `failed`, with an error of its own above, or it is `not in this run`, because `--database` or a `.sql` path left it out and its entry is missing or out of date.

```console
/work/App/Queries/Users.sql : error SQLSRC217: The sidecar of '/work/App/Queries/Users.sql' was not written, because not every query of the file has an entry
    query: GetInvoice: not in this run
    help: describe the whole file, or fix the queries that failed
```

Mend the queries that failed, or run `describe` without the filter that left a query out, so that every query of the file is described in one run.  The sidecar on the disk is as it was before the run.  The error is there so that a description that was not saved does not look like a success; a file that was held back with none of its queries described has only the errors of those queries.

See: [Describing again](tool.md#describing-again)

## SQLSRC218

**File could not be changed**

The tool could not write a sidecar, or could not delete one that its `.sql` file no longer needs, or could not read such a one to see that it may be deleted, or could not write the log of `--log` or `SQLSOURCE_LOG`.  The message says which, and ends with the reason the operating system gave.

```console
/work/App/Queries/Users.sql.json : error SQLSRC218: '/work/App/Queries/Users.sql.json' could not be written: Access to the path '/work/App/Queries/Users.sql.json' is denied.
```

**Fix:** give yourself the right to write in the folder of the `.sql` file, or close the program that holds the sidecar open, and run the command again.  The rest of the run goes on after an error about a sidecar.

- **A sidecar is never left half written.**  It is written to a temporary file beside it and then moved over the old one, so a run that fails or is stopped leaves the old file or the new one.
- **A sidecar is deleted only** in a run with no `--project`, `--database` or `.sql` path, in which nothing was reported as an error before the files were decided: a setting that is not valid, or a type that could not be read, can make a file look as if it needs no entry.
- **A sidecar that the tool cannot open**, for lack of permission, because it is locked or because it is a folder, is not deleted when the file beside it needs no entry, since it may be the file of a newer tool.  It is reported as one that `could not be read`.  Delete it yourself, or give yourself the right to read it.  `describe --check` reports the same, since it could not check the file.

For a log:

- **The log cannot be made or emptied as the run starts**, because its folder is not there, it is a folder, or the system will not let the tool write it.  Nothing runs and the exit code is 1.  Give a path that can be written, or run the command from another folder.
- **The path ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`**, in any case.  The tool refuses it with the reason `a log is not written over a .sql file, a sidecar, a project or a solution`, touches nothing and runs nothing, so that a name left out before a `.sql` path does not empty that file.  Give the log a name of its own.
- **The log stops being writable while the run goes on**, as when the disk is full.  The run finishes and writes its sidecars, the error is reported once at the end, and the exit code is 1.  The log holds what was written before it stopped.

See: [Logs](tool.md#logs)

## SQLSRC219

**Sidecar is out of date**

`describe --check` asked the database to describe every query of the run and compared what it said with the sidecars that are there.  It changes no file.  The message names a query and says what differs, or names the `.sql` file when the whole sidecar does:

| The message ends with | What it means |
|----|----|
| `the sidecar does not exist` | A query of the file needs an entry, and the file has no sidecar |
| `the sidecar cannot be read`, or `the sidecar has format 2, and this tool writes 1` | The sidecar is no sidecar of this version of the tool.  A merge conflict in it is the usual cause of the first |
| `the sidecar has no entry` | The query was added, or renamed, since the sidecar was written |
| `the query has changed since it was described` | The SQL, the type or the nullability of a `-- param:` marker, or the default of a token changed |
| `columns[2].nullable is true, and the database says false` | The database describes the query another way than the sidecar holds.  A change to the schema is the usual cause |
| `database is "billing", and the project says "reports"` | The query belongs to another database, or another kind of database, than the one it was described against |
| `the query is not in the file`, or `the query needs no entry` | The sidecar holds an entry for a query that was removed, or whose output is now `sql` |
| `no query of the file needs an entry` | The sidecar belongs to a file that needs none any more |

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': columns[2].nullable is true, and the database says false
    help: run 'dotnet sqlsource describe --force' and commit the sidecars
```

**Fix:** the `help:` line under each error names the command that mends it, and you then commit the sidecars it writes:

| The message | Run |
|----|----|
| Names a query that was added, changed or removed, another database, or a sidecar that does not exist, cannot be read or has a lower format | `dotnet sqlsource describe`, with the filters the check had |
| Names a difference that the database reports, such as `columns[2].nullable` | `dotnet sqlsource describe --force`: the tool cannot see a change to the schema, and skips a query whose SQL is as it was |
| Says the sidecar has a higher format than this tool writes | Update the `SqlSource.Tool` package: a newer tool wrote the sidecar, and `describe` leaves it as it is |

- **Columns and parameters are counted from 0**: `columns[2]` is the third column.
- **Only the first difference of a query is named.**
- **What is compared is what changes generated code**: the query as written (its SQL, the types and nullability of its `-- param:` markers, and its tokens' defaults), its database and the kind of that database, whether it returns rows and which table's rows they match, if any, and the name, position, type and nullability of each parameter and column.  The versions of the tool and of the server are not compared.
- **With `--database`**, an entry for a query that is no longer in the file, or that needs none, is reported, whatever its database, when `describe` with the same filters would rewrite the sidecar, which drops it.  A sidecar that such a `describe` leaves as it is, because another query of the file, of a database that `--database` does not name, has changed or was never described, keeps the entry, and the check does not report it.

The tool exits with the code 2 when it reported this error and no other, so that a build can tell a sidecar that is out of date from a check that could not be made.  A query that could not be described has an error of its own, and the exit code is then 1.

See: [Check in CI](tool.md#check-in-ci)

## SQLSRC220

**Project was not restored**

The tool tells that a project uses SqlSource by the package's MSBuild files, and those reach a project only through a restore.  This project either references the SqlSource package and has nothing of it, so it was not restored since the reference was added, or has no file of a restore at all, so nothing says whether it uses SqlSource.  The tool does not restore: a restore is slow and writes into the project.

```console
$ dotnet sqlsource describe
/work/src/App/App.csproj : error SQLSRC220: '/work/src/App/App.csproj' has not been restored, or not since the SqlSource package was added to it
    help: run 'dotnet restore'
```

Run `dotnet restore` on the solution or the project, then the command again.  In a solution every C# project is checked, one that has no SQL too: a run on a fresh checkout must not succeed by finding nothing.  A reference that keeps the package's MSBuild files out of the project, `ExcludeAssets="build"` for one, gives this error after a restore as well: remove that from the reference.

See: [Requirements](tool.md#requirements)

## SQLSRC221

**Sidecar was written by a newer tool**

A sidecar, the `.sql.json` file beside a `.sql` file, says which version of the format it has.  This one has a higher version than this tool writes, so a newer `sqlsource` wrote it, and this one must not write it back down: the generator of that newer version would no longer read it.

```console
/work/App/Queries/Users.sql.json : error SQLSRC221: '/work/App/Queries/Users.sql.json' has format 2, and this tool writes format 1
    help: update the SqlSource.Tool package
```

Update the tool, with `dotnet tool update SqlSource.Tool --prerelease`, to the version of the SqlSource package the project uses.  The file is left as it is, and every query of its `.sql` file that the run was asked to describe counts as failed.  A sidecar of a lower format version, of another version of the tool, or one that cannot be read, a merge conflict for one, is no error: the tool describes its queries and writes it again.  A run that may delete a sidecar, because a file needs no entry any more, does not delete one of a higher format version either.

## SQLSRC222

**Directory cannot be read**

`sqlsource describe` was given a folder, or none and so the current one, and the operating system would not list its files.  The message ends with the reason the system gave.  A path that holds `=` or `;` is named by its position on the command line instead, as `The path at position 2`, since it may be a connection string typed in the wrong place.  The reason is then only that the system would not list the files, since the system's own words repeat the path.

```console
$ dotnet sqlsource describe /srv/locked
sqlsource : error SQLSRC222: '/srv/locked' cannot be read: Access to the path '/srv/locked' is denied.
```

Give yourself the right to read the folder.

## SQLSRC223

**Solution cannot be read**

The `.sln` or `.slnx` file that `sqlsource describe` was given, or found in the folder, is not a solution that can be read.  The message ends with the reason of the library that reads it, which is the one the `dotnet` command uses.  A solution that lists one project twice is refused too.

```console
$ dotnet sqlsource describe
/work/App.sln : error SQLSRC223: '/work/App.sln' cannot be read: Not a solution file.
```

Mend the file.  `dotnet sln list` reads it the same way.

## SQLSRC224

**Database could not be opened**

The tool could not open a session with the database of a query that must be described: nothing answers at the address, the server refused the user or the password, the database does not exist, the driver cannot read the connection string, or a file that the connection string names cannot be used.  The `server:` line says what went wrong in the tool's own words, after the server's SQLSTATE or SQL Server's error number when the server gave one.  It names neither the host nor the user: what the server or the driver said would name them, or quote the connection string or a path in it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC224: The database 'app' could not be opened
    query: GetUser, database app, postgres
    server: 28P01: the server refused the password
    help: check that the database is running and that the connection string names it
```

**Fix:** start the database, or correct the connection string that `--connection` or the database's variable gives.  The error is reported once, at the first query of the database that the run would have described, and each of them counts as failed.

The `server:` line is one of these.  A SQLSTATE is PostgreSQL's code of the error, listed in [PostgreSQL's error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html).

| The `server:` line | What it means |
|----|----|
| `28P01: the server refused the password` | The password is wrong for the user.  When the server asks for a password, PostgreSQL says the same of a user that does not exist |
| `28000: the server refused to authenticate the user`, or another SQLSTATE that starts with `28` | The server refuses the user, as when its `pg_hba.conf` has no entry for this machine, the user and the database, the user may not log in, or the user does not exist |
| `3D000: the server has no such database` | The database that the connection string names is not on the server |
| `42501: the user may not connect to the database` | The user has no `CONNECT` privilege on the database |
| `53300: the server has too many connections` | The server, the user or the database has reached its limit of connections: try again later, or raise the limit |
| `57P03: the server is not accepting connections now` | The server is starting, stopping or recovering: try again when it is up |
| `08P01: the server refused the connection`, or another SQLSTATE that starts with `08` | The server would not make the connection |
| `<SQLSTATE>: the server refused to open a session` | Any other error of the server while the session was opened: its SQLSTATE says which |
| `the server asks for a password and the connection string gives none` | The server asks for a password, and neither the connection string, `PGPASSWORD` nor a password file gives one |
| `the connection to the server timed out` | Nothing answered at the address in time: the address drops what is sent to it, a firewall between drops the connection, or the server is too slow to answer |
| `Npgsql could not reach the server: ConnectionRefused` | The connection could not be made.  The word after the colon is the system's reason, such as `ConnectionRefused` when nothing listens at the port, or `HostNotFound` or `TryAgain` for a host name that is not found |
| `Npgsql could not open a connection to the server (<name of the error>)` | Any other failure of the driver while it opened the session.  The name in parentheses is the driver's or the system's name of the error, such as one of a TLS handshake that failed |
| `Npgsql cannot read the connection string` | The string is not written as Npgsql wants one: `Host=localhost;Database=app;Username=app;Password=...` |
| `a file that the connection string or the environment names cannot be read` | A client certificate, a key or a password file (`SslCertificate`, `SslKey`, `Passfile`, or `PGSSLCERT`, `PGSSLKEY`, `PGPASSFILE`) is not there, its folder is not there, or the tool may not open it |
| `a file that the connection string or the environment names is not what Npgsql expects: it is not in the right form, or its password is wrong` | Such a file was read and could not be used, as when a line of a password file is no entry |
| `Npgsql does not support these settings of the connection string together` | A `Target Session Attributes` other than `Any` for one host, or a `Replication Mode` for several |
| `GSS authentication is not available on this machine` | The server asks for GSS, and the operating system's GSS library cannot be loaded |
| `Npgsql cannot read the database's types` | Npgsql read the database's types when it connected and could not use them: a schema before `pg_catalog` on the connection's search path defines an operator `=` on two `oid` or two `"char"`.  Put `pg_catalog` first on the search path of the connection the tool uses, or leave that schema off it |

For SQL Server, the line starts with `Msg <number>, Level <level>, State <state>:` when the server or SqlClient numbered the error, as in `Msg 18456, Level 14, State 1: the server refused the login`.  The number is SQL Server's, listed in [Database Engine errors](https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors).  The text is one of these:

| The text of the `server:` line | What it means |
|----|----|
| `the server refused the login` | The login or its password is wrong, or the login may not connect (`Msg 18456`) |
| `the server cannot open the database for the login` | The database that the connection string names is not on the server, or the login may not open it (`Msg 4060`), or the login's default database cannot be opened (`Msg 4064`).  The server follows a `Msg 4060` line with a `Msg 18456` line, `the server refused the login`, which adds nothing to it |
| `the connection to the server timed out` | Nothing answered at the address in time: the address drops what is sent to it, a firewall between drops the connection, or the server is too slow to answer |
| `SqlClient could not reach the server: ConnectionRefused` | The connection could not be made.  The word after the colon is the system's reason, such as `ConnectionRefused` when nothing listens at the port, or `HostNotFound` for a host name that is not found.  On Linux the line has no reason |
| `the server's certificate is not trusted, or the TLS handshake failed` | The server's certificate is not one this machine trusts.  Install the certificate's authority, or, for a server of your own such as a container, add `TrustServerCertificate=True` to the connection string |
| `the server refused to open a session` | Any other error while the session was opened: its number says which |
| `SqlClient cannot read the connection string` | The string is not written as SqlClient wants one: `Server=localhost;Database=app;User ID=app;Password=...`.  It has a keyword that SqlClient does not know or does not support, such as `Network Library` or `Context Connection`, or a value that is not of its keyword's kind or is out of its range, such as a `Connect Timeout` too large |
| `SqlClient does not accept these settings of the connection string together` | Two settings that SqlClient reads one by one and refuses together, such as `Integrated Security` with an `Authentication` |
| `the connection string is empty` | The value sets nothing, such as `;` |
| `the Authentication of the connection string needs both a User ID and a Password` | An `Authentication` of `Sql Password`, `Active Directory Password` or `Active Directory Service Principal` signs in with a user and a password, and the connection string lacks one of them or leaves it empty.  Give both; an `Active Directory` method then needs a provider the tool does not have, the row below, so use a SQL Server login and its password, or, on Windows, Windows authentication with `Integrated Security=True` |
| `SqlClient does not support this server on this platform` | The connection string names a server that cannot be used from this operating system: a LocalDB instance, such as `(localdb)\MSSQLLocalDB`, can be used on Windows alone.  Describe against a SQL Server that listens on a port, such as one in a container |
| `SqlClient has no provider for the Authentication of the connection string` | An `Authentication` of Microsoft Entra ID, `Active Directory ...`, which the tool cannot sign in with.  Use a SQL Server login and its password, or, on Windows, Windows authentication with `Integrated Security=True` |

See: [Connecting](postgresql.md#connecting), [connecting to SQL Server](sqlserver.md#connecting)

## SQLSRC225

**Server rejected the query**

The tool asked the server to prepare the query, and the server refused it: a syntax error, a table or a column the database does not have, an operator that does not exist for the types it is given.  The message ends with what the server said, and the `server:` lines give all of it: the SQLSTATE and the message, then its `detail:` and `hint:` where it gave them.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P01: relation "user" does not exist
    server: at character 15
```

**Fix:** mend the query, or bring the database to the schema the query was written for.

- **`at character`** counts from the start of the query's SQL as the tool sent it: without its comments, with each token's default in the token's place and `$1` in the place of each parameter, so it is close to the place in the file and not always the same.  Run with `--log` for the SQL as it was sent.
- **An error at the step `resolve type`** has no `at character` line: there the server refused the tool's question about a type, and not the query.
- **A type that a `-- param:` marker declares** is checked against every use of the parameter, so a declared `int` compared with a text column is this error too.

A parameter with no declared type takes the type of its first use, so `placed_at > now() - @age` makes `@age` a `timestamp with time zone`, and the server then reports `operator does not exist: timestamp with time zone > interval`, an operator that the SQL does not hold.  For an error about a type or an operator, when the query has a parameter with no declared type and a use with no cast, the `help:` line says that the server may have given it the type of its first use, and names each such parameter.  The help is a guess: the server does not say whether its error is at a parameter, so the help can appear for an error that is not, such as a misspelt function.  The `at character` line says where the error is.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC225: The server rejected the query 'ListRecentOrders': operator does not exist: timestamp with time zone > interval
    query: ListRecentOrders, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42883: operator does not exist: timestamp with time zone > interval
    server: hint: No operator matches the given name and argument types. You might need to add explicit type casts.
    server: at character 86
    help: if the error is at a use of a parameter, the server gave @age the type of its first use, which may not be the type meant: cast the parameter where it is used to the type meant, @age::<type>, or declare its type with -- param: @age <type>
```

Write the type meant in the place of `<type>`: here `@age::interval`, or `-- param: @age interval`.

A statement that the server cannot prepare, though its SQL is valid, such as `CALL` or `CREATE TABLE`, is [SQLSRC233](#sqlsrc233) and not this error.

It is also given when a type that the query uses was dropped while the tool described it, since another session changed the schema between two of the tool's questions.  A table or a column that the query reads cannot be dropped then: a `DROP TABLE`, or an `ALTER TABLE` that drops or changes a column, waits until the tool has described the query.  The message says what went, as in `the type of the column 'id' no longer exists`; there is no `server:` line, since the server said nothing, and the help says to run the tool again.

It is also what a connection that the server ended while the queries were described is reported as.  The query that finds it out has the server's SQLSTATE and `the server ended the session`, as in `57P01: the server ended the session` for a backend that was ended, and it names neither the host nor the user.  When the server said nothing, the line is `the connection to the server was lost`, or `the server did not answer in time` when Npgsql stopped waiting for its answer, with no SQLSTATE.  Every query of that database after it has the line `the connection to the server was lost`, with no SQLSTATE: start the run again.

The step `check parameters` gives it too, for an error of the tool's comparison with Npgsql that says nothing of how Npgsql reads the SQL: a connection that was lost, or an error of the server that is not of class `42` (syntax errors and access rule violations), such as a statement that was cancelled or timed out.  The `server:` line is what the server said, or one of the lines above for a connection that was lost or ended, and the message ends with it.  An error of class `42` there is [SQLSRC231](#sqlsrc231), since a `@` that the two read differently gives one.  Start the run again.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'ListUsers': canceling statement due to statement timeout
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 57014: canceling statement due to statement timeout
```

For SQL Server, each error the server gave is a `server:` line, `Msg <number>, Level <level>, State <state>:` and its text, the cause first.  The last is often `Msg 11501`, which says only that the query did not compile, and the message ends with the cause's text:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': Invalid column name 'nope'.
    query: GetUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    server: Msg 207, Level 16, State 1: Invalid column name 'nope'.
    server: Msg 11501, Level 16, State 2: The batch could not be analyzed because of compile errors.
```

The step can also be `describe columns`, when SQL Server accepted the query's parameters and could not work out its columns.  There each line is `Msg <number>:` and its text, with no level or state, and the last is often `Msg 11529`, which says only that every path of the query failed.  A call of a procedure that the database does not have, with no argument, is such a query:

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC225: The server rejected the query 'ArchiveOrders': Could not find stored procedure 'dbo.archive_ordrs'.
    query: ArchiveOrders, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    server: Msg 2812: Could not find stored procedure 'dbo.archive_ordrs'.
    server: Msg 11529: The metadata could not be determined because every code path results in an error; see previous errors for some of these.
```

The same call with an argument, `EXEC dbo.archive_ordrs @before`, is `Msg 2812` at the step `describe parameters`, with its level and state, then `Msg 11501`.  Correct the procedure's name, or bring the database to the schema the query was written for.

- **An `OUTPUT` clause without `INTO`, on a table that has an enabled trigger,** is `Msg 334`.  SQL Server refuses the statement when it runs, too.  Output the rows `INTO` a table variable and select them from it.
- **A table or a column that the database does not have** is `Msg 208`, `Invalid object name`, or `Msg 207`, `Invalid column name`, as in the first SQL Server example.  A temporary table that the query reads and does not create is `Msg 208` too, since it does not exist when the query is described; one that the query creates is [SQLSRC265](#sqlsrc265).
- **A `GO` inside the query** is `Msg 102`, `Incorrect syntax near 'GO'`.  `GO` ends a batch in SQL Server Management Studio and `sqlcmd`, and is not T-SQL: give each batch a `-- name:` of its own.
- **A misspelt keyword at the start of the query**, such as `SELEC 1`, is `Msg 2812`, `Could not find stored procedure 'SELEC'`, then `Msg 11501`: SQL Server reads a query that starts with a name as a call of the procedure of that name.  Correct the spelling.
- **A type that a `-- param:` marker declares** is checked against every use of the parameter, so `-- param: @p xml` with `WHERE id = @p` is this error, `Msg 206`, `Operand type clash: xml is incompatible with int`.  A type that the query's own text names and the database lacks, `CAST(@p AS dbo.Nope)`, is this error too.
- **A parameter whose name, with its `@`, is longer than 128 characters** is this error, `Msg 103`, `The identifier that starts with '...' is too long`, whether it is used once, used more than once or declared with `-- param:`; a declared one gives it at the step `resolve type`.  Give the parameter a shorter name.
- **A statement whose result SQL Server cannot work out**, such as `CREATE PROCEDURE`, is `Msg 11527`, `... does not support metadata discovery`.  Give the query `-- output: sql`.
- **A connection that was lost** has the line `the connection to the server was lost`, or `the server did not answer in time` when SqlClient stopped waiting for its answer, after the error's number.  Every query of that database after it has the line `the connection to the server was lost`: start the run again.  At the step `resolve type`, the server could not be asked about a declared type for the same reasons.

## SQLSRC226

**Parameter type could not be determined**

The server gives each parameter a type from the way the query uses it, and nothing in this query says what the parameter is: on PostgreSQL, `SELECT @p IS NULL` holds for a value of any type.

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'IsNull' could not be determined
    query: IsNull, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P18: could not determine data type of parameter $1
    help: cast the parameter where it is used, @p::int, or declare its type with -- param: @p int
```

Cast the parameter where it is used, or declare its type with a `-- param:` marker.  The same error, at the step `resolve type` and with no `server:` line, says that the server did give the parameter a type and that the type is a pseudo-type, which no value has: `row_to_json(@p)` makes `@p` a `record`.  Its `help:` line names the pseudo-type, and the fix is the same.

SQL Server gives the error for a parameter whose uses fix no type, `SELECT @p AS x` or `COALESCE(@p, name)`, for two parameters in one expression with nothing else, `@a + @b`, and for a parameter that no type would make the query valid.  The `server:` line is SQL Server's error, which names the parameter:

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'Echo' could not be determined
    query: Echo, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    server: Msg 11506, Level 16, State 1: The parameter type for '@p' cannot be uniquely deduced; two possibilities are 'sql_variant' and 'xml'.
    help: add a CAST around a use of the parameter, or declare its type with -- param:
```

Write `CAST(@p AS int)` where the parameter is used, or `-- param: @p int`.  A parameter whose every use is like `@p IS NULL` is not this error: SQL Server gives it the type `int`.

The same error with no `server:` line is for a parameter used more than once whose uses SQL Server gives different types, or gives one of its uses no type.  The uses that say nothing of the type, such as `@p IS NULL`, are then left out, as [SQLSRC262](#sqlsrc262) says, and this error is given when every use is left out that way, or when a use that is left has no type.  When the uses that are left have two types, the error is [SQLSRC262](#sqlsrc262) instead, and when every use gets the same type, the parameter has that type and no error is given.  The fix is the same: a `CAST` at a use, or a `-- param:` marker.

See: [Parameters](postgresql.md#parameters), [SQL Server parameters](sqlserver.md#parameters)

## SQLSRC227

**Parameter's first use does not fix its type**

PostgreSQL takes a parameter's type from its first use.  Here the first use says nothing of the type and a later one does, which the server refuses.  The pattern of an optional filter does it: in `(@name IS NULL OR name = @name)` the first `@name` could be anything.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC227: The first use of the parameter '@name' of the query 'FindUsers' does not fix its type
    query: FindUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P08: could not determine data type of parameter $1
    server: at character 28
    help: declare the type with -- param:, or write the use that fixes the type before the one that does not: (name = @name OR @name IS NULL)
```

Declare the parameter's type with `-- param: @name text`, or turn the condition round so that the use that fixes the type comes first.

See: [Parameters](postgresql.md#parameters)

## SQLSRC228

**Declared type is not valid**

A `-- param:` marker gives a parameter a type, and the tool asks the server to resolve that type and to check it against every use of the parameter.  This type cannot be used, for the reason the message ends with:

- `it is not written as a type name`: the text is not a type name as a column definition writes one, with at most one modifier in parentheses and `[]`.  The server can give the same reason for a type it cannot use, such as `numeric(2,-3)` on PostgreSQL 14, which has no negative scale; the `server:` line then says what the server made of the type.
- `the database has no such type`: the server knows no type of that name on the session's search path.
- `it is a pseudo-type`: the server knows the name, and no value has that type, `record` and `anyelement` for two, or it is an array of such a type, `cstring[]`.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'user_state' declared for the parameter '@status' of the query 'ListUsers' is not valid: the database has no such type
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42704: type "user_state" does not exist
    help: write a type the database has, schema-qualified if it is not on the search path
```

Write the type as the database has it: `int`, `varchar(50)`, `numeric(18,2)`, `timestamp(3) without time zone`, `public.user_status`, `"My Schema"."Status"[]`.  A type in a schema that is not on the search path of the connection needs its schema.  A marker's type runs to the end of its line, but for a `null` or `not null` that ends it, so nothing else may follow it there.

For SQL Server the error is at the step `resolve type`, and its reasons are these:

- `it is not written as a type name`, followed by what was found: the text is not a type name as T-SQL writes one, a name of one or two parts, each plain, `[bracketed]` or `"quoted"`, then `(max)` or one or two numbers in parentheses.  A `;`, a comment, a second `(` and a line break are refused.  Nothing of the query is sent.
- `the database has no such type`: the database has no type of that name, `Msg 2715`.
- `it is not a type`: SQL Server did not accept the text as a type, such as `nvarchar(9000)`, longer than the most it allows, `Msg 131`.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'dbo.Nope' declared for the parameter '@email' of the query 'FindUser' is not valid: the database has no such type
    query: FindUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: resolve type
    server: Msg 2715: Column, parameter, or variable #1: Cannot find data type dbo.Nope.
    server: Msg 2724: Parameter or variable '@email' has an invalid data type.
    server: Msg 11501: The batch could not be analyzed because of compile errors.
    help: write a type the database has, with its schema when it is not a built-in type: dbo.Email
```

Write the type as a column definition would: `int`, `nvarchar(50)`, `varchar(max)`, `decimal(18,2)`, `dbo.Email`, `[billing].[Amount]`.  An alias type needs its schema unless it is in `dbo` or in the default schema of the login.

See: [Parameters](postgresql.md#parameters), [SQL Server parameters](sqlserver.md#parameters)

## SQLSRC229

**Column type is not supported**

A column of the query has a pseudo-type, a type that the server knows by name and that no value read from a column can be given.  `record` is the one a query meets: a row that is built in the select list and not taken apart again, `ROW(a, b)`, or the alias of a subquery selected whole.  An array of one, `record[]`, is another.  The tool cannot say what such a column holds, so the query is not described.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC229: The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: resolve type
    help: cast the column to a concrete type, or give the expression an alias and a cast
```

Select the fields of the row as columns of their own, or cast the expression to a type the database has: `ROW(a, b)::pair`, with `pair` a composite type, or `to_jsonb(s)` for a whole row.  A parameter of a pseudo-type that the query returns is reported for the parameter, as [SQLSRC226](#sqlsrc226) or [SQLSRC228](#sqlsrc228).

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC230

**Query holds more than one statement**

On PostgreSQL a query is one statement.  The tool prepares it on the server to describe it, and it found a second statement in the text.  Nothing of such a query is sent.  A SQL Server query can hold several statements: see [what SQL Server can describe](sqlserver.md#what-can-be-described).

```console
/work/App/Queries/Cleanup.sql(1,10): error SQLSRC230: The query 'Cleanup' holds more than one statement
    query: Cleanup, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: one statement for each query; a ; is allowed at the end alone
```

Give each statement a `-- name:` of its own.  A `;` at the end of the statement is allowed, with white space and comments after it; a `;` inside a string, a dollar-quoted string or a comment does not count.  A `;` inside parentheses counts as the end of a statement, since the tool does not read the structure of the SQL; no statement that can be prepared holds one.

A text that the tool reads as one statement and the server as two is [SQLSRC225](#sqlsrc225) with the server's `42601: cannot insert multiple commands into a prepared statement`.  Nothing of it is run either.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC231

**Parameters are read differently**

SqlSource finds the parameters of a query with its own reading of the SQL, and describes the query that way.  The tool checks that reading against the database's own, and the two differ, so what was described is not what would run.  The message starts with what reads the parameters differently: Npgsql for PostgreSQL, SQL Server for SQL Server.

For PostgreSQL, a generated query method sends the SQL as you wrote it and lets Npgsql find the `@name` parameters, so the tool also asks Npgsql to describe the SQL that way, and compares the two results: the columns, and where each parameter stands.  The message ends with what differs: where the two texts part, the number of columns, the name or the type of one, or what the server said of the SQL as Npgsql sent it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: syntax error at or near "@"
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 42601: syntax error at or near "@"
    help: a @ that one of the two reads as a parameter and the other as text; the server's lines say where; a type that another session changed during the run shows the same way, so run describe again
```

**Fix:** look for a `@` that stands where only one of the two would read a parameter: inside an unusual string or an identifier, or directly after another character of a name.  Put a space before the parameter, or write the text so that the `@` is plainly inside a string.

Npgsql's own refusal of the text is this error too.  It has no SQLSTATE, and the `server:` line is Npgsql's message: a `;` followed by more text, which Npgsql reads as the end of a statement where SqlSource read no end, is one.

A difference may leave the columns the same, and then the server accepts both readings.  Npgsql also reads `:name` as a parameter, so `tags[:id]` in a query with a parameter `@id` and a column `id` is a subscript for Npgsql, which sends `tags[$1]`, and the name of a column for SqlSource.  The query still returns the same columns, and only the comparison of the two texts sees it:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: the SQL with $n parts at character 45: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    help: a @ or a : that one of the two reads as a parameter and the other as text; the message says where the two texts part
```

The query is `SELECT id FROM users WHERE id = @id AND tags[:id] IS NOT NULL`.  The position counts the characters of the SQL as the tool sent it, with `$1` in the place of each parameter and without the query's comments: it is not a position in the file.  Rename the parameter so that it is not the name of a column that a slice reads.  Where the `:` follows a bound, `tags[1:id]`, Npgsql sends `tags[1$1]` and the server answers a `42601` like the first example's.

The error also follows a schema that changed while the tool ran: a type that another session dropped between two of the tool's questions gives an error of class `42` at this step, which cannot be told from a misread `@`.  A table or a column that the query reads cannot be dropped or changed then: a `DROP TABLE`, or an `ALTER TABLE` that drops or changes a column, waits until the tool has described the query.  Run `describe` again.

One difference has no `@` in it: a parameter that is an array of a domain, `-- param: @p email_t[]`, and is returned as a column.  Npgsql sends it as an array of the domain's base type, so the column is `character varying[]` for Npgsql and `email_t[]` for SqlSource, and the help is `Npgsql sends a parameter that is an array of a domain as an array of the domain's base type, so the column it is returned as has another type; cast the column, @p::varchar[] for an array of a domain over varchar` in place of the first example's help.  Cast the column to the type you want, `@p::varchar[]`.

An error of the check that says nothing of how Npgsql reads the SQL, such as a lost connection, is [SQLSRC225](#sqlsrc225) at the same step.  If the SQL is plain and the two still differ, report it.

For SQL Server, the tool compares the parameters that SqlSource reads in the query with those that SQL Server reports, ignoring case, and the message names each one that only one of them reads.  Under `mssql`, SqlSource reads a name as a [variable declared with `DECLARE`](parameters-and-tokens.md#declared-variables) when it stands after a comma of a list of declarations, and that list ends at a `;` or at a word that starts a statement, such as `SELECT` or `SET`.  A label is no such word, and neither is a word of the statements of Service Broker, such as `RECEIVE`.  So in this query SqlSource reads `@p` as a variable, and SQL Server as a parameter:

```sql mssql
-- name: NextMessage
DECLARE @x int = @n next_message: RECEIVE TOP (1) message_body, @p + 1 AS q FROM dbo.messages
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: SQL Server reads the parameters of the query 'NextMessage' differently from SqlSource: SQL Server reads '@p' as a parameter and SqlSource does not
    query: NextMessage, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: check parameters
    help: rewrite the query so that SqlSource and SQL Server read the same parameters, or give a parameter that SqlSource reads as a variable a -- param: marker with its type
```

**Fix:** end the list of declarations with a `;`, or give the parameter a `-- param:` marker with its type, `-- param: @p int`, which makes it a parameter for SqlSource too.

See: [What can be described](postgresql.md#what-can-be-described), [what SQL Server can describe](sqlserver.md#what-can-be-described)

## SQLSRC232

**Type cannot be bound by Npgsql**

The tool checks that Npgsql can send each parameter's type, given by its name, before it writes the sidecar.  Here Npgsql refused the type before it sent anything, or the type has no name that Npgsql could be given without ambiguity, so the query is not described.  Nothing is wrong with the SQL itself, but the query gets no entry, so it has no models and no method until it is fixed.  The `server:` line is Npgsql's own message, or, for a type with no name, the tool's own.  This is a known limit and not a fault of the query: Npgsql cannot send a value of these kinds of type, and the tool never rewrites your SQL to make a parameter sendable.  They are refused:

- a composite type, `public.pair`, and an array of one;
- a table's row type, and an array of one;
- `regclass`, which `WHERE c.oid = @t::regclass` gives a parameter, and the other types of the system catalogs that Npgsql does not load, such as `aclitem`, `txid_snapshot` and `pg_node_tree`;
- a type whose schema or whose name holds a `.`, which cannot be written to Npgsql without ambiguity, since `a.b.c` could name more than one type.

A type whose name or whose schema's name needs quotes (`"My Schema"."Status"`), `"char"`, the one-byte type of the system catalogs, a domain over another domain, the bit string type without a length, and an array of a type whose name is 63 characters long are bound.

```console
/work/App/Queries/Pairs.sql(1,10): error SQLSRC232: Npgsql cannot bind the type 'public.pair' of the parameter '@pair' of the query 'ListPairs'
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: Writing is not supported for parameters having DataTypeName 'public.pair'.
    help: Npgsql cannot send a value of this type; declare the parameter as text with -- param: and cast it to the type where it is used
```

**Fix:** declare the parameter as `text` and cast it where it is used, `-- param: @pair text` with `@pair::public.pair` in the SQL; or give the query `-- output: sql`, and the tool leaves it out.  For a type with no name the help is `the type has no name that Npgsql can be given without ambiguity; rename the schema or the type, or declare the parameter as text with -- param: and cast it to the type where it is used`.  The error is also given for a parameter that only a `-- param:` marker declares: Npgsql resolves the type of every parameter it is handed.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC233

**Statement cannot be described**

The tool describes a PostgreSQL query by asking the server to prepare it, and PostgreSQL prepares only `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `VALUES` and `TABLE`, with `WITH` around any of them.  This query starts with a statement of another kind: `CALL`, `CREATE`, `EXPLAIN`, `SHOW`, `SET` and the rest of the commands the server refuses to prepare.  The tool reads the first word of the statement before it sends anything, so the message names that word and the server is not asked.  On SQL Server, a statement whose result the server cannot work out, such as `CREATE PROCEDURE`, is [SQLSRC225](#sqlsrc225), `Msg 11527`.

```console
/work/App/Queries/Archive.sql(1,10): error SQLSRC233: The query 'Archive' is a CALL statement, which the tool cannot describe
    query: Archive, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: give the query -- output: sql; the tool describes SELECT without INTO, INSERT, UPDATE, DELETE, MERGE, VALUES and TABLE
```

Give the query `-- output: sql`: its SQL is still a string constant, or a method where the query has tokens, with no types, so the tool does not describe it.  The error still fails the query, so the file's sidecar is held back until the query is mended or given the marker: the tool does not decide on its own that such a query needs no entry, since that would override what you wrote.

`SELECT ... INTO` is refused as `SELECT INTO` on every version of the server.  PostgreSQL 15 and later prepare it and 14 does not, but it creates a table, as `CREATE TABLE AS` does, and a sidecar must not depend on the server it was described against.  Write `CREATE TABLE ... AS` with `-- output: sql`, or select the rows and insert them in a second query.  A statement that the tool's check misses and that the server names as one it cannot prepare, `42P14: utility statements cannot be prepared`, is this error too, and the message then reads "is a utility statement".

A word that the tool does not know stays [SQLSRC225](#sqlsrc225): `SELEC 1` is a typo and the server says so.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC234

**Query writes a positional parameter**

The tool finds a query's parameters by their `@name` and sends the SQL with `$1`, `$2` in their places.  A `$n` that the SQL writes itself would be taken for one of those, so the types the server gave would not be the parameters' own, and the query's method, which sends parameters by name, could not fill it.  Nothing of such a query is sent.  The message names the `$n` as it is written.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC234: The query 'GetUser' writes the parameter '$1' itself
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: write the parameter as @name; generated code binds parameters by name
```

Write the parameter as `@name`.  A `$` followed by a digit that is part of a name (`a$1`), or that stands in a string, a dollar-quoted string, a quoted identifier or a comment, is text and not a parameter.

See: [What can be described](postgresql.md#what-can-be-described)

## SQLSRC235

**Server version is not supported**

The server that the database's connection string reaches is older than the oldest version the tool supports: PostgreSQL 14, or SQL Server 2017, which reports version 14.  An Azure SQL Database or Azure SQL Managed Instance is accepted whatever version it reports.  The version is read when the session is opened, before any query is sent, and it is the one the server reports.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC235: The database 'app' runs PostgreSQL 13.12, and the tool supports 14 and later
    query: GetUser, database app, postgres
    help: upgrade the server, or describe against a database of PostgreSQL 14 or later
```

Upgrade the server, or point the tool at a database of a supported version, a copy of the schema on a newer server for one.  The error is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is, and each of the database's queries counts as failed.  A version that does not start with a number is not refused, since the tool cannot tell what it is.

See: [Connecting](postgresql.md#connecting), [connecting to SQL Server](sqlserver.md#connecting)

## SQLSRC236

**Connection does not keep a session**

The tool prepares a statement on the server and asks about it in later steps, in one transaction, which only works when every step reaches the same session.  This connection does not keep one.  There are two causes:

- The connection string sets `Multiplexing=true`, which shares physical connections between commands.  It is found when the database is opened, before the tool connects.
- A pooler in statement mode, such as PgBouncer with `pool_mode = statement`, sits between the tool and the server.  It refuses the transaction and closes the connection, so the first query of the database reports this error and every query after it reports [SQLSRC225](#sqlsrc225) with `the connection to the server was lost`.

The tool also reports this error at a query when a step finds that it reached another session: the server does not list the statement it just prepared, or answers `26000` (it does not exist) or `42P05` (it already exists).

For `Multiplexing=true` the tool refuses before it connects, so no query has been sent and the server is not yet known; the `server:` line is the tool's own text:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres
    server: Multiplexing=true does not keep one session
    help: connect directly to the server or through a pooler in session or transaction mode, and without Multiplexing=true
```

Behind a pooler in statement mode the session has opened, so the query line names the server and the driver, and the error is at the first step.  The `server:` line is the tool's own text, after the pooler's SQLSTATE; for `26000` and `42P05` it is the server's:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 08P01: the server refused the transaction of a describe
    help: connect directly to the server or through a pooler in session or transaction mode, and without Multiplexing=true
```

Connect directly to the server, or through a pooler in session or transaction mode, and without `Multiplexing=true`.  With PgBouncer, give the tool a database entry or a user whose `pool_mode` is `session` or `transaction`.  Only the tool needs this: your application can keep its own connection.  The error for `Multiplexing=true` is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is, and each of the database's queries counts as failed; one found at a query is reported at that query.

See: [Connecting](postgresql.md#connecting)

## SQLSRC237

**Sidecar cannot be read**

The sidecar of a `.sql` file, the `.sql.json` file beside it, is not a file that `dotnet sqlsource describe` wrote.  A merge conflict in it is the usual cause, and a hand edit the next.  The message says what is wrong, and the error is at the place in the sidecar where it is.

```console
/work/App/Queries/Users.sql.json(7,1): error SQLSRC237: The sidecar cannot be read, because it is not one JSON object.  Run 'sqlsource describe' to write it again.
```

Run `dotnet sqlsource describe`, which writes the whole file again, and commit it.  After a merge conflict, take either side of the conflict or none: the tool writes the file from your queries and your database, not from what the file held.  While the sidecar cannot be read, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC238

**Sidecar has a newer format**

The sidecar was written by a newer version of the `sqlsource` tool, in a format that this version of the `SqlSource` package does not read.  The message names the sidecar's format and the one this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC238: The sidecar has format 2, and this version of SqlSource reads format 1.  Update the SqlSource package.
```

Update the `SqlSource` package to the version of the tool that wrote the sidecar.  The package and the tool share a version, and a project should use the same one for both.  Until then, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC239

**Sidecar has an older format**

The sidecar was written by an older version of the `sqlsource` tool, in a format that this version of the `SqlSource` package no longer reads.  The message names the sidecar's format and the one this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC239: The sidecar has format 0, and this version of SqlSource reads format 1.  Run 'sqlsource describe' to write it again.
```

Run `dotnet sqlsource describe` with the tool of the package's version, and commit the sidecar it writes.  Until then, no query of the file gets an error about its entry.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC240

**Sidecar was written by another version of the tool**

The sidecar was written by a version of the `sqlsource` tool other than the version of the `SqlSource` package the project uses.  The package and the tool share a version, so this means that the tool and the package reference are out of step, or that the package was updated and the sidecars were not written again.  The sidecar is read and used all the same, since its format is the one this package reads.  A version's suffix, such as `-dev` in `0.1.0-dev`, is not compared.

```console
/work/App/Queries/Users.sql.json(1,1): warning SQLSRC240: The sidecar was written by sqlsource 0.1.0, and this project uses SqlSource 0.2.0.  Bring the tool and the package to one version, and run 'sqlsource describe'.
```

Update the tool or the package so that both have one version, run `dotnet sqlsource describe`, and commit the sidecars.  The tool describes every query of a file that another version wrote.

This is a warning, so that a build goes on while the sidecars are written again.  A project can list it in `NoWarn` to hide it, or in `WarningsNotAsErrors` to keep it a warning in a project that sets `TreatWarningsAsErrors`:

```xml
<PropertyGroup>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);SQLSRC240</WarningsNotAsErrors>
</PropertyGroup>
```

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC241

**Query has no entry in the sidecar**

The query's output is `models` or `codegen`, which needs a description of the query in its file's sidecar, and the sidecar has none: the file has no sidecar, or its sidecar has no entry of the query's name.  A query that was added or renamed since the tool last ran is the usual cause.  Names are compared as C# compares them, so `getuser` is not `GetUser`.  The message names the query and its output.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'codegen' needs one.  Run 'sqlsource describe'.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes.  The default output is `codegen`, so a project that only wants each query's SQL as a constant sets the output to `sql`: `"output": "sql"` in the [settings file](settings.md#settings-files) of the project's folder, `Output = GeneratorOutput.Sql` on the attribute, or `-- output: sql` in the file.  When the tool reports [SQLSRC271](#sqlsrc271) for the file, saving it as UTF-8 and describing again is the fix.  The error is reported once for a query, however many types use its file, and under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Output](settings.md#setting-output)

## SQLSRC242

**Entry is of another database**

The query's entry in the sidecar was described against a kind of database that is not the dialect of its file: the file's dialect was changed since the tool last ran, or the sidecar was copied from another file.  The message names the query, the kind of database of the entry and the file's dialect.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC242: The entry of the query 'GetUser' was described for 'mssql', and the dialect of this file is 'postgres'.  Run 'sqlsource describe'.
```

Check that the file's dialect is the right one, then run `dotnet sqlsource describe` and commit the sidecar it writes.

See: [Dialect](settings.md#setting-dialect)

## SQLSRC243

**No type map for the dialect**

No dialect of this release gets this error: `postgres` and `mssql` are both typed.  It is kept for a database that SqlSource can describe before it can give types for it, where it would be reported once for the file, at its first query that needs types, with no other error about the entries of the file's queries.

See: [Dialects](dialects.md)

## SQLSRC244

**Entry is stale**

The query has changed since its entry in the sidecar was written: its SQL, the type or the nullability of a `-- param:` marker, or the default of one of its tokens.  A change to a comment alone does not count, and neither does a C# type after `->`.  An entry that describes another version of the query could give it the wrong types, so it is not used.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC244: The query 'GetUser' has changed since its entry was written.  Run 'sqlsource describe'.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes.  When the tool reports [SQLSRC271](#sqlsrc271) for the file, saving it as UTF-8 is the fix.

See: [Describing again](tool.md#describing-again)

## SQLSRC245

**Entry names no query**

The sidecar has an entry for a query that does not need one: a query that was removed or renamed, or one whose output is now `sql` for every type that uses its file.  The entry is not used.  The warning is at the entry's name in the sidecar.

```console
/work/App/Queries/Users.sql.json(15,5): warning SQLSRC245: The entry 'GetUsers' names no query of the file that needs one.  Run 'sqlsource describe', which removes it.
```

Run `dotnet sqlsource describe` and commit the sidecar it writes, which has no such entry.  When no query of the file needs an entry any more, a run of `describe` over the whole project deletes the sidecar.

A `.sql` file that two projects include is the exception.  A run of `describe` over both projects keeps the entry that the other project needs, so the warning stays.  A run over only the project that warns removes the entry, and the other project's build then reports [SQLSRC241](#sqlsrc241).  For such a file the warning clears when the query needs an entry in both projects or in neither, which is an output other than `sql` in both or `sql` in both, or when the project lists `SQLSRC245` in `NoWarn`.  See [known issues](sql-files.md#known-issues).

This is a warning, since an entry that nothing uses changes no generated code.  A project can list it in `NoWarn` to hide it, or in `WarningsNotAsErrors` to keep it a warning in a project that sets `TreatWarningsAsErrors`.

See: [Sidecar files](tool.md#sidecar-files)

## SQLSRC246

**Type has no C# type**

A column or a parameter of the query has a database type that Npgsql, or Microsoft.Data.SqlClient for a query of the `mssql` dialect, cannot read as a C# value, so SqlSource can give it no C# type.  The message names the column or parameter, its type, and what to do.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC246: The column 'relation' has the type 'regclass', which SqlSource cannot give a C# type.  Cast it to oid in the query.
```

What to do depends on the type:

- **`regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation` and `regdictionary`.**  Cast the column to `oid` in the query, `SELECT 'users'::regclass::oid AS relation`, which Npgsql reads as a `uint`.  Cast an array of one to `oid[]`, as the message says.
- **A composite type, which includes a table's row type, and a base type of an extension that SqlSource does not know.**  Map it with the [type override](settings.md#setting-type-overrides) to the C# type that your application reads it as, `public.address=MyApp.Address` for a type that the data source maps, or select its fields as columns of their own.
- **A range of a subtype that SqlSource does not know, and a multirange of one.**  Map it with the type override to `NpgsqlTypes.NpgsqlRange<T>` of its subtype's C# type, `public.floatrange=NpgsqlTypes.NpgsqlRange<double>`, and call `EnableUnmappedTypes()` on the data source builder, without which Npgsql cannot read it.  A project that does not reference Npgsql cannot name `NpgsqlTypes.NpgsqlRange<T>`, so the message tells it to reference Npgsql first.
- **`cube` on Npgsql 8 or 9**, which cannot read it.  Update Npgsql to 10, which reads it as `NpgsqlTypes.NpgsqlCube`, or cast the column to `text` in the query, as the message says.
- **`pg_snapshot`, `txid_snapshot`, `pg_node_tree`, and any other base type of `pg_catalog` that SqlSource does not know, such as `aclitem`.**  No version of Npgsql reads them, so a type override would build and fail when the query runs.  Cast the column to `text` in the query, and an array of one to `text[]`, as the message says.
- **`hierarchyid`, `geography` and `geometry` of SQL Server.**  Microsoft.Data.SqlClient reads them only when the application references the package `Microsoft.SqlServer.Types`.  Reference it, and map the type with the type override to that package's type for it, as the message says: `geography=Microsoft.SqlServer.Types.SqlGeography`, `geometry=Microsoft.SqlServer.Types.SqlGeometry` or `hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId`.
- **A SQL Server type that this version of SqlSource does not know, and a CLR type of your own assembly.**  Map it with the type override to the C# type that your application reads it as, as the message says: `sys.newtype=MyApp.NewType` for a type of a newer SQL Server, or `dbo."Money"=MyApp.Money` for a CLR type of the schema `dbo`.
- **A parameter with no type, `record`, `unknown`, `void`, a kind of type that this version of SqlSource does not know, or a SQL Server type with no name.**  The `sqlsource` tool never writes these, so the sidecar was edited by hand or written by a newer tool.  Run `dotnet sqlsource describe`.

When the message says to map the type, it names the key of the type with its schema: `public.address`, or `pg_catalog.pg_class` for the row type of a catalog table, or `sys.newtype` for a type of SQL Server, since a key without a schema must be a type that SqlSource knows.  A schema or a name that needs quotes is in them, `"my schema"."Order Item"`, and one with a line break, a tab or any other control character is written with escapes, `"my schema".U&"Order\000AItem"`, so that the key can be written in a marker, which is one line.  A name with a capital letter is quoted too, `public."Status"`, so an entry for it maps that type alone, and not one whose name differs only by case.  For an array or a domain, the key is of the element or base that has no C# type, and an entry for it maps the array or domain too.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [PostgreSQL types](postgresql.md#type-map), [SQL Server types](sqlserver.md#types)

## SQLSRC247

**Npgsql is older than 8.0**

The project references a version of Npgsql older than 8.0, and SqlSource gives the queries of the `postgres` dialect the C# types of Npgsql 8.0 and later.  The message names the version the project references.

```console
CSC : error SQLSRC247: This project references Npgsql 7.0.4, and SqlSource needs Npgsql 8.0 or later, so no query method is generated.  Update the package.
```

Until the package is updated, no query of the `postgres` dialect gets its method; its models are still generated.  Update the `Npgsql` package to 8.0 or later.  When another package brings the older version, reference `Npgsql` from the project itself, at the version you want.  The error has no file and line, because it is about the project's references, and it is reported once for the project, only while a query's output is `models` or `codegen` under the `postgres` dialect.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC248

**Npgsql is newer than SqlSource knows**

The project references Npgsql 11 or later, and this version of SqlSource knows the C# types of Npgsql up to version 10.  It gives the queries of the `postgres` dialect the types that Npgsql 10 reads, which are right unless the newer driver reads one of them as another type.  The message names the version the project references.

```console
info SQLSRC248: This project references Npgsql 11.0.0, which is newer than this version of SqlSource knows.  Its models use the types of Npgsql 10.
```

Update the `SqlSource` package when a version that knows the driver is released.  This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds on the day it updates Npgsql.  A project can list it in `NoWarn` to hide it:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC248</NoWarn>
</PropertyGroup>
```

It has no file and line, and is reported once for the project, only while a query's output is `models` or `codegen` under the `postgres` dialect.  As information, it is not in the output of `dotnet build`.  An IDE that lists information shows it with the errors and warnings.  A build log has it at the detailed verbosity, `dotnet build -v:detailed -tl:off`, as the sample shows it, since the terminal logger leaves information out at any verbosity.  A binary log, `dotnet build -bl`, has it too.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC249

**Type needs Npgsql**

The project does not reference Npgsql, and a column or a parameter of the query has a type whose C# type depends on it.  It is either a type that Npgsql reads as a type of its own `NpgsqlTypes` namespace, such as `point`, `tsvector` or `int4range`, or a type that Npgsql 8 and 9 read as one C# type and Npgsql 10 as another: `date`, `time`, `cidr`, `daterange` and `datemultirange`, and an array or a domain of one of them.  Without a reference, SqlSource cannot tell which Npgsql the application runs on.  The message names the column or parameter and its type.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC249: The column 'born' has the type 'date', whose C# type depends on Npgsql, and this project does not reference it.  Reference Npgsql, or map it with the type override for the Npgsql the application runs on: date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10.
```

Reference the `Npgsql` package from the project, at the version the application runs on: every type then gets the C# type of that version.  A project that keeps its queries apart from the driver can map each type that differs with the [type override](settings.md#setting-type-overrides) instead, for the version the application runs on, as the message says: `"typeOverrides": { "date": "System.DateOnly", "time": "System.TimeOnly" }` in the [settings file](settings.md#settings-files) of the project's folder for Npgsql 10.  An entry for `date` maps an array or a domain of `date` too.  The message offers only C# types that a project without Npgsql can name: for `cidr`, `System.Net.IPNetwork` on Npgsql 10, since Npgsql 8 and 9 read it as a type of `NpgsqlTypes`.  `daterange` and `datemultirange` are read as a type of `NpgsqlTypes` on every version, so for them, as for every other type of the `NpgsqlTypes` namespace, the message says to reference Npgsql, which they need.  Every other type has the same C# type on every version of Npgsql, and needs neither.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Which version of Npgsql](postgresql.md#which-npgsql)

## SQLSRC250

**Mapped type is not found**

A C# type that you chose cannot be found.  It is a type that the [type override](settings.md#setting-type-overrides) gives a database type, that the [column types](settings.md#setting-column-types) give a column, or that a `-- param:` or `-- column:` marker gives after `->`, and either no type of that name and number of type arguments is in the project or its references; or the name is a short name, of one part, and more than one type of it, in different namespaces, is one that the project can see; or more than one reference has a type of the full name that the project can see and the project has none.  A type of a reference that the project cannot see does not count, so a name that one reference has as `public` and another as `internal` is the public one.  The message names the column or parameter; what is mapped: the type override's key, the column types' key, the name of a `-- column:` marker of the query, or the parameter of a `-- param:` marker; the C# type; and the place that chose it: the `typeOverrides` or the `columnTypes` of a settings file, which it names by its full path, a `-- type-override:` or `-- column:` marker in the file header or one of the query, or the `-- param:` marker of the parameter.  For a short name that several types have, it names their full names, the first three and a count of the rest.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC250: 'uuid' is mapped to 'MyApp.UserKey' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and no type of that name is found, or more than one that this project can see.  Correct the name, with its namespace and type arguments, or for a name that two references have, map to a type of this project.
/work/App/Queries/Posts.sql(1,10): error SQLSRC250: 'users.id' is mapped to 'UserId' for column 'author_id', in the columnTypes of the settings file /work/App/sqlsource.json, and no type of that name is found, or more than one that this project can see, and it could be MyApp.UserId or Legacy.UserId.  Correct the name, with its namespace and type arguments, or for a name that two references have, map to a type of this project.
```

Write the name of a type that the project has, with its type arguments: `UserKey`, `MyApp.Ids.UserKey`, or `System.Collections.Generic.List<int>`.  For a short name that several types have, write the full name, from its namespace, of the one you mean: `MyApp.UserId`.  A nested type is written with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.  When two references have a type of the full name that the project can see, map to a type of the project's own, which is always chosen first.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Type overrides](settings.md#setting-type-overrides), [column types](settings.md#setting-column-types)

## SQLSRC251

**Mapped type is not accessible**

A C# type that you chose, by the type override, the column types or a marker, cannot be used by the project: the type, or one of its type arguments, is `internal` in another assembly, or is a `private` or `protected` type nested in another, in the project itself or another assembly.  The message names the column or parameter, what is mapped, the C# type and the place that chose it, as [SQLSRC250](#sqlsrc250)'s does.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC251: 'uuid' is mapped to 'Shared.UserKey' for column 'id', in a -- type-override: marker of the query, and this project cannot see that type.  Make it, and each type it is nested in, public, or internal when it is this project's own, or give this project its assembly's internals with InternalsVisibleTo.
```

Make the type, and each type it is nested in, `public`.  A type of the project itself can be `internal` instead.  For a type of another assembly, you can also add `[assembly: InternalsVisibleTo("YourProject")]` to the assembly that declares it, which lets the project see its `internal` types.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

The same error is reported when the type is not `public` and the model that would have the column or parameter as a property is: a model is `public` when a `public` type claims its query, as [accessibility](generator.md#model-accessibility) says, and a `public` type cannot have a property of a type that is not.  Each such property is an error of its own, and the message then also names the model.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC251: 'uuid' is mapped to 'App.UserKey' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and that type is not public, and the model 'App.Queries.GetUserDto' is.  Make the type public, or make each type that claims the query's file internal.
```

Make the type `public`, or make each type that claims the query's file `internal`, which makes its models `internal`.  The model is not generated until then.  Under the output `models` the query's constant or token method still is; under `codegen` the query gets no method.

See: [Type overrides](settings.md#setting-type-overrides), [column types](settings.md#setting-column-types)

## SQLSRC252

**Mapped type cannot be a property's type**

A C# type that you chose, by the type override, the column types or a marker, is one that no property of a model can have: a `static` class, a `ref struct` such as `System.Span<T>`, `System.Void`, or `System.Nullable<T>` as the whole type.  A type argument or an array's element that is a `static` class, a `ref struct` or `System.Void` is refused too, as is a type argument that is an interface with a `static abstract` member, such as `System.Numerics.INumber<int>`, and so is `System.Nullable<T>` with a `?` after it, as a type argument or an array's element: `System.Nullable<int>?[]`.  So is a type when it, a type it is nested in or one of its type arguments is marked `[Obsolete]` as an error, as `[Obsolete("Use UserKey.", true)]`, since no code can use it without an error; and so is one marked `[Obsolete]` or `[Experimental]` under a diagnostic id that is not an identifier, such as `MY-001` or `1234`, since a model could not turn off the warning, or the error of `[Experimental]`, that its use gives.  `[Experimental]` on the assembly or module that declares the type counts as on the type.  So is a generic type with a type argument that breaks a constraint of its type parameter, as `System.Collections.Generic.List<System.Nullable<string>>`, whose `System.Nullable<T>` takes only a value type, or `MyApp.Box<string>` where `Box<T>` requires `where T : struct`.  A type argument written with a `?` breaks a constraint that forbids null: `notnull`, `class` without a `?`, or a base type or interface written without a `?`, as `System.Collections.Generic.Dictionary<string?, int>` does, whose key is `notnull`.  A type marked `[Obsolete]` or `[Experimental]` otherwise is accepted, and its model compiles without a warning.  The message names the column or parameter, what is mapped, the C# type and the place that chose it, as [SQLSRC250](#sqlsrc250)'s does.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC252: 'int4' is mapped to 'System.Nullable<int>' for column 'id', in the typeOverrides of the settings file /work/App/Queries/sqlsource.json, and no property can have that type.  Choose another type, and for System.Nullable<T> name T, since a column that can be null makes its property nullable.
```

Choose another C# type.  For `System.Nullable<T>`, choose `T`: a column that can be null already gets a nullable property, and one that cannot gets `T`.  For a type marked `[Obsolete]` as an error, map it to the type that replaces it.  For a generic type, give it type arguments that meet the constraints of its declaration.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Type overrides](settings.md#setting-type-overrides), [column types](settings.md#setting-column-types)

## SQLSRC253

**Column has no name**

A column of the query has no name, so its property in the output model would have none.  PostgreSQL names a column `?column?` when the query gives it no alias and it is no column of a table or call of a function: an expression such as `price * quantity`, or a literal.  On SQL Server such a column is [SQLSRC266](#sqlsrc266), which the tool reports when it describes the query.  The message gives the column's position, from 1, and the query.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC253: The column at position 2 of the query 'GetLines' has no name.  Give it an alias.
```

```sql error
-- name: GetLines
SELECT id, price * quantity FROM order_lines;
```

Give the column an alias, which names its property, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetLines
SELECT id, price * quantity AS total FROM order_lines;
```

Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Property names](generator.md#property-names)

## SQLSRC254

**Name is not an identifier after PascalCase**

The PascalCase of a column's or a parameter's name, which names its property, is not a C# identifier: it is empty, as for a name of underscores alone, it starts with a digit, as `_1st` gives `1st`, or it holds a character that an identifier cannot, as `unit-price` does.  The message names the column or parameter and the property name it gives.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC254: The column 'unit-price' gives the property name 'Unit-price', which is not a C# identifier.  Give it an alias.
```

```sql error
-- name: GetLines
SELECT id, "unit-price" FROM order_lines;
```

Give the column an alias that is a name a C# property can have, or rename the parameter, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetLines
SELECT id, "unit-price" AS unit_price FROM order_lines;
```

Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Property names](generator.md#property-names)

## SQLSRC255

**Two names give one property name**

Two columns, or two parameters, of the query give their model one property name, ignoring case: the same column selected from two tables, as `SELECT u.id, o.id`, or two spellings of a name, as `user_id` beside `userId`.  Names are compared ignoring case, since Dapper matches a column to a property ignoring case, and would fill both properties from one column.  The message names both, with their positions from 1, and the property name.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC255: The column 'id' at position 1 and the column 'id' at position 2 give one property name, 'Id', ignoring case.  Give one of them an alias.
```

```sql error
-- name: GetUserOrders
SELECT u.id, o.id FROM users u JOIN orders o ON o.user_id = u.id;
```

Give one of the columns an alias, or rename one of the parameters, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetUserOrders
SELECT u.id AS user_id, o.id AS order_id FROM users u JOIN orders o ON o.user_id = u.id;
```

Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Property names](generator.md#property-names)

## SQLSRC256

**Property name is taken**

A column or a parameter gives a property name that a record or `object` already has a member of: `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` or `Clone`.  A column named `equals` or `to_string` is one.  So is a property name that is the name of its model, which C# does not allow a member to have: a column `total` of a query whose `-- output-model:` marker names its model `Total`.  The message names the column or parameter and the property name.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC256: The column 'to_string' gives the property name 'ToString', which a record or object already has.  Give it an alias.
/work/App/Queries/Orders.sql(1,10): error SQLSRC256: The column 'total' gives the property name 'Total', which is the name of its model.  Give it an alias.
```

```sql error
-- name: GetUsers
SELECT id, to_string FROM users;
```

Give the column an alias, or rename the parameter, then run `dotnet sqlsource describe`:

```sql postgres
-- name: GetUsers
SELECT id, to_string AS display_name FROM users;
```

For a property named as its model, you can instead give the model [another name](settings.md#setting-model-name).  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Property names](generator.md#property-names)

## SQLSRC257

**Two models of one name differ**

Two queries give their models one full name, by an `-- input-model:` or `-- output-model:` marker, or by the suffixes and the namespace, and the two models are not the same.  Two queries share a model when the two agree on the properties' names, C# types and nullability, in the same order, on the model type, `record`, `sealed record`, `class` or `sealed class`, and on whether the properties are sorted: one model is generated, and its documentation names each query.  An input model and an output model of one name are never the same.  Two types that claim one file and give its query's model one name are two queries here too, so their settings must agree.  The queries are taken in the order of their files' paths, then of their order in a file, then of the types' names, and the error is at the later of the two, at its marker when it has one and at its name otherwise.  The message names the model, the earlier query, with the type when the two are one query, and the first difference.

```console
/work/App/Queries/Users.sql(6,1): error SQLSRC257: The output model 'App.Queries.UserRow' of the query 'GetUsers' is not the same as the output model of that name of the query 'GetUser' in Users.sql: it has 2 properties here and 3 there.  Give one of the two queries another model name, or make the two the same.
```

```sql error
-- name: GetUser
-- output-model: UserRow
SELECT id, name, email FROM users WHERE id = @id;

-- name: GetUsers
-- output-model: UserRow
SELECT id, name FROM users;
```

Give one of the two queries [another model name](settings.md#setting-model-name), or make the two models the same, then run `dotnet sqlsource describe` if you changed a query:

```sql postgres
-- name: GetUser
-- output-model: UserRow
SELECT id, name, email FROM users WHERE id = @id;

-- name: GetUsers
-- output-model: UserSummary
SELECT id, name FROM users;
```

The earlier query's model is still generated, and the later query has no model of that kind until the two agree.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Shared models](generator.md#shared-models)

## SQLSRC258

**Model name cannot be used**

A model's full name is one that a generated type cannot have, because the code would not compile or would be warned on:

- a type of the project has the full name, the type that carries `[SqlSourceGenerate]` among them;
- it is a namespace of the project, or a part of the model's namespace is a type of the project, as a [model namespace](settings.md#setting-model-namespace) of `App.Queries` beside a class `App.Queries`;
- it is the namespace of another model, or a part of its namespace is another model, as `App.Data` beside `App.Data.Row`, which reports both;
- its name is lower-case ASCII letters alone, as `usersdto`, which the compiler warns on, since such a name could become a keyword.

The model is not generated, and each query that would have it is told.  The message names the model, the query and the reason.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC258: The model 'App.Queries.usersdto' of the query 'GetUsers' cannot be generated: the compiler warns on a type name of lower-case ASCII letters alone.  Give the query another model name, or set another model namespace.
```

```sql error
-- name: GetUsers
-- output-model: usersdto
SELECT id, name FROM users;
```

Give the query [another model name](settings.md#setting-model-name), or set another [model namespace](settings.md#setting-model-namespace):

```sql postgres
-- name: GetUsers
-- output-model: UserDto
SELECT id, name FROM users;
```

Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Names that cannot be used](generator.md#unusable-model-names)

## SQLSRC259

**Output model on a query without rows**

The query has an `-- output-model:` marker, which names the type of its rows, and its statement returns none, as an `INSERT`, `UPDATE` or `DELETE` without `RETURNING` does, or on SQL Server one without `OUTPUT`, or whose `OUTPUT` goes `INTO` a table.  The error is at the marker.

```console
/work/App/Queries/Users.sql(2,1): error SQLSRC259: The query 'DeleteUser' names an output model, and its statement returns no rows.  Remove the -- output-model: marker.
```

```sql error
-- name: DeleteUser
-- output-model: DeletedUser
DELETE FROM users WHERE id = @id;
```

Remove the marker.  To read the rows the statement changes instead, add a `RETURNING` clause, or on SQL Server an `OUTPUT` clause, and run `dotnet sqlsource describe`.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Which queries get models](generator.md#which-models)

## SQLSRC260

**Column marker names no usable column**

A `-- column:` marker, which sets the nullability or the C# type of one column of its query's result, cannot be used.  Either it names no column of the result, its name compared ignoring case, or the query returns no rows; or it says `null` or `not null` of a column whose alias already ends in `!` or `?`, which says the column's nullability too.  A marker that gives only a C# type may name such a column.  The error is at the marker, and the message says which it is.

```console
/work/App/Queries/Payments.sql(2,1): error SQLSRC260: The -- column: marker for 'totl' names no column of the query's result.  Name a column as the query returns it.
/work/App/Queries/Payments.sql(3,1): error SQLSRC260: The -- column: marker for 'note' names a column whose alias already ends in ! or ?.  Remove the mark from the alias, or the marker.
```

```sql error
-- name: GetTotals
-- column: totl not null
-- column: note null
SELECT sum(amount) AS total, max(note) AS "note?" FROM payments;
```

Name the column as the query returns it: its alias, or its own name when it has none.  For a column whose alias ends in a mark, keep one of the two: remove the marker's `null` or `not null`, or remove the mark from the alias, which changes the query, and run `dotnet sqlsource describe`.  Where the rows are read by the names of their columns, as Dapper reads them, keep the marker:

```sql postgres
-- name: GetTotals
-- column: total not null
-- column: note null
SELECT sum(amount) AS total, max(note) AS note FROM payments;
```

Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Setting a column's nullability or type with a marker](generator.md#column-marker)

## SQLSRC261

**Entry names an array as the catalog does**

The type override has an entry for the name the database gives an array type, and a column or a parameter of the query has that array type, or is of a domain over it.  PostgreSQL names the array of a type with `_` before the type's name, so the array of `public.user_status` is `public._user_status`.  An entry for that name maps nothing, `default` included, since the type override names an array of a type outside `pg_catalog` as its element's key with `[]`: `public.user_status[]`.  A type whose own name starts with `_` and is no array, such as a composite type `public._tag`, is still mapped by its own name, and its array by `public._tag[]`.  The message names the column or parameter, the entry, the place that wrote it and the key to write.  It is reported once for the query, for its first column or parameter that has no C# type, as `SQLSRC246` and `SQLSRC249` to `SQLSRC252` are, and the query has no models until it is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC261: The type override's entry for 'public._user_status', in the typeOverrides of the settings file /work/App/sqlsource.json, maps nothing: 'public._user_status' is the database's own name of the array type of column 'statuses', and an entry names that array as its element's key with [].  Write the entry for 'public.user_status[]' instead.
```

Rename the entry to the key the message gives: the member `"public.user_status[]": "System.Collections.Generic.List<string>"` in a settings file's `typeOverrides`, or `-- type-override: public.user_status[]=System.Collections.Generic.List<string>` in a marker.  When the type override already has an entry for that key, remove the one the message names.  Under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Type overrides](settings.md#setting-type-overrides)

## SQLSRC262

**Copies of a parameter have different types**

SQL Server gives a parameter a type at each place the query uses it, and SqlSource takes each use as a copy of the parameter, typed on its own.  A use that says nothing of the type, such as `@p IS NULL`, is left out; the uses that fix a type must agree.  Here two of them give the parameter two types: `WHERE id = @p OR name = @p` compares it with an `int` column and with an `nvarchar(100)` one.  The message names the first two types in the order of the uses.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC262: The copies of the parameter '@p' of the query 'FindUser' have the types 'int' and 'nvarchar(100)'
    query: FindUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    help: declare the parameter's type with -- param:
```

Declare the parameter's type with a `-- param:` marker, `-- param: @p nvarchar(100)`: SQL Server then checks every use against the type you chose.  Or use two parameters, one for each type.  The optional filter, `(@name IS NULL OR name = @name)`, is not this error: its use in `@name IS NULL` fixes no type, and the parameter takes the type of the column.

See: [SQL Server parameters](sqlserver.md#parameters)

## SQLSRC263

**Query returns rows of different shapes**

SQL Server works out the columns of a query from its text, without running it, so every path through the query must return the same columns.  Here the paths return different ones: an `IF` whose two `SELECT`s differ in the number or the types of their columns.  A query has one set of columns, and one model for its rows, whichever path runs.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC263: The query 'FindUser' returns rows of different shapes on different paths
    query: FindUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    server: Msg 11509: The metadata could not be determined because the statement 'SELECT id, name FROM dbo.users WHERE name = @name' is not compatible with the statement 'SELECT id FROM dbo.users WHERE id = @id'.
```

```sql error
-- name: FindUser
IF @by_name = 1
    SELECT id, name FROM dbo.users WHERE name = @name
ELSE
    SELECT id FROM dbo.users WHERE id = @id;
```

Make every path return the same number of columns, of the same types, or split the query into one query for each set of columns and choose between them in your code:

```sql mssql
-- name: FindUserByName
SELECT id, name FROM dbo.users WHERE name = @name;

-- name: FindUserById
SELECT id, name FROM dbo.users WHERE id = @id;
```

Paths whose columns differ only in their names are not this error: SQL Server gives such a column no name, which is [SQLSRC266](#sqlsrc266).  Give it one name on every path.

See: [What SQL Server can describe](sqlserver.md#what-can-be-described)

## SQLSRC264

**Query runs SQL the server cannot read**

The query runs SQL that it builds in a string, `EXEC(@sql)`.  SQL Server works out the columns of a query from its text, without running it, and the string holds its SQL only when the query runs, so SQL Server cannot say what it returns.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC264: The query 'ListOrders' runs SQL that the server cannot read until it runs
    query: ListOrders, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    server: Msg 11513: The metadata could not be determined because statement 'EXEC(@sql)' contains dynamic SQL.  Consider using the WITH RESULT SETS clause to explicitly describe the result set.
```

```sql error
-- name: ListOrders
DECLARE @sql nvarchar(max) = N'SELECT id, total FROM dbo.orders WHERE customer_id = ' + CAST(@customer_id AS nvarchar(20));
EXEC(@sql);
```

Write the SQL in the query, with a query parameter where the string took a value:

```sql mssql
-- name: ListOrders
SELECT id, total FROM dbo.orders WHERE customer_id = @customer_id;
```

Or `EXEC` a literal string, `EXEC(N'SELECT id, total FROM dbo.orders')`, which SQL Server can read.  A part of the SQL that changes between calls, such as the name of a table, can be a [token](parameters-and-tokens.md#tokens), which the generated method takes as an argument.

See: [What SQL Server can describe](sqlserver.md#what-can-be-described)

## SQLSRC265

**Query uses a temporary table in a batch**

The query creates a temporary table, a `#` table, and uses it in a later statement.  SQL Server works out the columns of a query from its text, without running it, and the table does not exist until the query runs, so SQL Server cannot work out the statements that use it.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC265: The query 'RecentTotals' uses a temporary table in a batch of several statements
    query: RecentTotals, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    server: Msg 11525: The metadata could not be determined because statement 'INSERT #recent (customer_id, total) SELECT customer_id, total FROM dbo.orders WHERE placed_at > @sin' uses a temp table.  Metadata discovery only supports temp tables when analyzing a single-statement batch.
    help: make the query one statement, or use a table variable in place of the temporary table
```

```sql error
-- name: RecentTotals
CREATE TABLE #recent (customer_id int NOT NULL, total decimal(12,2) NOT NULL);
INSERT #recent (customer_id, total) SELECT customer_id, total FROM dbo.orders WHERE placed_at > @since;
SELECT customer_id, SUM(total) AS total FROM #recent GROUP BY customer_id;
```

Make the query one statement:

```sql mssql
-- name: RecentTotals
SELECT customer_id, SUM(total) AS total
FROM dbo.orders
WHERE placed_at > @since
GROUP BY customer_id;
```

Or declare a table variable in the place of the temporary table, `DECLARE @recent TABLE (customer_id int NOT NULL, total decimal(12,2) NOT NULL)`, whose columns SQL Server reads from its declaration.  A query that reads a temporary table it does not create is [SQLSRC225](#sqlsrc225) instead, with `Msg 208`, `Invalid object name`.

See: [What SQL Server can describe](sqlserver.md#what-can-be-described)

## SQLSRC266

**Column has no name**

A column of the query has no name, so its property in the output model would have none.  SQL Server gives no name to a column that is an expression or a literal without an alias, such as `COUNT(*)`, `price * quantity` or `1`.  The message gives the column's position, from 1, and the query.  It is what [SQLSRC253](#sqlsrc253) is for PostgreSQL, and the tool reports it when it describes the query.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC266: Column 2 of the query 'CountOrders' has no name
    query: CountOrders, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    help: give the column a name with AS
```

```sql error
-- name: CountOrders
SELECT customer_id, COUNT(*) FROM dbo.orders GROUP BY customer_id;
```

Give the column an alias, which names its property, then run `dotnet sqlsource describe` again:

```sql mssql
-- name: CountOrders
SELECT customer_id, COUNT(*) AS order_count FROM dbo.orders GROUP BY customer_id;
```

See: [What SQL Server can describe](sqlserver.md#what-can-be-described)

## SQLSRC267

**Microsoft.Data.SqlClient is older than 6.1**

The project references a version of Microsoft.Data.SqlClient older than 6.1, and SqlSource gives the queries of the `mssql` dialect the C# types of Microsoft.Data.SqlClient 6.1 and later.  The message names the version the project references.

```console
CSC : error SQLSRC267: This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update the package.
```

Until the package is updated, no query of the `mssql` dialect gets its method; its models are still generated.  Update the `Microsoft.Data.SqlClient` package to 6.1 or later.  When another package brings the older version, reference `Microsoft.Data.SqlClient` from the project itself, at the version you want.  EF Core 8 and 9 bring Microsoft.Data.SqlClient 5.1, so a project that uses either references Microsoft.Data.SqlClient 6.1 or later directly, beside the EF Core packages.  The error has no file and line, because it is about the project's references, and it is reported once for the project, only while a query's output is `models` or `codegen` under the `mssql` dialect.  A query with a `vector` column or parameter also gets [SQLSRC270](#sqlsrc270), at its name, and the same update clears both.

See: [Which version of Microsoft.Data.SqlClient](sqlserver.md#which-sqlclient)

## SQLSRC268

**Microsoft.Data.SqlClient is newer than SqlSource knows**

The project references Microsoft.Data.SqlClient 8 or later, and this version of SqlSource knows the C# types of Microsoft.Data.SqlClient up to version 7.  It gives the queries of the `mssql` dialect the types that Microsoft.Data.SqlClient 7 reads, which are right unless the newer driver reads one of them as another type.  The message names the version the project references.

```console
info SQLSRC268: This project references Microsoft.Data.SqlClient 8.0.1, which is newer than this version of SqlSource knows.  Its models use the types of Microsoft.Data.SqlClient 7.
```

Update the `SqlSource` package when a version that knows the driver is released.  This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds on the day it updates Microsoft.Data.SqlClient.  A project can list it in `NoWarn` to hide it:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC268</NoWarn>
</PropertyGroup>
```

It has no file and line, and is reported once for the project, only while a query's output is `models` or `codegen` under the `mssql` dialect.  As information, it is not in the output of `dotnet build`: see [SQLSRC248](#sqlsrc248) for where to find it.

See: [Which version of Microsoft.Data.SqlClient](sqlserver.md#which-sqlclient)

## SQLSRC269

**Type needs Microsoft.Data.SqlClient**

The project does not reference Microsoft.Data.SqlClient, and a column or a parameter of a query of the `mssql` dialect has a type whose C# type is one of Microsoft.Data.SqlClient's own: `vector`, which it reads as `Microsoft.Data.SqlTypes.SqlVector<float>`.  Without a reference, the project cannot name that type.  The message names the column or parameter and its type.

```console
/work/App/Queries/Documents.sql(1,10): error SQLSRC269: The column 'embedding' has the type 'vector(3)', whose C# type depends on Microsoft.Data.SqlClient, and this project does not reference it.  Reference Microsoft.Data.SqlClient.
```

Reference the `Microsoft.Data.SqlClient` package from the project, at 6.1 or later.  Every other type that Microsoft.Data.SqlClient reads without another package has a C# type of .NET itself, and needs no reference.  The error is reported at the query's name, once for the query, and under the output `models` the query's constant or token method is still generated; under `codegen` the query gets no method until the error is fixed.

See: [Which version of Microsoft.Data.SqlClient](sqlserver.md#which-sqlclient)

## SQLSRC270

**Type needs a newer Microsoft.Data.SqlClient**

The project references a version of Microsoft.Data.SqlClient older than 6.1, and a column or a parameter of a query of the `mssql` dialect has a type whose C# type that version does not have: `vector`, which Microsoft.Data.SqlClient reads as `Microsoft.Data.SqlTypes.SqlVector<float>`, first in 6.1.  The message names the column or parameter, its type, the version its C# type needs and the version the project references.

```console
/work/App/Queries/Documents.sql(1,10): error SQLSRC270: The column 'embedding' has the type 'vector(3)', whose C# type needs Microsoft.Data.SqlClient 6.1 or later, and this project references Microsoft.Data.SqlClient 6.0.5.  Update the package.
```

Update the `Microsoft.Data.SqlClient` package to 6.1 or later, which also clears [SQLSRC267](#sqlsrc267), reported for the project beside it.  When another package brings the older version, as EF Core 8 and 9 do, reference `Microsoft.Data.SqlClient` from the project itself, at the version you want.  The error is reported at the query's name, once for the query; the query has no models until it is fixed, and under the output `models` its constant or token method is still generated, while under `codegen` it gets no method.

See: [Which version of Microsoft.Data.SqlClient](sqlserver.md#which-sqlclient)

## SQLSRC271

**File is not saved as UTF-8**

The tool reports a `.sql` file saved in another encoding, such as Windows-1252, when a query of it needs an entry in its sidecar or when the tool cannot read the file as SQL.  A file that starts with a byte order mark is never reported.  Such a file has no one text: a build on Windows reads it in the system's code page, while the tool, and a build on macOS or Linux, read each character that is not UTF-8 as a replacement character, `�`, which then stands in the generated SQL too.  The tool describes no query of the file until it is saved as UTF-8.  A file that the tool can read as SQL and whose queries all have the output `sql` is not reported.

The build does not report this error itself.  It reports a query of the file that has no entry as missing one ([SQLSRC241](#sqlsrc241)), and on Windows, when the characters outside ASCII are in a query's SQL and not only in its comments, reports the query's entry as out of date ([SQLSRC244](#sqlsrc244)).  Otherwise the build may report nothing while the tool reports the file.

The tool also reports a C# file in another encoding whose `[SqlSourceGenerate]` has a `Path` written with a character outside ASCII, at that `Path`.  A build on Windows reads that `Path` in the system's code page, and so can look for the `.sql` files in another folder than the tool, which plans nothing for the type until the file is saved as UTF-8.  A C# file in another encoding is read as before when its `Path` is written with ASCII characters only, an escape such as `\u00ea` included.

```console
/work/App/Queries/Users.sql : error SQLSRC271: '/work/App/Queries/Users.sql' is not saved as UTF-8, so its text depends on the system that reads it.  Save it as UTF-8.
```

Save the file as UTF-8, with the "Save with encoding" command of your editor, and run `dotnet sqlsource describe` again.  That fixes the entries the build reported as missing or out of date.  For a `.sql` file the error is reported once, at the file with no line or column; for a C# file it is reported at the `Path` argument, with its line and column.  When the file has a parse error of its own, the tool reports this error instead of the parse errors, since what it read may not be what the build reads.  A file that is also [SQLSRC209](#sqlsrc209) gets this error alone.

See: [Describing again](tool.md#describing-again)

## SQLSRC272

**Result shape needs rows**

A query's name marker gives it the shape `one`, `one-optional` or `many`, and its entry in the sidecar says that its statement returns no rows, as an `UPDATE` without `RETURNING` or `OUTPUT` does.  Those shapes read rows, so the query's method cannot be generated.  The message names the query and its shape.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC272: The query 'RenameUser' has the shape 'one', and its statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.
```

Remove the shape, which gives a query without rows the shape `rowcount`, or write `-> rowcount` for the number of rows the statement affected, or `-> none` for nothing.  When the statement should return rows, change it, and run `dotnet sqlsource describe` again.  The error is reported only while the query's output is `codegen`, since a shape changes nothing under `models` or `sql`.

See: [Result shapes](sql-files.md#result-shapes)

## SQLSRC273

**Driver is not referenced**

A query of the `postgres` dialect has the output `codegen`, and the project does not reference Npgsql, or a query of the `mssql` dialect has it and the project does not reference Microsoft.Data.SqlClient.  The query's method runs on the driver, so it is not generated.  The message names the database and the package.

```console
CSC : error SQLSRC273: A query for PostgreSQL has the output codegen, and this project does not reference Npgsql, which its method needs.  Reference Npgsql, or set the output to models.
```

Reference the package from the project: `Npgsql` 8.0 or later for PostgreSQL, `Microsoft.Data.SqlClient` 6.1 or later for SQL Server.  A project that runs its queries some other way, with Dapper on a connection that a package of its own brings, sets the output to `models`, which gives the SQL and the models without the methods.  The error has no file and line, because it is about the project's references, and it is reported once for the project and database, however many of its queries have the output `codegen`.  The models are still generated.

See: [Query methods](generator.md#query-methods)

## SQLSRC274

**Method name is taken**

The method of a query whose output is `codegen` would have the name of something else, and so it is not generated.  Its name is the query's, with `Async` after it under `async-method-suffix`.  It is taken when it is:

- under the default `MethodLocation.ExtensionClass`, the name of the extension class that holds it, or of a member of `DbConnection` or of `object`, such as `Open` or `ToString`, which a call on a connection would find before the extension method;
- under `MethodLocation.Public`, `Internal` or `Private`, the name of a member of the type, of the type itself or of one of its type parameters, of the constant or token method of another query of the type under `SqlLocation.Direct`, or of the class that holds the type's queries under `SqlLocation.Nested`;
- the name of the method of another query of the same type, which is reported at both queries;
- under `MethodLocation.ExtensionClass`, the name of the method of a query of another type whose methods go into the same extension class, as two types of one name nested in two types do, or of a method of the same name and the same parameters in the extension class of another type of the same namespace, which a call on a connection could not tell apart from it.  Both queries get the error, and neither gets its method.  Two methods of one name in the classes of two types whose parameters differ, one with an input model and one without, say, are two overloads, and each is generated.

The message names the query, the method's name and what takes it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC274: The method of the query 'Open' cannot be named 'Open', because DbConnection has a member of that name, which a call on a connection finds before the method.  Rename the query.
```

Rename the query, with its `-- name:` marker, or rename the member that takes the name.  For a query `Get` under `async-method-suffix` beside a query `GetAsync` without it, rename one of them.  For the methods of two types, rename one of the queries, or set `MethodLocation` on one of the types to put its methods on the type itself.  The error is reported at the query's name, and the query's models are still generated.

See: [Query methods](generator.md#query-methods)

## SQLSRC275

**Token is named as an argument of the method**

A token of a query whose output is `codegen` has the name of one of the arguments that every query method has: `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`.  A token is an argument of the method too, so the two would have one name, and the method is not generated.  The message names the token and the query.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC275: The token 'transaction' of the query 'ListFrom' has the name of an argument that every query method has: connection, parameters, transaction, commandTimeout or cancellationToken.  Rename the token.
```

Rename the token, everywhere the query writes it and in its `-- token:` marker.  The error is reported at the query's name, once for each such token, and the query's models are still generated.

See: [Query methods](generator.md#query-methods)

## SQLSRC276

**Extension class name is taken**

Under the default `MethodLocation.ExtensionClass`, the methods of a type go into a `static partial class` named after the type with `Extensions` after it, `SampleExtensions` for `Sample`, in the type's namespace.  That class cannot be generated, so the type gets none of its methods, when:

- the project already has a type of the class's full name, or a namespace of it, such as a class `App.SampleExtensions` that you wrote;
- SqlSource writes a model of that full name, named by `-- output-model:` or `-- input-model:`, or by a query's name and a suffix, or a model in a namespace of that name;
- two types whose methods go into the one class, as two types `Queries` nested in two types of one namespace do, are of two accessibilities: the class is `public` when the type and every type around it are public, and `internal` otherwise, and one class cannot be both.

Two types of one accessibility that give one class share it, each writing a part of it.  The message names the type, the class and what stops it.

```console
/work/App/Sample.cs(5,6): error SQLSRC276: The query methods of 'App.Sample' cannot go into the extension class 'App.SampleExtensions', because the project has a type of that name.  Rename the type or what has the name, or set the attribute's MethodLocation to put the methods on the type.
```

Rename the type, or the type, namespace or model that has the class's name, or make the two types of one accessibility.  Or set `MethodLocation` on the attribute to `Public`, `Internal` or `Private`, which puts the methods on the type itself and needs no extension class.  The error is reported at the type's `[SqlSourceGenerate]`, once for each type whose methods would go into the class, and the type's models are still generated.

See: [Query methods](generator.md#query-methods)

## SQLSRC277

**Method marked as a converter is not one**

A method marked `[SqlSourceConverter]` must be a converter: a `static` method of one argument, declared in a type, that returns the argument converted to another type, as `public static Email FromDatabase(string value)` does.  The method is not one, and the message names it and the first of these that is wrong with it:

- `it is a local function`: the method is declared inside another method.  Only a method declared in a type is a converter, and the attribute on a lambda is ignored.
- `it is an operator`: the method is an operator, as `operator +`, or a conversion, as `implicit operator Email(string value)`, which the generated code cannot call by name.  Mark a static method that does the conversion instead; the operator can call it.
- `it is not static`: the method is an instance method.
- `it is abstract or virtual`: the method is a `static abstract` or `static virtual` member of an interface, which a call through the interface cannot reach.
- `it is generic`: the method has type parameters of its own.
- `it is in an extension block`: the method is declared inside an `extension` block, which the generated code cannot call it through.  Declare it in a class or a struct, as a member of the type it converts or of a static class.
- `it is in a generic type`: the method is in a generic type, or in a type nested in one.
- `it does not take exactly one argument`: the method has no argument, or more than one.
- `its argument is ref, out, in, params or optional`: the argument has one of those modifiers, or a default value.
- `it returns nothing`: the method returns `void`, `Task` or `ValueTask`, or `Task<T>` or `ValueTask<T>`.  A converter returns its result itself.
- `its return type is annotated as nullable`: the method returns a type written with `?`, as `string?` or `int?`.  A converter's result is never null.  Its argument may be written with `?`, as `Email?` or `UserId?`: it converts the type without the `?`, and is never given null.
- `this project cannot call it`: the method is `private`, `protected` or `private protected` (a `protected internal` method can be called), it is in a type that the project cannot reach, or it is in a `file` type, which code in another file of the project cannot name.
- `it is marked [Obsolete] as an error`: the method, or a type it is in, has `[Obsolete]` with `true` as its second argument, so that no call of it compiles.  A method marked `[Obsolete]` as a warning is a converter.

```console
/work/App/Email.cs(5,18): error SQLSRC277: The method 'App.Email.FromDatabase(string)' is marked [SqlSourceConverter], but it is not static.  A converter is a static method of one argument that returns the converted value.
```

Change the method so that it is a converter, or remove its `[SqlSourceConverter]` when it is not meant to be one.  The error is reported at the method's name, at an operator's symbol, or at the type that a conversion converts to.

A converter declared in a referenced assembly, on a type that a type override, a column type or a `-- param:` marker names, is reported at the name of each query whose output is `codegen` and whose method would convert through it, since the project cannot change the method.  That query gets no method until the library is fixed, or until the project declares a converter of its own between the same two types, which is used instead of the library's.  The library's own build reports the error at the method, since the method is in the library's code.  A library's converter is used only when the project can call it: when it is `public`, or `internal` in a library that gives the project its internals with `InternalsVisibleTo`.

See: [Converters](settings.md#converters)

## SQLSRC278

**Two converters convert between the same types**

Two methods of the project marked `[SqlSourceConverter]` take the same type and return the same type, so either could convert between the two.  The message names both methods and the two types.

```console
/work/App/Conversions.cs(8,25): error SQLSRC278: The converters 'App.Conversions.ToEmail(string)' and 'App.Email.FromDatabase(string)' both convert 'string' to 'App.Email'.  Keep one.
```

Remove `[SqlSourceConverter]` from one of them, or remove the method.  The error is reported at the name of each of the methods.  Two converters of opposite directions, one from `string` to `Email` and one from `Email` to `string`, are a pair and not this error, and a converter of a type to itself, as `DateTime` to `DateTime`, is one converter.  A method that has [SQLSRC277](#sqlsrc277) is not counted until it is fixed.  An argument written with `?` takes the type without it, since a converter is never given null, so `ToDatabase(Email? email)` and `ToDatabase(Email email)` both convert `Email` to `string`, and are this error together.

Two such converters declared in referenced assemblies, on the types that a type override, a column type or a `-- param:` marker names, are reported at the name of each query whose output is `codegen` and whose method would convert through one of them, and that query gets no method.  Declare a converter of your own between the same two types, which is used instead of both.

See: [Converters](settings.md#converters)

## SQLSRC279

**Converters do not convert from the database type**

A type override, a column type or a `-- param:` marker gives a column or a parameter of a query whose output is `codegen` a C# type that the database's driver never reads, such as your own `Email` or `UserId`, and the project has converters of that type, but none between it and the C# type that the driver reads the column's database type as.  So no converter can read the column or send the parameter.  A converter of an `int` never reads a `bigint` column, which the driver reads as a `long`: SqlSource never narrows or widens a value.  The message names the column or parameter, its type, the types its converters convert from and to, the type the driver reads, and the database type.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC279: The column 'id' has type 'App.UserId', whose converters convert from and to 'int', and not from and to 'long', the type the database type 'int8' is read as.  Add a converter between 'App.UserId' and 'long'.
```

Add a converter between the column's or parameter's type and the type the message names, in each direction the queries need: from it for a column, to it for a parameter.  Or change the converters you have to take and return that type, or map the column to another type.  The type the driver reads is the one it reads the database type as with no type override, a domain as its base type, so a converter of `MyApp.Email` over a domain of `text` converts from and to `string`.

The error has a second form, when the column's or parameter's database type has no C# type that the driver reads it as, such as a composite type, `regclass` or a range of a subtype SqlSource does not know: such a type has nothing to convert from.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC279: The column 'address' has type 'App.Address', which has converters, but the database type 'public.address' has no C# type to convert from.  Choose a C# type without converters for it, or remove the converters of 'App.Address'.
```

For an array whose element type a type override, a column type or a `-- param:` marker chose, such as `email[]` under `"public.email": "App.Email"` or `posts.tag_ids` under `"posts.tag_ids": "TagId[]"`, the element's converters are used, element by element, and the message, in either form, names the element of the column or parameter and the element's type.  A type the driver reads for some database type, such as `string`, `DateTimeOffset` or `DateOnly`, is never this error, so that a converter between it and another type does not touch the columns that are read as it.  The error is reported at the query's name, and the query gets no method until it is fixed; its models are still generated.

See: [Converters](settings.md#converters)

## SQLSRC280

**Converter is missing for one direction**

A type override, a column type or a `-- param:` marker gives a column or a parameter of a query whose output is `codegen` a C# type, and the project has a converter between that type and the C# type that the driver reads the column's database type as, but only in the other direction.  A column needs a converter from the type the driver reads, and a parameter one to it.  The message names the column or parameter, its type, the converter it has, and the method it needs.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC280: The column 'email' has type 'App.Email', which has a converter to 'string' but none from it, which a column needs.  Add a static method marked [SqlSourceConverter] that takes 'string' and returns 'App.Email'.
```

Add the converter that the message names, a static method marked `[SqlSourceConverter]` that takes the one type and returns the other, on the type itself or in a class of the project.  A method that takes a type and returns the same type serves both directions.  For an array whose elements are converted one by one, the message names the element of the column or parameter, and the converter to add is of the element's type.  The error is reported at the query's name, and the query gets no method until it is fixed; its models are still generated.

See: [Converters](settings.md#converters)

## SQLSRC284

**Short name finds a type that only a package brings**

A C# type that you chose, by the type override, the column types or a `-- param:` or `-- column:` marker, is a short name, of one part, or has one in its type arguments, and the one type of that name that the project can see is of an assembly that the project has only through a package that it does not reference itself, one that another package, or a project that it references, brings.  That may not be the type you meant.  A package can bring a type of a common name, such as `AccountId`, that has nothing to do with yours, and an update of the package can stop bringing it, or bring another of the name.  The type is used all the same.  The message names the column or parameter, what is mapped, the C# type and the place that chose it, as [SQLSRC250](#sqlsrc250) does, then the short name, the full name of the type it finds and that type's assembly.

```console
/work/App/Queries/Users.sql(1,10): warning SQLSRC284: 'int8' is mapped to 'AccountId' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and the short name 'AccountId' finds 'Brought.Client.AccountId', of the assembly Brought.Client, which this project has only through a package that it does not reference itself.  If that is the type meant, write its full name, 'Brought.Client.AccountId'; otherwise correct the name.
```

If that is the type you meant, write its full name, from its namespace, as the message gives it: `Brought.Client.AccountId`.  You may also reference that assembly's own package from the project, which turns the warning off whatever brought it.  If you meant another type, correct the name: write the full name, from its namespace, of the type you meant.  The query's models are generated, and under the output `codegen` its method, whether or not the warning is fixed.

This is a warning, so that a build goes on when an update of a package brings a new type of a name you use.  A project can list it in `NoWarn` to turn it off, or in `WarningsNotAsErrors` to keep it a warning in a project that sets `TreatWarningsAsErrors`:

```xml
<PropertyGroup>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);SQLSRC284</WarningsNotAsErrors>
</PropertyGroup>
```

A `.globalconfig` file in the project's folder turns it off too:

```ini
is_global = true
dotnet_diagnostic.SQLSRC284.severity = none
```

See: [Type overrides](settings.md#setting-type-overrides), [column types](settings.md#setting-column-types)
