# Generator parameters merge per option: design

## Intent

Today a list of generator parameters replaces the list of every level below it, whole.  A query that writes `-- generator: no-token-validation` under a file header that says `keep-comments` loses `keep-comments`, so each level has to repeat everything it wants from above.  The owner finds that illogical and unhelpful: a level should say only what it changes.

After this change each level turns options on or off, and leaves the rest as the levels above it set them.  `-- generator: keep-comments` on a query means "this query's settings as its file and folders give them, with `keep-comments` on".  This is how the library types already work (`-- library-types: nodatime=off`), and the generator parameters adopt the same on/off/default syntax, so there is one way to switch things on and off.

There is no release yet (`VersionPrefix` 0.1.0, no `v*` tags), so this changes behaviour and renames words without any compatibility path.

## Goals

- Every level adds to the levels below it, per option: the settings files from the farthest to the nearest (up to a `root: true`), then the file header, then the query's own lines.  An option a level does not name keeps the value of the levels below.
- Each option can be turned on and off at any level, and given back to its built-in default.
- `default` resets every option to its built-in default at its level, then the level's other words apply.  `default keep-comments` is "every default, with `keep-comments` on".  In a settings file, `["default", "sort-output"]` drops what the folders above say and starts that folder from "defaults plus `sort-output`".
- The syntax is the library types': a word is `name`, `name=on`, `name=off` or `name=default`.

## Non-goals

- No other setting changes how it merges.  The output, suffixes, dialect and the rest still take the nearest level whole.
- No compatibility for the old words or the old replace rule: no aliases for `no-token-validation`, `no-table-models` and `no-array-pool`, and no hint in their diagnostic.  Nothing has been released.
- No object form of `generatorParameters` in a settings file.  It stays an array of strings, the same words a marker takes.
- No new level: the attribute still takes no generator parameters.

## Anti-goals

- A query's settings must never depend on the order of words in a level.  `keep-comments default` is the same as `default keep-comments`.
- A level must never silently accept a contradiction.  One level that turns an option both on and off is an error, as a contradiction is today.
- No second on/off syntax beside the library types'.

## Approaches

1. **`name` / `name=off`, as the library types write it (chosen).**  One word per option, and `on`, `off` and `default` as its states, matched as the library types match theirs.  The three parameters named as negatives get positive names that are on by default, since `no-array-pool=off` would be a double negative.  It costs three renames.  It reuses a rule the product already has and documents, and gives each option its own `default` for free.
2. **Paired words, `X` and `no-X`.**  Every word users write today keeps its spelling, and the three positive forms of the negatives (`array-pool`, `token-validation`, `table-models`) and four negations (`no-keep-comments` and so on) are added.  No renames and no value parsing, but it is a second on/off syntax beside the library types', 14 words instead of 7, and no per-option reset.  The owner first chose it, then switched to 1 once the library types' precedent came up.
3. **Turned down early:** a settings file taking an object of booleans (it splits settings files from markers), and accepting both 1 and 2 (two spellings of one thing).

## The words

| Word | Built-in default | Replaces |
|----|----|----|
| `keep-comments` | off | `keep-comments` |
| `token-validation` | on | `no-token-validation` (now `token-validation=off`) |
| `sort-input` | off | `sort-input` |
| `sort-output` | off | `sort-output` |
| `table-models` | on | `no-table-models` (now `table-models=off`) |
| `async-method-suffix` | off | `async-method-suffix` |
| `array-pool` | on | `no-array-pool` (now `array-pool=off`) |
| `default` | | `default`, with its new meaning |

- A word is `name` (the same as `name=on`), `name=on`, `name=off` or `name=default`, with no spaces around the `=`, since a `-- generator:` line separates its words by spaces.
- Names are matched ignoring case, as today.  The states `on`, `off` and `default` are matched ignoring case, as the library types' are.
- `name=default` gives that option its built-in default at that level, whatever the levels below say, as a library's `default` does.
- `default` alone, anywhere in a level, gives every option its built-in default at that level; the level's other words then apply over it.  `default` may be repeated.  `default=x` is not valid.

## How the levels combine

A level says, for each option, nothing or a state.  The levels apply from the farthest up: each settings file of the chain, farthest first, then the file header, then the query.  The query's state of an option is the state of the nearest level that names it, or the built-in default when none does.  `default` names every option at its level, so nothing below it matters.

Several `-- generator:` lines in one level are one level, as today.  A settings file with `root: true` still ends the chain.

A file with no `-- name:` line is one query and one scope, so its lines are one level: `keep-comments` at its top and `keep-comments=off` lower down are a contradiction (SQLSRC112), not an override.  That is today's behaviour, and the manual says so.  *(Builder's call.)*

## Errors

