# Attributes

<a id="attributes"></a>

## The three attributes

SqlSource has three attributes that you write on your code.  This table gives each attribute, its properties and the section that tells how to use them.

| Attribute | Property | Section |
|----|----|----|
| `[SqlSourceGenerate]` | `Source` | [The attribute](#attribute), [`Source`](#setting-source) |
| `[SqlSourceGenerate]` | `GenerateInterface`, `InterfaceName` | [Repository interfaces](repositories.md#repository-interfaces) |
| `[SqlSourceConnection]` | `Database` | [The connection](repositories.md#repository-connection) |
| `[SqlSourceConverter]` | No property | [Converters](converters.md#converters) |

<a id="attribute"></a>

## The attribute

`[SqlSourceGenerate]` marks a type that the generator adds to.  The package puts the attribute, the [`[SqlSourceConverter]`](converters.md#converters) and [`[SqlSourceConnection]`](repositories.md#repository-connection) attributes and the exceptions that a query method throws, [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count), [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back) and [`CollectionItemException`](exceptions.md#collection-item-exception), in your project, as internal types in the namespace `SqlSource`, so the code below needs `using SqlSource;` and nothing else.  The exceptions are left out when your project uses another project's, and are public when it sets [`SqlSourcePublicExceptions`](exceptions.md#public-exceptions): see [projects that share internals](exceptions.md#projects-that-share-internals).  It also adds `Microsoft.CodeAnalysis.EmbeddedAttribute`, in the generated file `SqlSource-EmbeddedAttribute.g.cs`, an internal attribute that keeps every one of these types but the exceptions out of sight of other projects: see [the limit](#limit-embedded-attribute) if your project declares it too.

```csharp
[SqlSourceGenerate(Source = "Queries")]
internal static partial class Reports;
```

- **It goes on a class, a struct, a record or a record struct.**  The type may be static, generic, or nested in other types.  A static type has no instance, so its queries need an output other than `repository` ([SQLSRC034](../diagnostics.md#sqlsrc034)): the files of `Reports` above set the output `methods`, `models` or `sql`.
- **The type and every type that contains it must be `partial`** ([SQLSRC001](../diagnostics.md#sqlsrc001)), and none may be `file` ([SQLSRC002](../diagnostics.md#sqlsrc002)).  The generator adds its members in a second declaration of each.
- **A type with an error gets no members**, and asks for no models.  The code that uses them then fails to compile too, so fix the first error the build reports.

The attribute has three properties:

| Property | Sets |
|----|----|
| `Source` | Which `.sql` files belong to the type: [Source](#setting-source) |
| `GenerateInterface` | Whether a repository gets its interface, over the project's choice: [repository interfaces](repositories.md#repository-interfaces) |
| `InterfaceName` | The name of a repository's interface, `IUserRepository` by default: [repository interfaces](repositories.md#repository-interfaces) |

Every other setting of a query, its [output](settings.md#setting-output) and [where its members go](members.md#locations) among them, comes from its `.sql` file, through its markers and the [settings files](settings-file.md#settings-files) of its folder, and not from the type that claims it.

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

A type gets the file named for it, beside its C# file, or the files that its [`Source`](#setting-source) names, `Source = "."` for every `.sql` file of that folder, by the rules and with the errors given there.  What follows is what the generator does with the files.

- **A type's files are one set.**  A query name is used once across all of them: a second use is [SQLSRC008](../diagnostics.md#sqlsrc008), and the file with the second one gets no members and no models for that type.
- **Two types may claim one file**, and each gets its own members from it, though two types of one namespace can give one method twice: see [where the methods go](connection-methods.md#method-location).  They take the same settings from it, since every setting of a query comes from the file and its settings files: one output, one member location, one suffix and one shape of model for both.  Two types in one namespace share the models of the file's queries; two in two namespaces get a model each, unless the [namespace of the models](models.md#setting-model-namespace) gives them one ([shared models](models.md#shared-models)).  A type that needs other settings needs a file of its own.
- **A `.sql` file that no type claims is ignored**: see [Source](#setting-source).
- **A file with an error in how it is written produces no members** until it is fixed: see [`.sql` files](errors.md#what-an-error-costs).  The other files of the type are unaffected.  An error about describing a query or about its sidecar, such as [SQLSRC241](../diagnostics.md#sqlsrc241), leaves the query without models until it is fixed, and so without a [method](query-methods.md#which-methods) under `methods` or `repository`; under `models` it keeps its constant or token method.

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

`Source` is a property of the attribute and nothing else.  It says which `.sql` files belong to the type.

- Without `Source`, the type gets the one `.sql` file named for it in the folder of the C# file that carries the attribute: `UserRepository.sql` for `UserRepository`.  The name is the type's own, without its type parameters, so `Repository<T>` gets `Repository.sql`, and a nested type `Outer.Inner` gets `Inner.sql`.  For a partial type, the folder is the one of the file that carries the attribute.
- A `Source` that ends in `.sql` names one file.  Any other `Source` names a folder, whose own `.sql` files the type gets: `Source = "."` is every `.sql` file in the folder of the C# file.  Subfolders are not searched.
- `Source` is relative to the folder of the C# file, never to the project.  Both `/` and `\` separate folders.  Paths are compared ignoring case, so a project builds the same on every operating system: two `.sql` files of a type whose paths differ only by case are [SQLSRC013](../diagnostics.md#sqlsrc013).
- An empty `Source`, `Source = ""`, is the same as no `Source`.  A `Source` of white space is not: it is read as the name of a folder, like any other `Source`, and so matches nothing.
- A `Source` that matches nothing is [SQLSRC004](../diagnostics.md#sqlsrc004), and no file named for the type when there is no `Source` is [SQLSRC005](../diagnostics.md#sqlsrc005).
- Two types may use the same file.  A `.sql` file that no type uses is ignored, so a folder of migration scripts elsewhere in the project does no harm.

This is separate from [which files the compiler is given](msbuild.md#included-files), which comes first: a type can only claim a file that the project includes.  The attribute itself is on [the Attributes page](#attribute).

<a id="what-the-package-adds"></a>

## What the package adds to your project

- **`SqlSource`** is the generator.  Add it to each project whose queries you want generated.  It runs in the compiler when you build and adds no runtime dependency: no assembly of it ships with your application, and it is not a dependency of a package you build.  Only the code it generates, the attributes you mark your types and converters with, and the exceptions its methods throw, are compiled into your project.

<a id="limits"></a>

## Limits

<a id="limit-source-written-as-text"></a>

### SQLSRC208, and nothing is described, for a type whose `Source` is a constant or `nameof`

- **The tool reads the attribute without compiling your code.**  `sqlsource describe` finds `[SqlSourceGenerate]` by reading your C# files as text, so `Source` must be written out where the attribute is, as a string: `Source = "Queries"`.  A constant, `nameof`, a concatenation or a cast there compiles, and the tool reports [SQLSRC208](../diagnostics.md#sqlsrc208) and describes nothing for the type.  The build then reports [SQLSRC241](../diagnostics.md#sqlsrc241) for each of the type's queries that needs an entry in its sidecar and has none, or, for the file, [SQLSRC209](../diagnostics.md#sqlsrc209) under a dialect that has no database to ask, such as `ansi`.  Two more things follow from the same reading.  An alias of the attribute (`using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]`) works in the build and is not seen by the tool, so the type's queries are not described, and the build reports SQLSRC241 for them in the same way.  An attribute inside `#if` is read with the compilation constants the tool gets from your project, which may not be those of the configuration you build.  Write the attribute under its own name, and outside `#if` where you can.

<a id="limit-embedded-attribute"></a>

### A compile error about `Microsoft.CodeAnalysis.EmbeddedAttribute`

- **A project that declares `Microsoft.CodeAnalysis.EmbeddedAttribute` itself compiles only when its declaration can be joined to SqlSource's.**  SqlSource adds `internal sealed partial class EmbeddedAttribute : System.Attribute`, in the namespace `Microsoft.CodeAnalysis`, to every project.  A declaration of that name in your code, or in code that another package adds to your project, compiles when it is an `internal partial class` that derives from `System.Attribute` or names no base class, `sealed` or not.  Any other fails the build with the compiler's errors, which SqlSource cannot report first: CS0260 when it is not `partial`, CS0262 when it is `public`, CS0263 when it derives from another class, CS0441 when it is `static`, and CS0261 when it is a record, each with more errors after it.  Make the declaration an `internal partial class`, or remove it.
