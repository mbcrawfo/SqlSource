# Settings move from the attribute to the `.sql` file

## Intent

A type now reads the one `.sql` file named for it by default (`docs/superpowers/specs/2026-11-03-attribute-source-design.md`), so the attribute and the file header describe the same thing twice.  The `.sql` file, with its settings files, becomes the one source of truth for what its queries produce.  `[SqlSourceGenerate]` keeps `Source` alone.

Most settings already have a header marker and a settings-file key, so they simply lose the attribute.  Three exist only on the attribute (`SqlLocation`, `MethodLocation`, and `NestedClass`, which also has a settings-file key).  They are replaced by three settings of the file: `member-location`, `visibility` and `nested-class`.

This is a breaking change.  Nothing is released (`VersionPrefix` 0.1.0, no `v*` tag), so nothing keeps the old properties alive: no alias, no obsolete property, no diagnostic for a removed property beyond the compiler's own.

## Goals, non-goals, anti-goals

- **Goals.**
  - The attribute has only `Source`.
  - Every other setting comes from the query's markers, the file header and the settings files of the `.sql` file's folder.
  - Constants, token methods and query methods can be placed on the type, in a nested class or in an extension class, with an accessibility the user chooses.
- **Non-goals.**
  - Any change to `Source` or to how files are claimed.
  - Accessibility of the models: they keep following the type (`docs/manual/generator.md`, "Accessibility").
  - `protected internal` and `private protected`.
- **Anti-goals.**
  - A setting that the generator and the tool read from different places.  The tool must still find, without the generator, every query that needs describing.
  - Defaults that produce an error in a file that sets nothing.
  - A type whose output changes because another type claims the same file.  Each type's result must be decided by the file and its own name alone.

## Approaches

- **Two pull requests, split by concern (chosen),** after two pull requests of test refactoring (below).
  - The first removes the attribute properties whose settings already exist elsewhere.
  - The second builds the new placement model.
  - The first is mostly removal and shrinks the tool.  The second holds the hard engineering, moving the nested class's resolution after the files are parsed, and gets a review of its own.
- **One pull request (rejected).**  One round of docs and examples, but a review spanning the generator, the tool, diagnostics, manual and every example at once.
- **Add the new settings, then deprecate the properties (rejected).**  This suits a released package.  Here it would pay for docs and tests of an in-between state that no user ever sees.

Nothing is released between the two pull requests, so the in-between state needs no user-facing care beyond keeping every doc true for its own commit.

### Test refactoring first: two more pull requests

The database test projects claim one `.sql` file from several types with different attribute settings.  Under this design a file gives every type that claims it the same settings, so those claims must go.  Each of the two refactors below uses only what works today, changes no product code, and lands on `main` before pull request 1.  They are independent of each other.

- **Pull request A: variants move into the files.**  Wherever a test project claims one file twice to get a variant, the variant becomes a query of its own in that file.  It carries the query-level marker that makes it a variant, and the file has one claim.  The cases:
  - `Accounts.sql`, claimed again under `no-array-pool` (`tests/DatabaseTests/Postgres/EndToEnd/AccountQueries.cs` and `tests/DatabaseTests/QueryMethodAllocation/Postgres/AccountQueries.cs`).  The unpooled copies get `-- generator: no-array-pool`.
  - `Collections/Claimed.sql`, claimed once per collection type (`tests/DatabaseTests/SqlServer/EndToEnd/CollectionQueries.cs`).  Each copy gets `-- collection-type:`.
  - The `Models` claims for Dapper of `AllTypes.sql`, `Notes.sql`, `NewTypes.sql` and `Writes.sql` (`DapperQueries.cs`, `AllTypesSql.cs`, `NoteSql.cs`) beside their `CodeGen` claims.  Their copies get `-- output: models`.
  - Any other double claim of one file with different settings in `tests/DatabaseTests`, found by the builder.

  Sidecars are written again by `tools/describe-end-to-end.sh`, and the tests call the variants by their new names.
- **Pull request B: the realistic corpus is claimed once.**  Each case keeps one claim, at `CodeGen`, in both engines' `Realistic/Generation/RealisticMethods.cs`, and `RealisticQueries.cs` goes.  The Dapper tests get a case's SQL by running the generator in-process on the case's file at `models`, through the generator test harness (`tests/SqlSource.Testing/Generator/`).  They no longer read a compiled constant.  The models come, as now, from the compiled `CodeGen` claim.  The corpus keeps one query per case.  `RealisticClaimsTests`, the realistic AGENTS.md files and `tests/DatabaseTests/*/README.md` follow.

