# Diagnostics

Each section of this page is one `SQLSRC` diagnostic, under its id.  The help link of a build error opens the section of that error.  How errors are reported, and the list of the errors by area, are on the page [How errors are reported](reference/errors.md).  The samples of the tool's errors on this page leave out the `see:` line that the tool prints.

## SQLSRC001

**Type must be partial**

Reported by: the build.

The type, or a type that it is nested in, is not declared `partial`.

SqlSource adds members through a second declaration of the type, and of each type that it is nested in.  C# allows a second declaration only when every declaration is `partial`.

```csharp
[SqlSourceGenerate]
public class UserRepository { }
```

**Fix:**

1. Add `partial` to the type that the message names.

See: [The attribute](reference/attributes.md#attribute)

## SQLSRC002

**Type is file-local**

Reported by: the build.

The type is file-local, or it is nested in a file-local type.

A type declared with the `file` modifier exists only in its own source file.  So generated code cannot add to it, or to a type nested in it.

```csharp
[SqlSourceGenerate]
file partial class UserRepository { }
```

**Fix:** do one of these.

- Remove the `file` modifier.
- Move the attribute to a type that is not file-local.

See: [The attribute](reference/attributes.md#attribute)

## SQLSRC003

**Target framework is not supported**

Reported by: the build.

The project targets a framework older than .NET 8.

The generated code uses API that first appeared in .NET 8.

**Fix:** do one of these.

- Target `net8.0` or later.
- If the project targets several frameworks, put the attributed types under a condition that excludes the older frameworks.

See: [What the build needs](reference/msbuild.md#build-requirements)

## SQLSRC004

**Source matches no SQL file**

Reported by: the build.

No `.sql` file matches the `Source` of the attribute.

`Source` is relative to the folder of the source file that carries the attribute.  A value that ends in `.sql` names one file.  Any other value names a folder, and the type gets the `.sql` files directly in it.  Subfolders are not searched.

```csharp
[SqlSourceGenerate(Source = "Queries/User.sql")] // The file is Queries/Users.sql.
public partial class UserRepository { }
```

**Fix:**

1. Check the spelling of the path.
2. Check that the path starts from the folder of this source file, and not from the project.
3. If the file exists, check that the build sees it.

The package registers the `.sql` files under the folder of the project.  A file must be listed as an `AdditionalFiles` item in these cases:

- The file is outside the folder of the project, such as a linked file.
- The project sets `SqlSourceIncludeFiles` to `false`.  Then every `.sql` file of the project must be listed.

See: [Source](reference/attributes.md#setting-source)

## SQLSRC005

**Type has no SQL file**

Reported by: the build.

The folder of the source file that carries the attribute has no SQL file named for the type.

Without `Source`, the type gets the `.sql` file named for it in that folder: `UserRepository.sql` for `UserRepository`.

```console
/work/App/Queries/UserRepository.cs(3,2): error SQLSRC005: No 'UserRepository.sql' beside this file.  Add it, or set Source.
```

**Fix:** do one of these.

- Add `UserRepository.sql` next to the source file.
- Rename the file you have.
- For a file of another name, or for several files, set `Source`.  `Source = "Users.sql"` is one file, and `Source = "."` is every `.sql` file of the folder.
- If the file is there, check that the build sees it, as described for [SQLSRC004](#sqlsrc004).

See: [Source](reference/attributes.md#setting-source)

## SQLSRC007

**Type has a member named like its nested class**

Reported by: the build.

A query whose [member location](reference/member-placement.md#locations) is `nested-class` puts its members in a class nested in the type.  That class is named `Sql`, unless [the nested class](reference/member-placement.md#setting-nested-class) is named otherwise.  The type already has a member with that name, and the message names it.

The build reports the same error when the type itself, or one of its type parameters, has that name.  A nested class cannot share either name.

The build reports the error at the `[SqlSourceGenerate]` of the type, once for each name that is taken.  A file of the type can have a query whose members would go in a class of that name.  Such a file produces no members and no models for the type.  The other files of the type keep theirs.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    private string Sql { get; }
}
```

```console
/work/App/Queries/UserRepository.cs(3,2): error SQLSRC007: 'UserRepository' already has a member named 'Sql', which a file of it names as its nested class.  Rename the member, or set -- nested-class: or -- member-location: in the file.
```

**Fix:** do one of these.

- Rename the member, the type or the type parameter.
- Give the nested class another name, with `-- nested-class:` in the file or `nestedClass` in a settings file.
- Write `-- member-location: class`, so that the queries become members of the type itself.

See: [Where the members go](reference/member-placement.md#locations)

## SQLSRC008

**Query name is used in two files**

Reported by: the build.

Two `.sql` files of one type each have a query with the same name.  The error is at the second query, and the message names the file that holds the first.

A type cannot have two members with one name.  The file that holds the second query produces no members and no models for that type.

**Fix:**

1. Rename one of the two queries.

See: [Which files belong to a type](reference/attributes.md#files-of-a-type)

## SQLSRC009

**Query is named like its containing type**

Reported by: the build.

The member of a query would have the name of the type that contains the member.  The error is at the name of the query.

A member cannot have the name of the type that contains it.  The file produces no members and no models for the type until the error is fixed.

The rule applies to two kinds of query:

- A query whose output is `models` or `sql`, whose constant or token method is a member of that type.
- A query whose output is `methods`, whose method is in a nested class.

The type that contains the member depends on the [member location](reference/member-placement.md#locations):

- **Under `nested-class`, it is the generated class.**  No query there can have the name of that class: `Sql`, unless [the nested class](reference/member-placement.md#setting-nested-class) is named otherwise.  No query method there can have that name either.
- **Under `class`, it is the attributed type.**  No query whose output is `models` or `sql` can have the name of that type.

A query method takes its name from the query:

- An asynchronous method has the query's name, with `Async` after it under `async-method-suffix`, and always under the generator parameter [`method-type`](reference/generator-parameters.md#setting-generator-parameters) `both`.
- A synchronous method, under `sync` or `both`, has the query's name alone.

So under `both`, the generated class can have neither of the two names, whatever the shape of the query.

A query method on the type or in the extension class has no member of the class to clash with.  What its name cannot be is [SQLSRC274](#sqlsrc274).

```sql
-- name: Sql
-- output: sql
SELECT 1;
```

**Fix:** do one of these.

- Rename the query.
- Write `-- nested-class:` on the query.
- Write `-- member-location:` on the query.

See: [Where the members go](reference/member-placement.md#locations)

## SQLSRC012

**Language version is not supported**

Reported by: the build.

The project sets `LangVersion` to a version older than the code that SqlSource generates.  The message quotes the version that the project uses.

That code is C# 12, the default language version of a project that targets .NET 8.  The build reports the error at each `[SqlSourceGenerate]` attribute, and no type gets members until it is fixed.

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion>
</PropertyGroup>
```

**Fix:** do one of these.

- Remove `LangVersion` to get the default of the target framework.
- Set `LangVersion` to `12` or later.

The project file, `Directory.Build.props` and a `-p:` argument of the build command can each set it.

- **A project that targets a framework older than .NET 8 gets [SQLSRC003](#sqlsrc003) and not this error.**  Its language version is older by default, and targeting .NET 8 fixes both errors.

See: [What the build needs](reference/msbuild.md#build-requirements)

## SQLSRC013

**SQL file paths differ only by case**

Reported by: the build.

Two `.sql` paths of the project differ only by upper and lower case.  The error is at the start of the file that SqlSource would ignore, and the message names the other one.  The build reports the error only for a `.sql` file that a type claims.

SqlSource compares paths ignoring case, so that a project builds the same on every operating system.  So the two paths are one file to it.  It would use the one that the project lists first, and ignore the other.

The cause is one of these.

**Two files have paths that differ only by case.**  Examples are `Users.sql` and `users.sql` in one folder, or a file of the same name in `Queries` and in `queries`.  Such a pair can exist only on a file system that tells upper case from lower.  It already breaks a checkout of the repository on Windows and on macOS.

**Fix:**

1. Rename one of the files, or one of the folders.

**The project lists one file twice, with spellings that differ by case.**  SqlSource is not allowed to ask the file system whether two paths are one file, so the build reports the same error.  An `AdditionalFiles` item written by hand, for a file that the package already includes, causes it.  Here the file is `Queries/Users.sql`:

```xml
<ItemGroup>
    <AdditionalFiles Include="queries/users.sql" />
</ItemGroup>
```

**Fix:** do one of these.

- Remove the item, since the package already includes the file.
- To change the item that the package adds, write `Update` in place of `Include`.  Spell the path as it is on disk.

See: [Source](reference/attributes.md#setting-source)

## SQLSRC015

**Settings file is not a JSON object**

Reported by: the build and the tool.

A settings file, `sqlsource.json`, holds one JSON object, and this one does not.  The message says which of these is the cause:

- The text is not JSON.
- The text is JSON whose top level is an array, a string or another value that is not an object.

The JSON of a settings file can have `//` and `/* */` comments.  It can also have a comma after the last member of an object or the last element of an array.  Anything else that JSON does not allow is this error, such as a missing comma or a key without quotes.  So is a value nested more than 64 levels deep.

The error is at the first character that breaks the JSON, or at the end of a file that is cut short.  For a top level that is not an object, the error is at the whole value.

While the JSON is wrong, the file gives nothing.  None of its settings applies, and neither does its `root`.

```jsonc
{
    // A comma is missing after "models".
    "output": "models"
    "dialect": "postgres",
}
```

**Fix:**

1. Correct the JSON.

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC016

**Settings file key is not known**

Reported by: the build and the tool.

A key of the top-level object of a settings file is not one of the keys of a settings file.  The message quotes the key, and the error is at the key.

Keys are matched exactly, case included, so `Dialect` gets this error.  While the key is wrong, its value is not read, and the other members still apply.

```jsonc
{
    "Dialect": "postgres",
    "outptu": "models",
}
```

**Fix:** do one of these.

- Correct the key.
- Remove the member.

The keys are `$schema`, `root`, `dialect`, `database`, `output`, `generatorParameters`, `inputModelSuffix`, `outputModelSuffix`, `modelNamespace`, `inputModelType`, `outputModelType`, `collectionType`, `typeOverrides`, `columnTypes`, `libraryTypes`, `nestedClass`, `memberLocation` and `visibility`.

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC017

**Settings file key is written twice**

Reported by: the build and the tool.

A key is written twice in the top-level object of a settings file.  The first member applies.  Each later member gets this error at its key, whatever its value.

```jsonc
{
    "output": "models",
    "dialect": "postgres",
    "output": "methods",
}
```

**Fix:**

1. Remove the member whose value you do not want.

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC018

**Settings file value has the wrong type**

Reported by: the build and the tool.

A value of a settings file is not of the JSON type that its key takes.  The message names the key and what it takes, and the error is at the value.

Each key takes one JSON type:

- `root` takes `true` or `false`.
- `generatorParameters` takes an array of strings.
- `typeOverrides` and `libraryTypes` take an object whose values are strings.
- `columnTypes` takes an object whose values are strings, or objects of a `"type"` string and an optional `"json"` boolean.
- Every other key takes a string.

`null` is of the wrong type for every key.

While a value is of the wrong type, the file does not set its setting.  Arrays and objects follow these rules:

- An element of `generatorParameters`, or a member of `typeOverrides`, `columnTypes` or `libraryTypes`, that is of the wrong type gets this error on its own.  It is left out, and the others still apply.
- An array that has elements, none of them a string, sets nothing.  `[]` sets nothing too.

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

**Fix:** do one of these.

- Write the value as its key takes it, such as `"root": true` or `"generatorParameters": ["keep-comments"]`.
- To leave a setting unset, remove its member.

For a key that takes a string, an empty string also sets nothing, and so does a string of white space.  Such a string is not an error.

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC019

**Settings file value is not valid**

Reported by: the build and the tool.

A value of a settings file has the JSON type that its key takes, but its setting does not take the value.  The message quotes the value, trimmed, and names the key.  For a member of `typeOverrides`, `columnTypes` or `libraryTypes`, the message quotes the member as `key: value`.  The error is at the string.  For a member, the error runs from its key to its value.

A member of `columnTypes` can be written as an object.  The object takes a `"type"` string, its C# type.  It can also take `"json"`, `true` or `false`, and no other member.  With `"json": true`, its type cannot be `default`.  The message quotes the object by its key alone when the object has one of these:

- no `"type"` string;
- a `"json"` that is not `true` or `false`;
- a member twice;
- any other member.

If the type of the object is not valid, the message quotes it as `key: type`.  In each case, the message says what the object takes.

While the value is wrong, this file does not set the setting, so a farther settings file or the default applies.  In `generatorParameters`, `typeOverrides`, `columnTypes` and `libraryTypes`, each element or member that is not valid gets this error on its own.  The others still apply.

```jsonc
{
    "dialect": "pgsql",
    "output": "model",
    "generatorParameters": ["keep-comments", "keep-coments", "array-pool=no"],
    "typeOverrides": {
        "[dbo].[Email]": "MyApp.Email",
        "int4": "int?",
    },
    "columnTypes": {
        "id": "MyApp.UserId",
        "orders.metadata": { "type": "OrderMetadata", "json": "yes" },
    },
    "libraryTypes": {
        "nodaTime": "yes",
    },
}
```

**Fix:** do one of these.

- Correct the value.
- Remove the value.

The values that each key takes follow.

`dialect` takes the name of a dialect, in any case: `ansi`, `mssql`, `postgres`, `cockroachdb`, `mysql`, `mariadb`, `sqlite` or `oracle`.  Some names have another spelling:

- `sqlserver` and `tsql` also mean `mssql`.
- `postgresql` also means `postgres`.
- `cockroach` also means `cockroachdb`.

After `mysql` or `mariadb`, the value can name options, each after a comma, as in `"mysql,ansi-quotes"`.  The options are `ansi-quotes` and `no-backslash-escapes`, also written `ansi_quotes` and `no_backslash_escapes`.  Each of these makes the whole value wrong:

- an option that does not exist;
- an option after any other dialect;
- a comma with nothing before it or after it.

These keys take one value from a list.  The value matches in any case, and with or without hyphens and spaces:

- `output` takes `repository`, `methods`, `models` or `sql`.
- `inputModelType` and `outputModelType` take `record`, `sealed record`, `class` or `sealed class`.
- `collectionType` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` or `IAsyncEnumerable`.
- `memberLocation` takes `extension-class`, `class` or `nested-class`.
- `visibility` takes `public`, `internal`, `protected` or `private`.

These keys take a name or a word:

- `inputModelSuffix` and `outputModelSuffix` take characters that can be part of an identifier.
- `modelNamespace` takes a namespace such as `App.Models`.  None of its parts can be a reserved keyword or `file`, `required`, `scoped` or `extension`.  C# does not take these as the name of a type.
- `nestedClass` takes one identifier.  It cannot be one of those words, and it cannot be `var`.
- `database` takes one word of letters, digits, `-`, `_` and `.`.

`generatorParameters` takes one word in each element, in any case.  The word is one of these:

- A generator parameter, alone or followed by `=on`, `=off` or `=default`.  The generator parameters are `keep-comments`, `token-validation`, `sort-input`, `sort-output`, `table-models`, `async-method-suffix`, `array-pool`, `list-padding`, `collection-method`, `transaction` and `method-type`.
- `default` alone.

`transaction` also takes the words of an isolation level.  `method-type` takes only `=async`, `=sync`, `=both` or `=default`.

In `generatorParameters`, each of these gets this error:

- a name that is not a generator parameter, `no-token-validation`, `no-table-models` and `no-array-pool` of earlier previews among them;
- a value that is neither one of the values of the generator parameter nor `default`;
- `default` followed by `=`.

`[]` names no generator parameter and changes nothing.  `["default"]` gives every generator parameter its default.  The rules are in [generator parameters](reference/generator-parameters.md#setting-generator-parameters).

`typeOverrides` takes members `"key": "type"`.  The key and the type of each member are trimmed.

- **The key is the one-word name of a type.**  Write `timestamptz` and not `timestamp with time zone`, and `float8` and not `double precision`.  Every built-in type has one.
- **A type outside `pg_catalog`, or a SQL Server type outside `sys`, is written with its schema.**  Examples are `public.user_status` and `dbo.Email`.  A name without a schema must be a type that SqlSource knows.  So `user_status` and the misspelling `timestampz` get this error.
- **A value serves PostgreSQL and SQL Server alike.**  So a name of the types of either database, such as `int4` or `nvarchar`, does not get this error.
- **A schema or a name can be in double quotes.**  Each quote is written `\"` in JSON, as in `"\"my schema\".\"order-status\""`.
- **A part can be in `U&"..."` with escapes.**  An escape that is not valid makes the member not valid.
- **A key is not written in brackets.**  `"[dbo].[Email]"` gets this error.  The message shows the forms that SqlSource reads:
  - `dbo.Email`, which matches the name of the type in any case.
  - `"dbo"."Email"`, which matches its case exactly.  As a JSON key, it is written `"\"dbo\".\"Email\""`.
  - For a key without a schema, the plain form alone: `[INT]` is `INT`.  A quoted name without a schema must be in lower case.
  - For a part that holds anything but letters, digits, `_` and `$`, such as `[my schema]`, the quoted form alone: `"my schema"`.
- **The C# type is a keyword, a full name or a short name**, such as `System.DateTimeOffset` or `DateTimeOffset`.  It has no `?` at the end: `"int4": "int"` and not `"int4": "int?"`.  It can also be `default`.

The rules are in [type overrides](reference/type-mapping.md#setting-type-overrides).

`columnTypes` takes members `"key": "type"`, trimmed in the same way.  Its key is the column that a value is read from: `table.column` or `schema.table.column`.  Each part is plain or in double quotes.  So each of these gets this error:

- `id`, a key of one part;
- a key of four parts;
- a key in brackets;
- a key with `[]`.

Its C# type is read as the type of a type override is, or is `default`.  The rules are in [column types](reference/model-properties.md#setting-column-types).

`libraryTypes` takes members `"library": "state"`.  Each is trimmed and matched ignoring case, hyphens and spaces.  The library is `nodaTime`, `netTopologySuite` or `json`, and the state is `on`, `off` or `default`.  The message names them.  The rules are in [library types](reference/library-types-and-json.md#setting-library-types).

Values that are empty or white space follow these rules:

- **For a key that takes a string, a value that is empty or white space sets nothing.**  It is not an error.
- **An element of `generatorParameters` that is empty or white space is skipped.**
- **A member of `typeOverrides`, `columnTypes` or `libraryTypes` whose key or value is empty or white space gets this error.**

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC020

**Settings file entry repeats a key**

Reported by: the build.

An entry of a settings file repeats another entry of the same setting.  The cause is one of these:

- Two members of `typeOverrides`, two of `columnTypes`, or two of `libraryTypes`, have one key.
- Two elements of `generatorParameters` give one generator parameter two different states.

Of two members that are one, the first applies, and the build reports the second at its key.  The message quotes both keys and names the setting.

Two keys are one when they are the same once their quotes and escapes are read and their aliases resolved.  So each of these pairs is one key:

- `integer` and `int4` name one PostgreSQL type.
- `"users".id` and `users.id` name one column.
- `nodaTime` and `NodaTime` name one library.
- `timestamptz` and `TIMESTAMPTZ` are one key, since an unqualified built-in type name is one in any case.

Keys that differ only in the case of a plain part of a qualified name are two keys, and the build reports nothing.  So `users.id` and `Users.ID` both apply, and so do `dbo.Email` and `DBO.EMAIL`.  A type or column whose name matches one of them exactly takes that one.

In `generatorParameters`, `keep-comments` and `keep-comments=off` are two states of one generator parameter.  So are `keep-comments=default` and either of them, even where the default is the same.  The first applies, and the build reports the second at its element.  The same state twice is no error.

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
    "generatorParameters": ["keep-comments", "keep-comments=off"],
}
```

**Fix:**

1. Remove one of the two entries.  Keep the one with the C# type or the state that you want.

See: [Settings files](reference/settings-file.md#settings-files)

## SQLSRC021

**Compiler is too old**

Reported by: the build.

The error comes before the build compiles anything.

The C# compiler of the build is older than the compiler that SqlSource needs.  The message names the version of the compiler that the build found.

SqlSource needs the compiler of the .NET SDK 9.0.300 or later, or of Visual Studio 2022 17.14 or later.  A compiler that old cannot run SqlSource at all.  So the build stops, and no query gets a method.

**Fix:**

1. Update the .NET SDK to 9.0.300 or later, or Visual Studio to 2022 17.14 or later.
2. If a `global.json` pins an older SDK, change the version that it pins.  Such a file keeps the build on the old compiler.

- **A project can still target .NET 8.**  The SDK builds it, and the project runs on the framework that it targets.

See: [What the build needs](reference/msbuild.md#build-requirements)

## SQLSRC022

**Exceptions are seen in two assemblies**

Reported by: the build.

A project that uses SqlSource sees `UnexpectedRowCountException`, `TransactionRolledBackException` or `CollectionItemException` in two or more of the projects or packages that it references.  Each of those uses SqlSource, and either grants this project `InternalsVisibleTo` or makes its exceptions public.  The message names the assemblies.  The error has no file and line, because it is about the project's references.

The query methods of this project throw one type of each, and they cannot take one copy over the other.  The project still gets its own exceptions, so the error is the only one.  Code that names the exceptions can also get the compiler's warning CS0436.

```console
CSC : error SQLSRC022: This project sees UnexpectedRowCountException, TransactionRolledBackException or CollectionItemException in more than one referenced assembly: DataA, DataB.  Use SqlSource in one project that the others reference and that grants them InternalsVisibleTo, so that they all use its exceptions.
```

**Fix:**

1. Add a shared project that references SqlSource and needs no query.
2. Have the shared project grant `InternalsVisibleTo` to each project that uses SqlSource and sees it, this project among them.
3. Have those projects reference the shared project.

Each of those projects then uses the exceptions of the shared project instead of writing its own, and this project sees one copy.  See [the recipe](guides/several-projects.md#share-the-exceptions).

- **The shared project grants `InternalsVisibleTo` to every project that catches the exceptions by type.**  It also grants it to every project that uses SqlSource and sees the internals of a data project.  The test projects of the data projects are among them.  If such a project does not see the copy of the shared project, it gets its own.  Its `catch (UnexpectedRowCountException)` then compiles, but does not catch what a method of a data project throws.

See: [Projects that share internals](reference/exceptions.md#projects-that-share-internals)

## SQLSRC023

**Exceptions come from another version of SqlSource**

Reported by: the build.

A project that uses SqlSource sees `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` in a project or package that it references.  They are not as this version of SqlSource writes them: one of them is missing, or its constructors differ from this version's.  The message names the assembly.  The error has no file and line, because it is about the project's references.

That happens when the other project uses another version of SqlSource.  The project still gets its own exceptions, so the error is the only one from SqlSource.  Code that names the exceptions, its query methods among them, can also get the compiler's warning CS0436.

```console
CSC : error SQLSRC023: The UnexpectedRowCountException, TransactionRolledBackException and CollectionItemException that this project sees in Data are not those of this version of SqlSource.  Use the same version of SqlSource in both projects.
```

**Fix:**

1. Use the same version of SqlSource in every project of the solution, for example with central package management.

See: [Projects that share internals](reference/exceptions.md#projects-that-share-internals)

## SQLSRC024

**SqlSourceOnline is not valid**

Reported by: the build.

The error is at the project file.

The project sets the MSBuild property `SqlSourceOnline` to a value that is not `auto`, `true` or `false`.  The message names the value, without the white space around it.  The build stops before it compiles anything.

```console
/repo/App/App.csproj : error SQLSRC024: SqlSourceOnline is 'yes', which is not auto, true or false.  Set it to one of the three, or leave it empty for auto.
```

**Fix:** do one of these.

- Set `SqlSourceOnline` to `auto`, `true` or `false`.
- Remove the property to get `auto`.

The property can come from the project file or from a `Directory.Build.props` above it.

- **The case of the value and the white space around it do not matter.**  So ` True ` is `true`, and an empty value is `auto`.

See: [Running the tool in the build](reference/online-build.md#online-build)

## SQLSRC025

**Build cannot run the tool**

Reported by: the build.

The error is at the project file.

The build could not start the tool, and stops.  `SqlSourceOnline` is `true`, so a build that is not in CI runs `dotnet sqlsource describe` on the project before it compiles.

The cause is one of these:

- No tool manifest in the folder of the project, or in a folder above it, names `SqlSource.Tool`.
- The tool that the manifest names was never restored on this machine.
- The tool was restored with another major version of the .NET SDK, such as 10 before you moved to 11.  Each major version needs its own `dotnet tool restore`.

```console
/repo/App/App.csproj : error SQLSRC025: The build could not start the sqlsource tool, and SqlSourceOnline is true.  Install SqlSource.Tool as a local tool, run 'dotnet tool restore' if the tool manifest names it already, or set SqlSourceOnline to false to build from the committed sidecars.
```

**Fix:** do one of these.

- Install the tool as a local tool of the repository, as [install the tool](reference/cli.md#install-the-tool) says.
- If the tool manifest names the tool already, run `dotnet tool restore`.
- To build from the sidecars that you committed, without the tool, set `SqlSourceOnline` to `false`.

See: [Running in the build](reference/online-build.md#running-in-the-build)

## SQLSRC026

**Build cannot run the tool and uses the committed sidecars**

Reported by: the build.

The warning is at the project file.  A project can list `SQLSRC026` in the MSBuild property `MSBuildWarningsAsMessages`, which turns the warning into a message.

`SqlSourceOnline` is `auto`, or not set, and the build found a connection.  So it ran `dotnet sqlsource describe` on the project before it compiled, and it could not start the tool.

The connection that the build found is the variable `SQLSOURCE_CONNECTION` or the build's `.env` file.  That file is the one in the folder of the project, or the one that [`SqlSourceEnvFile`](reference/online-build.md#setting-env-file) names.  A `.env` file that holds your application's settings and no connection of SqlSource turns the build on too.

The cause is one of these:

- No tool manifest names `SqlSource.Tool`.
- The tool that the manifest names was never restored on this machine.
- The tool was restored with another major version of the .NET SDK, such as 10 before you moved to 11.  Each major version needs its own `dotnet tool restore`.

```console
/repo/App/App.csproj : warning SQLSRC026: The build could not start the sqlsource tool, and goes on with the committed sidecars.  Run 'dotnet tool restore', or install SqlSource.Tool as a local tool, for the build to describe the queries; or set SqlSourceOnline to false to build from the sidecars without this warning.
```

**Fix:** do one of these.

- Run `dotnet tool restore`.  The next build then describes the queries.
- Install the tool as [install the tool](reference/cli.md#install-the-tool) says.  The next build then describes the queries.
- To build the project from its committed sidecars, set `SqlSourceOnline` to `false`.  That also stops the warning.

This is a warning: the build goes on with the sidecars that it has, as a build with no connection does.  The next build tries the tool again.  It stays a warning under `-warnaserror`, and the project's `TreatWarningsAsErrors` does not reach it.

See: [Running in the build](reference/online-build.md#running-in-the-build)

## SQLSRC027

**Tool failed without an error**

Reported by: the build.

The error is at the project file.

The build ran `dotnet sqlsource describe` on the project before it compiled.  The tool ended with an exit code other than 0, and it reported no error of its own.  The message names the exit code and the first line that the tool printed.

The build cannot tell whether the sidecars are current, so it stops.  The usual cause is a tool older than the `SqlSource` package.  Such a tool does not know an option that the build gives it, and prints a line such as `sqlsource: unknown option at position 2`.  The message then says to update the tool.

```console
/repo/App/App.csproj : error SQLSRC027: The sqlsource tool exited with the code 1 and reported no error, so the sidecars may not be current.  The tool may be older than the SqlSource package, and not know the options the build gives it: update it to the package's version with 'dotnet tool update SqlSource.Tool --version <version>'.  It printed first: 'sqlsource: unknown option at position 2'.
```

**Fix:**

1. Bring the tool to the version of the `SqlSource` package that the project uses, with `dotnet tool update SqlSource.Tool --version <version>`.  The two are released together under one version number.
2. If the versions are the same, run `dotnet sqlsource describe` in the folder of the project to see what the tool says.

See: [Running in the build](reference/online-build.md#running-in-the-build)

## SQLSRC028

**Repository has no connection**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

The type has no connection for a database that its repository queries use.  The error is at the type's `[SqlSourceGenerate]`, once for each database without a connection.

A query whose output is `repository` gets an instance method of the type that claims it.  The method takes its connection from one of these.  The build looks among the members of the type first, then in the factories it holds, then among the `static` members of the project.

- a `string` field, property or primary-constructor parameter of the type named `connectionString`, `_connectionString` or `ConnectionString`;
- a field, property or primary-constructor parameter of the type, of a type that derives from `DbDataSource`;
- the member of the type that you mark `[SqlSourceConnection]`;
- a marked member of a factory that the type holds in a field, property or primary-constructor parameter;
- a marked `static` member of the project.

The repository queries of a database without a connection get no method.  The database is the one that the [database](reference/settings.md#setting-database) setting of the queries names, or the name of their dialect.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    private readonly string _connection = "";
}
```

```console
/work/App/UserRepository.cs(1,2): error SQLSRC028: 'App.UserRepository' has no connection for the database 'postgres', which its repository queries use.  Add a string field, property or primary-constructor parameter named connectionString, or one of a DbDataSource type, or mark the member that gives the connection [SqlSourceConnection], whether a member of the type, an instance member of a type it holds in a field, property or primary-constructor parameter, or a static member of the project.  Or set the output of its repository queries to methods.
```

**Fix:** do one of these.

- Give the type a member that the build finds by its name or type, such as a primary-constructor parameter `NpgsqlDataSource dataSource`.
- Mark the member that gives the connection `[SqlSourceConnection]`: a member of the type, a member of a factory that the type holds, or a `static` member of the project.
- **A `static` member of the project gives the connection only to the types that can reach it.**  A `private` member of another class gives none.  Make it `internal` or `public`.
- **When a member is marked, no other is found by its name or type.**  Also mark the member that gives this database its connection.
- **The type passes a primary-constructor parameter to its base type, or uses it to initialize a field or property.**  Then the parameter is not found by its name or type, and the message names it.  For a type whose repository queries use one database, the build then looks neither in a factory nor among the `static` members of the project.  Keep the value in a field or property named `connectionString` or of a `DbDataSource` type.  Or mark the parameter `[SqlSourceConnection]`.
- **When the repository queries of the type use more than one database, no member is found by its name or type.**  Nothing says which database the member is for.  Mark one member for each database, as `[SqlSourceConnection(Database = "reports")]`.  A factory or a `static` member of the project can give the connection too, marked for the database or without a `Database`.
- Set the [output](reference/settings.md#setting-output) of the queries to `methods`.  Their methods take the connection as an argument.

See: [Where the build looks](reference/repository-connections.md#connection-order), [the connection](reference/repository-connections.md#repository-connection)

## SQLSRC029

**Repository has more than one connection**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

The type has more than one member that could give its repository queries their connection for one database.  The error is at the type's `[SqlSourceGenerate]`, and the message lists the members.  The repository queries of that database get no method.

The members are one of these:

- two members that the build finds by their name or type;
- two members marked `[SqlSourceConnection]` for the same database that both return a `Task` or `ValueTask`, or both do not;
- three or more marked members for the same database;
- two such marked members of the factories that the type holds, as when two fields hold one factory type;
- two such marked `static` members of the project.

The members of the type come first, then those of its factories, then the `static` members of the project.  The build takes its members from the first of these that has any for the database, so members of two of them are never counted together.  The message names a member of a factory through its holder, `'_main.OpenAsync'`, with its interface when the build reaches it through the interface, as an explicit implementation, `'_main.IAuditFactory.OpenAsync'`, and a `static` member through its type, `'Db.Main'`.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    private readonly string _connectionString = "";
    private readonly NpgsqlDataSource _dataSource;
}
```

```console
/work/App/UserRepository.cs(1,2): error SQLSRC029: 'App.UserRepository' has more than one connection for the database 'postgres': '_connectionString', '_dataSource'.  Mark only the one to use [SqlSourceConnection], or two of which only one returns a task: the asynchronous methods use that one, and the synchronous methods the other.
```

**Fix:** do one of these.

- Mark the member to use `[SqlSourceConnection]`.  The others are then left alone.
- **When several members are marked**, leave one marked for the database.  Or set the `Database` of each to the database that it connects to.
- **When two fields hold factories with marked members**, keep one of the fields.  Or mark a member of the type itself that gives the database its connection, which then wins over both factories.

A database can keep two marked members when exactly one of them is a method that returns a `Task` or `ValueTask` of a connection.  Its asynchronous methods get their connection from that one, and its synchronous methods from the other.  The other can be a connection string, a data source, a connection or a method that returns a connection.  This sample shows such a pair, which is allowed:

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    [SqlSourceConnection]
    private Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) => ...;

    [SqlSourceConnection]
    private NpgsqlConnection Open() => ...;
}
```

- **A primary-constructor parameter is found by its name or type only when no field or property of the type or of a base type is.**  So a parameter that the type stores in a field of its own gives one connection, the field.

See: [The connection](reference/repository-connections.md#repository-connection), [a factory that a repository holds](reference/repository-connections.md#held-factory)

## SQLSRC030

**Connection needs a database**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

A member is marked `[SqlSourceConnection]` without a `Database`, and the repository queries of the type use more than one database.  The error is at the member.

The build cannot tell which database the member connects to.  Each database that no member is marked for by name gets no repository method until it is fixed.

A member of a factory that the type holds, or a `static` member of the project, marked without a `Database` does not get this error.  It serves each database that no other mark names: see [which database a shared mark is for](reference/repository-connections.md#shared-marks).

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    [SqlSourceConnection]
    private readonly NpgsqlDataSource _dataSource;
}
```

```console
/work/App/UserRepository.cs(4,39): error SQLSRC030: '_dataSource' of 'App.UserRepository' is marked [SqlSourceConnection] without a Database, and the repository queries of 'App.UserRepository' use more than one database: 'main', 'reports'.  Set its Database to the one it connects to.
```

**Fix:**

1. Set the `Database` of the member, as `[SqlSourceConnection(Database = "main")]`.
2. Mark a member for each other database.

See: [The connection](reference/repository-connections.md#repository-connection)

## SQLSRC031

**Connection's database is not used**

Reported by: the build.

A type gets this warning only when it claims a query whose output is `repository`.

A member is marked `[SqlSourceConnection]` for a database that no repository query of the type uses.  The warning is at the member.

The cause is one of these:

- The `Database` of the member is misspelt.
- The queries of that database have another output.
- The queries of that database are gone.

The member gives no method its connection, so the warning leaves out no method.  The build compares the name of a database without regard to case.

A member of a factory that the type holds, or a `static` member of the project, marked for a database that the type does not use gets no warning.  It serves other repositories: see [which database a shared mark is for](reference/repository-connections.md#shared-marks).

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    [SqlSourceConnection(Database = "archive")]
    private readonly NpgsqlDataSource _archive;

    [SqlSourceConnection(Database = "main")]
    private readonly NpgsqlDataSource _main;
}
```

```console
/work/App/UserRepository.cs(4,39): warning SQLSRC031: '_archive' of 'App.UserRepository' is marked [SqlSourceConnection] for the database 'archive', and no repository query of 'App.UserRepository' uses it: they use 'main'.  Correct its Database, or remove the mark.
```

**Fix:** do one of these.

- Correct the `Database`.
- Remove the mark from a member that your own code uses for other queries.

This is a warning, since a type that keeps a member for queries whose output is `methods` for a while still builds.  A project can list it in `NoWarn` to turn it off.  A project that sets `TreatWarningsAsErrors` can list it in `WarningsNotAsErrors` to keep it a warning:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC031</NoWarn>
</PropertyGroup>
```

See: [The connection](reference/repository-connections.md#repository-connection)

## SQLSRC032

**Connection member is not supported**

Reported by: the build.

A member of a repository, or of a factory that it holds, gets this error only when the repository claims a query whose output is `repository`.  A `static` member of the project, outside every type marked `[SqlSourceGenerate]`, gets it whatever the queries.  The queries that get no method still get their models.

A member marked `[SqlSourceConnection]` cannot give a repository method its connection.  Where the error is depends on where the member is:

- **A member of the type.**  The error is at the member.
- **A member of a factory that the type holds.**  The error is at the holder, in each type that would use the member.  The message names the member through its holder: `'IConnectionFactory.OpenAsync' reached through 'connections'`, by the holder's type, or by the interface for a member reached through it.
- **A `static` member of the project.**  The error is at the member, once, whether a type uses it or not.  The message names the member's own type in place of a repository.
- **A `static` member of a base type of a type marked `[SqlSourceGenerate]`.**  It is that type's own member, so the error is at the member, in each repository that derives from the base type and needs the member.

The repository queries of the database of the member get no method.  When the member names no database, the repository queries of every database get no method.  A `static` member of the project with this error is never a source, so a type that needs it gets [SQLSRC028](#sqlsrc028).

A connection comes from one of these members:

- a field, property or primary-constructor parameter of a `string`, of a type that derives from `DbDataSource`, or of a type that derives from `DbConnection`;
- a method that is not generic and takes nothing or one `CancellationToken`.  It must return a `string`, a type that derives from `DbConnection`, or a `Task` or `ValueTask` of such a type.

An indexer, a property with no getter of its own, unless it is an `override` that inherits one the repository can call, an accessor, an operator and an explicit implementation of an interface's member are none of these either.

A marked interface member that a `ref struct` factory implements explicitly cannot give the connection either, since a `ref struct` cannot be cast to its interface.  Implement it implicitly, as a `public` member of the `ref struct`.

A `static` member of the project also gets this error in these cases, since a method cannot reach it:

- It is in a generic type, or in a type nested in one.
- It is in a `file` type, or in a type nested in one.
- It is a `static abstract` or `static virtual` member of an interface.

A mark that can never give a connection gets this error at the mark too.  It is a mark on a parameter of a method, or of the primary constructor of a type that is not marked `[SqlSourceGenerate]`.  It is also a mark on a lambda, a local function or an enum member.

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    [SqlSourceConnection]
    private NpgsqlConnection Connect(string name) => new(name);
}
```

```console
/work/App/UserRepository.cs(4,30): error SQLSRC032: 'Connect' of 'App.UserRepository' is marked [SqlSourceConnection], and cannot give a connection.  Mark a field, property or primary-constructor parameter of a string, a DbDataSource or a DbConnection, or a method that is not generic, takes nothing or a CancellationToken, and returns a string, a DbConnection, or a Task or ValueTask of a DbConnection.
```

```csharp
public static class Connections<T>
{
    [SqlSourceConnection]
    public static string Main = "";
}
```

```console
/work/App/Connections.cs(4,26): error SQLSRC032: 'Main' of 'App.Connections<T>' is marked [SqlSourceConnection], and cannot give a connection.  Mark a field, property or primary-constructor parameter of a string, a DbDataSource or a DbConnection, or a method that is not generic, takes nothing or a CancellationToken, and returns a string, a DbConnection, or a Task or ValueTask of a DbConnection.
```

**Fix:** do the one that fits.

- Mark a member of one of the shapes above.
- Implement a marked interface member of a `ref struct` factory implicitly.
- Move a `static` member of the project out of a generic or `file` type.
- Remove a mark that can never give a connection.

See: [The connection](reference/repository-connections.md#repository-connection), [a factory that a repository holds](reference/repository-connections.md#held-factory), [a static member of the project](reference/repository-connections.md#static-connection)

## SQLSRC033

**Connection is of another database engine**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

The member that gives a database its connection is of a type of the other engine's driver.  The error is at the member of the type, at the holder for a member of a factory that the type holds, and at the type's `[SqlSourceGenerate]` for a `static` member of the project.  The type of the member is one of these:

- an Npgsql type, such as `NpgsqlDataSource`, for a SQL Server database;
- a Microsoft.Data.SqlClient type, such as `SqlConnection`, for a PostgreSQL database.

The repository queries of that database get no method.

A member of a factory, or a `static` member of the project, that is marked without a `Database` does not get this error.  A database of the other engine leaves it out.  Only a mark that names the database gets the error.

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(NpgsqlDataSource dataSource);
```

```console
/work/App/OrderRepository.cs(2,55): error SQLSRC033: 'dataSource' of 'App.OrderRepository' is a 'Npgsql.NpgsqlDataSource', of another engine's driver, and the database 'mssql' is SQL Server.  Give the database a connection of Microsoft.Data.SqlClient.
```

**Fix:**

1. Give the database a member of its own engine's driver, or of `DbDataSource` or `DbConnection`.
2. If the member is a connection, mark it `[SqlSourceConnection]`.
3. If the type uses several databases, mark the member for its database, as `[SqlSourceConnection(Database = "mssql")]`.
4. If the member belongs to a factory or is a `static` member of the project, correct the `Database` of its mark.

See: [The connection](reference/repository-connections.md#repository-connection), [which database a shared mark is for](reference/repository-connections.md#shared-marks)

## SQLSRC034

**Repository is static**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

The type is `static`.  The error is at the type's `[SqlSourceGenerate]`, once for the type.

A query whose output is `repository` gets an instance method, and the type has no instance.  So none of the repository queries of the type gets a method.

```csharp
[SqlSourceGenerate]
public static partial class UserQueries;
```

```console
/work/App/UserQueries.cs(1,2): error SQLSRC034: 'App.UserQueries' is static, and the method of a repository query is an instance method.  Set the output of its repository queries to methods, or remove static.
```

**Fix:** do one of these.

- Set the [output](reference/settings.md#setting-output) of the repository queries to `methods`.  Their methods are static or extension methods that take the connection.
- Remove `static`, and give the type a connection, as [SQLSRC028](#sqlsrc028) says.

See: [Repository methods](reference/repositories.md#repository-methods)

## SQLSRC035

**Repository interface name is taken**

Reported by: the build.

A type that claims a query whose output is `repository` gets an interface of its public repository methods.  Something beside the type already has the name of that interface.  The error is at the type's `[SqlSourceGenerate]`, and the type gets no interface.

The interface is named `I` and the type's name, unless `InterfaceName` names it.  It stands beside the type: in its namespace, or in the type it is nested in.  What has the name is one of these:

- a type of the project, other than an interface whose every declaration is `partial`, or a type of a referenced assembly;
- a type parameter of the type, or of a type it is nested in;
- for a nested type, the type it is nested in, or a member of it;
- something else that SqlSource writes beside it.  That is a model, the extension class of a type's `methods` queries, a namespace that holds models, or the interface of another type.  When it is the interface of another type, each of the two types gets the error.

```csharp
public class IUserRepository { }

[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

```console
/work/App/UserRepository.cs(3,2): error SQLSRC035: 'App.UserRepository' cannot get its interface 'IUserRepository', because the class App.IUserRepository already has that name.  Set InterfaceName on its [SqlSourceGenerate] to another name, or set GenerateInterface to false.
```

**Fix:** do one of these.

- Name the interface otherwise, with `[SqlSourceGenerate(InterfaceName = "IUserStore")]`.
- Rename what has the name.
- Turn the interface off with `GenerateInterface = false`.

To add members to the interface yourself, declare it `partial interface IUserRepository`.  The generated part joins it.

See: [The interface](reference/repositories.md#repository-interface), [repository interfaces](reference/repositories.md#repository-interfaces)

## SQLSRC036

**Connection parameter is hidden by a method argument**

Reported by: the build.

A type gets this error only when it claims a query whose output is `repository`.  The queries that get no method still get their models.

The primary-constructor parameter that gives a database its connection, or that holds the factory it comes from, has the name of an argument of the repository methods.  The error is at the parameter.  The name is one of these:

- `cancellationToken`, which every asynchronous repository method takes as an argument.  This name is refused whatever the `method-type` of the queries.
- `parameters`, which a method takes as its input model when its query has parameters.  A parameter named `parameters` is an error only when a repository query of that database has parameters.

Inside a method the argument hides the parameter, so the method could not reach its connection.  The repository queries of that database get no method.

```csharp
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource parameters);
```

```console
/work/App/UserRepository.cs(2,54): error SQLSRC036: 'parameters' of 'App.UserRepository' is the primary-constructor parameter that gives the database 'postgres' its connection, and the repository methods have an argument named 'parameters', which hides it.  Rename the parameter, or store it in a field or property and give the connection from that.
```

**Fix:** do one of these.

- Rename the parameter.
- Store the parameter in a field or property of the type, which is then the connection or holds the factory.  Mark that member `[SqlSourceConnection]` when it is a connection that is not found by its name or type.

See: [The connection](reference/repository-connections.md#repository-connection), [a factory that a repository holds](reference/repository-connections.md#held-factory)

## SQLSRC037

**Repository interface name is not valid**

Reported by: the build.

A type that claims a query whose output is `repository` gets an interface of its public repository methods.  Its `[SqlSourceGenerate]` names the interface with `InterfaceName`, but the name is not a C# identifier.  The error is at the type's `[SqlSourceGenerate]`.

The name holds a character that an identifier cannot hold, such as `-`, a namespace, type parameters, or an `@`.  A keyword such as `"class"`, or a contextual keyword such as `"file"`, is an identifier, and is written `@class` or `@file`.

The type gets no interface, and its repository methods are still generated.

```csharp
[SqlSourceGenerate(InterfaceName = "I-Users")]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

```console
/work/App/UserRepository.cs(1,2): error SQLSRC037: 'App.UserRepository' cannot get its interface 'I-Users', because the name is not a C# identifier.  Set InterfaceName on its [SqlSourceGenerate] to an identifier, without an @ or a namespace, or remove it.
```

**Fix:** do one of these.

- Write the name of the interface alone, such as `InterfaceName = "IUserStore"`.  The interface is declared beside the type, with the type parameters of the type.
- Remove `InterfaceName` to get `I` and the name of the type.

See: [The interface](reference/repositories.md#repository-interface), [repository interfaces](reference/repositories.md#repository-interfaces)

## SQLSRC038

**Exceptions come from a framework before .NET 6**

Reported by: the build.

A project on .NET 6 or later uses SqlSource.  It takes its exceptions from a project that it references, such as the [shared project](guides/several-projects.md#share-the-exceptions) of the recipe.  That project targets a framework before .NET 6, such as `netstandard2.0` or `netstandard2.1`.  The message names the project.  The error has no file and line, because it is about the project's references.

The `CollectionItemException` of that project was built without the `SqlState`, `IsTransient` and `BatchCommand` that .NET 6 added to `DbException`.  So it cannot pass on those of the database's error: a retry policy that tests `IsTransient` would never retry a deadlock.  This project still uses the exceptions of that project, so the error is the only one.

```console
CSC : error SQLSRC038: The CollectionItemException that this project sees in Data.Shared was compiled for a framework before .NET 6, so it does not pass on the SqlState, IsTransient and BatchCommand of the database's error.  Make Data.Shared target .NET 6 or later, or add such a target to its TargetFrameworks.
```

**Fix:** do one of these.

- Make the project that holds the exceptions target .NET 6 or later, as `<TargetFramework>net8.0</TargetFramework>`.
- Add such a target beside the one that it has, as `<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`.  A project on .NET 6 or later then gets the build for .NET 8.

See: [Projects that share internals](reference/exceptions.md#projects-that-share-internals)

## SQLSRC101

**Quote is not closed**

Reported by: the build and the tool.

A string or a quoted identifier starts and never ends.  The error is at the opening quote or bracket.

```sql error
SELECT 'unfinished FROM users;
```

**Fix:** do one of these.

- Close the quote.
- If the SQL is valid for your database, SqlSource is reading it by the rules of another database.  Set the dialect of the file.  For example, `'it\'s'` is one string in MySQL, and a string that is not closed elsewhere.

A few constructs are read differently from the database, whatever the dialect.  See [what is still read differently](reference/dialects.md#still-read-differently).

See: [What each dialect changes](reference/dialects.md#what-a-dialect-changes)

## SQLSRC102

**Comment is not closed**

Reported by: the build and the tool.

A block comment or a hint starts with `/*` and never ends.  The error is at the `/*`.

```sql error
/* outer /* inner */
SELECT 1;
```

**Fix:** do one of these.

- Close the comment.
- If the SQL is valid for your database, set the dialect of the file.

Whether a `/*` inside a comment needs its own `*/` depends on the dialect.  It does in the default dialect, in SQL Server, in PostgreSQL and in CockroachDB.  It does not in MySQL, MariaDB, SQLite and Oracle.

- **SQLite accepts a comment that is still open at the end of the file.**  SqlSource does not, in any dialect, because the comment would take every later query of the file with it.

See: [What each dialect changes](reference/dialects.md#what-a-dialect-changes)

## SQLSRC103

**Query name is not valid**

Reported by: the build and the tool.

The name of the query cannot be the name of a C# member.  It is not a C# identifier, or it is a reserved keyword such as `class`.

The value of a `-- name:` marker becomes the name of a C# member.

```sql error
-- name: get-user
SELECT 1;
```

**Fix:**

1. Rename the query, for example to `GetUser`.

- **The name ends at `->`, when the marker has one.**

See: [Queries and names](reference/sql-files.md#queries-and-names)

## SQLSRC104

**Query name is used twice**

Reported by: the build and the tool.

Two `-- name:` markers in one file have the same name.  The error is at the second marker.

**Fix:**

1. Rename one of the two queries.

See: [Queries and names](reference/sql-files.md#queries-and-names)

## SQLSRC105

**File name is not a valid query name**

Reported by: the build and the tool.

A file without a `-- name:` marker is one query, named after the file without its extension.  The name of this file cannot be the name of a C# member.

```text
001_init.sql
get-user.sql
```

**Fix:** do one of these.

- Add a `-- name:` marker at the top of the file.
- Rename the file.
- If the file is not meant to be a query of this type, move it out of the type's folder.
- If the file is not meant to be a query of this type, point the type's `Source` elsewhere.

See: [A file with no `-- name:`](reference/sql-files.md#a-file-with-no-name)

## SQLSRC106

**SQL before the first name**

Reported by: the build and the tool.

SQL stands before the first `-- name:` marker of the file.  There it would belong to no query.

In a file that has `-- name:` markers, only comments and the markers that describe the whole file can come before the first one.  The table of [SQLSRC116](#sqlsrc116) lists those markers.

```sql error
SET search_path TO app;

-- name: GetUser
SELECT 1;
```

**Fix:** do one of these.

- Move the SQL below a `-- name:` marker.
- Delete the SQL.

See: [The file header](reference/sql-files.md#the-file-header)

## SQLSRC107

**Summary before the first name**

Reported by: the build and the tool.

A `-- summary:` marker stands in the file header, before the first `-- name:` line.  There it is in no query.

A summary describes the query that it is in.

```sql error
-- summary: Loads one user.
-- name: GetUser
SELECT 1;
```

**Fix:**

1. Move the summary below the `-- name:` marker.

See: [Summaries](reference/sql-files.md#summaries)

## SQLSRC108

**Marker has no SQL after it**

Reported by: the build and the tool.

A marker stands after the last SQL of its query.

A marker comes before the SQL that it describes.  In this place, a reader would take the marker to belong to the next query.  That usually means that it was written above the next `-- name:` marker, and was meant for that query.

```sql error
-- name: GetUser
SELECT 1;
-- summary: Lists every user.
-- name: ListUsers
SELECT 2;
```

**Fix:** do one of these.

- Move the marker below the `-- name:` marker of the query that it describes.
- Delete the marker.

See: [Markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place)

## SQLSRC109

**Generator parameter is not known**

Reported by: the build and the tool.

A `-- generator:` marker holds a word whose name is not a generator parameter.  The name is what stands before an `=`, so `keep-coments=off` is this error too.

```sql error
-- generator: keep-comment
```

**Fix:**

1. Correct the name of the generator parameter.

A word is one of these:

- A generator parameter, alone or followed by `=on`, `=off` or `=default`.  The generator parameters are `keep-comments`, `token-validation`, `sort-input`, `sort-output`, `table-models`, `async-method-suffix`, `array-pool`, `list-padding`, `collection-method`, `transaction` and `method-type`.
- `default` alone.

`transaction` also takes `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` and `snapshot`.  `method-type` takes only `=async`, `=sync`, `=both` and `=default`.

- **The words `no-token-validation`, `no-table-models` and `no-array-pool` of earlier previews are not generator parameters.**  Write `token-validation=off`, `table-models=off` and `array-pool=off`.
- **If you used `token-ignore` as a generator parameter in an earlier preview, write it as a marker inside the query.**  The marker is `-- token-ignore: name`.
- **A dialect is not a generator parameter.**  It has a marker of its own, `-- dialect: name`.

See: [Generator parameters](reference/generator-parameters.md#setting-generator-parameters)

## SQLSRC110

**Generator parameter is missing**

Reported by: the build and the tool.

A `-- generator:` marker has nothing after the colon.

**Fix:** do one of these.

- Add a generator parameter.
- Delete the line.

See: [Generator parameters](reference/generator-parameters.md#setting-generator-parameters)

## SQLSRC111

**Marker value is not valid**

Reported by: the build and the tool.

The value of a marker is not valid.  The cause is one of these:

- A generator parameter has a value that it does not take.
- What follows a name in a `-- name:` marker is not a shape.
- A `-- dialect:` marker does not name a dialect.
- A marker that holds a setting has a value that the setting does not take.  Examples are `-- output:`, `-- database:`, `-- input-model-type:`, `-- collection-type:`, `-- type-override:` and `-- library-types:`.
- A `-- token:`, `-- token-ignore:`, `-- param:` or `-- column:` marker does not hold what it needs.

The values that each marker takes follow.

- **The value of a generator parameter is one of its own words, or `default`.**  The message lists them.  See [generator parameters](reference/generator-parameters.md#setting-generator-parameters).
  - Most generator parameters take `on` or `off`.
  - `transaction` also takes `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot`.
  - `method-type` takes `async`, `sync` or `both`, and takes no `on` or `off`.
  - `keep-comments=true` is this error.  So is `method-type` alone, which is `method-type=on`, and `default` followed by `=`, as in `default=on`.
- **What follows the name in a `-- name:` marker must be `->` and a shape.**  The shape is one of `many`, `one`, `one-optional`, `none` and `rowcount`, as in `-- name: GetUser -> one`.
- **`-- dialect:` needs the name of a dialect, as in `-- dialect: postgres`.**
  - Any options follow the name, as in `-- dialect: mysql, ansi-quotes`.
  - The names and the options are those of `dialect` in a settings file, listed in [SQLSRC019](#sqlsrc019).
  - The value is the rest of the line, so nothing else can follow it.

Each line of this sample is this error:

```sql error
-- generator: keep-comments=true
-- dialect: pgsql
-- dialect: mysql keep-comments
```

The other markers that hold a setting take these values.  `-- output:`, `-- input-model-type:`, `-- output-model-type:`, `-- collection-type:`, `-- member-location:` and `-- visibility:` match their values in any case, and with or without hyphens and spaces.

- `-- output:` takes `repository`, `methods`, `models` or `sql`.
- `-- database:` takes one word of letters, digits, `-`, `_` and `.`.
- `-- input-model-type:` and `-- output-model-type:` take `record`, `sealed record`, `class` or `sealed class`.
- `-- collection-type:` takes `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList` or `IAsyncEnumerable`.
- `-- input-model-suffix:` and `-- output-model-suffix:` take characters that can be part of an identifier.
- **`-- model-namespace:` takes a namespace such as `App.Models`.**  None of its parts can be a reserved keyword or `file`, `required`, `scoped` or `extension`.  C# does not take these as the name of a type.
- **`-- input-model:` and `-- output-model:` take the name of a type, or its full name with its namespace.**  Examples are `UserRow` and `App.Models.UserRow`.  No part of the name can be a reserved keyword or `file`, `required`, `scoped` or `extension`.  The marker cannot name a generic type.
- **`-- nested-class:` takes one identifier.**  It cannot be one of those words, and it cannot be `var`, with which the query methods of the type would not compile.  So `Queries` is valid, and `class`, `file` or `App.Sql` is not.
- `-- member-location:` takes `extension-class`, `class` or `nested-class`.
- `-- visibility:` takes `public`, `internal`, `protected` or `private`.
- **`-- type-override:` takes one entry, `key = type` or `key = default`.**  See [type overrides](reference/type-mapping.md#setting-type-overrides).
  - The key is the one-word name of a type: `timestamptz` and not `timestamp with time zone`.
  - A type outside `pg_catalog`, or a SQL Server type outside `sys`, is written with its schema: `public.user_status` or `dbo.Email`.
  - A schema or a name can be in double quotes, with a quote inside it doubled: `public."Status"`, `"my schema"."order-status"`.
  - A schema or a name can also be in `U&"..."` with escapes: `public.U&"Order\000AItem"`.  An escape is `\\`, `\` and four hex digits, or `\+` and six, for a code from 1 to 10FFFF.  Any other escape is not valid.  A character beyond FFFF is one `\+` escape, or the escapes of its two surrogates.
  - The type is a keyword, a full name or a short name, `System.DateTimeOffset` or `DateTimeOffset`, with no `?` at the end.
- **The dialect of the file decides how the key of a `-- type-override:` marker is read.**
  - In an `mssql` file, the key is the name of a SQL Server type, such as `int`, `nvarchar` or `sys.int`.  A name that SQL Server does not have, such as `int4` or `pg_catalog.text`, is this error.
  - In a file of any other dialect, a name that SQL Server alone has, such as `nvarchar` or `rowversion`, is this error.
  - A type of a schema of your own, `dbo.Email`, is read in either.
- **A key is not written in brackets.**  `-- type-override: [dbo].[Email] = MyApp.Email` is this error.  The message shows the forms that SqlSource reads:
  - `dbo.Email`, which matches the name of the type in any case.
  - `"dbo"."Email"`, which matches its case exactly.
  - For a key without a schema, the plain form alone: `[INT]` is `INT`.  A quoted name without a schema must be in lower case.
  - For a part that holds anything but letters, digits, `_` and `$`, such as `[my schema]`, the quoted form alone: `"my schema"`.
- **`-- library-types:` takes one entry or more, separated by white space, as in `-- library-types: nodatime=off`.**  See [library types](reference/library-types-and-json.md#setting-library-types).
  - Each entry is `name=state`, with no space around its `=`.
  - The library is `nodaTime`, `netTopologySuite` or `json`, and the state is `on`, `off` or `default`.  Both are matched ignoring case, hyphens and spaces.
  - A line with no entry is this error, and so is an entry without `=`.  So are `nodatime = on` with spaces, and a library or a state that is not one.  The message lists the libraries and the states.

A `-- token-ignore:` marker holds one name, a C# identifier, and nothing else.

A `-- token:` marker holds exactly one token with a default, `{{name:default}}`, and nothing else.  A quote or a block comment inside the default must close there.

A `-- param:` marker starts with the query parameter as the SQL writes it, `@name`.  The parameter stands alone, or a space or `->` follows it.  What follows its `->` is a C# type, written as the type of a type override is.  It is never `default`, since nothing below a query maps a parameter: `-- param: @author -> default` is this error.

Inside a query, a `-- column:` marker holds the name of a column, then one of these, or both in this order:

- `null` or `not null`;
- `->` and a C# type or `default`.

Examples are `-- column: total not null` and `-- column: note null -> NoteText`.  A name with white space or a `.` is written in double quotes, and so is a name that is the word `null` or `not`.  A quote inside such a name is doubled: `-- column: "total due" not null`.  A name with a `.` outside quotes, `-- column: users.id -> UserId`, is this error.  Its message says that a column's source is mapped in the file header.

In the file header, a `-- column:` marker holds the source of a column, `table.column` or `schema.table.column`, then `->` and a C# type or `default`.  An example is `-- column: users.id -> UserId`.  A marker without `->` is this error.  So are a marker that says `null` or `not null`, and one whose key has one part.

```sql error
-- column: users.id not null
-- column: id -> UserId

-- name: ListUsers
-- column: users.id -> UserId
-- param: @team -> default
SELECT id FROM users WHERE team_id = @team;
```

**Fix:** do one of these.

- Add the missing value.
- Correct the value that is wrong.
- Remove the value that does not belong.

See: [Marker index](reference/sql-files.md#marker-index)

## SQLSRC112

**Settings conflict**

Reported by: the build and the tool.

Two settings contradict each other.  The error is at the second.

The conflict is one of these:

- **Two words of `-- generator:` markers give one generator parameter two different states.**  An example is `keep-comments` and `keep-comments=off`.
  - The two words can be in the file header or inside one query, on one line or two.
  - `keep-comments=default` beside either is a conflict too, even where the default is the same.
  - The later word is not applied.
  - In a file with no `-- name:` line, every `-- generator:` line is in one place.
- **Two `-- dialect:` markers name different dialects, or one dialect with different options.**  A file has one dialect.
- **Two defaults for one token of a query differ.**  They can be two `{{name:default}}` in its SQL, two `-- token:` markers, or one of each.
- **Two `-- column:` markers inside one query give one column two nullabilities, or two C# types.**  The name of the column is compared ignoring case.  One marker says `null` and the other `not null`, or the two give two C# types, `default` among them.
- **Two `-- column:` markers in the file header give one key two C# types.**  The key is compared as [column types](reference/model-properties.md#setting-column-types) compares it.  So `users.id` and `"users".id` are one key, and `users.id` and `public.users.id` are two.
- **Two `-- param:` markers for one parameter give different types, different nullability or different C# types after `->`.**  `not null` counts as different from saying nothing: `-- param: @a int` and `-- param: @a int not null` conflict.  Write both the same.
- **Two `-- output:` markers give different values, or two `-- database:` markers name different databases.**  The two can be in the file header or inside one query.  `billing` and `Billing` are different databases.
- **Two markers of any other setting of the models and the collection type give different values.**  The two can be in the file header or inside one query.  Examples are two `-- input-model:` markers that name different types, two `-- model-namespace:` markers, and so on.
- **Two `-- member-location:`, `-- visibility:` or `-- nested-class:` markers give different values.**  The two can be in the file header or inside one query.
- **Two `-- type-override:` markers give one database type different C# types.**  The two can be in the file header or inside one query.
  - The key is compared as [type overrides](reference/type-mapping.md#setting-type-overrides) compares it.  So `integer` and `int4` are one type, `public.status` and `public."status"` are one type, and `public.Status` and `public.status` are two.
  - The C# types are compared by how they are named.  So `int` and `System.Int32` are two, and so are `UserId` and `MyApp.UserId`.
  - The same holds for the C# types of `-- column:` and `-- param:` markers.
- **Two entries of `-- library-types:` markers give one library two states.**  An example is `nodatime=on` and `nodatime=off`.  The two entries can be in the file header or inside one query, on one line or two.

In this sample, the second `-- output:` marker conflicts with the first:

```sql error
-- name: ListUsers
-- output: sql
-- output: models
SELECT id, name FROM users;
```

**Fix:**

1. Remove one of the two settings.

These are not a conflict:

- A query's `-- generator:` word for a generator parameter.  It replaces the file header's word for that parameter.
- A query's `-- type-override:` for a type.  It replaces the file header's for that type.
- A query's `-- library-types:` for a library.  It replaces the file header's for that library.
- A query's `-- column:` for a column whose source the file header maps.
- Two `-- column:` markers inside one query for one column, of which one gives the nullability and the other the C# type.
- The same dialect given twice with the same options.
- `default` beside other words of `-- generator:`.

See: [A value that is wrong](reference/settings.md#a-value-that-is-wrong)

## SQLSRC113

**Query has no SQL**

Reported by: the build and the tool.

A query, or a whole file without `-- name:` markers, holds nothing but whitespace, comments and markers.  A file that cannot be read is reported the same way, at its first line.

```sql error
-- name: GetUser
-- TODO: write this query
```

**Fix:** do one of these.

- Write the SQL.
- Delete the query.

See: [Queries and names](reference/sql-files.md#queries-and-names)

## SQLSRC114

**Token name is a keyword**

Reported by: the build and the tool.

The name of a token is a reserved C# keyword.

A token `{{name}}` becomes a parameter of a generated method.  So its name must not be a reserved C# keyword.

```sql error
SELECT * FROM {{class}};
```

The same holds for the name in a `-- token:` marker.  It also holds for a token in a comment of a query that keeps its comments.

A token in a comment is an error only for a type that keeps the comments.  The file that holds the token then produces no members and no models for that type.

**Fix:** do one of these.

- Rename the token.
- If the braces are literal text and not a token, add `-- token-ignore: class` to the query.

See: [Tokens](reference/tokens.md#tokens)

## SQLSRC115

**Dialect marker is misplaced**

Reported by: the build and the tool.

A `-- dialect:` marker stands inside a named query, or after SQL.

A `-- dialect:` marker sets the dialect of a whole file, and it changes how the text after it is read.  So it must stand in the file header, before the first `-- name:` line of the file and before its first SQL.

```sql error
-- name: GetUser
-- dialect: mysql
SELECT 1;
```

**Fix:**

1. Move the marker to the top of the file.

In a file with no `-- name:` marker, which is one query, the top of the file is above the SQL of the query.

Comments can come before the marker, such as a licence header.  In this sample, the marker stands at the top of the file:

```sql
-- dialect: mysql

-- name: GetUser
SELECT 1;
```

- **A `-- dialect:` marker after the last SQL of its query gets two errors.**  They are this error and [SQLSRC108](#sqlsrc108).
- **A file cannot mix dialects.**  Put the queries for another database in a file of their own.

See: [Markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place)

## SQLSRC116

**Marker is not allowed here**

Reported by: the build and the tool.

A marker stands in the wrong part of its file.  The message says which of the two kinds this marker is.

Some markers describe one query.  They go inside the query, after its `-- name:` line.  Other markers describe the whole file.  They go before the first `-- name:` line.

```sql error
-- token: {{filter:AND deleted_at IS NULL}}

-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter}};
```

**Fix:**

1. Move the marker to the place that the message names.

A file with no `-- name:` line is one query, and takes every marker.

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
| `-- member-location:` | yes | yes |
| `-- visibility:` | yes | yes |
| `-- nested-class:` | yes | yes |
| `-- type-override:` | yes | yes |
| `-- column:` | yes | yes |
| `-- library-types:` | yes | yes |

The table is this error's, with two exceptions:

- A `-- summary:` in the file header is [SQLSRC107](#sqlsrc107).
- A `-- dialect:` inside a query or after SQL is [SQLSRC115](#sqlsrc115).

See: [Markers in the wrong place](reference/sql-files.md#markers-in-the-wrong-place)

## SQLSRC117

**Parameter has no type**

Reported by: the build and the tool.

A `-- param:` marker without a type names a parameter that does not stand as a parameter in the SQL of the query.  One of these is true:

- The parameter appears only in the default of a token.
- Under `mssql`, the parameter appears only where SqlSource reads it as a variable declared with `DECLARE`.
- The parameter appears nowhere, as when its name is misspelt.

Nothing but the marker can then say its type.

The first sample shows the first case, and the second sample the second case:

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

A marker that gives only a C# type, `-- param: @page -> PageNumber`, has no type either.  The C# type is not the database's.

**Fix:** do one of these.

- Give the type, in the database's own words: `-- param: @page int`, or `-- param: @page int -> PageNumber`.
- Under `mssql`, if the name is a variable that the query declares, remove the marker.
- Under `mssql`, if the name is a parameter that SqlSource took for a variable, give the type.  See [variables declared with `DECLARE`](reference/parameters.md#declared-variables).
- If the name is misspelt, correct it.

See: [Giving a parameter a type: `-- param:`](reference/parameters.md#param-marker)

## SQLSRC118

**Parameter is not declared**

Reported by: the build and the tool.

A parameter appears in the default of a token, in the SQL or in a `-- token:` marker, and nowhere else in the query.

A default is a sample, and a type taken from a sample alone would be a guess.

```sql error
-- name: ListUsers
SELECT id FROM users {{paging:LIMIT 20 OFFSET @page}};
```

**Fix:**

1. Declare the parameter with its type, as in `-- param: @page int`.

See: [Defaults](reference/tokens.md#token-defaults)

## SQLSRC119

**Query has no parameters**

Reported by: the build and the tool.

An `-- input-model:` marker names the type of the parameters of a query, and this query has none.  It has no `@name` in its SQL and no `-- param:` marker.

Under the `mssql` dialect, a name that the query declares with `DECLARE` is a local variable, and is not a parameter.

```sql error
-- name: CountUsers
-- input-model: CountArgs
SELECT COUNT(*) FROM users;
```

**Fix:**

1. Remove the marker.

See: [Name of one query's model](reference/models.md#setting-model-name)

## SQLSRC120

**Token crosses a comment**

Reported by: the build and the tool.

A token and a comment overlap.  The comment is not wholly inside the default of the token, nor outside the token.

Comments are removed before tokens are found.  A query that keeps its comments (the `keep-comments` [generator parameter](reference/generator-parameters.md#setting-generator-parameters)) also has its tokens found in the SQL with the comments.  So the two would read different tokens.

It happens in three ways.

**A comment inside a default holds `}}`.**  Without the comment those braces are gone, so the default runs on to the next `}}`, and a token written before that `}}` is lost.  A `--` comment runs to the end of its line, so the `}}` written after one on that line is part of the comment.  The comment is an error wherever the file is used.

```sql error
-- name: ListUsers
SELECT id FROM users WHERE 1 = 1 {{filter:AND active -- only live rows}}
ORDER BY {{orderBy:name}};
```

**A comment opens a default, `{{name:`, and the SQL after the comment closes it.**  Without comments, the query is as you wrote it.  Where comments are kept, the default runs from the comment to the next `}}`, and the token after it is lost.  A token written whole inside a comment, such as `-- see {{note:x}}`, is fine.

```sql error
-- name: ListUsers
-- generator: keep-comments
SELECT id -- e.g. {{note: here
FROM users WHERE {{filter}};
```

**A comment stands inside the braces of a token, before its default.**  It stands between `{{` and the name, or between the name and the `:` or the closing `}}`.  Without comments, the token is read.  Where comments are kept, the braces stay in the SQL as written, and the method has no argument for the token.

```sql error
-- name: ListFrom
-- generator: keep-comments
SELECT id FROM {{table/* users or archive */}};
```

The second and third are errors only for a type that keeps the comments of the query.  That type gets no members and no models from the file until it is fixed.

All three hold for a name in a `-- token-ignore:` marker too.  Its text runs to its closing `}}`, as the text of a token does.  So a comment there changes which tokens the rest of the query has.

**Fix:**

1. Keep the comment wholly inside the default of the token, or outside the token.  Move it onto a line of its own, or after the `}}` of the token.

See: [Defaults](reference/tokens.md#token-defaults)

## SQLSRC121

**Parameter is written in two spellings**

Reported by: the build.

A query in the `mssql` dialect writes one parameter in two spellings that differ only in case: `@Id`, and then `@id`.  The warning is at the place of the other spelling, and names both.  The build warns of each further spelling of a parameter once in a query, at its first place.

SqlSource reads the two spellings as one parameter.  It binds the parameter under the spelling that the SQL first writes outside a token's default.  A spelling in a default or in a `-- param:` marker does not choose it.

The collation that counts is the server's, which `SELECT SERVERPROPERTY('Collation')` shows:

- If the server collation of a SQL Server is case-sensitive, the server reads the other spelling as another variable.  The query does not declare that variable, so the server fails the query when it runs.
- If the server collation ignores case, which is the usual one, the server runs the query as written.
- The collation of the database does not change how the variables of the query are read, except in a contained database.  In a contained database, the variables ignore case, whatever the collation of the server.

```sql error
-- name: GetUser
SELECT name FROM dbo.users WHERE id = @Id OR parent_id = @id;
```

```console
/work/App/Queries/Users.sql(2,58): warning SQLSRC121: The parameter '@Id' is also written '@id'.  SqlSource binds it as '@Id', and a SQL Server whose server collation is case-sensitive reads '@id' as another variable, which the query does not declare.  Write it '@Id' throughout the query.
```

**Fix:**

1. Write the parameter in one spelling throughout the query.

The build checks a parameter written in the default of a token as it checks the rest of the query.

- **A parameter that only a token's default writes, and that a `-- param:` marker declares, is bound as the marker spells it.**  The spellings in the defaults are compared against that spelling.

This is a warning, since a query runs as written when the server collation ignores case.  So a project that uses only such servers can do without it.  A project can list it in `NoWarn` to turn it off.  A project that sets `TreatWarningsAsErrors` can list it in `WarningsNotAsErrors` to keep it a warning:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC121</NoWarn>
</PropertyGroup>
```

A `.globalconfig` file in the project's folder turns it off too:

```ini
is_global = true
dotnet_diagnostic.SQLSRC121.severity = none
```

See: [Parameters](reference/parameters.md#query-parameters)

## SQLSRC122

**Member location does not fit the query**

Reported by: the build.

The [member location](reference/member-placement.md#locations) of a query does not fit its output.  The value was written on the query, in the file's header or in a settings file.  The message names the query, the type that claims the file and where the value was written.  If two types claim one file, each type reports the error.

The member location does not fit in these cases:

- **The output is `models` or `sql`, and the member location is `extension-class`.**  The query has a constant or a token method.  That member cannot go in the extension class, since only a query method extends the connection.
- **The output is `repository`, and the member location is `extension-class` or `nested-class`.**  The query has an instance method of the type, which cannot go in the extension class or in a nested class.  Its default member location, `class`, fits.

A `memberLocation` in a header or a settings file reaches every query below it, whatever its output.  So a folder-wide `"memberLocation": "ExtensionClass"` is this error at each query of the folder whose output is `repository`, `models` or `sql`.

The error is at the `-- member-location:` marker of the query.  If the query has no such marker, the error is at its name.  Until the error is fixed, the file produces no members and no models for the type.

A file header that sets `extension-class` for a query whose output is `sql`:

```sql
-- member-location: extension-class

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;
```

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC122: The query 'CountUsers' of 'App.UserRepository' cannot go in the extension class, which the file header sets, because its output is sql.  Only a query method extends the connection.  Use class or nested-class.  Write -- visibility: or -- member-location: on the query.
```

For a query whose output is `repository`, the message names the location that does not fit, `extension class` or `nested class`.  It ends with "A repository method is an instance method of the type.  Use class."

**Fix:** do one of these.

- Write `-- member-location: class` or `-- member-location: nested-class` on the query.  A `repository` query takes only `class`.
- Keep `extension-class` to the files and folders whose queries are all `methods`.

See: [Where the members go](reference/member-placement.md#locations)

## SQLSRC123

**Visibility does not fit the member location**

Reported by: the build.

The visibility of a query and its [member location](reference/member-placement.md#locations) together are something C# cannot write.  The message names these:

- the query, and the type that claims the file;
- the visibility, and where it was written, or that it is the default of the `repository` output;
- the member location;
- the reason.

The reason is one of these four:

- **The member location is `extension-class`, and the visibility is `private` or `protected`.**  The extension class is a class of the namespace, and its methods are public or internal.
- **The member location is `extension-class`, the visibility is `public`, and the type, or a type around it, is not public.**  This is an error only when the method of the query takes or returns a model, as a method with parameters or rows does.  The models of the type are then internal, and a public method cannot take them.
  - A query method with no parameters and no rows takes no models, and fits.
  - A method whose models the type shares with a public type fits too, since that makes the models public.
- **The visibility is `protected`, on a class that cannot have protected members.**  That is the member location `class` on a static class, or `class` or `nested-class` on a struct or a record struct.
- **The output is `repository` or `methods`, and the method takes or returns a model that is internal, on a type reachable as protected.**  All of these are true:
  - The member location is `class` or `nested-class`, and the visibility is `public` or `protected`.
  - The type, or a type around it, is `protected` or `protected internal`.
  - None of those types is `internal`, `private` or `private protected`.
  - The method of the query takes or returns a model that is internal.

  A class derived from the type in another assembly could call the method, and such a method cannot take or return an internal model.  These fit: an `internal` or `private` method or nested class, a constant or token method of any visibility, and a query method with no parameters and no rows, which takes no models.

The error is at the `-- visibility:` marker of the query.  If the query has none, the error is at its `-- member-location:` marker, and else at its name.  What the error costs depends on the reason:

- **The first and third reasons cost the whole file.**  Until the error is fixed, the file produces no members and no models for the type.
- **The second and fourth reasons are about the models of the method, so they leave out that method alone.**  The other members of the file, and the models of the query, are still generated.  The error comes once the query has its entry, which is where its models come from.

**A `visibility` in a header or a settings file reaches every query below it**, whatever its member location.  Here a file has queries of the outputs `methods` and `sql`, and its header says `-- visibility: private`.  The constants go in a private nested class.  The method of each query at `methods` goes in the extension class, so each such query gets this error.  A repository method, on the type, could be private.

```sql
-- visibility: private
-- output: methods

-- name: GetUserName
-- output: sql
SELECT name FROM users WHERE id = @id;

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

```console
/work/App/Queries/Users.sql(8,10): error SQLSRC123: The query 'GetUser' of 'App.UserRepository' cannot be private, which the file header sets, at the member location extension-class, because an extension method is public or internal.  Write -- visibility: or -- member-location: on the query.
```

**A query method that writes no visibility takes the visibility of its nested class.**  Another query of the type, in any of its files, can write that visibility.  If that visibility does not fit the models that the method takes or returns:

- The error is at the query of the method: at its `-- member-location:` marker, else at its name.
- The message names the query that wrote the visibility.
- Only that method is left out.  Both files keep their other members and their models.

Here `Constants.sql` makes the class `Sql` of `App.Outer.Sample` public.  `App.Outer.Sample` is a `protected` class nested in a public one.  The constant of `Constants.sql` can be public, and the method of `GetUser` cannot:

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC123: The query 'GetUser' of 'App.Outer.Sample' cannot be public, which the file header for the query 'GetName' sets, at the member location nested-class, because 'App.Outer.Sample' is reachable as protected, so its models are internal, which a method reachable from another assembly cannot take or return.  Write -- visibility: or -- member-location: on the query.
```

**A repository method that writes no visibility is `public`**, the default of the `repository` output.  On a type reachable as protected, `public` does not fit the models of the method: the fourth reason.  The error is at the `-- member-location:` marker of the query, else at its name.  It leaves out that method alone.  Here `App.Outer.UserRepository` is a `protected` class nested in a public one:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC123: The query 'GetUser' of 'App.Outer.UserRepository' cannot be public, the default of the repository output, at the member location class, because 'App.Outer.UserRepository' is reachable as protected, so its models are internal, which a method reachable from another assembly cannot take or return.  Write -- visibility: on the query.
```

**Fix:** do the one of these that fits the case.

- **A visibility written for the query, as in the first example.**  Write `-- visibility:` on the query, as `-- visibility: internal` on `GetUser`.  Or write `-- member-location: class` or `-- member-location: nested-class` on it, where a member can be private.
- **A visibility that the method takes from its nested class, as in the second example.**  Give the method a class of its own with `-- nested-class:`.  Or put it on the type or in the extension class with `-- member-location:`.  Do not write `-- visibility: internal` on it: that gives the class two visibilities, [SQLSRC124](#sqlsrc124).
- **The default visibility of a repository method, as in the third example.**  Write `-- visibility: internal` on the query, or in the file header for every query of the file.

Where `protected` is allowed, and what the message of a repository query says:

- **`protected` is allowed on a sealed class or record, and in a nested class of a static class.**
- **The message of a repository query says to write `-- visibility:` alone.**  A repository method is always on the type.

See: [Where the methods go](reference/connection-methods.md#method-location)

## SQLSRC124

**Nested class has two visibilities**

Reported by: the build.

Queries of one type put their members in one nested class, and two of them write different visibilities for that class.  The queries can be in one file or in two.  Each visibility can be written on the query, in a file's header or in a settings file.  The message names the class, both visibilities, where each was written and the query of each.

A class has one accessibility.  If the visibility of a query is only the default, the query takes the visibility that another query writes for its class, with no error.  Two written visibilities that differ are this error.

```console
/work/App/Queries/Orders.sql(3,10): error SQLSRC124: The nested class 'Sql' of 'App.UserRepository' cannot be internal, which the file header sets for the query 'GetOrder', since the file header sets it public for the query 'GetUser'.  Write one visibility for the queries of the class, or write -- nested-class: or -- member-location: on the query.
```

The error is at the query that writes the second visibility: at its `-- visibility:` marker, else at its name.  Until it is fixed, every file of the type with a query in that class produces no members and no models for the type.  Neither visibility is more right than the other.

**Fix:** do one of these.

- Write one visibility for the class.
- Write the visibility for one query, and leave it to the others.
- Give the queries of the other visibility a class of their own with `-- nested-class:`.
- Put the queries of the other visibility on the type with `-- member-location: class`.

Two nested classes of one type, each of its own visibility, are fine.

See: [Name of the nested class](reference/member-placement.md#setting-nested-class)

## SQLSRC125

**Query's member on the type has a name that is taken**

Reported by: the build.

A query whose output is `models` or `sql` has its constant or token method on the type, since its [member location](reference/member-placement.md#locations) is `class`.  The name of that member is taken.  The message names the query, the type and what has the name, one of these:

- a member, a nested type or a type parameter that the type declares;
- a nested class of that name, in which another query of the type puts its members.

A type cannot have two members of one name.  A token method cannot overload a method of yours of the same name, even with other parameters.

```sql
-- name: Sql
-- output: sql
-- member-location: class
SELECT 1;

-- name: Get
-- output: sql
-- member-location: class
SELECT id, name FROM users WHERE id = {{id}};

-- name: GetUser
-- output: sql
SELECT id, name FROM users;
```

```csharp
[SqlSourceGenerate(Source = "Queries")]
public partial class UserRepository
{
    public string Get() => Sql.GetUser;
}
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC125: The member of the query 'Sql' cannot go on 'App.UserRepository', because a nested class of the type's queries has that name.  Rename the query, or write -- member-location: on it.
/work/App/Queries/Users.sql(6,10): error SQLSRC125: The member of the query 'Get' cannot go on 'App.UserRepository', because the type declares a member, a nested type or a type parameter of that name.  Rename the query, or write -- member-location: on it.
```

The error is at the name of the query.  Until it is fixed, its file produces no members and no models for the type.

**Fix:** do one of these.

- Rename the query.
- Write `-- member-location: nested-class` on the query.
- If the name is that of a nested class of the type's queries, give that class another name with `-- nested-class:`.

Two other errors are about the name of a query:

- **A query named like the type itself is [SQLSRC009](#sqlsrc009).**
- **A query method on the type that would take a name the type has is [SQLSRC274](#sqlsrc274).**

See: [Where the members go](reference/member-placement.md#locations)

## SQLSRC126

**List parameter is used outside IN**

Reported by: the build and the tool.

A query parameter written directly after `IN` or `NOT IN`, without parentheses, is a list of values: `category IN @categories`.  A list can stand only there, or before `IS NULL` or `IS NOT NULL`, as in `(@categories IS NULL OR category IN @categories)`.  Every other use of the list is this error, at that use.  The other uses are these:

- **As one value**, such as `parent_category = @categories`, or `IN (@categories)`, which SQL reads as a list of one value.  A parameter is a list in every spelling of its name, so `IN @Categories` makes `@categories` one too.
- **In the default of a token**, in the SQL or in a `-- token:` marker.  The text of a token is yours when the method runs, so the list cannot be expanded there.  A parameter written directly after `IN` in a default is this error too, even when it is no list elsewhere in the query.
- **With the JSON opt-in**, `-- param: @categories -> json Category`.  A list holds values, not JSON.  The error is at the value of the marker.

```sql error
-- name: ListProducts
SELECT id, name FROM products
WHERE category IN @categories OR parent_category = @categories;
```

```console
/work/App/Queries/Products.sql(3,52): error SQLSRC126: The parameter '@categories' is a list, written after IN, and is used here as one value.  A list can stand after IN or NOT IN, or before IS NULL or IS NOT NULL.
```

**Fix:** do the one of these that fits the use.

- **As one value:** write the list after `IN` at each place, `parent_category IN @categories`.  Or give the single value a query parameter of its own.
- **In the default of a token:** move the list out of the token.  Or give the default a query parameter that is no list.
- **With the JSON opt-in:** remove `json` from the marker: `-- param: @categories -> Category`.

See: [List parameters](reference/list-parameters.md#list-parameters)

## SQLSRC127

**Negated list stands where SQL Server refuses an empty list**

Reported by: the build.

A [list](reference/list-parameters.md#list-parameters) of a SQL Server query stands after a `NOT`, in a place where SQL Server refuses an empty list.  The output of the query is `repository` or `methods`.  The message names the list and where it stands.

The `NOT` can be `category NOT IN @categories` or `NOT (category IN @categories)`.  The places are these:

- inside an aggregate, such as `COUNT` or `SUM`;
- in a `GROUP BY`;
- in an `OUTPUT` clause.

SQL Server refuses the form in which the query method sends an empty list there.  So the method would fail whenever the list is empty or null.

```sql error
-- name: CountOtherProducts
SELECT COUNT(IIF(category NOT IN @categories, 1, NULL)) AS others
FROM products;
```

```console
/work/App/Queries/Products.sql(2,34): error SQLSRC127: The list '@categories' stands after a NOT inside an aggregate, where SQL Server refuses the form an empty list is sent in.  Test the list in a WITH query, or a subquery in FROM, and use its result here.
```

The error is at the list.  The query gets no method until the error is fixed.  The other queries of its file keep theirs.

**Fix:**

1. Test the list in a `WITH` query, or in a subquery in `FROM`.
2. Use the result in the aggregate, the `GROUP BY` or the `OUTPUT` clause.

The query of the sample, fixed with a `WITH` query:

```sql mssql
-- name: CountOtherProducts
WITH tested AS (
    SELECT IIF(category NOT IN @categories, 1, NULL) AS other
    FROM products
)
SELECT COUNT(other) AS others
FROM tested;
```

- **The aggregates of `geometry` and `geography`, such as `geometry::UnionAggregate`, are aggregates too.**
- **An aggregate that `OVER` follows is not one of these places.**
- **A `NOT` reaches only the condition it stands in.**  One in a `WHERE` reaches no list in the `HAVING` after it.  One before a `CASE` or an `IIF` reaches no list in its `WHEN` or its condition.  A list that no `NOT` reaches runs empty everywhere.
- **Under the outputs `models` and `sql` it is no error**, since your own code sends the list.

See: [List parameters](reference/list-parameters.md#list-parameters)

## SQLSRC200

**The tool failed unexpectedly**

Reported by: the tool.

`sqlsource` stopped on an exception that it has no error of its own for.  The message holds the type of the exception and its message.

```console
$ dotnet sqlsource describe
sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.IO.FileNotFoundException: Unable to find the specified file.
```

Here the current folder was deleted while a terminal was still in it.

**Fix:**

1. If the message names such a condition of your machine, mend that.
2. If it does not, the error is a bug in the tool.  Set the environment variable `SQLSOURCE_DEBUG` to any value, and run the command again.
3. [Report the bug](https://github.com/mbcrawfo/SqlSource/issues) with the lines that start with `trace:`.

See: [Environment variables](reference/cli.md#environment-variables)

## SQLSRC201

**No project or solution found**

Reported by: the tool.

`sqlsource describe` was given a folder that holds no `.sln`, `.slnx` or `.csproj` file.  If it was given no path, that folder is the current one.  The tool looks in that folder alone, not in the folders below it.

**Fix:** do one of these.

- Run the command from the folder of the project or the solution.
- Name the project or the solution to run on, as in this command:

```console
$ dotnet sqlsource describe src/App/App.csproj
```

- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC202

**More than one project or solution found**

Reported by: the tool.

`sqlsource describe` was given a folder that holds more than one `.sln`, `.slnx` or `.csproj` file.  If it was given no path, that folder is the current one.  The tool does not choose between them, as `dotnet build` does not.

**Fix:**

1. Name the one to run on:

```console
$ dotnet sqlsource describe App.slnx
```

- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC203

**Path is not a project or a solution**

Reported by: the tool.

The path given to `sqlsource describe` does not exist, or it is a file that is not a `.sln`, `.slnx` or `.csproj` file.  An empty path is this error too: a shell gives one for a variable that is not set.  The message holds the full path that the tool looked at.  The tool resolves a relative path against the current folder.

**Fix:**

1. Give the path of a solution, of a C# project, or of a folder that holds exactly one of them.

- **A solution filter, `.slnf`, and a project of another language are not read.**
- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC204

**Project does not use SqlSource**

Reported by: the tool.

The project given to `sqlsource describe`, or named with `--project`, was restored and does not use the SqlSource package.

```console
$ dotnet sqlsource describe src/Tools/Tools.csproj
/work/src/Tools/Tools.csproj : error SQLSRC204: '/work/src/Tools/Tools.csproj' does not use SqlSource
    help: add the SqlSource package to the project
```

**Fix:** do one of these.

- Add the package with `dotnet add package SqlSource`, and restore.
- Run the command on the project that has the queries.

The tool reads projects by these rules:

- **A project of a solution that was not named is not an error.**  The tool leaves it out, and says nothing.
- **A project with several target frameworks is read by the first of them.**  So a project that references SqlSource for a later framework alone is this error.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC205

**Project could not be evaluated**

Reported by: the tool.

`sqlsource` asks MSBuild about each project with `dotnet msbuild`: first what the project is, then which files the compiler is given.  One of those runs failed.  The lines under the error give the reason, and then the first twenty lines that MSBuild wrote.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC205: MSBuild could not evaluate '/work/App/App.csproj'
    reason: dotnet msbuild ended with the exit code 1
    msbuild: /work/App/App.csproj(4,5): error MSB4019: The imported project "/work/Shared.props" was not found.
```

**Fix:**

1. Mend what MSBuild reports.  `dotnet build` of the project reports the same.

Some reasons have a known cause:

- **MSBuild says that the target `SqlSourceWriteManifest` does not exist.**  The SqlSource package of the project is older than the tool.  Update the package.
- **The reason is that the project file does not exist.**  The solution lists a project that is not on the disk.  Restore the file, or take the project out of the solution.
- **The reason is that `dotnet` could not be started.**  The .NET SDK is not installed or not on the path.  The tool needs the SDK, and not only the runtime it runs on.

See: [Requirements](reference/cli.md#requirements)

## SQLSRC206

**Project manifest cannot be read**

Reported by: the tool.

For each project, `sqlsource` has MSBuild write a file that lists what the compiler is given: the project manifest, which the SqlSource package writes.  The tool could not read it.  The message ends with what is wrong.

```console
$ dotnet sqlsource describe
/work/App/App.csproj : error SQLSRC206: The project manifest of '/work/App/App.csproj' cannot be read: it has version '2' of the format and this tool reads version 1.  The SqlSource package and the sqlsource tool are out of step: update the older one
```

Here the SqlSource package of the project and the `sqlsource` tool are of different versions.  The two are released together under one version number.

**Fix:**

1. Update the older of the two: the package in the project file, or the tool with `dotnet tool update SqlSource.Tool`.

- **When the build runs the tool, the message names the manifest file that the build wrote, by its full path**, as `The project manifest '/work/App/obj/Debug/net8.0/App.csproj.SqlSource.manifest' cannot be read`, whatever the path holds.
- **Any other reason, such as one that names a line, is a bug in SqlSource.**  The package wrote a file that the tool of its own version does not expect.  [Report it](https://github.com/mbcrawfo/SqlSource/issues) with the message.

See: [Install the tool](reference/cli.md#install-the-tool)

## SQLSRC207

**Project is not in the run**

Reported by: the tool.

A path given with `--project` is not a C# project of the solution that the run is on.  If the run is on a project, the option can name that project and no other.  The message holds the full path that the tool looked at.  The tool resolves a relative path against the current folder, not against the folder of the solution.

```console
$ dotnet sqlsource describe App.slnx --project Tools.csproj
sqlsource : error SQLSRC207: '/work/Tools.csproj' is not a project of '/work/App.slnx'
```

**Fix:**

1. Give the path of a `.csproj` file that the solution lists.  `dotnet sln list` shows them.

- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.

See: [Options](reference/cli.md#options)

## SQLSRC208

**Attribute argument is not a literal**

Reported by: the tool.

`Source` of `[SqlSourceGenerate]` is not a string literal.  A constant, `nameof`, a concatenation and an interpolated string are all this error, though the compiler accepts them.

The `sqlsource` tool finds `[SqlSourceGenerate]` by reading the C# files of a project as text.  It does not compile them, so it cannot follow a name to its value.  `Source` decides which `.sql` files the type claims, and the tool reads it alone.  So it must be a string literal.

```csharp
using SqlSource;
internal static class Folders { public const string Queries = "Queries"; }
[SqlSourceGenerate(Source = Folders.Queries)]
internal static partial class Queries;
```

```console
/work/App/Queries.cs(3,29): error SQLSRC208: 'Source' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here
```

Until the error is mended, the tool describes nothing for the type.  So the build reports [SQLSRC241](#sqlsrc241) for each query of the type that needs an entry in its sidecar and has none.  Under a dialect that has no database to ask, such as `ansi`, the build reports [SQLSRC209](#sqlsrc209) instead.

**Fix:**

1. Write the value where the attribute is: `Source = "Queries"`.

See: [`Source` written as text](reference/attributes.md#limit-source-written-as-text)

## SQLSRC209

**Output needs a dialect that can be described**

Reported by: the build and the tool.

A query whose output is `repository`, `methods` or `models` is described by a database.  The dialects that the tool can describe are `postgres` and `mssql`.  This file has a query with such an output and another dialect.  The build reports this error, and so does the tool.

The default output is `repository`, and the default dialect is `ansi`.  So a project that sets neither gets this error for each file that a type claims.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC209: The output 'methods' needs each query described by its database, and the dialect 'ansi' has no database to ask.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.
```

The error is reported once for a file, at the first query that needs types.  Until it is mended, no query of the file is described, and none gets an error about its entry in the sidecar.  What else the file gets depends on its output:

- Under `repository` or `methods`, its queries get no method.
- Under `models`, its constants and token methods are generated as before.

**Fix:** do one of these.

- **For PostgreSQL or SQL Server files**, set the dialect, `postgres` or `mssql`, in one of these places:
  - in the [settings file](reference/settings-file.md#settings-files) of the project's folder, for every file under it;
  - in the settings file of the files' own folder, for those files alone;
  - with `-- dialect: postgres` or `-- dialect: mssql` at the top of the file.
- **For any other database, and for queries that only want their SQL**, set the output to `sql`: `"output": "sql"` in a settings file, or `-- output: sql` in the file.

A settings file that sets the dialect for every file of the project:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
}
```

See: [Database support](reference/databases/index.md#database-support)

## SQLSRC210

**Token has no default**

Reported by: the build and the tool.

A query with a `{{token}}` cannot be sent to a database as it is written.  To describe the query, the tool puts a sample in the place of the token: the default of the token.  The output of this query is `repository`, `methods` or `models`, so the query must be described, and the token that the message names has no default.  The build reports this error, and so does the tool, which prints the `help:` line under it.

```sql
-- name: FindUsers
SELECT id, name FROM users {{where}};
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC210: The token 'where' has no default, which a query whose output is 'methods' needs.  Give it a default, or set the output to 'sql'.
    help: write the token with a default, {{where:default}}, or give one in a marker of the query: -- token: {{where:default}}
```

The error is reported once for each token without a default, at the name of its query.  Until it is mended, the query is not described, and it gets no error about its entry in the sidecar.  What else the query gets depends on its output:

- Under `repository` or `methods`, it gets no method.
- Under `models`, its constant or token method is generated as before.

**Fix:** do one of these.

- Give the token a default where it stands: `{{where:WHERE deleted_at IS NULL}}`.
- Give the default once for the query, with a marker: `-- token: {{where:WHERE deleted_at IS NULL}}`.
- If the query only wants its SQL, give it the output `sql`, which needs no default.

The default follows these rules:

- **An empty default, `{{where:}}`, describes the query with nothing there.**
- **The default is a sample for describing and nothing else.**  The generated method still takes the token as an argument.

See: [Defaults](reference/tokens.md#token-defaults)

## SQLSRC211

**Database has two dialects**

Reported by: the tool.

Two files give one logical database two dialects.  A name is one database across the whole run, with one connection, and the dialect picks the driver of that connection.  Names are compared ignoring case.

Each query that is described belongs to one logical database, the first of these:

- the database that its `-- database:` marker names;
- the `database` of the nearest [settings file](reference/settings-file.md#settings-files) that sets one, in the folder of its file or in a folder above it;
- the name of the dialect of its file.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC211: The database 'main' has the dialect 'mssql' here and 'postgres' in '/work/App/Queries/Users.sql'
```

The database keeps the dialect of the first file that names it, in the order of the projects and then of the paths of the files.  The error is reported once for each other file, at its first query of that database.  Until it is mended, no query of that database in that file is described.

**Fix:** do one of these.

- Give the queries of one of the two files another database.
- Give that file the dialect of the other file.

Some queries and files are not counted:

- **A query whose output is `sql` belongs to no database, and is not counted.**
- **A file that already has [SQLSRC209](#sqlsrc209) is not counted either.**

See: [Database](reference/settings.md#setting-database)

## SQLSRC212

**File is not in the run**

Reported by: the tool.

A path given to `sqlsource describe` that ends in `.sql` restricts the run to that file.  The run holds the `.sql` files that a type with `[SqlSourceGenerate]` claims, in the projects that the run is on.  This path is none of them.  The message holds the full path that the tool looked at.  The tool resolves a relative path against the current folder.

```console
$ dotnet sqlsource describe Queries/User.sql
sqlsource : error SQLSRC212: '/work/App/Queries/User.sql' is not a .sql file that a type of the run claims
```

**Fix:**

1. Check the spelling of the path.
2. If the file exists, find which of these is true, and mend it:
   - The file is not a file of the project, as when `SqlSourceIncludeFiles` is off and the project does not list it.
   - No type claims the file.  A type claims the `.sql` file named for it beside its own source file, or the ones its `Source` names.
   - The run has `--project`, and no type of the projects named claims the file.  With `--project`, the file must be claimed by a type of one of the projects named.

- **The rest of the run goes on.**
- **On Linux and macOS, a path written with backslashes whose `..` go above the root of the file system is reported here too**, with its `..` as typed.
- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC213

**Database has no connection**

Reported by: the tool.

A query that must be described belongs to a database that the run has no connection for.

A connection is given for the name of a database, and never in the project, since it holds credentials.  It can be given in these ways:

- **On the command line**, as `--connection billing=<connection string>`.
- **In the variable of the name**, in the environment or in a [`.env` file](reference/connections.md#env-file).  The variable is `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`.  So `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.
- **In `SQLSOURCE_CONNECTION`, with no name.**  It serves a run that has exactly one database that can be described.  A database that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), does not count.

Which connection wins:

- The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.
- The environment wins over the `.env` file, variable by variable.
- A variable that is empty or holds only white space is not set.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC213: No connection is given for the database 'postgres'
    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES in the environment or in /work/.env.  The run reads that file alone: --env-file, or SqlSourceEnvFile in a build, names another
```

The error is reported once for a database, at its first query that must be described.  Those queries count as failed.

**Fix:** do one of these.

- Give the connection in one of the ways that the `help:` line names.
- Leave the database out of the run with `--database`.

**The `help:` line names the `.env` file that the run reads**, whether or not the file is there.  It is the one in the folder of the solution or project that the run is on, or the one that `--env-file` names.  The line ends with `The run reads that file alone: --env-file, or SqlSourceEnvFile in a build, names another`.  So you see that a `.env` file elsewhere, such as in a folder above the project's, is not read.  Under `--env-file none` the line says `The run reads no .env file` instead.  The run never reads a `.env` file in a folder above its own.

**A run on a solution reads the `.env` file beside the solution, and not one in the folder of a project.**  A project of the database can have a `.env` file in its folder, or in a folder between it and the folder of the solution.  Then the `help:` line names each such file, in the place of the sentence above, as `The run did not read /work/App/.env: it reads /work/.env alone, and --env-file, or SqlSourceEnvFile in a build, names another`.  Do one of these:

- Move the file, or its connection variables, to the folder of the solution.
- Name the file with [`--env-file`](reference/connections.md#env-file), or for a build with [`SqlSourceEnvFile`](reference/online-build.md#setting-env-file).

**`--connection` can be given for a name that no database of the run has.**  Then the `help:` line lists the names that are close to a database of the run, so that you see a misspelt one.  It counts any other by the position of its value on the command line, as `The value of --connection at position 3 names no database of this run`.  A name that is far from every database of the run is not printed, such as the start of a connection string typed without its `name=`.

- **A database whose queries all have a current entry needs no connection.**  Its summary line says `no connection`.  Under `--force` every query is described, so every database of the run needs one.
- **A build that [runs the tool](reference/online-build.md#running-in-the-build) because it found a connection reports this error only when another database with a query to describe has one.**  When none has, the build goes on with the committed sidecars, unless one of these is true:
  - `SQLSOURCE_CONNECTION` is set for several databases: [SQLSRC215](#sqlsrc215).
  - A variable that two databases share is set: [SQLSRC214](#sqlsrc214).
- **Under `SqlSourceOnline` `true`, the build reports this error for every database without a connection.**

See: [Connections](reference/connections.md#connections)

## SQLSRC214

**Two databases share a connection variable**

Reported by: the tool.

Two databases of this run have names that give one connection variable, as `app-v2` and `app_v2` do.  So the variable cannot say which of them it is for.  The variable of the name of a database is the name in upper case, with every character that is not a letter or a digit written as `_`.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2
```

The error is reported once for each of the two databases that has a query to describe, at its first such query.  Those queries count as failed.

**Fix:** do one of these.

- Give both databases their connection on the command line: `--connection app-v2=<connection string> --connection app_v2=<connection string>`.
- Rename one of the two databases.

This error also follows these rules:

- **A connection on the command line for one of the two is not enough.**  The variable would then be read for the other alone, and a reader of the command could not tell.
- **A build that [runs the tool](reference/online-build.md#running-in-the-build) reports this error too when the variable is set**, in the environment or in the `.env` file.  It does not go on with the committed sidecars.

See: [Connections](reference/connections.md#connections)

## SQLSRC215

**Connection names no database**

Reported by: the tool.

`SQLSOURCE_CONNECTION` is set, and it is the connection of a run with exactly one database.  This run has more, and the database of this query has no connection of its own.  So nothing says that the variable is for it.

The message counts and names the databases that can be described.  A database that is in the run only through queries with an error of their own, such as [SQLSRC209](#sqlsrc209), needs no connection and is left out.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: postgres, reports
    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_REPORTS for 'reports', or pass --connection <name>=<connection string> for each
```

The error stands in the place of [SQLSRC213](#sqlsrc213), and is reported as that one is: once for a database, at its first query that must be described.

**Fix:** do one of these.

- Set the variable of each database, as the `help:` line names them.
- Give the connection of each database with `--connection`.
- Restrict the run to one database with `--database`.

See: [Connections](reference/connections.md#connections)

## SQLSRC216

**No describer for the dialect**

Reported by: the tool.

No dialect of this release gets this error.  The tool describes `postgres` and `mssql`, the two dialects that pass [SQLSRC209](#sqlsrc209).

The error is kept for a database whose dialect passes that check before the tool can describe it.  The error would then be reported at each query that the run would have described, and each of those queries would count as failed.  A dialect that the tool does not describe at all is SQLSRC209: every row marked No in [database support](reference/databases/index.md#database-support).

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC216: This version of sqlsource cannot describe 'mssql'
```

A tool of an earlier release reported it so for `mssql`, which that release could not describe.  Update the tool, together with the `SqlSource` package, as [SQLSRC206](#sqlsrc206) asks.

See: [Database support](reference/databases/index.md#database-support)

## SQLSRC217

**Sidecar was not written**

Reported by: the tool.

The tool did not write the sidecar of a `.sql` file.  A sidecar is written whole or not at all: when every query of its `.sql` file that needs an entry was described in this run or already has a current entry.  Here the run described a query of the file and could not write the result, because another query of the file has neither.

The lines under the message name each such query and say why:

- `failed`: the query has an error of its own, above.
- `not in this run`: `--database` or a `.sql` path left the query out, and its entry is missing or out of date.

```console
/work/App/Queries/Users.sql : error SQLSRC217: The sidecar of '/work/App/Queries/Users.sql' was not written, because not every query of the file has an entry
    query: GetInvoice: not in this run
    help: describe the whole file, or fix the queries that failed
```

The sidecar on the disk is as it was before the run.  The error is there so that a description that was not saved does not look like a success.

**Fix:** do the one of these that fits each query.

- Mend the queries that failed.
- Run `describe` without the filter that left a query out, so that every query of the file is described in one run.

A file that was held back with none of its queries described has only the errors of those queries.

See: [Describing again](reference/sidecars.md#describing-again)

## SQLSRC218

**File could not be changed**

Reported by: the tool.

The tool could not change a file.  The message says which file, and ends with the reason that the operating system gave.  The tool could not do one of these:

- write a sidecar;
- delete a sidecar that its `.sql` file no longer needs, or read such a sidecar to see that it can be deleted;
- write the log of `--log` or `SQLSOURCE_LOG`;
- write the bundle of `diagnose`, or its mapping.

```console
/work/App/Queries/Users.sql.json : error SQLSRC218: '/work/App/Queries/Users.sql.json' could not be written: Access to the path '/work/App/Queries/Users.sql.json' is denied.
```

**Fix:** do the one of these that fits the file.

- **A sidecar:** give yourself the right to write in the folder of the `.sql` file, or close the program that holds the sidecar open.  Then run the command again.
- **A log or a bundle:** find the case in the lists below.  Most cases end with what to do.

For a sidecar:

- **The rest of the run goes on after an error about a sidecar.**
- **A sidecar is never left half written.**  The tool writes it to a temporary file beside it, and then moves that file over the old one.  So a run that fails or is stopped leaves the old file or the new one.
- **A program that has the sidecar open can keep the tool from changing it**, as an editor or a build can on Windows.  That holds for the move, and for the delete of a sidecar that is no longer needed.  The tool tries again for about a second, so a file that is held only for a moment is written or deleted without an error.  One that stays open is reported as one that `could not be written` or `could not be deleted`.  A refusal of access looks the same to the tool, so a sidecar that you have no right to change is reported after that second too.
- **A sidecar is deleted only in a run with no `--project`, `--database` or `.sql` path, in which nothing was reported as an error before the files were decided.**  A setting that is not valid, or a type that could not be read, can make a file look as if it needs no entry.
- **A sidecar that the tool cannot open is not deleted** when the file beside it needs no entry, since it may be the file of a newer tool.  The tool cannot open it for lack of permission, because it is locked, or because it is a folder.  It is reported as one that `could not be read`.  Delete it yourself, or give yourself the right to read it.  `describe --check` reports the same, since it could not check the file.

For a log:

- **The log cannot be made or emptied as the run starts.**  Its folder is not there, it is a folder, or the system does not let the tool write it.  Nothing runs, and the exit code is 1.  Give a path that can be written, or run the command from another folder.
- **The path ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`**, in any case.  The tool refuses it with the reason `a log is not written over a .sql file, a sidecar, a project or a solution`.  It touches nothing and runs nothing, so that a name left out before a `.sql` path does not empty that file.  Give the log a name of its own.
- **The log stops being writable while the run goes on**, as when the disk is full.  The run finishes and writes its sidecars.  The error is reported once at the end, and the exit code is 1.  The log holds what was written before it stopped.

For a bundle of `diagnose`:

- **The path of `--out` ends in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx`**, in any case.  The tool refuses it with the reason `a bundle is not written over a .sql file, a sidecar, a project or a solution`, before it describes anything, and touches nothing.  Give the bundle a name of its own, such as `report.zip`.
- **The bundle or its mapping cannot be written.**  Its folder is not there, it is a folder, or the system does not let the tool write it.  The query has been described by then.  Nothing is written past the file that failed: when the mapping fails, the bundle is written and its line printed, and the error is at the mapping.  The exit code is 1.  Run the command again with an `--out` in a folder that exists and can be written.

See: [Logs](reference/cli.md#logs), [making a bundle with `diagnose`](reference/bundles.md#diagnose)

## SQLSRC219

**Sidecar is out of date**

Reported by: the tool.

`describe --check` asked the database to describe every query of the run, and compared what it said with the sidecars that are there.  It changes no file.  The message names a query and says what differs.  If the whole sidecar differs, the message names the `.sql` file instead.

What the end of the message means:

| The message ends with | What it means |
|----|----|
| `the sidecar does not exist` | A query of the file needs an entry, and the file has no sidecar |
| `the sidecar cannot be read`, or `the sidecar has format 2, and this tool writes 1` | The sidecar is no sidecar of this version of the tool.  A merge conflict in it is the usual cause of the first |
| `the sidecar has no entry` | The query was added, or renamed, since the sidecar was written |
| `the query has changed since it was described` | The SQL, the type or the nullability of a `-- param:` marker, or the default of a token changed |
| `columns[2].nullable is true, and the database says false` | The database describes the query another way than the sidecar holds, such as a column that can now be null, or one now read from another table (`columns[0].origin.table`).  A change to the schema is the usual cause |
| `database is "billing", and the project says "reports"` | The query belongs to another database, or another kind of database, than the one it was described against |
| `the query is not in the file`, or `the query needs no entry` | The sidecar holds an entry for a query that was removed, or whose output is now `sql` |
| `no query of the file needs an entry` | The sidecar belongs to a file that needs none any more |

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': columns[2].nullable is true, and the database says false
    help: run 'dotnet sqlsource describe --force' and commit the sidecars
```

**Fix:**

1. Run the command that the `help:` line under each error names.  This table gives the command for each message.
2. Commit the sidecars that the command writes.

| The message | Run |
|----|----|
| Names a query that was added, changed or removed, another database, or a sidecar that does not exist, cannot be read or has a lower format | `dotnet sqlsource describe`, with the filters the check had |
| Names a difference that the database reports, such as `columns[2].nullable` | `dotnet sqlsource describe --force`: the tool cannot see a change to the schema, and skips a query whose SQL is as it was |
| Says the sidecar has a higher format than this tool writes | Update the `SqlSource.Tool` package: a newer tool wrote the sidecar, and `describe` leaves it as it is |

- **Columns and parameters are counted from 0**: `columns[2]` is the third column.
- **Only the first difference of a query is named.**
- **What is compared is what changes generated code.**  That is these:
  - the query as written: its SQL, the types and nullability of its `-- param:` markers, and the defaults of its tokens;
  - its database, and the kind of that database;
  - whether it returns rows, and which table's rows they match, if any;
  - the name, position, type and nullability of each parameter and column;
  - the table column that each column is read from.

  The versions of the tool and of the server are not compared.
- **With `--database`, an entry for a query that is no longer in the file, or that needs none, is reported, whatever its database.**  That is so when `describe` with the same filters would rewrite the sidecar, which drops the entry.  Such a `describe` can leave the sidecar as it is, because another query of the file, of a database that `--database` does not name, has changed or was never described.  Then the sidecar keeps the entry, and the check does not report it.

The tool exits with the code 2 when it reported this error and no other.  So a build can tell a sidecar that is out of date from a check that could not be made.  A query that could not be described has an error of its own, and the exit code is then 1.

See: [Check in CI](reference/describing.md#check-in-ci)

## SQLSRC220

**Project was not restored**

Reported by: the tool.

The tool tells that a project uses SqlSource by the MSBuild files of the package, and those reach a project only through a restore.  This project is one of these:

- It references the SqlSource package and has nothing of it, so it was not restored after the reference was added.
- It has no file of a restore at all, so nothing says whether it uses SqlSource.

The tool does not restore: a restore is slow and writes into the project.

```console
$ dotnet sqlsource describe
/work/src/App/App.csproj : error SQLSRC220: '/work/src/App/App.csproj' has not been restored, or not since the SqlSource package was added to it
    help: run 'dotnet restore'
```

**Fix:**

1. Run `dotnet restore` on the solution or the project.
2. Run the command again.

- **In a solution every C# project is checked, one that has no SQL too.**  A run on a fresh checkout must not succeed by finding nothing.
- **A reference that keeps the MSBuild files of the package out of the project gives this error after a restore as well**, `ExcludeAssets="build"` for one.  Remove that from the reference.

See: [Requirements](reference/cli.md#requirements)

## SQLSRC221

**Sidecar was written by a newer tool**

Reported by: the tool.

A sidecar, the `.sql.json` file beside a `.sql` file, says which version of the format it has.  This one has a higher version than this tool writes, so a newer `sqlsource` wrote it.  This tool must not write it back down: the generator of that newer version would no longer read it.

```console
/work/App/Queries/Users.sql.json : error SQLSRC221: '/work/App/Queries/Users.sql.json' has format 2, and this tool writes format 1
    help: update the SqlSource.Tool package
```

The file is left as it is.  Every query of its `.sql` file that the run was asked to describe counts as failed.

**Fix:**

1. Update the tool with `dotnet tool update SqlSource.Tool`, to the version of the SqlSource package that the project uses.

- **A sidecar of a lower format version is no error.**  Nor is one of another version of the tool, or one that cannot be read, a merge conflict for one.  The tool describes the queries of such a sidecar and writes it again.
- **A run that can delete a sidecar does not delete one of a higher format version either**, when a file needs no entry any more.

See: [Sidecar files](reference/sidecars.md#sidecar-files)

## SQLSRC222

**Directory cannot be read**

Reported by: the tool.

`sqlsource describe` was given a folder, and the operating system would not list its files.  If it was given no path, that folder is the current one.  The message ends with the reason that the system gave.

```console
$ dotnet sqlsource describe /srv/locked
sqlsource : error SQLSRC222: '/srv/locked' cannot be read: Access to the path '/srv/locked' is denied.
```

**Fix:**

1. Give yourself the right to read the folder.

- **A path that holds `=` or `;` is named by its position on the command line instead**, as `The path at position 2`.  It may be a connection string typed in the wrong place.  The reason is then only `the system would not list its files`, since the words of the system repeat the path.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC223

**Solution cannot be read**

Reported by: the tool.

The `.sln` or `.slnx` file that `sqlsource describe` was given, or found in the folder, is not a solution that can be read.  A solution that lists one project twice is refused too.  The message ends with the reason of the library that reads the solution, which is the one that the `dotnet` command uses.

```console
$ dotnet sqlsource describe
/work/App.sln : error SQLSRC223: '/work/App.sln' cannot be read: Not a solution file.
```

**Fix:**

1. Mend the file.  `dotnet sln list` reads it the same way.

See: [Arguments](reference/cli.md#arguments)

## SQLSRC224

**Database could not be opened**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

The tool could not open a session with the database of a query that must be described.  The cause is one of these:

- nothing answers at the address;
- the server refused the user or the password;
- the database does not exist;
- the driver cannot read the connection string;
- a file that the connection string names cannot be used.

The `server:` line says what went wrong, in the tool's own words.  If the server gave a SQLSTATE or SQL Server's error number, the line starts with it.  The line names neither the host nor the user: what the server or the driver said would name them, or quote the connection string or a path in it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC224: The database 'app' could not be opened
    query: GetUser, database app, postgres
    server: 28P01: the server refused the password
    help: check that the database is running and that the connection string names it
```

The error is reported once, at the first query of the database that the run would have described.  Each of those queries counts as failed.

**Fix:** do one of these.

- Start the database.
- Correct the connection string that `--connection` or the variable of the database gives.
- Do what the row of the `server:` line in the tables below says.

In a build that [runs the tool](reference/online-build.md#running-in-the-build), the error fails the build.  The `help:` line then also says how to build without the database: `To build without the database, remove its connection or set SqlSourceOnline to false`.

**On PostgreSQL.**  The `server:` line is one of these.  A SQLSTATE is PostgreSQL's code of the error, listed in [PostgreSQL's error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html).

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

**On SQL Server.**  The line starts with `Msg <number>, Level <level>, State <state>:` when the server or SqlClient numbered the error, as in `Msg 18456, Level 14, State 1: the server refused the login`.  The number is SQL Server's, listed in [Database Engine errors](https://learn.microsoft.com/sql/relational-databases/errors-events/database-engine-events-and-errors).  The text of the line is one of these:

| The text of the `server:` line | What it means |
|----|----|
| `the server refused the login` | The login or its password is wrong, or the login may not connect (`Msg 18456`) |
| `the server cannot open the database for the login` | The database that the connection string names is not on the server, or the login may not open it (`Msg 4060`), or the login's default database cannot be opened (`Msg 4064`).  The server follows a `Msg 4060` line with a `Msg 18456` line, `the server refused the login`, which adds nothing to it |
| `the connection to the server timed out` | Nothing answered at the address in time: the address drops what is sent to it, a firewall between drops the connection, or the server is too slow to answer.  On Windows a port where nothing listens gives this line too, after the connection string's `Connect Timeout`, since SqlClient tries again until the time is up there (`Msg 258`).  On macOS it gives either this line or the one below, from one attempt to the next |
| `SqlClient could not reach the server: ConnectionRefused` | The connection could not be made.  The word after the colon is the system's reason, such as `ConnectionRefused` when nothing listens at the port, or `HostNotFound` for a host name that is not found.  On Linux the line has no reason, and on Windows a port where nothing listens gives the line above instead |
| `the server's certificate is not trusted, or the TLS handshake failed` | The server's certificate is not one this machine trusts.  Install the certificate's authority, or, for a server of your own such as a container, add `TrustServerCertificate=True` to the connection string |
| `the server refused to open a session` | Any other error while the session was opened: its number says which |
| `SqlClient cannot read the connection string` | The string is not written as SqlClient wants one: `Server=localhost;Database=app;User ID=app;Password=...`.  It has a keyword that SqlClient does not know or does not support, such as `Network Library` or `Context Connection`, or a value that is not of its keyword's kind or is out of its range, such as a `Connect Timeout` too large |
| `SqlClient does not accept these settings of the connection string together` | Two settings that SqlClient reads one by one and refuses together, such as `Integrated Security` with an `Authentication` |
| `the connection string is empty` | The value sets nothing, such as `;` |
| `the Authentication of the connection string needs both a User ID and a Password` | An `Authentication` of `Sql Password`, `Active Directory Password` or `Active Directory Service Principal` signs in with a user and a password, and the connection string lacks one of them or leaves it empty.  Give both; an `Active Directory` method then needs a provider the tool does not have, the row below, so use a SQL Server login and its password, or, on Windows, Windows authentication with `Integrated Security=True` |
| `SqlClient does not support this server on this platform` | The connection string names a server that cannot be used from this operating system: a LocalDB instance, such as `(localdb)\MSSQLLocalDB`, can be used on Windows alone.  Describe against a SQL Server that listens on a port, such as one in a container |
| `SqlClient has no provider for the Authentication of the connection string` | An `Authentication` of Microsoft Entra ID, `Active Directory ...`, which the tool cannot sign in with.  Use a SQL Server login and its password, or, on Windows, Windows authentication with `Integrated Security=True` |

See: [Connecting](reference/databases/postgresql/index.md#connecting), [connecting to SQL Server](reference/databases/sqlserver/index.md#connecting)

## SQLSRC225

**Server rejected the query**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

The tool asked the server about the query, and the server refused it.  The query can have a syntax error, name a table or a column that the database does not have, or use an operator that does not exist for the types it is given.  The message ends with what the server said, and the `server:` lines give all of it.

**Fix:** do one of these.

- Mend the query.
- Bring the database to the schema that the query was written for.

**On PostgreSQL.**  The tool asks the server to prepare the query.  The `server:` lines give the SQLSTATE and the message, then the `detail:` and `hint:` of the server, where it gave them.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': relation "user" does not exist
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P01: relation "user" does not exist
    server: at character 15
```

- **`at character` counts from the start of the SQL of the query as the tool sent it.**  That SQL has no comments, has the default of each token in the place of the token, and has `$1` in the place of each parameter.  So the place is close to the place in the file, and not always the same.  Run with `--log` for the SQL as it was sent.
- **An error at the step `resolve type` has no `at character` line.**  There the server refused the tool's question about a type, and not the query.
- **A type that a `-- param:` marker declares is checked against every use of the parameter.**  So a declared `int` compared with a text column is this error too.

**A query parameter with no declared type takes the type of its first use.**  So `placed_at > now() - @age` makes `@age` a `timestamp with time zone`.  The server then reports `operator does not exist: timestamp with time zone > interval`, an operator that the SQL does not hold.

For an error about a type or an operator, the `help:` line can name each such parameter.  It does so when the query has a parameter with no declared type and a use with no cast.  The line says that the server may have given the parameter the type of its first use.  The help is a guess: the server does not say whether its error is at a parameter.  So the help can appear for an error that is not, such as a misspelt function.  The `at character` line says where the error is.

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

**A statement that the server cannot prepare, though its SQL is valid, is [SQLSRC233](#sqlsrc233) and not this error.**  `CALL` and `CREATE TABLE` are such statements.

**A type that the query uses can be dropped while the tool describes the query.**  Then another session changed the schema between two of the tool's questions, and the tool gives this error too.  The message says what went, as in `the type of the column 'id' no longer exists`.  There is no `server:` line, since the server said nothing, and the help says to run the tool again.  A table or a column that the query reads cannot be dropped then: a `DROP TABLE`, or an `ALTER TABLE` that drops or changes a column, waits until the tool has described the query.

**The server can end the connection while the queries are described.**  The tool reports that as this error too.  The query that finds it out has the server's SQLSTATE and `the server ended the session`, as in `57P01: the server ended the session` for a backend that was ended.  The line names neither the host nor the user.  When the server said nothing, the line is `the connection to the server was lost`, or `the server did not answer in time` when Npgsql stopped waiting for its answer, with no SQLSTATE.  Every query of that database after it has the line `the connection to the server was lost`, with no SQLSTATE.  Start the run again.

**The step `check parameters` gives this error too**, for an error of the tool's comparison with Npgsql that says nothing of how Npgsql reads the SQL.  That is a connection that was lost, or an error of the server that is not of class `42` (syntax errors and access rule violations), such as a statement that was cancelled or timed out.  The `server:` line is what the server said, or one of the lines above for a connection that was lost or ended, and the message ends with it.  An error of class `42` there is [SQLSRC231](#sqlsrc231), since a `@` that the two read differently gives one.  Start the run again.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'ListUsers': canceling statement due to statement timeout
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 57014: canceling statement due to statement timeout
```

**On SQL Server.**  Each error that the server gave is a `server:` line: `Msg <number>, Level <level>, State <state>:` and its text, the cause first.  The last line is often `Msg 11501`, which says only that the query did not compile.  The message ends with the text of the cause:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC225: The server rejected the query 'GetUser': Invalid column name 'nope'.
    query: GetUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    server: Msg 207, Level 16, State 1: Invalid column name 'nope'.
    server: Msg 11501, Level 16, State 2: The batch could not be analyzed because of compile errors.
```

The step can also be `describe columns`, when SQL Server accepted the parameters of the query and could not work out its columns.  There each line is `Msg <number>:` and its text, with no level or state.  The last line is often `Msg 11529`, which says only that every path of the query failed.  A call of a procedure that the database does not have, with no argument, is such a query:

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC225: The server rejected the query 'ArchiveOrders': Could not find stored procedure 'dbo.archive_ordrs'.
    query: ArchiveOrders, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe columns
    server: Msg 2812: Could not find stored procedure 'dbo.archive_ordrs'.
    server: Msg 11529: The metadata could not be determined because every code path results in an error; see previous errors for some of these.
```

The same call with an argument, `EXEC dbo.archive_ordrs @before`, is `Msg 2812` at the step `describe parameters`, with its level and state, then `Msg 11501`.  Correct the name of the procedure, or bring the database to the schema that the query was written for.

Other causes on SQL Server:

- **An `OUTPUT` clause without `INTO`, on a table that has an enabled trigger,** is `Msg 334`.  SQL Server refuses the statement when it runs, too.  Output the rows `INTO` a table variable, and select them from it.
- **A table or a column that the database does not have** is `Msg 208`, `Invalid object name`, or `Msg 207`, `Invalid column name`, as in the first SQL Server example.  A temporary table that the query reads and does not create is `Msg 208` too, since it does not exist when the query is described.  One that the query creates is [SQLSRC265](#sqlsrc265).
- **A `GO` inside the query** is `Msg 102`, `Incorrect syntax near 'GO'`.  `GO` ends a batch in SQL Server Management Studio and `sqlcmd`, and is not T-SQL.  Give each batch a `-- name:` of its own.
- **A misspelt keyword at the start of the query**, such as `SELEC 1`, is `Msg 2812`, `Could not find stored procedure 'SELEC'`, then `Msg 11501`.  SQL Server reads a query that starts with a name as a call of the procedure of that name.  Correct the spelling.
- **A type that a `-- param:` marker declares** is checked against every use of the parameter.  So `-- param: @p xml` with `WHERE id = @p` is this error, `Msg 206`, `Operand type clash: xml is incompatible with int`.  A type that the text of the query names and the database lacks, `CAST(@p AS dbo.Nope)`, is this error too.
- **A parameter whose name, with its `@`, is longer than 128 characters** is this error, `Msg 103`, `The identifier that starts with '...' is too long`.  That is so whether the parameter is used once, used more than once or declared with `-- param:`.  A declared one gives it at the step `resolve type`.  Give the parameter a shorter name.
- **A statement whose result SQL Server cannot work out**, such as `CREATE PROCEDURE`, is `Msg 11527`, `... does not support metadata discovery`.  Give the query `-- output: sql`.
- **A connection that was lost** has the line `the connection to the server was lost`, or `the server did not answer in time` when SqlClient stopped waiting for its answer, after the number of the error.  Every query of that database after it has the line `the connection to the server was lost`.  Start the run again.  At the step `resolve type`, the server could not be asked about a declared type for the same reasons.

See: [What describing does to your database](reference/databases/postgresql/index.md#what-describing-does), [What can be described](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC226

**Parameter type could not be determined**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

The tool could not determine the type of a parameter of the query.

The server gives each parameter a type from the way the query uses it.  Nothing in this query says what the parameter is.

**On PostgreSQL.**  A use such as `SELECT @p IS NULL` says nothing of the type: it holds for a value of any type.

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'IsNull' could not be determined
    query: IsNull, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P18: could not determine data type of parameter $1
    help: cast the parameter where it is used, @p::int, or declare its type with -- param: @p int
```

The same error can come at the step `resolve type`, with no `server:` line.  Then the server did give the parameter a type, but that type is a pseudo-type, which no value has.  For example, `row_to_json(@p)` makes `@p` a `record`.  The `help:` line names the pseudo-type.

**On SQL Server.**  SQL Server cannot type a parameter in each of these cases:

- The uses of the parameter fix no type: `SELECT @p AS x` or `COALESCE(@p, name)`.
- Two parameters stand in one expression with nothing else: `@a + @b`.
- No type would make the query valid.

A parameter whose every use is like `@p IS NULL` is not this error: SQL Server gives it the type `int`.

The `server:` line is the error of SQL Server, which names the parameter:

```console
/work/App/Queries/Checks.sql(1,10): error SQLSRC226: The type of the parameter '@p' of the query 'Echo' could not be determined
    query: Echo, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    server: Msg 11506, Level 16, State 1: The parameter type for '@p' cannot be uniquely deduced; two possibilities are 'sql_variant' and 'xml'.
    help: add a CAST around a use of the parameter, or declare its type with -- param:
```

The same error with no `server:` line is for a parameter that the query uses more than once:

- If every use gets the same type, the parameter has that type, and the tool reports no error.
- Otherwise SQL Server gives its uses different types, or gives one of its uses no type.  Each use that says nothing of the type, such as `@p IS NULL`, is then left out, as [SQLSRC262](#sqlsrc262) says.
- If every use is left out that way, or a use that is left has no type, the tool reports this error.
- If the uses that are left have two types, the error is [SQLSRC262](#sqlsrc262) instead.
- If the uses that are left all get one type, the parameter has that type, and the tool reports no error.

**Fix:** do one of these, on either database.

- Cast the parameter where it is used.  On SQL Server, write a `CAST` at a use: `CAST(@p AS int)`.
- Declare the type of the parameter with a `-- param:` marker: `-- param: @p int`.

See: [Parameters](reference/databases/postgresql/index.md#parameters), [SQL Server parameters](reference/databases/sqlserver/index.md#parameters)

## SQLSRC227

**Parameter's first use does not fix its type**

Reported by: the tool.

Database: PostgreSQL.

The first use of a parameter says nothing of its type, and a later use does.  The server refuses this.

PostgreSQL takes the type of a parameter from its first use.  The pattern of an optional filter does this: in `(@name IS NULL OR name = @name)`, the first `@name` could be anything.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC227: The first use of the parameter '@name' of the query 'FindUsers' does not fix its type
    query: FindUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42P08: could not determine data type of parameter $1
    server: at character 28
    help: declare the type with -- param:, or write the use that fixes the type before the one that does not: (name = @name OR @name IS NULL)
```

**Fix:** do one of these.

- Declare the type of the parameter: `-- param: @name text`.
- Turn the condition round, so that the use that fixes the type comes first.

See: [Parameters](reference/databases/postgresql/index.md#parameters)

## SQLSRC228

**Declared type is not valid**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

A `-- param:` marker gives a parameter a type, and that type cannot be used.  The message ends with the reason.

The tool asks the server to resolve the type, and to check it against every use of the parameter.

**On PostgreSQL.**  The reason is one of these:

- **`it is not written as a type name`.**  The text is not a type name as a column definition writes one: a name, with at most one modifier in parentheses, and `[]`.
  - When nothing of the query has been sent, the reason goes on with what the tool found where the text stops being a type name.
  - That is a `;`, a comment, a line break, a name of more than two parts, or a quote or a parenthesis left open.
  - It is also a modifier that holds more than numbers and plain words.  For anything else, the reason gives the shape of a type name.
  - On PostgreSQL the modifier holds numbers and plain words, separated by commas, as in PostGIS's `geometry(Point,4326)`.  The server checks the words.
  - A word that the server does not know, such as `geometry(Nonsense,4326)`, gets this reason too, with a `server:` line and nothing after the reason.
  - The server can give the same reason for a type that it cannot use, such as `numeric(2,-3)` on PostgreSQL 14, which has no negative scale.  The `server:` line then says what the server made of the type.
- **`the database has no such type`.**  The server knows no type of that name on the search path of the session.
- **`it is a pseudo-type`.**  The server knows the name, and no value has that type: `record` and `anyelement` are two.  An array of such a type, `cstring[]`, is one too.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'user_state' declared for the parameter '@status' of the query 'ListUsers' is not valid: the database has no such type
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 42704: type "user_state" does not exist
    help: write a type the database has, schema-qualified if it is not on the search path
```

**Fix:**

1. Write the type as the database has it: `int`, `varchar(50)`, `numeric(18,2)`, `timestamp(3) without time zone`, `public.user_status`, `"My Schema"."Status"[]`.
2. If the type is in a schema that is not on the search path of the connection, write its schema.
3. After the type, write only a `null` or `not null`, and `->` with a C# type, where you need them.

The type of a marker runs to the end of its line, or to a `null`, a `not null` or a `->` that ends it.

**On SQL Server.**  The error is at the step `resolve type`, and the reason is one of these:

- **`it is not written as a type name`, followed by what the tool found.**  The text is not a type name as T-SQL writes one.
  - A type name of T-SQL is a name of one or two parts, each plain, `[bracketed]` or `"quoted"`, with any spaces around the dot.
  - Then it can have `(max)`, one or two numbers, or an XML schema collection in parentheses, as in `xml(CONTENT dbo.Schemas)`.
  - A `;`, a comment, a second `(` and a line break are refused.  Nothing of the query is sent.
- **`the database has no such type`.**  The database has no type of that name, `Msg 2715`, or no XML schema collection of that name, `Msg 6314`.
- **`it is not a type`.**  SQL Server did not accept the text as a type.  An example is `nvarchar(9000)`, which is longer than the most it allows, `Msg 131`.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC228: The type 'dbo.Nope' declared for the parameter '@email' of the query 'FindUser' is not valid: the database has no such type
    query: FindUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: resolve type
    server: Msg 2715: Column, parameter, or variable #1: Cannot find data type dbo.Nope.
    server: Msg 2724: Parameter or variable '@email' has an invalid data type.
    server: Msg 11501: The batch could not be analyzed because of compile errors.
    help: write a type the database has, with its schema when it is not a built-in type: dbo.Email
```

**Fix:**

1. Write the type as a column definition would: `int`, `nvarchar(50)`, `varchar(max)`, `decimal(18,2)`, `dbo.Email`, `[billing].[Amount]`, `xml(dbo.Schemas)`.
2. For an alias type, write its schema, unless the type is in `dbo` or in the default schema of the login.

See: [Parameters](reference/databases/postgresql/index.md#parameters), [SQL Server parameters](reference/databases/sqlserver/index.md#parameters)

## SQLSRC229

**Column type is not supported**

Reported by: the tool.

Database: PostgreSQL.

A column of the query has a pseudo-type, so the tool does not describe the query.

A pseudo-type is a type that the server knows by name, and that no value read from a column can have.  The tool cannot say what such a column holds.

`record` is the pseudo-type that a query meets.  It comes from a row that is built in the select list and not taken apart again, `ROW(a, b)`.  It also comes from the alias of a subquery that is selected whole.  An array of one, `record[]`, is another pseudo-type.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC229: The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: resolve type
    help: cast the column to a concrete type, or give the expression an alias and a cast
```

**Fix:** do one of these.

- Select the fields of the row as columns of their own.
- Cast the expression to a type that the database has: `ROW(a, b)::pair`, with `pair` a composite type.
- For a whole row, select `to_jsonb(s)`.

A parameter of a pseudo-type that the query returns is reported for the parameter, as [SQLSRC226](#sqlsrc226) or [SQLSRC228](#sqlsrc228).

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described)

## SQLSRC230

**Query holds more than one statement**

Reported by: the tool.

Database: PostgreSQL.

The tool found a second statement in the text of the query.  Nothing of such a query is sent.

On PostgreSQL a query is one statement.  The tool prepares it on the server to describe it.  A SQL Server query can hold several statements: see [what SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described).

```console
/work/App/Queries/Cleanup.sql(1,10): error SQLSRC230: The query 'Cleanup' holds more than one statement
    query: Cleanup, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: one statement for each query; a ; is allowed at the end alone
```

**Fix:**

1. Give each statement a `-- name:` of its own.

- **A `;` at the end of the statement is allowed.**  White space and comments can follow it.
- **A `;` inside a string, a dollar-quoted string or a comment does not count.**
- **A `;` inside parentheses counts as the end of a statement.**  The tool does not read the structure of the SQL.  No statement that can be prepared holds one.
- **A text that the tool reads as one statement and the server reads as two is [SQLSRC225](#sqlsrc225).**  Its `server:` line is `42601: cannot insert multiple commands into a prepared statement`.  Nothing of it is run either.

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described)

## SQLSRC231

**Parameters are read differently**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

Two readings of the parameters of the query differ, so what the tool described is not what would run.  The message starts with what reads the parameters differently: Npgsql for PostgreSQL, SQL Server for SQL Server.

SqlSource finds the parameters of a query with its own reading of the SQL, and describes the query that way.  The tool checks that reading against the database's own.

**On PostgreSQL.**  A generated query method sends the SQL as you wrote it, and lets Npgsql find the `@name` parameters.  So the tool also asks Npgsql to describe the SQL that way, and compares the two results.  It compares the columns, and where each parameter stands.

The message ends with what differs.  That is where the two texts part, the number of columns, the name or the type of one column, or what the server said of the SQL as Npgsql sent it.  The cases are these:

**A `@` that only one of the two reads as a parameter.**  It stands inside an unusual string or an identifier, or directly after another character of a name.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: syntax error at or near "@"
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: 42601: syntax error at or near "@"
    help: a @ that one of the two reads as a parameter and the other as text; the server's lines say where; a type that another session changed during the run shows the same way, so run describe again
```

**Npgsql refuses the text.**  This error then has no SQLSTATE, and the `server:` line is the message of Npgsql.  An example is a `;` followed by more text: Npgsql reads it as the end of a statement, where SqlSource read no end.

**A `:name` that only the comparison of the two texts sees.**  A difference can leave the columns the same, and then the server accepts both readings.

- Npgsql also reads `:name` as a parameter.
- Take a query with a parameter `@id` and a column `id`.  Then `tags[:id]` is a subscript for Npgsql, which sends `tags[$1]`, and the name of a column for SqlSource.
- The query still returns the same columns.  Only the comparison of the two texts sees the difference.

The query is `SELECT id FROM users WHERE id = @id AND tags[:id] IS NOT NULL`:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: the SQL with $n parts at character 45: '$1] IS NOT' for Npgsql and ':id] IS NO' for SqlSource
    query: ListUsers, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    help: a @ or a : that one of the two reads as a parameter and the other as text; the message says where the two texts part
```

- The position counts the characters of the SQL as the tool sent it, with `$1` in the place of each parameter, and without the comments of the query.  It is not a position in the file.
- If the `:` follows a bound, as in `tags[1:id]`, Npgsql sends `tags[1$1]`.  The server then answers a `42601` like the first example's.

**A schema that changed while the tool ran.**  Another session dropped a type between two questions of the tool.  This gives an error of class `42` at this step, which cannot be told from a misread `@`.  A table or a column that the query reads cannot be dropped or changed then: a `DROP TABLE`, or an `ALTER TABLE` that drops or changes a column, waits until the tool has described the query.

**A parameter that is an array of a domain, and that the query returns as a column.**  This difference has no `@` in it.  An example is `-- param: @p email_t[]`.  Npgsql sends it as an array of the base type of the domain.  So the column is `character varying[]` for Npgsql, and `email_t[]` for SqlSource.  The help is then `Npgsql sends a parameter that is an array of a domain as an array of the domain's base type, so the column it is returned as has another type; cast the column, @p::varchar[] for an array of a domain over varchar` in place of the first example's help.

- **An error of the check that says nothing of how Npgsql reads the SQL is [SQLSRC225](#sqlsrc225), at the same step.**  A lost connection is one.
- **If the SQL is plain and the two readings still differ, report it.**

**Fix:** on PostgreSQL, the fix depends on the case.

- For a misread `@`, put a space before the parameter, or write the text so that the `@` is plainly inside a string.
- For a misread `:name`, rename the parameter, so that it is not the name of a column that a slice reads.
- For a schema that changed, run `describe` again.
- For an array of a domain, cast the column to the type you want: `@p::varchar[]`.

**On SQL Server.**  The tool compares the parameters that SqlSource reads in the query with those that SQL Server reports, ignoring case.  The message names each one that only one of them reads.

Under `mssql`, SqlSource reads a name as a [variable declared with `DECLARE`](reference/parameters.md#declared-variables) when it stands after a comma of a list of declarations.

- That list ends at a `;`, or at a word that starts a statement, such as `SELECT` or `SET`.
- A label is no such word.  Neither is a word of the statements of Service Broker, such as `RECEIVE`.

So in this query SqlSource reads `@p` as a variable, and SQL Server reads it as a parameter:

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

**Fix:** on SQL Server, do one of these.

- End the list of declarations with a `;`.
- Give the parameter a `-- param:` marker with its type, `-- param: @p int`.  This makes it a parameter for SqlSource too.

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described), [what SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC232

**Type cannot be bound by Npgsql**

Reported by: the tool.

Database: PostgreSQL.

The type of a parameter cannot be bound by Npgsql, so the tool does not describe the query.  The `server:` line is the message of Npgsql, or, for a type with no name, the tool's own text.

Before the tool writes the sidecar, it checks that Npgsql can send the type of each parameter, given by its name.  Here Npgsql refused the type before it sent anything, or the type has no name that Npgsql can be given without ambiguity.

Nothing is wrong with the SQL itself.  But the query gets no entry, so it has no models and no method until it is fixed.

This is a known limit, and not a fault of the query.  Npgsql cannot send a value of these kinds of type, and the tool never rewrites your SQL to make a parameter sendable.  These are refused:

- a composite type, `public.pair`, and an array of one;
- the row type of a table, and an array of one;
- `regclass`, which `WHERE c.oid = @t::regclass` gives a parameter, and the other types of the system catalogs that Npgsql does not load, such as `aclitem`, `txid_snapshot` and `pg_node_tree`;
- a type whose schema or whose name holds a `.`, which cannot be written to Npgsql without ambiguity, since `a.b.c` could name more than one type;
- a type of an extension that Npgsql cannot send, such as `seg`.

These are bound:

- a type whose name or whose schema's name needs quotes (`"My Schema"."Status"`);
- `"char"`, the one-byte type of the system catalogs;
- a domain over another domain;
- the bit string type without a length;
- an array of a type whose name is 63 characters long;
- PostGIS's `geometry` and `geography`, their arrays, and domains over them.

```console
/work/App/Queries/Pairs.sql(1,10): error SQLSRC232: Npgsql cannot bind the type 'public.pair' of the parameter '@pair' of the query 'ListPairs'
    query: ListPairs, database app, postgres 18.6, Npgsql 10.0.3
    step: check parameters
    server: Writing is not supported for parameters having DataTypeName 'public.pair'.
    help: Npgsql cannot send a value of this type; declare the parameter as text with -- param: and cast it to the type where it is used
```

For a type with no name, the help is `the type has no name that Npgsql can be given without ambiguity; rename the schema or the type, or declare the parameter as text with -- param: and cast it to the type where it is used`.

**Fix:** do one of these.

- Declare the parameter as `text`, and cast it where it is used: `-- param: @pair text`, with `@pair::public.pair` in the SQL.
- Give the query `-- output: sql`.  The tool then leaves it out.

A parameter that only a `-- param:` marker declares gets this error too: Npgsql resolves the type of every parameter that it is handed.

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described)

## SQLSRC233

**Statement cannot be described**

Reported by: the tool.

Database: PostgreSQL.

The query starts with a kind of statement that the tool does not describe.

The tool describes a PostgreSQL query by asking the server to prepare it.  The tool describes only `SELECT` (without `INTO`), `INSERT`, `UPDATE`, `DELETE`, `MERGE` (PostgreSQL 15 and later), `VALUES` and `TABLE`, with `WITH` around any of them.  This query starts with a statement of another kind: `CALL`, `CREATE`, `EXPLAIN`, `SHOW`, `SET` and the rest of the commands that the server refuses to prepare.

The tool reads the first word of the statement before it sends anything.  So the message names that word, and the server is not asked.

```console
/work/App/Queries/Archive.sql(1,10): error SQLSRC233: The query 'Archive' is a CALL statement, which the tool cannot describe
    query: Archive, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: give the query -- output: sql; the tool describes SELECT without INTO, INSERT, UPDATE, DELETE, MERGE, VALUES and TABLE
```

**Fix:**

1. Give the query `-- output: sql`.  The tool then does not describe it.

Its SQL is still a string constant, or a method where the query has tokens, with no types.

The error still fails the query, so the tool holds back the sidecar of the file until the query is mended or given the marker.  The tool does not decide on its own that such a query needs no entry, since that would override what you wrote.

- **`SELECT ... INTO` is refused as `SELECT INTO` on every version of the server.**  PostgreSQL 15 and later prepare it, and 14 does not.  But it creates a table, as `CREATE TABLE AS` does, and a sidecar must not depend on the server that it was described against.  Write `CREATE TABLE ... AS` with `-- output: sql`, or select the rows and insert them in a second query.
- **A statement that the check of the tool misses is this error too, when the server names it as one that it cannot prepare.**  The server answers `42P14: utility statements cannot be prepared`, and the message then reads "is a utility statement".
- **A word that the tool does not know stays [SQLSRC225](#sqlsrc225).**  `SELEC 1` is a typo, and the server says so.
- **On SQL Server, a statement whose result the server cannot work out is [SQLSRC225](#sqlsrc225), `Msg 11527`.**  `CREATE PROCEDURE` is one.

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described)

## SQLSRC234

**Query writes a positional parameter**

Reported by: the tool.

Database: PostgreSQL.

The SQL of the query writes a `$n` itself.  Nothing of such a query is sent.  The message names the `$n` as it is written.

The tool finds the parameters of a query by their `@name`, and sends the SQL with `$1`, `$2` in their places.  A `$n` that the SQL writes itself would be taken for one of those.  Then the types that the server gave would not be the parameters' own.  And the method of the query, which sends parameters by name, could not fill it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC234: The query 'GetUser' writes the parameter '$1' itself
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    help: write the parameter as @name; generated code binds parameters by name
```

**Fix:**

1. Write the parameter as `@name`.

- **A `$` followed by a digit is text, and not a parameter, when it is part of a name (`a$1`).**  It is text too when it stands in a string, a dollar-quoted string, a quoted identifier or a comment.

See: [What can be described](reference/databases/postgresql/index.md#what-can-be-described)

## SQLSRC235

**Server version is not supported**

Reported by: the tool.

Database: PostgreSQL and SQL Server.

The server that the connection string of the database reaches is older than the oldest version that the tool supports.  That is PostgreSQL 14, or SQL Server 2017, which reports version 14.

- The version is the one that the server reports.  The tool reads it when it opens the session, before it sends any query.
- An Azure SQL Database or Azure SQL Managed Instance is accepted, whatever version it reports.
- A version that does not start with a number is not refused, since the tool cannot tell what it is.

The error is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is.  Each query of the database counts as failed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC235: The database 'app' runs PostgreSQL 13.12, and the tool supports 14 and later
    query: GetUser, database app, postgres
    help: upgrade the server, or describe against a database of PostgreSQL 14 or later
```

**Fix:** do one of these.

- Upgrade the server.
- Point the tool at a database of a supported version.  A copy of the schema on a newer server is one.

See: [Supported versions](reference/databases/index.md#supported-versions), [Connecting](reference/databases/postgresql/index.md#connecting), [connecting to SQL Server](reference/databases/sqlserver/index.md#connecting)

## SQLSRC236

**Connection does not keep a session**

Reported by: the tool.

Database: PostgreSQL.

The connection to the database does not keep one session.

The tool prepares a statement on the server and asks about it in later steps, in one transaction.  That works only when every step reaches the same session.

The cause is one of these:

- **The connection string sets `Multiplexing=true`.**  That setting shares physical connections between commands.
  - The tool finds it when it opens the database, and refuses before it connects.  No query has been sent, and the server is not yet known.
  - The `server:` line is the tool's own text.
  - The error is reported once, at the first query of the database that the run would have described, as [SQLSRC224](#sqlsrc224) is.  Each query of the database counts as failed.
- **A pooler in statement mode stands between the tool and the server.**  PgBouncer with `pool_mode = statement` is one.
  - The pooler refuses the transaction and closes the connection.
  - The first query of the database reports this error.  Every query after it reports [SQLSRC225](#sqlsrc225) with `the connection to the server was lost`.
  - The session has opened, so the query line names the server and the driver.  The error is at the first step.
  - The `server:` line is the tool's own text, after the SQLSTATE of the pooler.
- **A step reached another session.**  The server does not list the statement that the tool just prepared.  Or it answers `26000` (it does not exist) or `42P05` (it already exists).
  - The error is reported at that query.
  - For `26000` and `42P05`, the `server:` line is the server's text.

The error for `Multiplexing=true`:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres
    server: Multiplexing=true does not keep one session
    help: connect directly to the server or through a pooler in session or transaction mode, and without Multiplexing=true
```

The error behind a pooler in statement mode:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC236: The connection to the database 'app' does not keep one session
    query: GetUser, database app, postgres 18.6, Npgsql 10.0.3
    step: describe parameters
    server: 08P01: the server refused the transaction of a describe
    help: connect directly to the server or through a pooler in session or transaction mode, and without Multiplexing=true
```

**Fix:**

1. Remove `Multiplexing=true` from the connection string that the tool uses.
2. Connect directly to the server, or through a pooler in session or transaction mode.  With PgBouncer, give the tool a database entry or a user whose `pool_mode` is `session` or `transaction`.

Only the tool needs this connection.  Your application can keep its own connection.

See: [Connecting](reference/databases/postgresql/index.md#connecting)

## SQLSRC237

**Sidecar cannot be read**

Reported by: the build.

The sidecar of a `.sql` file, the `.sql.json` file beside it, is not a file that `dotnet sqlsource describe` wrote.  The message says what is wrong.  The error is at the place in the sidecar where the problem is.

A merge conflict in the sidecar is the usual cause, and a hand edit the next.

```console
/work/App/Queries/Users.sql.json(7,1): error SQLSRC237: The sidecar cannot be read, because it is not one JSON object.  Run 'sqlsource describe' to write it again.
```

**Fix:**

1. After a merge, first bring your database to the schema of the merged code.  Applying the migrations of the other branch is one way.  The tool writes what your database says.
2. Write the sidecar again, in one of these ways:
   - Run `dotnet sqlsource describe`, which writes the whole file again.  After a merge conflict, leave the conflict in the file.  The tool cannot read it, so it describes every query of the file again.
   - If you have already taken one side of the conflict, run `dotnet sqlsource describe --force` on the `.sql` file instead.  The side you took can hold entries written before a schema change.
3. Commit the sidecar.

- **While the sidecar cannot be read, no query of the file gets an error about its entry.**

See: [Sidecars in git](reference/sidecars.md#sidecars-in-git)

## SQLSRC238

**Sidecar has a newer format**

Reported by: the build.

The sidecar was written by a newer version of the `sqlsource` tool, in a format that this version of the `SqlSource` package does not read.  The message names the format of the sidecar, and the one that this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC238: The sidecar has format 2, and this version of SqlSource reads format 1.  Update the SqlSource package.
```

**Fix:**

1. Update the `SqlSource` package to the version of the tool that wrote the sidecar.

- **The package and the tool share a version.**  A project should use the same one for both.
- **Until you update the package, no query of the file gets an error about its entry.**

See: [Sidecar files](reference/sidecars.md#sidecar-files)

## SQLSRC239

**Sidecar has an older format**

Reported by: the build.

The sidecar was written by an older version of the `sqlsource` tool, in a format that this version of the `SqlSource` package no longer reads.  The message names the format of the sidecar, and the one that this package reads.

```console
/work/App/Queries/Users.sql.json(1,1): error SQLSRC239: The sidecar has format 0, and this version of SqlSource reads format 1.  Run 'sqlsource describe' to write it again.
```

**Fix:**

1. Run `dotnet sqlsource describe` with the tool of the version of the package.
2. Commit the sidecar that it writes.

- **Until then, no query of the file gets an error about its entry.**

See: [Sidecar files](reference/sidecars.md#sidecar-files)

## SQLSRC240

**Sidecar was written by another version of the tool**

Reported by: the build.

The sidecar, or an entry of it, was written by a version of the `sqlsource` tool other than the version of the `SqlSource` package that the project uses.  The sidecar is read and used all the same, since its format is the one that this package reads.

The package and the tool share a version.  So the tool and the package reference are out of step, or the package was updated and the sidecars were not written again.  The suffix of a version, such as `-dev` in `0.1.0-dev`, is not compared.

Each entry of a sidecar records the version that wrote it.  So a git merge can bring an entry of an older version into a sidecar that this version wrote.  The warning for such an entry is at the key of its query, with the version of the entry in the message.

A sidecar gets one warning, however many of its entries are of another version:

- at its start, when the sidecar itself was written by another version;
- otherwise, at the first such entry.

```console
/work/App/Queries/Users.sql.json(1,1): warning SQLSRC240: The sidecar was written by sqlsource 0.1.0, and this project uses SqlSource 0.2.0.  Bring the tool and the package to one version, and run 'sqlsource describe'.
```

**Fix:**

1. Update the tool or the package, so that both have one version.
2. Run `dotnet sqlsource describe`.  The tool describes again every entry that another version wrote.
3. Commit the sidecars.

This is a warning, so that a build goes on while the sidecars are written again.  A project can list it in `NoWarn` to hide it.  A project that sets `TreatWarningsAsErrors` can list it in `WarningsNotAsErrors` to keep it a warning:

```xml
<PropertyGroup>
    <WarningsNotAsErrors>$(WarningsNotAsErrors);SQLSRC240</WarningsNotAsErrors>
</PropertyGroup>
```

See: [Sidecar files](reference/sidecars.md#sidecar-files)

## SQLSRC241

**Query has no entry in the sidecar**

Reported by: the build.

If the file's dialect cannot be described, or a token has no default, the query gets [SQLSRC209](#sqlsrc209) or [SQLSRC210](#sqlsrc210) instead of this error.

The query has no entry in the sidecar of its file.  The message names the query and its output.

The output of the query is `repository`, `methods` or `models`, which needs a description of the query in the sidecar of its file.  The file has no sidecar, or its sidecar has no entry of the name of the query.

- A query that was added or renamed after the tool last ran is the usual cause.
- Names are compared as C# compares them, so `getuser` is not `GetUser`.

The error is reported once for a query, however many types claim its file.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC241: The query 'GetUser' has no entry in the sidecar of its file, and its output 'methods' needs one.  Run 'sqlsource describe'.
```

**Fix:** do the one of these that fits.

- If the query needs its entry:
  1. Run `dotnet sqlsource describe`.
  2. Commit the sidecar that it writes.
- If the project only wants the SQL of each query as a constant, set the output to `sql`.  The default output is `repository`.  Write `"output": "sql"` in the [settings file](reference/settings-file.md#settings-files) of the project's folder, or `-- output: sql` in the file.
- If the tool reports [SQLSRC271](#sqlsrc271) for the file, save the file as UTF-8, and describe it again.
- If the sidecar exists and has the entry, the build does not see it.  List the sidecar as an `AdditionalFiles` item too, as [included files](reference/msbuild.md#included-files) says.  A project needs this when it sets `SqlSourceIncludeFiles` to `false`, or lists a `.sql` file from outside its folder.

See: [Output](reference/settings.md#setting-output)

## SQLSRC242

**Entry is of another database**

Reported by: the build.

The entry of the query in the sidecar was described against a kind of database that is not the dialect of its file.  The message names the query, the kind of database of the entry, and the dialect of the file.

The cause is one of these:

- The dialect of the file was changed after the tool last ran.
- The sidecar was copied from another file.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC242: The entry of the query 'GetUser' was described for 'mssql', and the dialect of this file is 'postgres'.  Run 'sqlsource describe'.
```

**Fix:**

1. Check that the dialect of the file is the right one.
2. Run `dotnet sqlsource describe`.
3. Commit the sidecar that it writes.

See: [Dialect](reference/settings.md#setting-dialect)

## SQLSRC243

**No type map for the dialect**

Reported by: the build.

No dialect of this release gets this error: `postgres` and `mssql` are both typed.

The error is kept for a database that SqlSource can describe before it can give types for it.  For such a database, it would be reported once for the file, at its first query that needs types.  The queries of the file would then get no other error about their entries.

See: [Database support](reference/databases/index.md#database-support)

## SQLSRC244

**Entry is stale**

Reported by: the build.

The entry of the query in the sidecar is out of date: the query changed after its entry was written.  A change to one of these counts:

- the SQL of the query;
- the type or the nullability of a `-- param:` marker;
- the default of one of its tokens.

A change to a comment alone does not count, and neither does a C# type after `->`.

An entry that describes another version of the query could give it the wrong types, so it is not used.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC244: The query 'GetUser' has changed since its entry was written.  Run 'sqlsource describe'.
```

**Fix:**

1. Run `dotnet sqlsource describe`.
2. Commit the sidecar that it writes.

- **When the tool reports [SQLSRC271](#sqlsrc271) for the file, saving the file as UTF-8 is the fix.**

See: [Describing again](reference/sidecars.md#describing-again)

## SQLSRC245

**Entry names no query**

Reported by: the build.

The sidecar has an entry for a query that does not need one.  The entry is not used.  The warning is at the name of the entry in the sidecar.

The query was removed or renamed, or its output is now `sql` for every type that claims its file.

```console
/work/App/Queries/Users.sql.json(15,5): warning SQLSRC245: The entry 'GetUsers' names no query of the file that needs one.  Run 'sqlsource describe', which removes it.
```

**Fix:**

1. Run `dotnet sqlsource describe`.  The sidecar that it writes has no such entry.
2. Commit the sidecar.

When no query of the file needs an entry any more, a run of `describe` over the whole project deletes the sidecar.

**A `.sql` file that two projects include is the exception.**  One of the projects can miss a settings file of its folder that the other finds.  That is a settings file that a build target of your own adds, or one named in another case, such as `SqlSource.json`, that one project lists itself.  The output of the query can then be `sql` in one project and not in the other.

- A run of `describe` over both projects keeps the entry that the other project needs, so the warning stays.
- A run over only the project that warns removes the entry.  The build of the other project then reports [SQLSRC241](#sqlsrc241).
- For such a file, the warning clears when both projects find the same settings files, or when the project lists `SQLSRC245` in `NoWarn`.  See [a file in two projects](reference/sidecars.md#limit-file-in-two-projects).

This is a warning, since an entry that nothing uses changes no generated code.  A project can list it in `NoWarn` to hide it.  A project that sets `TreatWarningsAsErrors` can list it in `WarningsNotAsErrors` to keep it a warning.

See: [Sidecar files](reference/sidecars.md#sidecar-files)

## SQLSRC246

**Type has no C# type**

Reported by: the build.

A query gets this error only while its entry in the sidecar is current.  The error is at the query's name, once for the query: for its first column or parameter that has no C# type.  Parameters come before columns.

A column or a parameter of the query has a database type that the driver cannot read as a C# value, so SqlSource can give it no C# type.  The driver is Npgsql, or Microsoft.Data.SqlClient for a query of the `mssql` dialect.  The message names the column or parameter, its type, and what to do.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC246: The column 'relation' has the type 'regclass', which SqlSource cannot give a C# type.  Cast it to oid in the query.
```

**Fix:** what to do depends on the type.

- **`regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation` and `regdictionary`.**  Cast the column to `oid` in the query, `SELECT 'users'::regclass::oid AS relation`.  Npgsql reads it as a `uint`.  Cast an array of one to `oid[]`, as the message says.
- **A composite type, which includes the row type of a table, and a base type of an extension that SqlSource does not know.**  Do one of these:
  - Map it with the [type override](reference/type-mapping.md#setting-type-overrides) to the C# type that your application reads it as: `public.address=MyApp.Address` for a type that the data source maps.
  - Select its fields as columns of their own.
- **A range of a subtype that SqlSource does not know, and a multirange of one.**  Do both of these:
  1. Map it with the type override to `NpgsqlTypes.NpgsqlRange<T>` of the C# type of its subtype: `public.floatrange=NpgsqlTypes.NpgsqlRange<double>`.
  2. Call `EnableUnmappedTypes()` on the data source builder.  Without it, Npgsql cannot read the type.

  A project that does not reference Npgsql cannot name `NpgsqlTypes.NpgsqlRange<T>`, so the message tells it to reference Npgsql first.
- **`cube` on Npgsql 8 or 9**, which cannot read it.  Update Npgsql to 10, which reads it as `NpgsqlTypes.NpgsqlCube`.  Or cast the column to `text` in the query, as the message says.
- **`pg_snapshot`, `txid_snapshot`, `pg_node_tree`, and any other base type of `pg_catalog` that SqlSource does not know, such as `aclitem`.**  No version of Npgsql reads them, so a type override would build, and then fail when the query runs.  Cast the column to `text` in the query, and an array of one to `text[]`, as the message says.
- **PostGIS's `geometry` and `geography`, in any schema, and an array of one or a domain over one.**
  - Reference `Npgsql.NetTopologySuite`.  A query whose output is `repository` or `methods` then reads them as NetTopologySuite's `Geometry`, as the message says: see [NetTopologySuite on PostgreSQL](reference/databases/postgresql/libraries.md#nettopologysuite).
  - A query whose output is `models` gets this error with the plugin too.  So does a query where the [library types](reference/library-types-and-json.md#setting-library-types) turn NetTopologySuite off, since only the query methods where it is on get its types.
  - For a query whose output is `models`, give it the output `repository` or `methods`.  For a query where the library types turn NetTopologySuite off, turn it on.  Or, for either, map the type with the type override to the C# type that your code reads it as, by the key that the message names, `public.geometry`.
- **`geography` and `geometry` of SQL Server.**  Do one of these:
  - Reference `NetTopologySuite.IO.SqlServerBytes` and NetTopologySuite 2.5.0 or later, and turn NetTopologySuite on with the [library types](reference/library-types-and-json.md#setting-library-types).  A query whose output is `repository` or `methods` then reads them as NetTopologySuite's `Geometry`: see [NetTopologySuite on SQL Server](reference/databases/sqlserver/libraries.md#nettopologysuite).  A query whose output is `models` gets this error with NetTopologySuite on.
  - Reference the package `Microsoft.SqlServer.Types`, which Microsoft.Data.SqlClient needs to read them.  Map the type with the type override to the type of that package for it, as the message says: `geography=Microsoft.SqlServer.Types.SqlGeography` or `geometry=Microsoft.SqlServer.Types.SqlGeometry`.
- **`hierarchyid` of SQL Server.**  Microsoft.Data.SqlClient reads it only when the application references the package `Microsoft.SqlServer.Types`.  Reference the package, and map the type with the type override to the type of that package, as the message says: `hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId`.
- **A SQL Server type that this version of SqlSource does not know, and a CLR type of your own assembly.**  Map it with the type override to the C# type that your application reads it as, as the message says.  Write `sys.newtype=MyApp.NewType` for a type of a newer SQL Server, or `dbo."Money"=MyApp.Money` for a CLR type of the schema `dbo`.
- **A parameter with no type, `record`, `unknown`, `void`, a kind of type that this version of SqlSource does not know, or a SQL Server type with no name.**  The `sqlsource` tool never writes these, so the sidecar was edited by hand, or written by a newer tool.  Run `dotnet sqlsource describe`.

When the message says to map the type, it names the key of the type by these rules:

- **The key has the schema of the type.**  It is `public.address`, `pg_catalog.pg_class` for the row type of a catalog table, or `sys.newtype` for a type of SQL Server.  A key without a schema must be a type that SqlSource knows.
- **A schema or a name that needs quotes is in them**, as in `"my schema"."Order Item"`.
- **A schema or a name with a line break, a tab or any other control character is written with escapes**, as in `"my schema".U&"Order\000AItem"`.  So the key can be written in a marker, which is one line.
- **A name with a capital letter is quoted too**, as in `public."Status"`.  So an entry for it maps that type alone, and not one whose name differs only by case.
- **For an array or a domain, the key is of the element or base that has no C# type.**  An entry for it maps the array or domain too.

See: [PostgreSQL types](reference/databases/postgresql/types.md#type-map), [SQL Server types](reference/databases/sqlserver/types.md#types)

## SQLSRC247

**Npgsql is older than 8.0**

Reported by: the build.

The project references a version of Npgsql older than 8.0.  The message names the version that the project references.

SqlSource gives the queries of the `postgres` dialect the C# types of Npgsql 8.0 and later.  Until the package is updated, no query of the `postgres` dialect gets its method.  Its models are still generated.

The error has no file and line, because it is about the references of the project.  It is reported once for the project, and only while a query under the `postgres` dialect has the output `repository`, `methods` or `models`.

```console
CSC : error SQLSRC247: This project references Npgsql 7.0.4, and SqlSource needs Npgsql 8.0 or later, so no query method is generated.  Update the package.
```

**Fix:**

1. Update the `Npgsql` package to 8.0 or later.
2. If another package brings the older version, reference `Npgsql` from the project itself, at the version you want.

See: [Supported versions](reference/databases/index.md#supported-versions), [Which version of Npgsql](reference/databases/postgresql/types.md#which-npgsql)

## SQLSRC248

**Npgsql is newer than SqlSource knows**

Reported by: the build.

The project references Npgsql 11 or later, and this version of SqlSource knows the C# types of Npgsql up to version 10.  The message names the version that the project references.

SqlSource gives the queries of the `postgres` dialect the types that Npgsql 10 reads.  These are right, unless the newer driver reads one of them as another type.

This message has no file and line.  It is reported once for the project, and only while a query under the `postgres` dialect has the output `repository`, `methods` or `models`.

```console
info SQLSRC248: This project references Npgsql 11.0.0, which is newer than this version of SqlSource knows.  Its models use the types of Npgsql 10.
```

**Fix:**

1. Update the `SqlSource` package when a version that knows the driver is released.

This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds on the day it updates Npgsql.  A project can list it in `NoWarn` to hide it:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC248</NoWarn>
</PropertyGroup>
```

As information, it is not in the output of `dotnet build`.  These show it:

- An IDE that lists information shows it with the errors and warnings.
- A build log has it at the detailed verbosity, `dotnet build -v:detailed -tl:off`, as the sample shows it.  The terminal logger leaves information out at any verbosity.
- A binary log, `dotnet build -bl`, has it too.

See: [Which version of Npgsql](reference/databases/postgresql/types.md#which-npgsql)

## SQLSRC249

**Type needs Npgsql**

Reported by: the build.

A query of the `postgres` dialect gets this error only while its entry in the sidecar is current.  The error is at the query's name, once for the query: for its first column or parameter that has no C# type.  Parameters come before columns.

The project does not reference Npgsql, and a column or a parameter of the query has a type whose C# type depends on Npgsql.  The message names the column or parameter, and its type.

The type is one of these:

- a type that Npgsql reads as a type of its own `NpgsqlTypes` namespace, such as `point`, `tsvector` or `int4range`;
- a type that Npgsql 8 and 9 read as one C# type, and Npgsql 10 as another: `date`, `time`, `cidr`, `daterange` and `datemultirange`, and an array or a domain of one of them.

Without a reference, SqlSource cannot tell which Npgsql the application runs on.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC249: The column 'born' has the type 'date', whose C# type depends on Npgsql, and this project does not reference it.  Reference Npgsql, or map it with the type override for the Npgsql the application runs on: date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10.
```

**Fix:** do one of these.

- Reference the `Npgsql` package from the project, at the version that the application runs on.  Every type then gets the C# type of that version.
- Map each type that differs with the [type override](reference/type-mapping.md#setting-type-overrides), for the version that the application runs on, as the message says.  A project that keeps its queries apart from the driver can do this instead.  For Npgsql 10, write `"typeOverrides": { "date": "System.DateOnly", "time": "System.TimeOnly" }` in the [settings file](reference/settings-file.md#settings-files) of the project's folder.

An entry for `date` maps an array or a domain of `date` too.

- **The message offers only C# types that a project without Npgsql can name.**  For `cidr`, that is `System.Net.IPNetwork` on Npgsql 10, since Npgsql 8 and 9 read it as a type of `NpgsqlTypes`.
- **`daterange` and `datemultirange` are read as a type of `NpgsqlTypes` on every version.**  So for them, as for every other type of the `NpgsqlTypes` namespace, the message says to reference Npgsql, which they need.
- **Every other type has the same C# type on every version of Npgsql, and needs neither.**

See: [Which version of Npgsql](reference/databases/postgresql/types.md#which-npgsql)

## SQLSRC250

**Mapped type is not found**

Reported by: the build.

The error is at the name of each query that has a column or a parameter the type is chosen for.  A query gets it once: for its first column or parameter that has no C# type.  Parameters come before columns.

A C# type that you chose cannot be found.  You chose it in one of these places:

- the [type override](reference/type-mapping.md#setting-type-overrides), which gives a database type a C# type;
- the [column types](reference/model-properties.md#setting-column-types), which give a column a C# type;
- a `-- param:` or `-- column:` marker, after `->`.

The type is not found in one of these cases:

- No type of that name and number of type arguments is in the project or its references.
- The name is a short name, of one part, and the project can see more than one type of it, in different namespaces.
- More than one reference has a type of the full name that the project can see, and the project has none.

A type of a reference that the project cannot see does not count.  So if one reference has a name as `public` and another as `internal`, the name is the public one.

The message names these:

- the column or parameter;
- what is mapped: the key of the type override, the key of the column types, the name of a `-- column:` marker of the query, or the parameter of a `-- param:` marker;
- the C# type;
- the place that chose it: the `typeOverrides` or the `columnTypes` of a settings file, which it names by its full path, a `-- type-override:` or `-- column:` marker in the file header or one of the query, or the `-- param:` marker of the parameter.

For a short name that several types have, the message names their full names: the first three, and a count of the rest.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC250: 'uuid' is mapped to 'MyApp.UserKey' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and no type of that name is found, or more than one that this project can see.  Correct the name, with its namespace and type arguments, or for a name that two references have, map to a type of this project.
/work/App/Queries/Posts.sql(1,10): error SQLSRC250: 'users.id' is mapped to 'UserId' for column 'author_id', in the columnTypes of the settings file /work/App/sqlsource.json, and no type of that name is found, or more than one that this project can see, and it could be MyApp.UserId or Legacy.UserId.  Correct the name, with its namespace and type arguments, or for a name that two references have, map to a type of this project.
```

**Fix:** what to do depends on the case.

- If no type of the name is found, write the name of a type that the project has, with its type arguments: `UserKey`, `MyApp.Ids.UserKey`, or `System.Collections.Generic.List<int>`.
- If several types have the short name, write the full name, from its namespace, of the one you mean: `MyApp.UserId`.
- If two references have a type of the full name that the project can see, map to a type of the project's own.  A type of the project is always chosen first.

Write a nested type with its full name, with a `.` before its name, as in C#: `MyApp.Outer.Inner`.

See: [Type overrides](reference/type-mapping.md#setting-type-overrides), [column types](reference/model-properties.md#setting-column-types)

## SQLSRC251

**Mapped type is not accessible**

Reported by: the build.

If the project cannot use the type, each query with a column or a parameter of that type gets the error at its name.  A query gets it once: for its first column or parameter that has no C# type.  Parameters come before columns.  For a type that is not public in a public model, the error is at the name of each query of the model.

A C# type that you chose cannot be used by the project.  You chose it by the type override, the column types or a marker.  The message names the column or parameter, what is mapped, the C# type and the place that chose it, as the message of [SQLSRC250](#sqlsrc250) does.

The cause is one of these two.

**The project cannot see the type.**  The type, or one of its type arguments, is `internal` in another assembly.  Or it is a `private` or `protected` type nested in another, in the project itself or in another assembly.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC251: 'uuid' is mapped to 'Shared.UserKey' for column 'id', in a -- type-override: marker of the query, and this project cannot see that type.  Make it, and each type it is nested in, public, or internal when it is this project's own, or give this project its assembly's internals with InternalsVisibleTo.
```

**Fix:** do one of these.

- Make the type, and each type that it is nested in, `public`.  A type of the project itself can be `internal` instead.
- For a type of another assembly, add `[assembly: InternalsVisibleTo("YourProject")]` to the assembly that declares it.  This lets the project see its `internal` types.

**The type is not `public`, and the model that would have the column or parameter as a property is.**  A model is `public` when a `public` type claims its query, as [accessibility](reference/models.md#model-accessibility) says.  A `public` type cannot have a property of a type that is not.  Each such property is an error of its own, and the message then also names the model.  The model is not generated until the error is fixed.  Under the output `models`, the constant or token method of the query still is.  Under `repository` or `methods`, the query gets no method.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC251: 'uuid' is mapped to 'App.UserKey' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and that type is not public, and the model 'App.Queries.GetUserDto' is.  Make the type public, or make each type that claims the query's file internal.
```

**Fix:** do one of these.

- Make the type `public`.
- Make each type that claims the file of the query `internal`, which makes its models `internal`.

See: [Type overrides](reference/type-mapping.md#setting-type-overrides), [column types](reference/model-properties.md#setting-column-types)

## SQLSRC252

**Mapped type cannot be a property's type**

Reported by: the build.

The error is at the name of each query that has a column or a parameter the type is chosen for.  A query gets it once: for its first column or parameter that has no C# type.  Parameters come before columns.

A C# type that you chose is one that no property of a model can have.  You chose it by the type override, the column types or a marker.  The message names the column or parameter, what is mapped, the C# type and the place that chose it, as the message of [SQLSRC250](#sqlsrc250) does.

A type is refused in each of these cases:

- It is a `static` class, a `ref struct` such as `System.Span<T>`, `System.Void`, or `System.Nullable<T>` as the whole type.
- A type argument or the element of an array is a `static` class, a `ref struct` or `System.Void`.
- A type argument is an interface with a `static abstract` member, such as `System.Numerics.INumber<int>`.
- A type argument or the element of an array is `System.Nullable<T>` with a `?` after it: `System.Nullable<int>?[]`.
- The type, a type it is nested in, or one of its type arguments is marked `[Obsolete]` as an error, as `[Obsolete("Use UserKey.", true)]`.  No code can use such a type without an error.
- A type is marked `[Obsolete]` or `[Experimental]` under a diagnostic id that is not an identifier, such as `MY-001` or `1234`.  A model could not turn off the warning, or the error of `[Experimental]`, that its use gives.
- It is a generic type with a type argument that breaks a constraint of its type parameter.  An example is `System.Collections.Generic.List<System.Nullable<string>>`, whose `System.Nullable<T>` takes only a value type.  Another is `MyApp.Box<string>` where `Box<T>` requires `where T : struct`.

Some rules decide these cases:

- **`[Experimental]` on the assembly or module that declares the type counts as on the type.**
- **A type argument written with a `?` breaks a constraint that forbids null.**  Such a constraint is `notnull`, `class` without a `?`, or a base type or interface written without a `?`.  `System.Collections.Generic.Dictionary<string?, int>` breaks one, since its key is `notnull`.
- **A type marked `[Obsolete]` or `[Experimental]` otherwise is accepted.**  Its model compiles without a warning.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC252: 'int4' is mapped to 'System.Nullable<int>' for column 'id', in the typeOverrides of the settings file /work/App/Queries/sqlsource.json, and no property can have that type.  Choose another type, and for System.Nullable<T> name T, since a column that can be null makes its property nullable.
```

**Fix:** choose another C# type.

- For `System.Nullable<T>`, choose `T`.  A column that can be null already gets a nullable property, and one that cannot gets `T`.
- For a type marked `[Obsolete]` as an error, map it to the type that replaces it.
- For a generic type, give it type arguments that meet the constraints of its declaration.

See: [Type overrides](reference/type-mapping.md#setting-type-overrides), [column types](reference/model-properties.md#setting-column-types)

## SQLSRC253

**Column has no name**

Reported by: the build.

The error is at the query's name.  If several types claim the query's file, the query still gets this error only once.  The query gets no models until it is fixed.

A column of the query has no name, so its property in the output model would have none.  The message gives the position of the column, from 1, and the query.

PostgreSQL names a column `?column?` when the query gives it no alias and it is no column of a table or call of a function.  An expression such as `price * quantity` is one, and so is a literal.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC253: The column at position 2 of the query 'GetLines' has no name.  Give it an alias.
```

```sql error
-- name: GetLines
SELECT id, price * quantity FROM order_lines;
```

**Fix:**

1. Give the column an alias, which names its property.
2. Run `dotnet sqlsource describe`.

The query with the alias:

```sql postgres
-- name: GetLines
SELECT id, price * quantity AS total FROM order_lines;
```

- **On SQL Server, such a column is [SQLSRC266](#sqlsrc266).**  The tool reports it when it describes the query.

See: [Property names](reference/model-properties.md#property-names)

## SQLSRC254

**Name is not an identifier after PascalCase**

Reported by: the build.

The error is at the query's name.  If several types claim the query's file, the query still gets this error only once.  The query gets no models until it is fixed.

The PascalCase of the name of a column or a parameter names its property, and here it is not a C# identifier.  The message names the column or parameter, and the property name that it gives.

The PascalCase is not an identifier in one of these cases:

- It is empty, as for a name of underscores alone.
- It starts with a digit, as `_1st` gives `1st`.
- It holds a character that an identifier cannot hold, as `unit-price` does.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC254: The column 'unit-price' gives the property name 'Unit-price', which is not a C# identifier.  Give it an alias.
```

```sql error
-- name: GetLines
SELECT id, "unit-price" FROM order_lines;
```

**Fix:**

1. Give the column an alias that a C# property can have as its name, or rename the parameter.
2. Run `dotnet sqlsource describe`.

The query with the alias:

```sql postgres
-- name: GetLines
SELECT id, "unit-price" AS unit_price FROM order_lines;
```

See: [Property names](reference/model-properties.md#property-names)

## SQLSRC255

**Two names give one property name**

Reported by: the build.

The error is at the query's name.  If several types claim the query's file, the query still gets this error only once.  The query gets no models until it is fixed.

Two columns, or two parameters, of the query give their model one property name, ignoring case.  The message names both, with their positions from 1, and the property name.

- The same column selected from two tables does this, as in `SELECT u.id, o.id`.
- Two spellings of a name do this, as `user_id` beside `userId`.

Names are compared ignoring case, since Dapper matches a column to a property ignoring case.  Dapper would fill both properties from one column.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC255: The column 'id' at position 1 and the column 'id' at position 2 give one property name, 'Id', ignoring case.  Give one of them an alias.
```

```sql error
-- name: GetUserOrders
SELECT u.id, o.id FROM users u JOIN orders o ON o.user_id = u.id;
```

**Fix:**

1. Give one of the columns an alias, or rename one of the parameters.
2. Run `dotnet sqlsource describe`.

The query with an alias for each column:

```sql postgres
-- name: GetUserOrders
SELECT u.id AS user_id, o.id AS order_id FROM users u JOIN orders o ON o.user_id = u.id;
```

See: [Property names](reference/model-properties.md#property-names)

## SQLSRC256

**Property name is taken**

Reported by: the build.

For a name that a record or `object` has, the error is at the query's name.  If several types claim the query's file, the query still gets it only once, and gets no models until it is fixed.  For a property named as its model, the error is at the name of each query of the model.  The model is not generated until it is fixed.

A column or a parameter gives a property name that cannot be used.  The message names the column or parameter, and the property name.  The property name is one of these:

- **The name of a member that a record or `object` already has.**  These are `EqualityContract`, `Equals`, `GetHashCode`, `ToString`, `GetType`, `MemberwiseClone`, `Finalize`, `ReferenceEquals`, `PrintMembers`, `Deconstruct` and `Clone`.  A column named `equals` or `to_string` is one.
- **The name of its model.**  C# does not allow a member to have that name.  A column `total` of a query whose `-- output-model:` marker names its model `Total` is one.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC256: The column 'to_string' gives the property name 'ToString', which a record or object already has.  Give it an alias.
/work/App/Queries/Orders.sql(1,10): error SQLSRC256: The column 'total' gives the property name 'Total', which is the name of its model.  Give it an alias.
```

```sql error
-- name: GetUsers
SELECT id, to_string FROM users;
```

**Fix:**

1. Give the column an alias, or rename the parameter.
2. Run `dotnet sqlsource describe`.

The query with the alias:

```sql postgres
-- name: GetUsers
SELECT id, to_string AS display_name FROM users;
```

- **For a property named as its model, you can give the model [another name](reference/models.md#setting-model-name) instead.**

See: [Property names](reference/model-properties.md#property-names)

## SQLSRC257

**Two models of one name differ**

Reported by: the build.

Two queries give their models one full name, and the two models are not the same.  The message names the model, the earlier query, with the type when the two are one query, and the first difference.

The name comes from an `-- input-model:` or `-- output-model:` marker, or from the suffixes and the namespace.

Two queries share a model when they agree on all of these:

- the names, C# types and nullability of the properties, in the same order;
- the model type: `record`, `sealed record`, `class` or `sealed class`;
- whether the properties are sorted.

Then one model is generated, and its documentation names each query.  An input model and an output model of one name are never the same.

Two types that claim one file, and give the model of its query one name, are two queries here too.  So their settings must agree.

The queries are taken in the order of the paths of their files, then of their order in a file, then of the names of the types.  The error is at the later of the two, at its marker when it has one, and at its name otherwise.

The model of the earlier query is still generated, and the later query has no model of that kind until the two agree.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

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

**Fix:**

1. Give one of the two queries [another model name](reference/models.md#setting-model-name), or make the two models the same.
2. If you changed a query, run `dotnet sqlsource describe`.

The queries with a model name for each:

```sql postgres
-- name: GetUser
-- output-model: UserRow
SELECT id, name, email FROM users WHERE id = @id;

-- name: GetUsers
-- output-model: UserSummary
SELECT id, name FROM users;
```

See: [Shared models](reference/shared-models.md#shared-models)

## SQLSRC258

**Model name cannot be used**

Reported by: the build.

The error is at the name of each query that would have the model.

The full name of a model is one that a generated type cannot have, since the code would not compile, or would be warned on.  The model is not generated.  The message names the model, the query and the reason.

The name is one of these:

- A type of the project has the full name.  The type that carries `[SqlSourceGenerate]` is among them.
- It is a namespace of the project, or a part of the namespace of the model is a type of the project.  An example is a [model namespace](reference/models.md#setting-model-namespace) of `App.Queries` beside a class `App.Queries`.
- It is the namespace of another model, or a part of its namespace is another model.  An example is `App.Data` beside `App.Data.Row`, which reports both.
- Its name, without the namespace, is lower-case ASCII letters alone, as `usersdto`.  The compiler warns on such a name, since it could become a keyword.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC258: The model 'App.Queries.usersdto' of the query 'GetUsers' cannot be generated: the compiler warns on a type name of lower-case ASCII letters alone.  Give the query another model name, or set another model namespace.
```

```sql error
-- name: GetUsers
-- output-model: usersdto
SELECT id, name FROM users;
```

**Fix:** do one of these.

- Give the query [another model name](reference/models.md#setting-model-name).
- Set another [model namespace](reference/models.md#setting-model-namespace).

The query with another model name:

```sql postgres
-- name: GetUsers
-- output-model: UserDto
SELECT id, name FROM users;
```

See: [Names that cannot be used](reference/models.md#unusable-model-names)

## SQLSRC259

**Output model on a query without rows**

Reported by: the build.

If several types claim the query's file, the query still gets this error only once.  The query gets no models until it is fixed.

The query has an `-- output-model:` marker, which names the type of its rows, and its statement returns no rows.  The error is at the marker.

These statements return no rows:

- an `INSERT`, `UPDATE` or `DELETE` without `RETURNING`;
- on SQL Server, one without `OUTPUT`, or one whose `OUTPUT` goes `INTO` a table.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(2,1): error SQLSRC259: The query 'DeleteUser' names an output model, and its statement returns no rows.  Remove the -- output-model: marker.
```

```sql error
-- name: DeleteUser
-- output-model: DeletedUser
DELETE FROM users WHERE id = @id;
```

**Fix:** do one of these.

- Remove the marker.
- To read the rows that the statement changes instead, add a `RETURNING` clause, or on SQL Server an `OUTPUT` clause.  Then run `dotnet sqlsource describe`.

See: [Which queries get models](reference/models.md#which-models)

## SQLSRC260

**Column marker names no usable column**

Reported by: the build.

If several types claim the query's file, the query still gets this error only once.  The query gets no models until it is fixed.

A `-- column:` marker cannot be used.  Such a marker sets the nullability or the C# type of one column of the result of its query.  The error is at the marker, and the message says which case it is.

The case is one of these:

- **The marker names no column of the result.**  Its name is compared ignoring case.  A query that returns no rows is this case too.
- **The marker says `null` or `not null` of a column whose alias already ends in `!` or `?`.**  That mark says the nullability of the column too.  A marker that gives only a C# type can name such a column.

Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

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

**Fix:** what to do depends on the case.

- For a marker that names no column, name the column as the query returns it: its alias, or its own name when it has none.
- For a column whose alias ends in a mark, keep one of the two.  Remove the `null` or `not null` of the marker, or remove the mark from the alias.  Removing the mark changes the query, so then run `dotnet sqlsource describe`.

Where the rows are read by the names of their columns, as Dapper reads them, keep the marker:

```sql postgres
-- name: GetTotals
-- column: total not null
-- column: note null
SELECT sum(amount) AS total, max(note) AS note FROM payments;
```

See: [Setting a column's nullability or type with a marker](reference/model-properties.md#column-marker)

## SQLSRC261

**Entry names an array as the catalog does**

Reported by: the build.

The type override has an entry for the name that the database gives an array type.  A column or a parameter of the query has that array type, or is of a domain over it.  The message names the column or parameter, the entry, the place that wrote it and the key to write.

PostgreSQL names the array of a type with `_` before the name of the type, so the array of `public.user_status` is `public._user_status`.  An entry for that name maps nothing, `default` included.  The type override names an array of a type outside `pg_catalog` by the key of its element with `[]`: `public.user_status[]`.

The build reports the error once for the query, for its first column or parameter that has no C# type.  It does the same for `SQLSRC246` and `SQLSRC249` to `SQLSRC252`.  The query has no models until the error is fixed.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC261: The type override's entry for 'public._user_status', in the typeOverrides of the settings file /work/App/sqlsource.json, maps nothing: 'public._user_status' is the database's own name of the array type of column 'statuses', and an entry names that array as its element's key with [].  Write the entry for 'public.user_status[]' instead.
```

**Fix:** do one of these.

- If the type override has no entry for the key that the message gives, rename the entry to that key.
- If the type override already has an entry for that key, remove the entry that the message names.

The renamed entry, as a member of `typeOverrides` in a settings file, is `"public.user_status[]": "System.Collections.Generic.List<string>"`.  As a marker, it is `-- type-override: public.user_status[]=System.Collections.Generic.List<string>`.

- **A type whose own name starts with `_` and is no array is still mapped by its own name.**  A composite type `public._tag` is one.  Its array is mapped by `public._tag[]`.

See: [Type overrides](reference/type-mapping.md#setting-type-overrides)

## SQLSRC262

**Copies of a parameter have different types**

Reported by: the tool.

Database: SQL Server.

SQL Server gives a query parameter two different types at two of the places where the query uses it.  The message names the first two types, in the order of the uses.

SQL Server gives a parameter a type at each place where the query uses it.  SqlSource takes each use as a copy of the parameter, typed on its own.  A use that says nothing of the type, such as `@p IS NULL`, is left out.  The uses that fix a type must agree.

In this sample, `WHERE id = @p OR name = @p` compares the parameter with an `int` column and with an `nvarchar(100)` one:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC262: The copies of the parameter '@p' of the query 'FindUser' have the types 'int' and 'nvarchar(100)'
    query: FindUser, database app, mssql 17.0.5015.4, Microsoft.Data.SqlClient 7.1.1
    step: describe parameters
    help: declare the parameter's type with -- param:
```

**Fix:** do one of these.

- Declare the type of the parameter with a `-- param:` marker, such as `-- param: @p nvarchar(100)`.  SQL Server then checks every use against the type that you chose.
- Use two parameters, one for each type.

**The optional filter, `(@name IS NULL OR name = @name)`, is not this error.**  Its use in `@name IS NULL` fixes no type, and the parameter takes the type of the column.

See: [SQL Server parameters](reference/databases/sqlserver/index.md#parameters)

## SQLSRC263

**Query returns rows of different shapes**

Reported by: the tool.

Database: SQL Server.

The paths through the query return different columns.  An `IF` whose two `SELECT`s differ in the number or the types of their columns is one example.

SQL Server works out the columns of a query from its text, without running it.  So every path through the query must return the same columns.  A query has one set of columns, and one model for its rows, whichever path runs.

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

**Fix:** do one of these.

- Make every path return the same number of columns, of the same types.
- Split the query into one query for each set of columns, and choose between them in your code.

The query split into two queries:

```sql mssql
-- name: FindUserByName
SELECT id, name FROM dbo.users WHERE name = @name;

-- name: FindUserById
SELECT id, name FROM dbo.users WHERE id = @id;
```

- **Paths whose columns differ only in their names are not this error.**  SQL Server gives such a column no name, which is [SQLSRC266](#sqlsrc266).  Give the column one name on every path.

See: [What SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC264

**Query runs SQL the server cannot read**

Reported by: the tool.

Database: SQL Server.

The query runs SQL that it builds in a string, `EXEC(@sql)`.

SQL Server works out the columns of a query from its text, without running it.  The string holds its SQL only when the query runs.  So SQL Server cannot say what the query returns.

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

**Fix:** do one of these.

- Write the SQL in the query.  Use a query parameter where the string took a value.
- `EXEC` a literal string, such as `EXEC(N'SELECT id, total FROM dbo.orders')`, which SQL Server can read.

The query with its SQL written in it:

```sql mssql
-- name: ListOrders
SELECT id, total FROM dbo.orders WHERE customer_id = @customer_id;
```

- **A part of the SQL that changes between calls can be a [token](reference/tokens.md#tokens).**  The name of a table is such a part.  The generated method takes the token as an argument.

See: [What SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC265

**Query uses a temporary table in a batch**

Reported by: the tool.

Database: SQL Server.

The query creates a temporary table, a `#` table, and uses it in a later statement.

SQL Server works out the columns of a query from its text, without running it.  The table does not exist until the query runs.  So SQL Server cannot work out the statements that use it.

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

**Fix:** do one of these.

- Make the query one statement.
- Declare a table variable in the place of the temporary table, such as `DECLARE @recent TABLE (customer_id int NOT NULL, total decimal(12,2) NOT NULL)`.  SQL Server reads its columns from its declaration.

The query as one statement:

```sql mssql
-- name: RecentTotals
SELECT customer_id, SUM(total) AS total
FROM dbo.orders
WHERE placed_at > @since
GROUP BY customer_id;
```

- **A query that reads a temporary table that it does not create is not this error.**  It is [SQLSRC225](#sqlsrc225), with `Msg 208`, `Invalid object name`.

See: [What SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC266

**Column has no name**

Reported by: the tool.

Database: SQL Server.

A column of the query has no name, so its property in the output model would have none.  The message gives the position of the column, from 1, and the query.

SQL Server gives no name to a column that is an expression or a literal without an alias, such as `COUNT(*)`, `price * quantity` or `1`.

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

**Fix:**

1. Give the column an alias.  The alias names its property.
2. Run `dotnet sqlsource describe` again.

The column with an alias:

```sql mssql
-- name: CountOrders
SELECT customer_id, COUNT(*) AS order_count FROM dbo.orders GROUP BY customer_id;
```

- **This error is to SQL Server what [SQLSRC253](#sqlsrc253) is to PostgreSQL.**  The tool reports this error when it describes the query.

See: [What SQL Server can describe](reference/databases/sqlserver/index.md#what-can-be-described)

## SQLSRC267

**Microsoft.Data.SqlClient is older than 6.1**

Reported by: the build.

The project references a version of Microsoft.Data.SqlClient older than 6.1.  The message names the version that the project references.

SqlSource gives the queries of the `mssql` dialect the C# types of Microsoft.Data.SqlClient 6.1 and later.  Until the package is updated, no query of the `mssql` dialect gets its method.  Its models are still generated.

```console
CSC : error SQLSRC267: This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update the package.
```

**Fix:**

1. Update the `Microsoft.Data.SqlClient` package to 6.1 or later.
2. If another package brings the older version, reference `Microsoft.Data.SqlClient` from the project itself, at the version you want.

- **EF Core 8 and 9 bring Microsoft.Data.SqlClient 5.1.**  So a project that uses either references Microsoft.Data.SqlClient 6.1 or later directly, beside the EF Core packages.
- **The error has no file and line, because it is about the references of the project.**  The build reports it once for the project.  It reports it only while a query has the output `repository`, `methods` or `models` under the `mssql` dialect.
- **A query with a `vector` column or parameter also gets [SQLSRC270](#sqlsrc270), at its name.**  The same update clears both errors.

See: [Supported versions](reference/databases/index.md#supported-versions), [Which version of Microsoft.Data.SqlClient](reference/databases/sqlserver/types.md#which-sqlclient)

## SQLSRC268

**Microsoft.Data.SqlClient is newer than SqlSource knows**

Reported by: the build.

The project references Microsoft.Data.SqlClient 8 or later.  This version of SqlSource knows the C# types of Microsoft.Data.SqlClient up to version 7.  The message names the version that the project references.

SqlSource gives the queries of the `mssql` dialect the types that Microsoft.Data.SqlClient 7 reads.  These types are right unless the newer driver reads one of them as another type.

```console
info SQLSRC268: This project references Microsoft.Data.SqlClient 8.0.1, which is newer than this version of SqlSource knows.  Its models use the types of Microsoft.Data.SqlClient 7.
```

**Fix:**

1. Update the `SqlSource` package when a version that knows the driver is released.

- **It has no file and line.**  The build reports it once for the project, and only while a query has the output `repository`, `methods` or `models` under the `mssql` dialect.
- **As information, it is not in the output of `dotnet build`.**  See [SQLSRC248](#sqlsrc248) for where to find it.

This is information, not a warning, so that a project that sets `TreatWarningsAsErrors` still builds on the day it updates Microsoft.Data.SqlClient.  A project can list it in `NoWarn` to hide it:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);SQLSRC268</NoWarn>
</PropertyGroup>
```

See: [Which version of Microsoft.Data.SqlClient](reference/databases/sqlserver/types.md#which-sqlclient)

## SQLSRC269

**Type needs Microsoft.Data.SqlClient**

Reported by: the build.

A query gets this error only while its entry in the sidecar is current.  It is for the query's first column or parameter that has no C# type, and parameters come before columns.

The project does not reference Microsoft.Data.SqlClient.  A column or a parameter of a query of the `mssql` dialect has a type whose C# type is one of Microsoft.Data.SqlClient's own.  That type is `vector`, which Microsoft.Data.SqlClient reads as `Microsoft.Data.SqlTypes.SqlVector<float>`.  The message names the column or parameter and its type.

Without a reference, the project cannot name that C# type.  The build reports the error at the name of the query, once for the query.  Under the output `models`, the constant or token method of the query is still generated.  Under `repository` or `methods`, the query gets no method until the error is fixed.

```console
/work/App/Queries/Documents.sql(1,10): error SQLSRC269: The column 'embedding' has the type 'vector(3)', whose C# type depends on Microsoft.Data.SqlClient, and this project does not reference it.  Reference Microsoft.Data.SqlClient.
```

**Fix:**

1. Reference the `Microsoft.Data.SqlClient` package from the project, at 6.1 or later.

- **No other type needs the reference.**  Every other type that Microsoft.Data.SqlClient reads without another package has a C# type of .NET itself.

See: [Which version of Microsoft.Data.SqlClient](reference/databases/sqlserver/types.md#which-sqlclient)

## SQLSRC270

**Type needs a newer Microsoft.Data.SqlClient**

Reported by: the build.

A query gets this error only while its entry in the sidecar is current.  It is for the query's first column or parameter that has no C# type, and parameters come before columns.

The project references a version of Microsoft.Data.SqlClient older than 6.1.  A column or a parameter of a query of the `mssql` dialect has a type whose C# type that version does not have.  That type is `vector`, which Microsoft.Data.SqlClient reads as `Microsoft.Data.SqlTypes.SqlVector<float>`, first in 6.1.  The message names the column or parameter, its type, the version that its C# type needs and the version that the project references.

The build reports the error at the name of the query, once for the query.  The query has no models until the error is fixed.  Under the output `models`, its constant or token method is still generated.  Under `repository` or `methods`, it gets no method.

```console
/work/App/Queries/Documents.sql(1,10): error SQLSRC270: The column 'embedding' has the type 'vector(3)', whose C# type needs Microsoft.Data.SqlClient 6.1 or later, and this project references Microsoft.Data.SqlClient 6.0.5.  Update the package.
```

**Fix:**

1. Update the `Microsoft.Data.SqlClient` package to 6.1 or later.  This also clears [SQLSRC267](#sqlsrc267), which the build reports for the project beside this error.
2. If another package brings the older version, reference `Microsoft.Data.SqlClient` from the project itself.  Use the version you want.

- **EF Core 8 and 9 bring an older version.**

See: [Which version of Microsoft.Data.SqlClient](reference/databases/sqlserver/types.md#which-sqlclient)

## SQLSRC271

**File is not saved as UTF-8**

Reported by: the tool.

The tool reports a `.sql` file saved in another encoding, such as Windows-1252, in one of two cases:

- a query of the file needs an entry in its sidecar;
- the tool cannot read the file as SQL.

The tool describes no query of the file until the file is saved as UTF-8.

Such a file has no one text.  A build on Windows reads it in the code page of the system.  The tool, and a build on macOS or Linux, read each character that is not UTF-8 as a replacement character, `�`.  That character then stands in the generated SQL too.

```console
/work/App/Queries/Users.sql : error SQLSRC271: '/work/App/Queries/Users.sql' is not saved as UTF-8, so its text depends on the system that reads it.  Save it as UTF-8.
```

**Fix:**

1. Save the file as UTF-8, with the "Save with encoding" command of your editor.
2. Run `dotnet sqlsource describe` again.  That fixes the entries that the build reported as missing or out of date.

- **A file that starts with a byte order mark is never reported.**
- **A file that the tool can read as SQL, and whose queries all have the output `sql`, is not reported.**
- **The build does not report this error itself.**  It reports a query of the file that has no entry as missing one ([SQLSRC241](#sqlsrc241)).  On Windows, it can also report the entry of a query as out of date ([SQLSRC244](#sqlsrc244)).  It does so when the characters outside ASCII are in the SQL of the query, and not only in its comments.  Otherwise the build may report nothing while the tool reports the file.
- **The tool also reports a C# file in another encoding, in two cases.**  The first is a `[SqlSourceGenerate]` with a `Source` written with a character outside ASCII: the error is at that `Source`.  The second is a `[SqlSourceGenerate]` with no `Source`, on a type whose name is written with such a character: the error is at the name.
- **A build on Windows reads that `Source`, or that name, in the code page of the system.**  So the build can look for other `.sql` files than the tool.  The tool describes nothing for the type until the file is saved as UTF-8.
- **A C# file in another encoding is read as before when its `Source`, or the name of its type, is written with ASCII characters only.**  An escape such as `\u00ea` counts as ASCII.
- **For a `.sql` file, the tool reports the error once, at the file, with no line or column.**  For a C# file, it reports the error at the `Source` argument or the name of the type, with its line and column.
- **When the file has a parse error of its own, the tool reports this error instead of the parse errors.**  What the tool read may not be what the build reads.
- **A file that is also [SQLSRC209](#sqlsrc209) gets this error alone.**

See: [Describing again](reference/sidecars.md#describing-again)

## SQLSRC272

**Result shape needs rows**

Reported by: the build.

The error is at the query's name.  If several types claim the query's file, the query still gets it only once.  The query also gets each other error about its method, and gets no method until all are fixed.  Its models are still generated.

The name marker of the query gives it the shape `one`, `one-optional` or `many`.  But its entry in the sidecar says that its statement returns no rows, as an `UPDATE` without `RETURNING` or `OUTPUT` does.  The message names the query and its shape.

Those shapes read rows, so the method of the query cannot be generated.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC272: The query 'RenameUser' has the shape 'one', and its statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.
```

**Fix:** do one of these.

- Remove the shape.  A query without rows then gets the shape `rowcount`.
- Write `-> rowcount` for the number of rows that the statement affected.
- Write `-> none` for nothing.
- If the statement should return rows:
  1. Change the statement.
  2. Run `dotnet sqlsource describe` again.

**The build reports the error only while the output of the query is `repository` or `methods`.**  A shape changes nothing under `models` or `sql`.

See: [Result shapes](reference/sql-files.md#result-shapes)

## SQLSRC273

**Driver is not referenced**

Reported by: the build.

A query has the output `repository` or `methods`, and the project does not reference the driver of its database.  That is one of these:

- a query of the `postgres` dialect, in a project that does not reference Npgsql;
- a query of the `mssql` dialect, in a project that does not reference Microsoft.Data.SqlClient.

The message names the database and the package.

The method of the query runs on the driver, so it is not generated.  The models are still generated.

```console
CSC : error SQLSRC273: A query for PostgreSQL has the output methods or repository, and this project does not reference Npgsql, which its method needs.  Reference Npgsql, or set the output to models.
```

**Fix:** do one of these.

- Reference the package from the project: `Npgsql` 8.0 or later for PostgreSQL, `Microsoft.Data.SqlClient` 6.1 or later for SQL Server.
- If the project runs its queries some other way, set the output to `models`.  Dapper on a connection that a package of the project brings is such a way.  The output `models` gives the SQL and the models without the methods.

**The error has no file and line, because it is about the references of the project.**  The build reports it once for the project and database, however many of its queries have the output `repository` or `methods`.

See: [Query methods](reference/query-methods.md#query-methods)

## SQLSRC274

**Method name is taken**

Reported by: the build.

If several types claim the query's file, the query gets this error once.  A cause that belongs to one type, such as a member of that type, is the exception: each type that has it reports it.  If two types of one namespace claim the file, each gives the query a method of one signature.  The query then gets one error for each type, each naming the other.  The query also gets each other error about its method, and gets no method until all are fixed.

The method of a query whose output is `repository` or `methods` would have the name of something else, so it is not generated.  The message names the query, the name of the method and what takes the name.

The name of the method is the name of the query, with `Async` after it under `async-method-suffix`.  Under the generator parameter [`method-type`](reference/generator-parameters.md#setting-generator-parameters), a synchronous method has the name of the query alone.  Under `both`, an asynchronous method always has `Async` after it, whatever `async-method-suffix` says.  So it never has the name of the synchronous method beside it.

Each name of the methods of a query is checked, and the message names the one that is taken.  A query with one name taken gets none of its methods.  The exception is the last case below, a clash with a method of another type in an extension class.  That clash leaves out the methods of that name alone.

The name is taken in each of these cases:

- **Under the [member location](reference/member-placement.md#locations) `extension-class`, it is the name of the extension class that holds the method.**  It is also taken by a member of `DbConnection` or of `object`, such as `Open` or `ToString`.  A call on a connection would find that member before the extension method.  `extension-class` is the default of the output `methods`.
- **Under `class`, it is the name of a member of the type, of the type itself, or of one of its type parameters.**  It is also taken by the constant or token method of another query of the type, when that method is on the type too.  A nested class that the queries of the type put members in also takes it.
- **For a `repository` query, it is also the name of the member that its method gets its connection from, or of the field, property or parameter that holds the factory it comes from.**  That is the member of a base type, or the primary-constructor parameter, which the method would hide.
- **For a synchronous method of a `repository` query that takes no argument, it is also the name `Finalize`.**  Such a method has no parameters and no tokens.  A method of that name and no argument would be taken for the finalizer of the type, and the compiler warns of it.  The asynchronous method of the query, which takes a cancellation token, is not refused.  Nor is a synchronous method that takes a parameter or a token.
- **For a public method of a `repository` query, it is also the name of the [interface](#sqlsrc035) of the type.**  The interface holds the method too, and a member of it cannot have its name.  The interface is still generated, without the method.
- **Under `nested-class`, it is the name of the constant, token method or method of another query of the type in the same class.**  That query may come from another file of the type.
- **It is the name of the method of another query of the same type in the same class.**  The build reports the error at both queries.
- **Under `extension-class`, it is the name of a method of another type, in the same extension class or in another of the same namespace.**  That is one of two methods:
  - the method of a query of another type whose methods go into the same extension class.  Two types of one name nested in two types do that;
  - a method of the same name and the same parameters in the extension class of another type of the same namespace.  A call on a connection could not tell the two apart.

  Both queries get the error, and neither gets its methods of that name.  Two methods of one name in the classes of two types whose parameters differ are two overloads, and each is generated.  One method with an input model and one without is such a pair.

The build reports the error at the name of the query.  The models of the query are still generated.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC274: The method of the query 'Open' cannot be named 'Open', because DbConnection has a member of that name, which a call on a connection finds before the method.  Rename the query.
```

**Fix:** do one of these.

- Rename the query, with its `-- name:` marker.
- Rename the member that takes the name.
- For the methods of two types, write `-- member-location: class` in the files of one of the types.  That puts its methods on the type itself.

If a query `Get` under `async-method-suffix` stands beside a query `GetAsync` without it, rename one of the two queries.  Do the same for `Get` and `GetAsync` under `method-type=both`.  For the methods of two types, you can also rename one of the queries.

See: [Query methods](reference/query-methods.md#query-methods)

## SQLSRC275

**Token is named as an argument of the method**

Reported by: the build.

If several types claim the query's file, the query does not get this error more often.  A token that hides a repository's connection is the exception: each type whose connection it hides reports it.  The query also gets each other error about its method, and gets no method until all are fixed.

A token of a query whose output is `repository` or `methods` has the name of an argument of a query method.  These are the names: `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken`.  The message names the token, the query and what has the name.

A token is an argument of the method too, so it cannot take one of these names.  That holds even in a query without parameters, whose method has no `parameters`.  So the method is not generated.

A token of a `repository` query also cannot take the name of what its method gets its connection through.  The argument of the token would hide it.  That is the primary-constructor parameter that gives the connection or holds the factory it comes from.

The first sample is a token with the name of an argument.  The second is a token with the name of the primary-constructor parameter:

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC275: The token 'transaction' of the query 'ListFrom' has the name of an argument that a query method can have: connection, parameters, transaction, commandTimeout or cancellationToken.  Rename the token.
```

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC275: The token 'dataSource' of the query 'ListFrom' has the name of the primary-constructor parameter 'dataSource', which gives the repository methods of 'App.UserRepository' their connection.  Rename the token.
```

**Fix:** do one of these.

- Rename the token, everywhere the query writes it and in its `-- token:` marker.
- For a `repository` query, rename the parameter.

**The build reports the error at the name of the query, once for each such token.**  The models of the query are still generated.

See: [Query methods](reference/query-methods.md#query-methods)

## SQLSRC276

**Extension class name is taken**

Reported by: the build.

Under the [member location](reference/member-placement.md#locations) `extension-class`, the default of the output `methods`, the methods of a type go into a `static partial class`.  The class has the name of the type with `Extensions` after it, `SampleExtensions` for `Sample`, and is in the namespace of the type.  Here the class cannot be generated, so the type gets none of its methods.  The message names the type, the class and what stops it.

The class cannot be generated in each of these cases:

- **The project already has a type of the full name of the class, or a namespace of it.**  A class `App.SampleExtensions` that you wrote is one.
- **SqlSource writes a model of that full name, or a model in a namespace of that name.**  The model is named by `-- output-model:` or `-- input-model:`, or by the name of a query and a suffix.
- **Two types whose methods go into the one class are of two accessibilities.**  Two types `Queries` nested in two types of one namespace give one class.  The class is `public` when one of the methods of the type is public, and `internal` otherwise.  One class cannot be both.

The build reports the error at the `[SqlSourceGenerate]` of the type, once for each type whose methods would go into the class.  The models of the type are still generated.

```console
/work/App/Sample.cs(5,6): error SQLSRC276: The query methods of 'App.Sample' cannot go into the extension class 'App.SampleExtensions', because the project has a type of that name.  Rename the type or what has the name, or set -- member-location: to put the methods on the type.
```

**Fix:** do one of these.

- Rename the type, or the type, namespace or model that has the name of the class.
- Give the methods of the two types one widest visibility.
- Write `-- member-location: class` in the files of the type.  That puts the methods on the type itself, and needs no extension class.

**Two types of one accessibility that give one class share it.**  Each of the two types writes a part of the class.

See: [Query methods](reference/query-methods.md#query-methods)

## SQLSRC277

**Method marked as a converter is not one**

Reported by: the build.

A method of the project's own code gets this error whether or not a query uses it.  The output of the project, its types and its files do not change this.  A query that gets the error for a converter of a referenced assembly keeps its models.

A method marked `[SqlSourceConverter]` must be a converter, and this method is not one.  A converter is a `static` method of one argument, declared in a type, that returns the argument converted to another type.  `public static Email FromDatabase(string value)` is one.

The message names the method and the first of these that is wrong with it:

- **`it is a local function`.**  The method is declared inside another method.  Only a method declared in a type is a converter, and the attribute on a lambda is ignored.
- **`it is an operator`.**  The method is an operator, as `operator +`, or a conversion, as `implicit operator Email(string value)`, which the generated code cannot call by name.  Mark a static method that does the conversion instead.  The operator can call it.
- **`it is not static`.**  The method is an instance method.
- **`it is abstract or virtual`.**  The method is a `static abstract` or `static virtual` member of an interface, which a call through the interface cannot reach.
- **`it is generic`.**  The method has type parameters of its own.
- **`it is in an extension block`.**  The method is declared inside an `extension` block, through which the generated code cannot call it.  Declare it in a class or a struct, as a member of the type it converts or of a static class.
- **`it is in a generic type`.**  The method is in a generic type, or in a type nested in one.
- **`it does not take exactly one argument`.**  The method has no argument, or more than one.
- **`its argument is ref, out, in, params or optional`.**  The argument has one of those modifiers, or a default value.
- **`it returns nothing`.**  The method returns `void`, `Task` or `ValueTask`, or `Task<T>` or `ValueTask<T>`.  A converter returns its result itself.
- **`its return type is annotated as nullable`.**  The method returns a type written with `?`, as `string?` or `int?`.  The result of a converter is never null.  Its argument can be written with `?`, as `Email?` or `UserId?`: it then converts the type without the `?`, and is never given null.
- **`this project cannot call it`.**  The method is `private`, `protected` or `private protected`, it is in a type that the project cannot reach, or it is in a `file` type.  A `protected internal` method can be called.  Code in another file of the project cannot name a `file` type.
- **`it is marked [Obsolete] as an error`.**  The method, or a type it is in, has `[Obsolete]` with `true` as its second argument, so no call of it compiles.  A method marked `[Obsolete]` as a warning is a converter.

```console
/work/App/Email.cs(5,18): error SQLSRC277: The method 'App.Email.FromDatabase(string)' is marked [SqlSourceConverter], but it is not static.  A converter is a static method of one argument that returns the converted value.
```

**Fix:** do one of these.

- Change the method so that it is a converter.
- If the method is not meant to be a converter, remove its `[SqlSourceConverter]`.

The build reports the error at the name of the method.  For an operator, it is at the symbol of the operator.  For a conversion, it is at the type that the conversion converts to.

- **A converter in a referenced assembly is reported at each query that would convert through it.**  That is a converter on a type that a type override, a column type or a `-- param:` marker names.  The project cannot change the method.  So the build reports the error at the name of each query whose output is `repository` or `methods` and whose method would convert through it.
- **That query gets no method until the library is fixed.**  It also gets its method once the project declares a converter of its own between the same two types.  The converter of the project is used instead of the converter of the library.
- **The build of the library reports the error at the method.**  The method is in the code of the library.
- **The converter of a library is used only when the project can call it.**  That is when it is `public`, or `internal` in a library that gives the project its internals with `InternalsVisibleTo`.

See: [Converters](reference/converters.md#converters)

## SQLSRC278

**Two converters convert between the same types**

Reported by: the build.

A method of the project's own code gets this error whether or not a query uses it.  The output of the project, its types and its files do not change this.  A query that gets the error for a converter of a referenced assembly keeps its models.

Two methods of the project marked `[SqlSourceConverter]` take the same type and return the same type, so either could convert between the two types.  The message names both methods and the two types.

```console
/work/App/Conversions.cs(8,25): error SQLSRC278: The converters 'App.Conversions.ToEmail(string)' and 'App.Email.FromDatabase(string)' both convert 'string' to 'App.Email'.  Keep one.
```

**Fix:** do one of these.

- Remove `[SqlSourceConverter]` from one of the two methods.
- Remove one of the methods.

The build reports the error at the name of each of the methods.

- **Two converters of opposite directions are a pair, and not this error.**  One from `string` to `Email` and one from `Email` to `string` are such a pair.
- **A converter of a type to itself, as `DateTime` to `DateTime`, is one converter.**
- **A method that has [SQLSRC277](#sqlsrc277) is not counted until it is fixed.**
- **An argument written with `?` takes the type without it, since a converter is never given null.**  So `ToDatabase(Email? email)` and `ToDatabase(Email email)` both convert `Email` to `string`, and are this error together.
- **Two such converters in referenced assemblies are reported at each query that would convert through one of them.**  They are converters on the types that a type override, a column type or a `-- param:` marker names.  The build reports the error at the name of each query that would convert through one of them, if its output is `repository` or `methods`.  That query gets no method.  Declare a converter of your own between the same two types, which is used instead of both.

See: [Converters](reference/converters.md#converters)

## SQLSRC279

**Converters do not convert from the database type**

Reported by: the build.

A type override, a column type or a `-- param:` marker gives a column or a parameter a C# type that the driver never reads.  Your own `Email` or `UserId` is such a type.  The query has the output `repository` or `methods`.  The project has converters of that type.  But none of them is between it and the C# type that the driver reads the database type of the column as.

The message names the column or parameter, its type, and the types that its converters convert from and to.  It also names the type that the driver reads, and the database type.

So no converter can read the column or send the parameter.  SqlSource never narrows or widens a value: a converter of an `int` never reads a `bigint` column, which the driver reads as a `long`.  The build reports the error at the name of the query.  The query gets no method until it is fixed, and its models are still generated.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC279: The column 'id' has type 'App.UserId', whose converters convert from and to 'int', and not from and to 'long', the type the database type 'int8' is read as.  Add a converter between 'App.UserId' and 'long'.
```

The error has a second form, when the database type of the column or parameter has no C# type that the driver reads it as.  A composite type, `regclass` or a range of a subtype that SqlSource does not know is such a type.  It has nothing to convert from.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC279: The column 'address' has type 'App.Address', which has converters, but the database type 'public.address' has no C# type to convert from.  Choose a C# type without converters for it, or remove the converters of 'App.Address'.
```

**Fix:** do one of these.

- Add a converter between the type of the column or parameter and the type that the message names.  Add one in each direction that the queries need: from it for a column, to it for a parameter.
- Change the converters that you have to take and return that type.
- Map the column to another type.

The type that the driver reads is the one that it reads the database type as with no type override.  It reads a domain as its base type.  So a converter of `MyApp.Email` over a domain of `text` converts from and to `string`.

- **For an array whose element type was chosen, the converters of the element are used, element by element.**  A type override, a column type or a `-- param:` marker chooses it, as `email[]` under `"public.email": "App.Email"` or `posts.tag_ids` under `"posts.tag_ids": "TagId[]"`.  The message, in either form, names the element of the column or parameter and the type of the element.
- **A type that the driver reads for some database type is never this error.**  `string`, `DateTimeOffset` and `DateOnly` are such types.  So a converter between such a type and another type does not touch the columns that are read as it.
- **On SQL Server, the query methods of a project that references NodaTime convert the types of NodaTime themselves.**  So a NodaTime type given to a database type that it is not converted from is this error too, as `datetimeoffset=NodaTime.LocalDateTime`.  The message names the type that it is converted from and to.  See [NodaTime on SQL Server](reference/databases/sqlserver/libraries.md#nodatime).
- **The types of System.Text.Json are this error in the same way.**  The query methods convert them from and to a `string`: on SQL Server, and on Npgsql 8 and 9 for `JsonNode`, `JsonObject` and `JsonArray`.  Such a type is this error when it is given to a database type that is not read as a `string`.  `int=System.Text.Json.JsonElement` on SQL Server is one such case.  That holds for a column and a parameter alike.  The message says that its converters convert from and to `string`, not `int`.  See JSON on [SQL Server](reference/databases/sqlserver/libraries.md#json) and [PostgreSQL](reference/databases/postgresql/libraries.md#json).
- **A class of yours in a column that holds JSON, whose converters are all of other types than `string`, is this error too.**  It is not read through the serializer of System.Text.Json.  See [a class in a JSON column](reference/library-types-and-json.md#json-class).

See: [Converters](reference/converters.md#converters)

## SQLSRC280

**Converter is missing for one direction**

Reported by: the build.

A type override, a column type or a `-- param:` marker gives a column or a parameter a C# type.  The query has the output `repository` or `methods`.  The project has a converter between that type and the C# type that the driver reads the database type as.  But the converter is only in the other direction.  The message names the column or parameter, its type, the converter that it has, and the method that it needs.

A column needs a converter from the type that the driver reads, and a parameter needs one to it.  The build reports the error at the name of the query.  The query gets no method until it is fixed, and its models are still generated.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC280: The column 'email' has type 'App.Email', which has a converter to 'string' but none from it, which a column needs.  Add a static method marked [SqlSourceConverter] that takes 'string' and returns 'App.Email'.
```

**Fix:**

1. Add the converter that the message names, on the type itself or in a class of the project.

The converter is a static method marked `[SqlSourceConverter]` that takes the one type and returns the other.

- **A method that takes a type and returns the same type serves both directions.**
- **For an array whose elements are converted one by one, the message names the element of the column or parameter.**  The converter to add is of the type of the element.

See: [Converters](reference/converters.md#converters)

## SQLSRC281

**A library that is on is not referenced**

Reported by: the build.

The `libraryTypes` of a settings file, or a `-- library-types:` marker, turns a library on, such as NodaTime, for queries whose output is `repository` or `methods`.  The project does not reference what the library needs on the database of those queries.  The message names the library, the database and the package that is missing.

What the library needs:

- **On PostgreSQL, it is the plugin of the library for Npgsql: `Npgsql.NodaTime` or `Npgsql.NetTopologySuite`.**  The plugin brings the library, and lets Npgsql read and send its types.
- **On SQL Server, it is `NodaTime`, or for NetTopologySuite, `NetTopologySuite.IO.SqlServerBytes` with NetTopologySuite 2.5.0 or later referenced by name.**  `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, which is [SQLSRC282](#sqlsrc282).
- **System.Text.Json, which `json` turns on, is in every .NET project.**

```console
/work/App/sqlsource.json(5,5): error SQLSRC281: NodaTime is on for the PostgreSQL query methods it reaches here, but the project does not reference Npgsql.NodaTime.  Reference Npgsql.NodaTime, or turn NodaTime off for these queries.
```

**Fix:** do one of these.

- Reference the package that the message names from the project.
- Where the error points, set the library to `off` for those queries, or remove the entry.  A library that nothing turns on is on only where the project references what it needs.

The build reports the error at the entry that says `on`: the member of the settings file, or the line of the marker.  It reports it once for each database whose `repository` or `methods` queries the entry reaches, however many queries, files and types those are.

- **A query whose output is `models` or `sql` is not reported.**
- **The queries keep their methods and their models.**  They have the C# types that they have while the library is off.

See: [Library types](reference/library-types-and-json.md#setting-library-types)

## SQLSRC282

**A library is older than SqlSource supports**

Reported by: the build.

The project references a version of a library older than SqlSource supports.  A query whose output is `repository` or `methods` uses the library.  That is, a column or parameter of the query has one of the types of the library.  The [library types](reference/library-types-and-json.md#setting-library-types) give it that type, or SqlSource converts the type for it.  The message names the package, the version that the project references and the oldest that SqlSource supports.

SqlSource needs these versions:

- NodaTime 3.1.9 or later;
- NetTopologySuite 2.5.0 or later;
- for a query of SQL Server that uses NetTopologySuite, `NetTopologySuite.IO.SqlServerBytes` 2.1.0 or later.

`NetTopologySuite.IO.SqlServerBytes` 2.1.0 alone brings NetTopologySuite 2.0.0, so a SQL Server project references NetTopologySuite by name too.

Until the package is updated, a query that uses the library gets no method.  Its models are still generated, with the types of the library.  The queries that do not use the library keep their methods.

```console
CSC : error SQLSRC282: This project references NodaTime 3.1.8, and SqlSource needs NodaTime 3.1.9 or later for the query methods that use it, so they are not generated.  Update the package.
```

**Fix:**

1. Update the package to the version that the message gives, or later.
2. If another package brings the older version, reference the package of the library from the project itself.  Use the version you want.
3. Fix each other error about the method that the build reports beside this one.  Otherwise the query still has no method once the package is updated.

- **The error has no file and line, because it is about the references of the project.**  The build reports it once for the project and package, however many queries and databases use the library.  It never reports it for a query whose output is `models` or `sql`.

See: [Library types](reference/library-types-and-json.md#setting-library-types)

## SQLSRC283

**JSON opt-in does not apply**

Reported by: the build.

The query gets this error once for each column and parameter whose C# type has the word `json` where the opt-in does not apply.

A column type, a `-- column:` marker or a `-- param:` marker gives a column or a parameter a C# type with the word `json` before it.  That is `"json": true` in a settings file, or `-> json OrderMetadata` in a marker.  The query has the output `repository` or `methods`, and the opt-in cannot read and send that column or parameter as JSON.  The message names the column or parameter, its C# type, the place that chose it, as [SQLSRC250](#sqlsrc250) does, and the reason.

The opt-in says that a column of a string type, such as `text` or `nvarchar(max)`, holds JSON.  Your own class is then read and sent through the serializer of System.Text.Json, as it is for a `json` or `jsonb` column.  The opt-in does not apply in these cases:

- **A column or parameter of any other database type, such as `int` or `text[]`.**
- **A C# type that is not a class of your own.**  A type of System.Text.Json, such as `JsonElement`, is one.  A type that the driver of the database reads, such as `string` or `long`, is another.
- **On an array of a JSON type, an array of a type of System.Text.Json or of a type that the driver reads.**  `JsonElement[]` and `string[]` are such arrays.  On an array of a JSON type, such as `jsonb[]`, each element is read and sent on its own.  So the opt-in takes an array of your class, `OrderMetadata[]`.  The message then names the element.

The build reports the error at the name of the query.  The query gets no method until it is fixed, and its models are still generated.

```console
/work/App/Queries/Orders.sql(1,10): error SQLSRC283: The column 'id' is mapped to json 'App.OrderMetadata' in a -- column: marker of the query, but the database type 'int4' is read as 'int', not as a string.  Remove json, or use it only to map a column or parameter of a string or JSON type to a class of this project.
```

**Fix:** do one of these.

- Remove the word `json` from the place that the message names, or `"json": true` from the column type.  The column or parameter is then mapped as any other type is mapped.
- Move the opt-in to the column or parameter that holds JSON, and give it your class.  The opt-in fits a `string` column of a database that has no JSON type.  It also fits a `json` or `jsonb` column, on which the opt-in is allowed and changes nothing.

**A query whose output is `models` is not reported.**

See: [The JSON opt-in](reference/library-types-and-json.md#json-opt-in), [a class in a JSON column](reference/library-types-and-json.md#json-class)

## SQLSRC284

**Short name finds a type that only a package brings**

Reported by: the build.

The warning is at the query's name, under the output `repository`, `methods` or `models`.  A query gets it once for each column and parameter whose C# type has such a short name.

A C# type that you chose is a short name, of one part, or has one in its type arguments.  You chose it by the type override, the column types, or a `-- param:` or `-- column:` marker.  The project can see one type of that name.  That type is of an assembly that the project has only through a package that it does not reference itself.  Another package, or a project that it references, brings that package.

The message names the column or parameter, what is mapped, the C# type and the place that chose it, as [SQLSRC250](#sqlsrc250) does.  Then it names the short name, the full name of the type that it finds, and the assembly of that type.

That may not be the type you meant.  A package can bring a type of a common name, such as `AccountId`, that has nothing to do with yours.  An update of the package can stop bringing it, or bring another of the name.  The type is used all the same.

```console
/work/App/Queries/Users.sql(1,10): warning SQLSRC284: 'int8' is mapped to 'AccountId' for column 'id', in the typeOverrides of the settings file /work/App/sqlsource.json, and the short name 'AccountId' finds 'Brought.Client.AccountId', of the assembly Brought.Client, which this project has only through a package that it does not reference itself.  If that is the type meant, write its full name, 'Brought.Client.AccountId'; otherwise correct the name.
```

**Fix:** do one of these.

- If that is the type you meant, write its full name, from its namespace, as the message gives it: `Brought.Client.AccountId`.
- If that is the type you meant, you can also reference that assembly's own package from the project.  That turns the warning off, whatever brought it.
- If you meant another type, correct the name.  Write the full name of the type you meant, from its namespace.

Until the warning is fixed, the query uses the type that the short name finds.  Whether or not the warning is fixed, the models of the query are generated, and under the output `repository` or `methods` its method.

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

See: [Type overrides](reference/type-mapping.md#setting-type-overrides), [column types](reference/model-properties.md#setting-column-types)

## SQLSRC285

**.env file cannot be read**

Reported by: the tool.

`sqlsource describe` reads the `.env` file of the run for its connections, and the file cannot be read: the system would not open or read it.  The message names the file and ends with the reason that the system gave.

The run goes on as if the file gave no connection at all.  So the tool reports [SQLSRC213](#sqlsrc213) for each database that needed a connection from the file, unless another source gives one.

```console
$ dotnet sqlsource describe /work/App
/work/App/.env : error SQLSRC285: '/work/App/.env' cannot be read: Access to the path '/work/App/.env' is denied.
```

**Fix:** do one of these.

- Give yourself the right to read the file.
- Remove the file.
- Give the connections another way, with `--connection` or in the variables of the environment.  Then make the run read no file, with `--env-file none`, or in a build with [`SqlSourceEnvFile`](reference/online-build.md#setting-env-file) `none`.

See: [A `.env` file](reference/connections.md#env-file)

## SQLSRC286

**Connection in .env cannot be read**

Reported by: the tool.

A line of the `.env` file sets a connection variable, `SQLSOURCE_CONNECTION` or one that starts with `SQLSOURCE_CONNECTION_`, and the tool cannot tell what it sets.  The error is at the line and the column of the name of the variable.  The message says which variable and why.

The value is never printed, since it is a connection and holds credentials.

The run goes on as if the file gave no connection at all, even from its good lines.  A file that is partly wrong is not one to trust.

The reason is one of six:

- **`it has no '='`.**  The line is the name of the variable and nothing else.
- **`the key is not a valid name`.**  The text before the `=` starts with `SQLSOURCE_CONNECTION` but is not a name of letters, digits and `_` that does not start with a digit.  `SQLSOURCE_CONNECTION-A` is such a text.  A letter or a digit is one of any script, since the variable of a database called `données` is `SQLSOURCE_CONNECTION_DONNÉES`.
- **`a quote is not closed`.**  The value starts with `'` or `"`, and the line ends before the matching quote.
- **`text follows the closing quote`.**  After the closing quote there is something other than white space or a `#` comment.
- **`a backslash starts no escape that .env takes`.**  A value in double quotes holds a `\` before a character other than `\`, `"`, `n`, `r` or `t`.  Write a literal backslash as `\\`, or put the value in single quotes, which take every character as it is.
- **`it is not saved as UTF-8`.**  The name or the value of the variable holds a `�`.  The tool reads a `�` for each byte of the file that is not UTF-8.  An `ä` of a password in a file saved as Windows-1252 or Latin-1 is such a byte.  Only the lines of connection variables are checked, so the rest of the file can stay as it is saved.  A value that really holds `�` cannot be given in a `.env` file; give it with `--connection`.

```console
/work/.env(2,1): error SQLSRC286: The line that sets 'SQLSOURCE_CONNECTION_BILLING' cannot be read: a quote is not closed
```

**Fix:**

1. Fix the line that the message names.
2. For `it is not saved as UTF-8`, save the file as UTF-8 with the "Save with encoding" command of your editor.

- **A line that sets any other variable is not read by the tool, and is never an error.**
- **A connection string that holds a `#`, a quote or a backslash is safest in single quotes.**  `SQLSOURCE_CONNECTION_BILLING='Host=db;Password=a#b'` is one.

See: [A `.env` file](reference/connections.md#env-file)

## SQLSRC287

**The watcher failed**

Reported by: the tool.

`sqlsource describe --watch` asks the system to tell it when a file changes.  The system refused to watch a folder, or failed while it watched one.  The message names the folder and ends with the reason that the system gave.

The usual cause is one of the two limits that Linux puts on watching files, of `inotify instances` and of `inotify watches`.  A machine with many projects can reach them, as can one with other tools that watch folders, such as editors.

The watch goes on.  It describes again, and reads your projects again, since the folder may have changed unseen.  Each time it reads your projects again, it tries the folder again.  So a file that you save there may not be described until the watch can watch the folder.  An error that repeats with the same folder and the same reason is written once in a watch.

```console
$ dotnet sqlsource describe /work/App --watch
/work/App : error SQLSRC287: watching '/work/App' for changes failed: The configured user limit (128) on the number of inotify instances has been reached, or the per-process limit on the number of open file descriptors has been reached.
    help: set DOTNET_USE_POLLING_FILE_WATCHER=true to poll for changes instead
```

**Fix:** do one of these.

- On Linux, raise the limit that the message names.  For a message about `inotify instances`, as above, raise `fs.inotify.max_user_instances`:
  1. Run `sudo sysctl fs.inotify.max_user_instances=1024`.
  2. Add `fs.inotify.max_user_instances=1024` to `/etc/sysctl.conf` to keep it.

  For a message about `inotify watches`, such as `The configured user limit (65536) on the number of inotify watches has been reached, or the operating system failed to allocate a required resource.`, raise `fs.inotify.max_user_watches` the same way, to `524288`.
- Set the environment variable `DOTNET_USE_POLLING_FILE_WATCHER` to `true` or `1` before you start the watch.  The watch then looks at the folders at a fixed interval instead of asking the system.  That needs no limit, and finds a change within a second or two.

**Polling is also the way to watch a folder on a network share, or in a container that does not pass on file changes.**

- **A watch that polls, with `DOTNET_USE_POLLING_FILE_WATCHER` set, reports this error when it cannot list a folder.**  A folder that you may not read is one such folder.  A folder that was deleted is another, when the watch was not also looking at the folder that held it.  The folder of your repository itself is such a folder, since no watch looks at the folder above it.
- **The error of a watch that polls has no help line.**  The watch reports it once while the folder stays that way.  It sees the changes of the folder again as soon as it can list it.

See: [Describe on save](reference/describing.md#describe-on-save)

## SQLSRC288

**Bundle cannot be read**

Reported by: the tool.

`sqlsource replay <bundle>` runs a query from a bundle, without a database.  The path that you gave is not a bundle that the tool can read.  The message ends with the reason, one of these:

- the path does not exist;
- it is not a zip file or a folder of a bundle;
- a file that every bundle holds is missing;
- a file of it is damaged.

A bundle is a zip file, or an unzipped folder, that someone made of one query for a bug report.

```console
$ dotnet sqlsource replay /work/report.zip
/work/report.zip : error SQLSRC288: The bundle cannot be read: there is no query.sql at the root of the zip
```

**Fix:**

1. Check the path first.
2. Check that the download or the mail that carried the zip file did not cut it short.
3. If the path is right, ask the person who made the bundle for it again.

- **A bundle that was edited by hand is read the same way as a damaged one.**  So is a bundle that was unzipped and zipped again with a file left out.

See: [Checking a bundle with `replay`](reference/bundles.md#replay)

## SQLSRC289

**Bundle is of a newer version**

Reported by: the tool.

A newer version of `sqlsource` than the one you run made the bundle, in a form that this version does not know.  The message names the member that is newer: `bundleVersion` or `captureVersion`, or `formatVersion`, the sidecar format that the answer of the bundle is written in.  Then it names the number that the bundle has and the number that this version reads.

```console
$ dotnet sqlsource replay /work/report.zip
/work/report.zip : error SQLSRC289: The bundle's bundleVersion is 2, and this version of sqlsource reads 1
```

**Fix:**

1. Update the `sqlsource` tool to the latest version.
2. Replay the bundle again.

See: [Checking a bundle with `replay`](reference/bundles.md#replay)

## SQLSRC290

**Bundle does not answer a question of the query**

Reported by: the tool.

`sqlsource replay`, or `sqlsource diagnose --scrub` before it renames a raw bundle, runs the query from what the bundle holds.  The bundle has no answer to something that the query asks.  The message names the question, the version of `sqlsource` that made the bundle, and the version you run.

This happens in one of two cases:

- **You run a version other than the one that made the bundle.**  A different version can ask something that the other never did.
- **A file of the bundle was edited by hand or damaged.**  An answer that it holds can then no longer be read.

An unzipped folder without `bundle.json`, the bundle's own description file, says `another version of sqlsource`, since it does not tell which version made it.

```console
$ dotnet sqlsource replay /work/report.zip
/work/report.zip : error SQLSRC290: The bundle has no answer to 'columns' that the query asks: it was made by sqlsource 1.4.0, and this is sqlsource 1.6.0
```

**Fix:** do one of these.

- Install the version that made the bundle:
  1. Install the version of `sqlsource` that the message names as the maker of the bundle.  For example, run `dotnet tool update SqlSource.Tool --version 1.4.0`.
  2. Replay or scrub the bundle again.
- If the message says `another version of sqlsource`, ask the person who made the bundle which version it was.
- If the message names the same version twice, ask the person who made the bundle for it again.  The bundle was edited by hand or damaged, and installing another version does not help.  A bundle edited by hand cannot be replayed.

See: [Checking a bundle with `replay`](reference/bundles.md#replay)

## SQLSRC291

**Bundle's engine is not supported**

Reported by: the tool.

The bundle is of a query for a database engine that this version of `sqlsource` cannot run a query of without a database.  The message names the engine.

This happens when a newer version, which supports an engine that this one does not, made the bundle.

```console
$ dotnet sqlsource replay /work/report.zip
/work/report.zip : error SQLSRC291: The bundle is of the engine 'mysql', which this version of sqlsource does not describe
```

**Fix:**

1. Update the `sqlsource` tool to the latest version.
2. Replay the bundle again.

See: [Checking a bundle with `replay`](reference/bundles.md#replay)

## SQLSRC292

**Query is not in the file**

Reported by: the tool.

`sqlsource diagnose --query <file>:<name>` makes a bug report of one query, and the query that it names cannot be described from that file.  The message names the query and the file, and ends with the reason:

```console
/work/App/Queries/Users.sql : error SQLSRC292: There is no query 'getUser' to diagnose in '/work/App/Queries/Users.sql': the file has no query of that name
```

The reason is one of two, each with its own fix:

- **`the file has no query of that name`.**  No `-- name:` of the file gives the name after the last `:`.
  - Names are compared exactly, by case too, so `getUser` does not find `GetUser`.
  - Fix: copy the name from the `-- name:` line of the query, letter for letter.
- **`the query's output is sql, which needs no description`.**  The query is in the file, but its output is `sql`.
  - So the query is never described against the database, and a bug report of it would hold nothing.  A query whose output is `sql` gets no description to report.
  - Fix: if its generated code is wrong, open an issue that shows the query and the code you got.

See: [Making a bundle with `diagnose`](reference/bundles.md#diagnose)

## SQLSRC293

**Bundle cannot be made safe to post**

Reported by: the tool.

`sqlsource diagnose` gives every name of your schema and of your query a new name in the bundle that it makes.  The bundle can then be posted in a public issue.  It checks the bundle before it writes it.  Here the check found something that it could not rename safely.  The message gives the reason, the place in the bundle and the text that it found, or `(none)` when the reason names no text.

Rather than blank the text out, `diagnose` writes no bundle.  A bundle with the text blanked out would no longer show the problem.

The text found is yours, such as a name of your schema, so do not paste this error into an issue.

```console
/tmp/report.zip : error SQLSRC293: The bundle cannot be made safe to post: a word is not one of the new names, at query.sql, found ssn_hash
    help: make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it
```

**Fix:**

1. Make a raw bundle instead: run the same command with `--no-obfuscate`.
2. Keep the raw bundle to yourself.  It keeps every name of yours.
3. Open an issue on GitHub that says a raw bundle is ready.
4. Do not attach the bundle.  A maintainer tells you how to send it.

See: [Making a bundle with `diagnose`](reference/bundles.md#diagnose), [When a bundle cannot be made safe](guides/report-a-bug.md#raw-bundle)

## SQLSRC294

**Renamed bundle does not reproduce the query's answer**

Reported by: the tool.

`sqlsource diagnose` checks the bundle that it renamed before it writes it, by running the query from it, as `sqlsource replay` does.  Here the renamed bundle gives another answer than the one your query got from your database.  So the bundle would not show the problem, and no bundle was written.  The message names the place in the bundle where the answers differ.

This is a fault of the renaming, and not of your query.

```console
/tmp/report.zip : error SQLSRC294: The renamed bundle does not reproduce the query's answer: its replay differs at expected.json
    help: make a raw bundle with --no-obfuscate and keep it, then open an issue on GitHub that says a raw bundle is ready, without attaching it: a maintainer will say how to send it
```

**Fix:**

1. Make a raw bundle instead: run the same command with `--no-obfuscate`.
2. Keep the raw bundle to yourself.  It keeps every name of yours.
3. Open an issue on GitHub that says a raw bundle is ready.
4. Do not attach the bundle.  A maintainer tells you how to send it.

See: [Making a bundle with `diagnose`](reference/bundles.md#diagnose), [When a bundle cannot be made safe](guides/report-a-bug.md#raw-bundle)

## SQLSRC295

**Bundle is not a raw bundle**

Reported by: the tool.

`sqlsource diagnose --scrub <bundle>` gives the names of a raw bundle new names, so that the bundle can be posted.  A raw bundle is one that `diagnose --no-obfuscate` made.  Here the bundle that you gave is not a raw bundle, and nothing was written.  The message names the bundle and ends with the reason:

```console
/tmp/report.zip : error SQLSRC295: '/tmp/report.zip' is not a raw bundle: it is renamed already
```

The reason is one of two, each with its own fix:

- **`it is renamed already`.**  `diagnose` without `--no-obfuscate`, or an earlier `--scrub`, made it, and its names are new names already.
  - Fix: post the bundle as it is.  Keep the mapping that was written beside it to yourself.
- **`it has no bundle.json that says it is raw`.**  The bundle has no `bundle.json`, the file in which `diagnose` says how it made a bundle.  Or its `bundle.json` does not say that the bundle is raw, as for a bundle edited by hand, or unzipped and zipped again without it.
  - Fix: make the raw bundle again with `diagnose --no-obfuscate`, and give `--scrub` that one.

See: [Making a bundle with `diagnose`](reference/bundles.md#diagnose), [When a bundle cannot be made safe](guides/report-a-bug.md#raw-bundle)

## SQLSRC296

**Raw bundle is not reproduced by this version**

Reported by: the tool.

`sqlsource diagnose --scrub <bundle>` runs the query from the raw bundle, as `sqlsource replay` does, before it gives its names new names.  Here the version of `sqlsource` that you run gives another answer from what the bundle holds than the one that the bundle records.  So a renamed bundle would not show the problem either, and nothing was written.  The message names the first place where the answers differ, the version that made the bundle, and the version you run.

This happens when another version made the bundle, and its answer to the same query differs.

```console
/tmp/report.zip : error SQLSRC296: This version of sqlsource does not reproduce the bundle's answer, so it is not renamed: columns[0].nullable: true -> false; it was made by sqlsource 1.4.0, and this is sqlsource 1.6.0
```

**Fix:** do one of these.

- Install the version that made the bundle:
  1. Install the version of `sqlsource` that the message names as the maker of the bundle.  For example, run `dotnet tool update SqlSource.Tool --version 1.4.0`.
  2. Scrub the bundle again.
- If the message names the same version twice, the bundle was edited by hand or damaged.  Make the raw bundle again with `diagnose --no-obfuscate`.

See: [Making a bundle with `diagnose`](reference/bundles.md#diagnose), [When a bundle cannot be made safe](guides/report-a-bug.md#raw-bundle), [Checking a bundle with `replay`](reference/bundles.md#replay)

## SQLSRC297

**Transaction isolation level is not the database's**

Reported by: the build.

The error is at the query's name, once for the query.

A query turns on the `collection-method` generator parameter, and has parameters.  It sets the `transaction` generator parameter to an isolation level that its database does not have.  The message names the query, the level and the database.

PostgreSQL has no `read-uncommitted` and no `snapshot`, so a query of the `postgres` dialect cannot ask for either.  SQL Server has all five levels.

The query keeps its own method.  Only its [collection method](reference/collection-methods.md#collection-methods) is not generated until the error is fixed.

```console
/work/App/Queries/Users.sql(2,10): error SQLSRC297: The query 'AddUser' asks for the transaction isolation level 'snapshot', which PostgreSQL does not have
```

**Fix:**

1. Write a level that the database has, or `on` or `off`.

For a query of the `postgres` dialect, these values fit:

- `read-committed`, `repeatable-read` or `serializable`;
- `on`, which begins the transaction at the default level of the database;
- `off`, which begins none.

**A query without `collection-method`, or without parameters, is not reported, whatever `transaction` says.**

See: [A collection method's transactions](reference/collection-methods.md#collection-transactions)

## SQLSRC298

**A stream has no synchronous method**

Reported by: the build.

The error is at the query's name, once for the query.

A query whose output is `repository` or `methods` streams its rows, with the [collection type](reference/return-types.md#setting-collection-type) `IAsyncEnumerable` and the shape `many`.  The generator parameter [`method-type`](reference/generator-parameters.md#setting-generator-parameters) of the query is `sync`.  The message names the query.

A stream reads each row as the caller asks for it, which only an asynchronous method can do.  So there is no synchronous method.  The query gets no method, and no [collection method](reference/collection-methods.md#collection-methods) either, until the error is fixed.  Its models are still generated.

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC298: The query 'ListUsers' streams its rows as an IAsyncEnumerable, which has no synchronous method; give it method-type=async or method-type=both
```

**Fix:** do one of these.

- Write `-- generator: method-type=async` in the query.
- Write `method-type=both` in the same way.  The query then keeps its stream, named with `Async` after the name of the query.  It gets no synchronous method, but for a synchronous [collection method](reference/collection-methods.md#collection-methods) when it has one.
- Give the query a collection type that is read whole, such as `List`, which a synchronous method can return.

**A query of another shape is not reported, whatever its collection type.**

See: [Collection type](reference/return-types.md#setting-collection-type)

## SQLSRC299

**Synchronous method has no connection**

Reported by: the build.

The error is at the query's name, once for the query, beside its other errors.  A query that has no entry in its sidecar yet gets [SQLSRC241](#sqlsrc241), not this error.  It gets this error once it is described, as it gets the other errors about its method.  Its models are still generated.

A query whose output is `repository` would have a [synchronous method](reference/synchronous-methods.md#synchronous-methods), under the generator parameter [`method-type`](reference/generator-parameters.md#setting-generator-parameters) `sync` or `both`.  But the one member that gives the connection of its database is a method that returns a `Task` or `ValueTask` of a connection.  A synchronous method cannot call it.  The message names the query, the database, the type and the member.

Under `method-type=both`, the query keeps its asynchronous method, and its asynchronous [collection method](reference/collection-methods.md#collection-methods) when it has one, on the type and in its interface.  Only the synchronous ones are not generated until the error is fixed.  Under `sync`, the query gets no method.

In this sample, the one connection is a method that returns a task:

```csharp
[SqlSourceGenerate]
public partial class UserRepository
{
    [SqlSourceConnection]
    private Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) => ...;
}
```

```console
/work/App/Queries/Users.sql(2,10): error SQLSRC299: The query 'GetUser' would have a synchronous method, but the one connection of its database 'postgres' in 'App.UserRepository', 'OpenAsync', returns a task, which a synchronous method cannot call.  Mark a member that returns no task [SqlSourceConnection] beside it, or give the query method-type=async.
```

**Fix:** do one of these.

- Mark a second member `[SqlSourceConnection]` beside it that returns no task.  That member is a connection string, a data source, a connection, or a method that returns a connection.  The synchronous methods then get their connection from it, and the asynchronous methods keep the one that returns a task.
- Write `-- generator: method-type=async` in the query, which then has no synchronous method.

**A query under `method-type=async` is not reported.**

See: [Synchronous repository methods](reference/repositories.md#synchronous-repository-methods)