| Case | In a marker | In a settings file |
|----|----|----|
| A word that is no parameter, the old three among them | SQLSRC109, the other words apply | SQLSRC019 at the element, the other elements apply |
| A state that is not `on`, `off` or `default`, or `default=…` | SQLSRC111 | SQLSRC019 at the element |
| An empty `-- generator:` line | SQLSRC110, as today | |
| One option given two different states in one level (`keep-comments` and `keep-comments=off`) | SQLSRC112 at the later word, which is not applied | SQLSRC020 at the later element, and the first applies, as for a repeated key of `libraryTypes` |
| `name=default` beside `name=on` or `name=off`, even where the default is the same value | SQLSRC112, as two different states *(builder's call)* | SQLSRC020, as two different states |
| The same state repeated | Fine | Fine |
| `default` beside other words | Fine: no longer an error | Fine: no longer an error |

`[]` in a settings file names nothing, so it changes nothing; today it means "every default".  `["default"]` is the reset.  An element that is empty or white space is still skipped.

## What the change touches

**Code.**
- [`GeneratorParameters`](../../../src/SqlSource/Settings/GeneratorParameters.cs): `NoTokenValidation`, `NoTableModels` and `NoArrayPool` become `TokenValidation`, `TableModels` and `ArrayPool`, and every check that reads them flips (`QuerySettings`, `MethodPlanner`, `TypeModels`).
- [`GeneratorParameterList`](../../../src/SqlSource/Settings/GeneratorParameterList.cs): the new names, the states, the built-in defaults, and `ReadElements` under the new rules.
- [`SqlGeneratorParameterScope`](../../../src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs): reads `name=state`, drops the conflict of `default` with other words, and reports a conflict between two states of one option.
- [`SettingsLevel`](../../../src/SqlSource/Settings/SettingsLevel.cs): `Parameters` holds what a level names, not a whole list, and `Over` merges it per option.  `QuerySettings.Resolve` applies the result over the built-in defaults.
- The parser's choice of whether a query keeps its comments ([`SqlFileParser.ResolveSettings`](../../../src/SqlSource/Parsing/SqlFileParser.cs)) and [`FileParseInput.CommentsWanted`](../../../src/SqlSource/Generation/FileParseInput.cs): the chain's resolved `keep-comments`, unless the markers name it.  `CommentsWanted` becomes exact, where today it may be true for queries whose own list drops it.
- [`schemas/settings-v1.schema.json`](../../../schemas/settings-v1.schema.json): the new words with an optional `=on|off|default`, and the rule against `default` beside another word removed.

**Documentation**, in the same pull request, by the rules of the root `AGENTS.md`:
- `docs/manual/settings.md`: the "replaces whole" exception becomes part of the per-key merging bullet, the generator parameters row and section, the settings files example and its "a file's own marker still wins" line, and the "A value that is wrong" table.
- `docs/manual/diagnostics.md`: SQLSRC019, SQLSRC020 (which now covers a generator parameter given twice), SQLSRC109 (the new names), SQLSRC111 and SQLSRC112.
- `docs/manual/concepts.md`, the generator parameter entry, and `docs/manual/sql-files.md` wherever it states the old rule.
- `README.md`: the FAQ answer on `async-method-suffix`, which explains the replace rule today.
- `docs/architecture/generator.md`: the markers and settings sections, including "the exception to the rule that a level replaces the one below it whole".
- `src/SqlSource/Settings/AGENTS.md` and any other AGENTS.md or doc comment that states the replace rule.

**Tests, examples and sidecars.**  About 53 `.sql` files in `tests/` and `examples/` and two `sqlsource.json` files in tests write generator parameters.  The old words are renamed in all of them, and everywhere else that names them: `tools/package-install/` (its `sqlsource.json`, `Program.cs` and `expected-output.txt`), C# test sources such as `tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs`, the database-test READMEs and `docs/tech-debt/TD-0089`.  The historical plans and specs under `docs/superpowers/` are left as written.  Wherever the merge rule changes a query's settings, the default is to keep what the query gets today by adding `default` to its list, unless the test exists to show how lists combine, in which case it is rewritten to show merging.  If any query's generated SQL changes (most likely through `keep-comments`), its sidecar is written again with `tools/describe-end-to-end.sh`.  The examples gain or update an example of a query that adds to its file's list.

## Left to the builder

- How a level stores what it names, for example a mask of the options it names and their values.  It must stay equatable for the incremental generator's caching.
- Test layout and names, and the exact wording of the documentation and messages, in the manual's style.
- Whether SQLSRC112's and SQLSRC020's messages need new wording for a generator parameter, or their current text already fits.
- Whether the tool (`SqlSource.Tool`) needs any change of its own beyond what it shares with the generator; it parses `.sql` files with the same code.