`SharedModelTests` and the other generator unit tests that set removed properties are rewritten in pull request 1, since they test the attribute level that it removes.

## Pull request 1: remove the mirrored properties

**Removed from the attribute:** `Output`, `Parameters`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType` and `CollectionType`.  The enums used only by them, `GeneratorOutput`, `GeneratorModelType` and `GeneratorCollectionType`, leave `AttributeSource.Text`.

**Stays until pull request 2:** `Source`, `SqlLocation`, `NestedClass` and `MethodLocation`.

**Precedence.**  The attribute level disappears from every removed setting's chain: query, file header, nearest settings file, farther settings files, default.  `docs/manual/settings.md` "Which place wins" shows `Output` as its example and must be redrawn.  The generator-parameters chain also loses its step 3.

**The tool.**
- `AttributeReader` reads `Source` and the type's name, and stops reading `Output`.
- `TypeClaim` loses its output.  Which queries need describing now comes from markers and settings files alone.
- SQLSRC208 stays, and names only `Source`.

**Diagnostics.**  SQLSRC006 loses every case for a removed property and keeps those of `SqlLocation`, `MethodLocation` and `NestedClass`.

**Two types claiming one file** can no longer ask for different outputs, suffixes or model shapes from it.  `docs/manual/generator.md` "Which files belong to a type" says today that each type's attribute applies to its own members.  That sentence changes.

**Users of the removed properties move to header markers:**
- `examples/*/Sql/CountQueries.cs`, whose `Output = GeneratorOutput.Sql` becomes `-- output: sql` in its file.
- `tools/package-install/Queries.cs`, whose second claim `KeptQueries` (`Users.sql` again, with `Parameters = "keep-comments"`) goes, with its lines in `Program.cs` and `expected-output.txt`.  Moving the parameter to `Users.sql`'s header would reach the first claim too, and its list would replace the project's `no-token-validation`.  `Kept.sql` already shows `keep-comments` in a file header.
- Every test that sets one of them.

## Pull request 2: placement and accessibility belong to the file

### The new settings

| Setting | Values | Written as | Default |
|----|----|----|----|
| Member location | `ExtensionClass`, `Class`, `NestedClass` | `-- member-location:` in the file header and inside a query; `memberLocation` in a settings file | From the query's output: `NestedClass` for `sql` and `models`, `ExtensionClass` for `codegen` |
| Visibility | `Public`, `Internal`, `Protected`, `Private` | `-- visibility:` in the file header and inside a query; `visibility` in a settings file | From the query's member location: see below |
| Nested class | A C# identifier that is not a keyword | `-- nested-class:` in the file header and inside a query; `nestedClass` in a settings file | `Sql` |

- **Matching.**  Values match as every listed value does today, ignoring case, hyphens and spaces, so `nested-class` and `NestedClass` are one value.
- **Precedence.**  Each of the three resolves for each query on its own, by the ordinary chain: the query's marker, the file header's, the nearest settings file, farther settings files, the default.  There is no attribute level and no special fallback.  A value that reaches a query applies to it, whatever its output, and a combination that does not fit is an error (below).
- **Mixing within a file.**  A file may send its queries to different locations, nested classes and visibilities: some `codegen` methods public and others private, some constants on the type and others in a nested class, or a file that feeds two nested classes, `Users.Get` and `Orders.List`.
- **Settings files.**  The settings files consulted are those of the `.sql` file's folder and above, as for every other setting.  `nestedClass` used to be read from the folder of the type's C# file.  The two folders are the same for a type that reads its default file.  The manual says so where it explains the change of folder.
- **Per file.**  A type whose files place their members differently is fine: several files on one type likely have different purposes.
- **Mixed outputs and a header visibility.**  A file that sets no `visibility` gives each query the default for its location, so `sql` constants go to a private nested class and `codegen` methods to an internal extension class, as today.  A file whose header sets a `visibility` gives it to every query, including those where it does not fit.  For example, `-- visibility: private` with a `codegen` query that defaults to `ExtensionClass` is an error.  The user then writes `-- visibility:` or `-- member-location:` on those queries.  The error's message says so.  This is the owner's call: an explicit error over a hidden fallback.

### What visibility means at each location

| Member location | `visibility` sets | Members |
|----|----|----|
| `Class` | Each constant, token method and query method on the type | Static members of the type |
| `NestedClass` | The nested class's own accessibility | Always `public`, inside it.  Query methods there are static methods that take the connection, `Sql.GetUser(connection, ...)`: a nested class cannot hold extension methods (CS1109).  This is new: today `codegen` methods never go in the nested class |
| `ExtensionClass` | Each query method's accessibility | Extension methods of `DbConnection` in `<Type>Extensions`, as today.  Within one type, the class takes the widest accessibility of that type's methods.  Two types that share one extension class (same name, same namespace) whose widest accessibilities differ stay SQLSRC276, as today: widening across types would make one type's output depend on another type's files.  (Designer's call) |

### Defaults

The default visibility follows the resolved member location, never the output.  That way a file with `sql` and `codegen` queries in one nested class cannot clash by default.

| Member location | Default visibility | Why |
|----|----|----|
| `NestedClass` | `Private` | Today's output exactly: public members in a private nested class |
| `Class` | `Public` when the type and every type around it are public, else `Internal` | Today's `SqlLocation.Direct` writes `public`, which has this effect |
| `ExtensionClass` | `Internal` | The owner's choice.  It changes today's output: a public type's extension class becomes internal, so a library no longer puts `DbConnection` extensions in its consumers' IntelliSense without asking |

### Combinations that are errors

| Combination | Reason |
|----|----|
| `ExtensionClass` on a query whose output is `sql` or `models` | A constant or token method extends nothing.  The message suggests `NestedClass` or `Class` |
| `ExtensionClass` with `Private` or `Protected` | A top-level class cannot be either.  The message suggests `Class` (`MethodLocation.Private` users will hit this) |
| `ExtensionClass` with `Public` on a type that is not effectively public | The models it takes are internal, so the generated method would not compile (CS0051) |
| `Protected` where C# forbids it: under `Class`, on a static class (CS1057) or a struct or record struct (CS0666); under `NestedClass`, on a struct or record struct (CS0666) | Only what the language forbids is an error, the owner's rule for `protected`.  A protected nested class inside a static class compiles, so `NestedClass` with `Protected` on a static class is allowed |
| Two queries of one type, in one file or in two, that put members in one nested class with different visibilities, each written somewhere (a query marker, a header marker or a settings file) | One class cannot have two accessibilities.  The error names both places.  A query whose visibility is only the default takes the visibility written for its class by another query, without error: a written value beats a default.  Every file involved loses its members for the type, since neither value is more right.  Two different nested classes, each with its own visibility, are fine |

Allowed, with no diagnostic:
- `Class` with `Public` on an internal type.  This is legal C#, and the members are effectively internal.
- `NestedClass` with `Public` or `Internal`.  Code outside the type can then use `UserRepository.Sql.GetUser`.
- `Protected` on a sealed class or sealed record.  It is legal C#, if illogical (owner's call).  The compiler's warning CS0628 is a warning about the user's choice, so the generated code suppresses it with `#pragma warning disable CS0628` around those members.  Otherwise a project with warnings as errors would fail on a file it did not write.
- `nested-class` set where the member location is not `NestedClass`.  It is ignored, as today; a folder-wide `nestedClass` beside a `Class` file is normal.

Each error leaves that file without members for that type, as other marker errors do today.  It is reported at each query where the combination does not fit, at the query's own marker when it has one, and otherwise at its `-- name:` line.  The message names where the inherited value came from, the header or a settings file, and suggests writing `-- visibility:` or `-- member-location:` on the query.  The builder confirms that this matches the generator's existing reporting.

### The attribute and the code that goes

- **The attribute.**  `AttributeSource.Text` keeps `SqlSourceGenerateAttribute` with `Source` alone, and `SqlSourceConverterAttribute`.  `SqlLocation`, `MethodLocation`, the `SqlLocation`, `MethodLocation` and `NestedClass` properties, and their constants go.
- **The settings code.**  `MemberPlacement` and `MethodPlacement` are replaced by the new settings, read through `SettingId` and the existing `SettingsLevel` machinery.  `SettingPlaceKind.Attribute` goes, with its rank in `SettingPlaces.Specificity`.
- **SQLSRC006.**  It has no remaining cases.  Retiring the id follows whatever the project does for a retired diagnostic, and the builder sets that up if there is no precedent.  `docs/manual/diagnostics.md` and its test must agree.
- **The tool.**  It needs no new reading, because placement does not affect describing.  It must accept the new header markers without error, through the shared parser.

### Diagnostics that change

- **SQLSRC007 (a member named like the nested class).**  Checked against every nested-class name that any of the type's files uses.  Its fix text stops naming `SqlLocation.Direct`.
- **SQLSRC009 (a query named like its containing class).**  Also covers `codegen` methods under `NestedClass`, where a method named like its class is CS0542.
- **SQLSRC274 (method name clash).**  Gains the case of a method named like another member of its nested class, a member that can now come from another file of the type.  A name must also not clash with any of the type's nested classes.  Its text stops naming `MethodLocation`.
- **SQLSRC276 (extension class clash).**  Its causes stay.  "Two accessibilities" now compares each type's widest method accessibility.  Its fix stops naming `MethodLocation`.
- **New ones.**  One per row of "combinations that are errors", or fewer where a single id reads well.  Numbering is the builder's call, in the generator's range.

### Test claims that differ only by placement

Some test projects claim one file from several types that differ only by `SqlLocation` or `MethodLocation`: `Locations.sql` in both engines' end-to-end projects, and the PostgreSQL `AccountReports` claim.  No query-level marker for placement exists before this pull request, so pull request A cannot move them.  This pull request does: each variant becomes a query of its own with `-- member-location:` and `-- visibility:`, and the file has one claim.

### Engineering note

The nested class is resolved per type today, before any query is parsed (`NestedClassCheck.cs`, `QuerySettings.cs`).  Its name and visibility now come from the files, so the check moves after parsing.  The incremental pipeline must keep a type's result dependent only on its own files, their settings files and the type's syntax.

## Documentation and examples (both pull requests, each for its own part)

- **`docs/manual/settings.md`.**  The "All settings" table, "Which place wins", "Where the SQL goes" and "Where the methods go" merge into one section for member location and visibility.  "Name of the nested class" changes too.  The "exist only on the attribute" exception reduces to `Source`.
- **`docs/manual/generator.md`.**  The attribute's table shrinks to `Source`.  "Where the members go" and "Where the methods go" move to the new model.  It states plainly that models follow the type's accessibility while members follow `visibility`.  It warns that a folder-wide `memberLocation: ExtensionClass` is an error on every `sql` query below it.  It also warns that a header or folder-wide `visibility` reaches every query, so a file that mixes outputs may need `-- visibility:` on some queries.  It shows that case as an example.
- **Other docs.**
  - `docs/manual/sql-files.md`: the marker index, with the three new markers allowed in the file header and inside a query.
  - `docs/manual/diagnostics.md`.
  - `README.md` and `docs/manual/recipes.md`, where they show attribute properties.
  - `docs/architecture/generator.md` and `tool.md`.
  - The `AGENTS.md` files that name the removed types.
  - `schemas/`: the settings-file JSON schema gains `memberLocation` and `visibility`.
- **Examples.**
  - `examples/*/Sql/CountQueries.cs`, whose `NestedClass = "Counts"` becomes `-- nested-class: Counts`.
  - Each example project gains at least one file that uses `member-location: class` or a non-default `visibility`, per AGENTS.md's rule that a delivered feature gets an example.
  - `tools/package-install/Queries.cs`, whose `SqlLocation.Direct` becomes `-- member-location: class`.
- **Records.**  Past specs and plans under `docs/superpowers/` are records and stay as written.  `docs/tech-debt/TD-0089` is checked for the names it uses.

## Left to the builder

- Diagnostic ids, titles and message wording, within the reasons above.
- How SQLSRC006 is retired.
- The order of work inside each pull request, and the tests.  Each new error and each default needs a generator test, and the tool needs a test that the new markers are accepted.
- What a type writes when none of its queries lands in a nested class.  Today an empty `Sql` class is kept for a type with no queries.  Keep whatever avoids a cascade of extra compile errors in the user's code when a file has an error.
