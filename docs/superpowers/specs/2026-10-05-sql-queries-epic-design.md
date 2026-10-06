# SQL queries from `.sql` files - epic outline

Date: 2026-10-05

This outline coordinates the phases of the epic.  It is kept current until the epic closes: a phase that changes the plan updates it in the same pull request.

## Goal

Let a C# project keep its SQL in `.sql` files and use it through generated members.  A type marked `[SqlQueries]` gains a string constant for each query in the `.sql` files it points at, and a method for each query that contains replacement tokens.

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name FROM users WHERE id = @id;

-- name: ListFrom
SELECT id, name FROM {{table}} ORDER BY name;
```

```csharp
[SqlQueries]
public partial class UserRepository
{
    // Generated: Sql.GetUser (a constant) and Sql.ListFrom(string table) (a method).
}
```

## Phases

| Phase | Status | Spec | Delivers |
|----|----|----|----|
| 1. SQL parser | Designed | [sql-parser-design](2026-10-05-sql-parser-design.md) | The text of one `.sql` file becomes named blocks with their directives, summary, cleaned SQL and token segments, or a list of errors.  Pure code with no generator pipeline. |
| 2. Constants | Not designed | - | The `[SqlQueries]` attribute and `SqlQueriesMode` enum, `.sql` discovery and `Path` resolution, type-shape checks, constant emission with XML docs, diagnostics located in the `.sql` file, and `build/SqlSource.props` in the package. |
| 3. Tokens | Not designed | - | Method emission with `string.Create`, parameter validation, and the `SqlSourceTokenValidation` MSBuild property. |

Each phase has its own spec, plan and pull request.  Phase 1 changes nothing a user can see.  Phase 2 makes queries without tokens usable; until phase 3 lands, the generator reports a block that contains tokens as an error.

## Decisions

These hold across phases.  A phase spec may add detail and must not contradict them.

### Consumers

- Generated code targets .NET 8 and later.  A project that targets an older framework gets a diagnostic, not output that fails to compile.
- The host floor is unchanged: Roslyn 4.8.0, the .NET 8 SDK and Visual Studio 2022 17.8.

### The attribute

- `SqlQueriesAttribute` and `SqlQueriesMode` are emitted by the generator as internal source and marked `[Conditional]`, so the package stays a development dependency with nothing in `lib/` and the attribute does not reach the consumer's metadata.
- Two projects that both use SqlSource, where one has `InternalsVisibleTo` the other, get warning CS0436.  The clean fix, `AddEmbeddedAttributeDefinition`, needs Roslyn 4.14, above the floor.  Phase 2 records this in `docs/tech-debt`.
- By default every `.sql` file in the folder of the `.cs` file that carries the attribute belongs to the type.  The `Path` property points at another folder or at one `.sql` file, relative to that `.cs` file.
- `.sql` files reach the generator as `AdditionalFiles`.  The package registers them through `build/SqlSource.props`.

### Modes

| Mode | Generated shape |
|----|----|
| `Nested` (default) | `private static class Sql` nested in the type, with public members |
| `Direct` | Public members on the type itself |

There is no accessibility option.

### Supported types

Partial classes, structs, record classes and record structs, including static, generic and nested ones.  The type and each containing type must be `partial`; otherwise the generator reports its own error.

### SQL files

- Three markers are recognised, each a line comment that starts its line, matched case-insensitively and always removed from the SQL: `-- name:`, `-- summary:` and `-- SqlSource:`.
- `-- name:` starts a named block that runs to the next name marker or the end of the file.  A file with no name marker is one block named after the file.
- Before the first name marker, comments and `-- SqlSource:` directives are allowed and the directives apply to every block.  SQL there is an error.
- Directives:

| Directive | Effect |
|----|----|
| `preserve-comments` | Comments are kept in the SQL |
| `no-token-validation` | The generated method does not validate its parameters |
| `token-validation` | The generated method validates its parameters even when validation is off for the project |
| `token-ignore=name` | `{{name}}` stays in the SQL as literal text |

- Comments are stripped by one conservative lexer with no dialect setting.  Where dialects disagree it keeps the text, and where input is unbalanced it reports an error: a comment left in is harmless, SQL removed is a bug.  The dialect-sensitive choices sit in one place in the lexer so that a later dialect setting can become flags; no such setting is built now.
- Line endings in generated SQL are always `\n`.

### Tokens

- The form is `{{name}}`, where the name is a valid C# identifier; spaces inside the braces are ignored.
- Each distinct name becomes one `string` parameter.  Parameters are ordered by first appearance.
- Tokens are replaced anywhere in the SQL, including inside string literals.
- Parameters are validated with `ArgumentException.ThrowIfNullOrWhiteSpace` unless validation is turned off for the query (`no-token-validation`) or for the project (`<SqlSourceTokenValidation>false</SqlSourceTokenValidation>`, exposed to the generator through `CompilerVisibleProperty`).
- Token replacement is string concatenation into SQL.  It is an escape hatch for the places a query parameter cannot go, and it does not make a value safe.  The README says that tokens are for trusted fragments only.

### Generated documentation

Every generated member has XML documentation, so a consumer that treats CS1591 as an error is not broken:

- a `<summary>` taken from the block's `-- summary:` lines, or generated text that names the query and its file;
- the SQL in `<remarks>`;
- a `<param>` for each token parameter.

### Errors

Every problem is an error, never a warning: an unknown directive, a duplicate or invalid name, an empty block, an unterminated string or comment, a `Path` that matches nothing.  A `.sql` file with any error produces no members.

## Left to the phase specs

| Phase | Open questions |
|----|----|
| 2 | Whether folder discovery is recursive.  Multiple attributes on one type.  Partial types spread across files.  Names that collide across files, with existing members or with the enclosing type.  Diagnostic ids and the analyzer release-tracking files.  Whether `docs/tech-debt/TD-0001` is resolved or kept, since its trigger fires in this phase. |
| 3 | The shape of the emitted method.  Precedence between the MSBuild property and the two validation directives. |

## Out of scope for the epic

- A dialect setting, and dialect-specific lexers.
- An accessibility option on the attribute.
- Parameter types other than `string`.
- Consumers that target a framework older than .NET 8.
