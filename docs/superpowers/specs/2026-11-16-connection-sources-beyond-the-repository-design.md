# Connection sources beyond the repository

## Intent

Today a repository's connection source must be a member of the repository, of a base type it can reach, or of its primary constructor ([`ConnectionCandidateReader`](../../../src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs)).  A `[SqlSourceConnection]` on any other type compiles and is ignored.  A project that opens connections through a shared factory, or keeps its connection string in a static class, must write a forwarding member into every repository:

```csharp
[SqlSourceGenerate]
public sealed partial class UserRepository(IDatabaseFactory dbf)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken ct) => dbf.Open(ct);
}
```

The change lets the mark live where the connection is made, once, and lets every repository use it with no glue:

```csharp
public interface IDatabaseFactory
{
    [SqlSourceConnection]
    Task<DbConnection> Open(CancellationToken ct);
}

public static class Db
{
    [SqlSourceConnection(Database = "reports")]
    public static NpgsqlDataSource Reports { get; set; } = null!;
}

[SqlSourceGenerate]
public sealed partial class UserRepository(IDatabaseFactory dbf);   // calls dbf.Open(ct)
```

## Goals

- A repository gets its connection from a marked **instance member of a type it holds** (a *held factory*): the type of one of its fields, properties or primary-constructor parameters, or of a reachable member of a base type.  The holder needs no mark.
- A repository gets its connection from a marked **static member anywhere in the project** (a *global*), with no holder and no wrapper.
- Every kind of member that can be a source on a repository today can be one in both new places: a connection string, a data source, a factory method, a borrowed `DbConnection`.  The rules of each kind are unchanged: a method is a factory whose connection the method disposes, and a held connection is borrowed.
- The most specific source wins, so adding a global never changes what an existing repository uses.

## Non-goals

These were ruled out, and are not deferred work (no `docs/deferred` entry):

- A factory method that takes the logical database's name, such as `Open(string database, CancellationToken ct)`.  A marked method still takes nothing or one `CancellationToken`.
- Factory types the user cannot mark, such as a library's that does not reference SqlSource.  The user bridges them with a marked member of their own.
- Globals in referenced assemblies.  Only marks in the project being built are global.  A held factory in a referenced assembly is found, as a marked member of a base type in metadata is today: `[SqlSourceConnection]` is not `[Conditional]` and stays in a library's metadata.
- Following a factory held by a factory.  The holder walk goes one level deep.
- Steering users away from questionable connection management, such as one `DbConnection` property of a shared factory that every repository borrows.

## Anti-goals

- A global silently overriding a repository's own source.
- Noise: a shared factory or a global reporting a warning in every repository that has no use for one of its marks.
- A broken global mark reported once per repository.

## Constraints

- Rules of `AGENTS.md`: the user documentation, `site/diagnostics.md`, `docs/architecture/generator.md` and the example projects change in the same PR; any shortfall is recorded in `docs/tech-debt` or `docs/deferred`.
- The generator is incremental and every value between its steps is equatable.  `RepositoryShapes` is already a `Select` over `CompilationProvider` and runs on every compilation ([`SqlSourceGenerator.cs:389`](../../../src/SqlSource/SqlSourceGenerator.cs)), so it can take a project-wide value as an input without a new rebuild pattern.
- Diagnostic ids keep their meaning: no new id is needed by this design.

## Approaches

**A. Collect the globals once, then hand them to each repository.**  *Chosen.*  A new stage finds every `[SqlSourceConnection]` in the project through `ForAttributeWithMetadataName`, keeps the static members as globals, and reports a broken global once at its mark.  The global set is combined into `RepositoryShapes`, whose reader also walks each held member into its type.  The resolver works through three tiers.  Its cost: two paths read marks, the global stage and the holder walk, so the rules for what a mark may be must be one shared piece of code.

**B. Each repository scans the project itself.**  One reading path, no new stage, but every type of the project is walked once per repository, a broken global is reported once per repository or needs deduplication, and it does not separate the global candidates from the repositories as the owner asked.

A third way, one walk of every type's symbols instead of the attribute index, is approach A done more slowly, and was dropped.

## Behaviour

### The three tiers

