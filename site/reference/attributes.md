# Attributes

<a id="attributes"></a>

## The three attributes

SqlSource has three attributes that you write on your code.  This table gives each attribute, its properties and the section that tells how to use them.

| Attribute | Property | Section |
|----|----|----|
| `[SqlSourceGenerate]` | `Source` | [The attribute](#attribute), [`Source`](#setting-source) |
| `[SqlSourceGenerate]` | `GenerateInterface`, `InterfaceName` | [Repository interfaces](repositories.md#repository-interfaces) |
| `[SqlSourceConnection]` | `Database` | [The connection](repository-connections.md#repository-connection), [a factory that a repository holds](repository-connections.md#held-factory), [a static member of the project](repository-connections.md#static-connection) |
| `[SqlSourceConverter]` | No property | [Converters](converters.md#converters) |

<a id="attribute"></a>

## The attribute

```csharp
[SqlSourceGenerate(Source = "Queries")]
internal static partial class Reports;
```

`[SqlSourceGenerate]` marks a type that the generator adds members to.  The package adds this attribute, two other attributes and three exceptions to your project, in the namespace `SqlSource`: see [what the package adds](#what-the-package-adds).

- **It goes on a class, a struct, a record or a record struct.**  The type can be static, generic, or nested in other types.
- **The queries of a static type need an output other than `repository`** ([SQLSRC034](../diagnostics.md#sqlsrc034)).  A static type has no instance.  So in the sample, the files of `Reports` must set the output `methods`, `models` or `sql`.
- **The type and every type that contains it must be `partial`** ([SQLSRC001](../diagnostics.md#sqlsrc001)).  The generator adds its members in a second declaration of each.
- **Neither the type nor a type that contains it can be `file`** ([SQLSRC002](../diagnostics.md#sqlsrc002)).
- **A type with an error gets no members, and asks for no models.**  Your code that uses its members then fails to compile too.  So fix the first error that the build reports.

The attribute has three properties:

| Property | Sets |
|----|----|
| `Source` | Which `.sql` files the type claims: [Source](#setting-source) |
| `GenerateInterface` | Whether a repository gets its interface, over the project's choice: [repository interfaces](repositories.md#repository-interfaces) |
| `InterfaceName` | The name of a repository's interface, `IUserRepository` by default: [repository interfaces](repositories.md#repository-interfaces) |

Every other setting of a query comes from its `.sql` file, through its markers and the [settings files](settings-file.md#settings-files) of its folder.  It does not come from the type that claims the query.  This is true of the [output](settings.md#setting-output) of the query and of [where its members go](member-placement.md#locations).

<a id="files-of-a-type"></a>

## Which files belong to a type

```text
Repository/
    UserRepository.cs      [SqlSourceGenerate] on UserRepository
    UserRepository.sql     belongs to UserRepository: the file named for it
    Orders.sql             not UserRepository's: another name
    Reports/
        Monthly.sql        not searched: it is in a subfolder
```

A type claims the file named for it, beside the C# file that carries the attribute, or the files that its [`Source`](#setting-source) names.  `Source = "."` claims every `.sql` file of that folder.  [`Source`](#setting-source) gives the rules and the errors.  This section says what the generator does with the files that a type claims.

- **The files of a type are one set.**  A query name can be used only once across all of them.  A second use is [SQLSRC008](../diagnostics.md#sqlsrc008).  The file with the second use then gets no members and no models for that type.
- **Two types can claim one file.**  Each type gets its own members from it.  Two types of one namespace can give one method twice: see [where the methods go](connection-methods.md#method-location).
- **Two types that claim one file take the same settings from it.**  Every setting of a query comes from the file and its settings files.  So both types get one output, one member location, one suffix and one shape of model.  A type that needs other settings needs a file of its own.
- **Two types in one namespace share the models of the queries of the file.**  Two types in two namespaces get a model each, unless the [namespace of the models](models.md#setting-model-namespace) gives them one ([shared models](shared-models.md#shared-models)).
- **A `.sql` file that no type claims is ignored**: see [Source](#setting-source).
- **A file with an error in how it is written produces no members until you fix it**: see [`.sql` files](errors.md#what-an-error-costs).  The other files of the type are unaffected.
- **An error about describing a query, or about its sidecar, leaves the query without models until you fix it.**  [SQLSRC241](../diagnostics.md#sqlsrc241) is such an error.
  - Under `repository` or `methods`, the query then gets no [method](query-methods.md#which-methods).
  - Under `models`, the query keeps its constant or token method.

<a id="setting-source"></a>

## Source

```csharp
[SqlSourceGenerate]                                  // UserRepository.sql, beside this C# file
public partial class UserRepository;

[SqlSourceGenerate(Source = ".")]                    // every .sql file beside this C# file
public partial class StoreQueries;

[SqlSourceGenerate(Source = "Queries/Users.sql")]    // one file
public partial class UserQueries;

[SqlSourceGenerate(Source = "../Queries")]           // the .sql files of a folder
public partial class SharedQueries;
```

`Source` says which `.sql` files a type claims.  It is a property of the attribute, and you can write it nowhere else.

- **A `Source` that ends in `.sql` names one file.**
- **Any other `Source` names a folder, and the type claims the `.sql` files of that folder.**  `Source = "."` is every `.sql` file in the folder of the C# file.  SqlSource does not search subfolders.
- **Without `Source`, the type claims the one `.sql` file named for it.**  The file is in the folder of the C# file that carries the attribute: `UserRepository.sql` for `UserRepository`.
- **The name of that file is the name of the type itself, without its type parameters.**  So `Repository<T>` claims `Repository.sql`, and a nested type `Outer.Inner` claims `Inner.sql`.
- **For a partial type, the folder is the folder of the file that carries the attribute.**
- **An empty `Source`, `Source = ""`, is the same as no `Source`.**
- **A `Source` of white space is not the same as no `Source`.**  SqlSource reads it as the name of a folder, like any other `Source`, so it matches nothing.
- **`Source` is relative to the folder of the C# file, never to the project.**  Both `/` and `\` separate folders.
- **SqlSource ignores case when it compares paths**, so that a project builds the same on every operating system.  Two `.sql` files whose paths differ only by case are [SQLSRC013](../diagnostics.md#sqlsrc013) when a type claims that path.
- **A `Source` that matches nothing is [SQLSRC004](../diagnostics.md#sqlsrc004).**
- **If there is no `Source`, and no file is named for the type, that is [SQLSRC005](../diagnostics.md#sqlsrc005).**
- **Two types can claim one file**: see [which files belong to a type](#files-of-a-type).
- **A `.sql` file that no type claims is ignored.**  So a folder of migration scripts elsewhere in the project does no harm.

A type can claim only a file that the project includes.  [Which files the compiler is given](msbuild.md#included-files) is a separate rule, and it applies first.  The attribute itself is described under [the attribute](#attribute).

<a id="what-the-package-adds"></a>

## What the package adds to your project

`SqlSource` is the generator.  Add the package to each project whose queries you want generated.

- **The package adds no runtime dependency.**  The generator runs in the compiler when you build.  No assembly of it ships with your application, and it is not a dependency of a package that you build.
- **Only what the generator writes is compiled into your project.**  That is the code it generates for your queries, and the types in this list.
- **Three attributes**, as internal types in the namespace `SqlSource`:
  - [`[SqlSourceGenerate]`](#attribute), which you mark your types with;
  - [`[SqlSourceConnection]`](repository-connections.md#repository-connection), which you mark their connection sources with: a member of a repository, of a [factory that it holds](repository-connections.md#held-factory), or a [static member](repository-connections.md#static-connection) of any type of the project;
  - [`[SqlSourceConverter]`](converters.md#converters), which you mark your converters with.
- **Three exceptions that a query method throws**, as internal types in the namespace `SqlSource`: [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count), [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back) and [`CollectionItemException`](exceptions.md#collection-item-exception).
  - If your project uses the exceptions of another project, the package leaves them out.
  - If your project sets [`SqlSourcePublicExceptions`](exceptions.md#public-exceptions), they are public.
  - See [projects that share internals](exceptions.md#projects-that-share-internals).
- **`Microsoft.CodeAnalysis.EmbeddedAttribute`**, an internal attribute, in the generated file `SqlSource-EmbeddedAttribute.g.cs`.  It keeps the three attributes out of sight of other projects, but not the exceptions.  If your project declares it too, see [the limit](#limit-embedded-attribute).
- **Code that uses these types needs `using SqlSource;` and nothing else.**

<a id="limits"></a>

## Limits

<a id="limit-source-written-as-text"></a>

### SQLSRC208, and nothing is described, for a type whose `Source` is a constant or `nameof`

**What you see.**  One of these:

- `sqlsource describe` reports [SQLSRC208](../diagnostics.md#sqlsrc208) and describes nothing for the type.  The `Source` of the type is a constant, `nameof`, a concatenation or a cast.  The build then reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each query of the type that needs an entry in its sidecar and has none.  Under a dialect that has no database to ask, such as `ansi`, the build reports [SQLSRC209](../diagnostics.md#sqlsrc209) for the file instead.
- The attribute is inside an `#if` that holds in the configuration you build and not in the one the tool reads, such as `#if RELEASE`.  The tool does not describe the queries of the type, and the build reports SQLSRC241 for them in the same way.

**Why.**  The tool reads the attribute without compiling your code.  `sqlsource describe` finds `[SqlSourceGenerate]` by reading your C# files as text.  So the tool can read `Source` only when it is written out where the attribute is, as a string: `Source = "Queries"`.  A constant, `nameof`, a concatenation or a cast there compiles, but the tool cannot read it.  The tool finds the attribute by its name, `[SqlSourceGenerate]` or `[SqlSourceGenerateAttribute]`, or by an alias of it: a `using` alias in the file or around the type, a `global using` alias, or a `<Using>` item with an `Alias` in the project.  It reads an attribute inside `#if` with the compilation constants of one configuration, `Debug` unless you give another or your environment sets one.

**What to do.**

1. Write `Source` as a string where the attribute is.
2. For an attribute inside `#if`, describe in the configuration you build: `dotnet sqlsource describe --configuration Release`.  See [`--configuration`](cli.md#options).
3. Write the attribute outside `#if` where you can, so that every configuration describes the same queries.

<a id="limit-attribute-of-the-same-name"></a>

### Sidecars for a type that has an attribute of your own named `SqlSourceGenerate`

**What you see.**  `sqlsource describe` describes the `.sql` files of a type whose `[SqlSourceGenerate]` is an attribute of your own, in a namespace of yours, and not SqlSource's.  It writes their sidecars, and the build generates nothing for the type.

**Why.**  The tool reads the attribute without compiling your code, so it knows an attribute by its name and not by its namespace.  An attribute of yours named `SqlSourceGenerate` or `SqlSourceGenerateAttribute` reads as SqlSource's.

**What to do.**  Give your attribute another name.  Or set the [output](settings.md#setting-output) of those files to `sql`, so that the tool describes none of their queries.

<a id="limit-embedded-attribute"></a>

### A compile error about `Microsoft.CodeAnalysis.EmbeddedAttribute`

**What you see.**  The build fails with the compiler's errors about your declaration of `Microsoft.CodeAnalysis.EmbeddedAttribute`.  You get one of these, each with more errors after it:

- CS0260 when it is not `partial`;
- CS0262 when it is `public`;
- CS0263 when it derives from another class;
- CS0441 when it is `static`;
- CS0261 when it is a record.

**Why.**  SqlSource adds `internal sealed partial class EmbeddedAttribute : System.Attribute`, in the namespace `Microsoft.CodeAnalysis`, to every project.  A project that declares `Microsoft.CodeAnalysis.EmbeddedAttribute` itself compiles only when its declaration can be joined to SqlSource's.  This is true of a declaration in your code, and of one in code that another package adds to your project.  The declaration must be an `internal partial class` that derives from `System.Attribute` or names no base class, `sealed` or not.  SqlSource cannot report the problem before the compiler does.

**What to do.**  Make the declaration an `internal partial class`, or remove it.