Each database a repository needs is resolved on its own, from the first of these tiers that has any candidate for it:

1. **The repository's own members**, its reachable base members and its primary-constructor parameters.  Within this tier today's rules hold unchanged: once one is marked, none is found by its name or type.
2. **The marked instance members of the factories it holds.**
3. **The globals of the project.**

- **In tiers 2 and 3, a mark without `Database` is a default** (the owner's decision): for each database, the tier's marks that name it are its candidates, and only when none does are the tier's marks without `Database` its candidates, on a repository of one database or of several.  So `Db.Main` without `Database` and `Db.Reports` with `Database = "reports"` give `reports` to `Db.Reports` and every other database to `Db.Main`.  Tier 1 keeps today's rule, in which a mark without `Database` is for a repository's one database and `SQLSRC030` on a repository of several.
- Two candidates for a database in the deciding tier are `SQLSRC029`, as today, unless they are a pair of which exactly one returns a task.  A pair is never formed across tiers, nor between a mark that names the database and a default.
- A lower tier is never consulted for a database that a higher tier decides, even when the higher tier's candidate is broken: a broken candidate fails the database, as today.  *(My call:)* a database counts as decided by a tier when anything in that tier fails it today: an unsupported mark without `Database`, which stands for every database, in tier 1 a mark without `Database` on a repository of several databases (`SQLSRC030`), which fails each database no other mark names, and, in tier 1, a primary-constructor parameter that the conventions would take but that the type passes to its base type or reads in an initializer, which stays `SQLSRC028` with today's advice.  Otherwise `Repo(string connectionString) : Base(connectionString)`, an error today, would silently start using a global.
- `SQLSRC028` is reported only when no tier has a candidate.  Its advice mentions the new places a source can be.
- This replaces an earlier idea in the conversation that a factory's mark would beat a convention of the repository: a `connectionString` field found by name beats a held factory.

### Held factories

- **Holders** are the repository's fields and properties, those of its base types that it can reach, static ones too, and its primary-constructor parameters, whose type is not itself a source type (`string`, `DbDataSource`, `DbConnection` or a type derived from them).  A holder that is marked is a tier 1 source, not a holder.
- **Only a member whose type has candidates is a holder** *(my call)*: a field that holds an `ILogger` is not one.
- **Parameters follow today's rule** *(my call)*: a primary-constructor parameter is a holder only when the type has no field or property holder, and not when the type passes it to its base type or reads it in an initializer (`CS9107`, `CS9124`).  So `UserRepository(IDatabaseFactory dbf) { private readonly IDatabaseFactory _dbf = dbf; }` holds `_dbf` alone, and `UserRepository(IDatabaseFactory dbf) : RepositoryBase(dbf)` holds the base type's member.
- **The factory's candidates** are its marked instance members: fields, properties and methods of the holder's type, of its base types, and of the interfaces it implements, that the repository can reach.
- **A member that implements a marked interface member is marked as it is**, `Database` included *(my call)*, as an override of a marked member is today.  So a holder typed by the class `DatabaseFactory` finds the mark on `IDatabaseFactory.Open`, whether `DatabaseFactory` implements it implicitly, explicitly or by the interface's default; a member that the class cannot be called by is reached through a cast of the holder to the interface, `((global::App.IDatabaseFactory)(dbf ?? throw ...)).Open(cancellationToken)`.  This applies to factory types alone: tier 1 keeps today's rule, so a repository that implements an interface with a marked member gains no mark and keeps what it resolves to today.
- **Static members of a factory type are globals**, not reached through a holder, so they are never found twice.
- **Two holders of factories with marked members are two candidates** (`_main.Open`, `_audit.Open`): `SQLSRC029` unless their `Database`s differ or they make a pair.
- **`Database`** names the database a mark is for, and a mark without it is a default, as the tiers say.  A factory's mark for a database the repository does not use is ignored silently, not `SQLSRC031`.

### Globals

- **A global** is a marked static field, property or method in the project's own source, in a static class or not, outside every type marked `[SqlSourceGenerate]`.  A static member of a repository's base type is a global of other repositories, and never of its derived repositories, whose own member it is in tier 1, found there or hidden by a member of its name.
- **A repository sees a global only when it can reach it.**  A global it cannot reach is not a candidate, and nothing is reported.
- **A global's mark for a database the repository does not use is ignored silently.**
- **A global without `Database`** is a default, as the tiers say.
- **A shared mark of one driver's type, without `Database`, is not a candidate for a database of the other engine** *(my call)*.  This holds for globals and for held factories' members alike: an `NpgsqlDataSource` without `Database` is ignored by a SQL Server repository, rather than `SQLSRC033` in each, so a shared type serving both engines does not need `Database` on every mark.  A mark of a type of neither driver, such as `string` or `DbDataSource`, is a candidate for both.  A mark whose `Database` names the repository's database is never filtered: the wrong engine there is `SQLSRC033`, at the holder for a factory and at the repository's attribute for a global.
- **A global in a generic type** is `SQLSRC032`, reported at its mark *(my call)*: no type arguments can be chosen for it.  So is a `static abstract` or `static virtual` interface member.
- **A mark the global stage finds on anything else outside a repository**, a parameter of a method or of a non-repository primary constructor, is `SQLSRC032` at its mark *(my call)*: it can never be a source.  Instance members outside a repository are not reported by this stage: they are factory members, read through holders.
- **A broken global is reported once, at its mark**, by the global stage, and is never a candidate.  "Broken" is what makes a member `CandidateType.Unsupported` today, plus the two cases above.

### Diagnostics

- `SQLSRC028`, `SQLSRC029` and `SQLSRC034` stay at the repository's attribute.  `SQLSRC029` lists candidates as the method reaches them: `dbf.Open`, `Db.Reports`.
- A problem of a held factory's member (`SQLSRC032`, `SQLSRC033`) is reported at the holder, where the repository names it, since the factory may be in another assembly, and its message names both: `'IDatabaseFactory.Open' reached through 'dbf'`.  When several repositories hold a broken factory, each reports it.
- `SQLSRC036` applies to a parameter holder as to a parameter source: a holder parameter named `cancellationToken`, or `parameters` when a query of the database has parameters, would be hidden.
- A static repository stays `SQLSRC034`, globals or not.

### Generated code

- A held member is reached through its holder: `this._dbf.Open(cancellationToken)`, `dbf.Open(cancellationToken)`, or `UserRepository.Factory.Open(...)` for a static holder.
- A global is reached through its fully qualified type, `global::App.Db.Reports`, nested types included, so no local or argument can hide it: its `LeadingName` is null.
- **A null holder throws** "The connection source 'dbf' of 'App.UserRepository' is null." before the member is read.  A null value of the member, or a factory that returns null, names the member as reached: `'dbf.Open'`.  A global's message names `'Db.Reports'`.  Nothing is sent before either throws.
- A struct repository's streaming method copies the holder to a local before its iterator, as it copies a source today.

## Documentation, examples and tests

- `site/reference/repository-connections.md`: the three tiers, held factories and globals, and the rules above.  `site/reference/attributes.md`: where the attribute can go.  `site/guides/repository-connection.md` and `site/guides/several-databases.md`: a factory and a global as the way to share a connection.
- `site/diagnostics.md`: the sections of `SQLSRC028` to `SQLSRC033` and `SQLSRC036` that change.
- `docs/architecture/generator.md`: the new stage among the pipeline's stages and the steps that touch symbols, the shape reader's holder walk, and "The repository's connection" with the tiers.
- The XML documentation of `SqlSourceConnectionAttribute` in `AttributeSource`.
- An example of a held factory and of a global in `examples/PostgresExamples` and `examples/SqlServerExamples`.
- Unit tests of the reader, the global stage and the resolver; generator tests of the generated code and of each diagnostic case above; a caching test that an edit unrelated to a mark leaves the global set equal; a compile test of the generated code of each kind of source through a holder and as a global; an end-to-end query through a held factory and a global on each engine.

## Left to the builder

- The names and shape of the new records: how a candidate records its tier and its holder, and how the global set reaches `RepositoryShapeReader`.
- How the global stage and the holder walk share the code that decides what a mark may be.
- The exact message texts, within the content above.
- How the work is split into commits or phases.

## Open questions

None yet.
