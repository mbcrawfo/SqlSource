# Escape for type names that hold a line break Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0066: a quoted part of a type-override key can be written in PostgreSQL's Unicode-escape form, `U&"a\000Ab"`, so that a type whose schema or name holds a line break, a tab or two spaces in a row has a key that survives the MSBuild trim and a `-- type-override:` marker, and the help of `SQLSRC246` writes such a key.

**Architecture:** The escaped form is a third spelling of a quoted part, owned by `TypeKeys` alone.  `TrySplit` reads it as a part, `IsQuoted` is true for it and `TextOf` decodes it, so `TypeOverrideMap`, which compares parts by `TextOf`, treats `U&"a\000Ab"` and a `"..."` part of the same text as one key with no change of its own.  `TryNormalize` gives an escaped part in the quoted form `WritePart` writes for its text, and `WritePart` writes the escaped form for a text that the trim, the compiler's cut at `;` and `#`, or a line comment would change.

**Tech Stack:** C# (netstandard2.0 generator, spans), xUnit v3 on Microsoft Testing Platform, Shouldly.

**Spec:** `docs/tech-debt/TD-0066-type-name-with-a-line-break-has-no-key.md` (Problem and Proposed fix), with the key grammar as merged by #84 (`docs/superpowers/plans/2026-10-20-quoted-case-exact-type-override-keys.md`) and #88 (`docs/superpowers/plans/2026-10-20-one-override-key-for-arrays-outside-pg-catalog.md`).

## Where the code differs from the TD file

- **The normalized form of an escaped part is the quoted form `WritePart` writes for its text, not always `"..."`.**  The TD says the escaped form is read "to the same text as the plain quoted form".  The key's identity, the text of its parts, is the same either way.  But a normalized key reaches the messages of `SQLSRC250` to `SQLSRC252` and `SQLSRC261` through `TypeMapping.Key`, and a `"..."` part holding a line break would put one in a message.  So `U&"st\0061tus"` normalizes to `"status"`, and `U&"a\000ab"` to `U&"a\000Ab"`, the one spelling `WritePart` gives that text.  A `"..."` part is still kept as written, so nothing a `"..."` or plain part means or gives changes.
- **`WritePart` also escapes `;`, `#`, every white space but a lone U+0020, and control characters.**  The TD names a line break, a tab and two spaces in a row.  The trim is `Regex.Replace(value, '\s+', ' ')`, so any Unicode white space (U+00A0, U+2028, ...) becomes a space, and the compiler cuts a value at `;` or `#` (`docs/manual/settings.md#values-written-in-msbuild`).  Escaping those too makes the help's key survive the MSBuild path, which the TD asks for.
- **No `UESCAPE` clause.**  `U&"a!000Ab" UESCAPE '!'` holds a space outside quotes, which separates entries of an MSBuild value, and `\\` already writes a backslash.  A key with one is not valid.
- `TypeOverrideGrammar` needs no code change: it counts a `"` as opening and closing quotes, and the escaped form keeps `""` for a quote, so `IndexOfEquals` and `NextEntry` read it already.  Its remarks change only where they describe a key.

## Global Constraints

- `src/SqlSource` targets `netstandard2.0`; no collection expression targets `ImmutableArray<T>`; nothing a user writes may make the generator throw (`TextOf` must not throw on any input).
- Allocate nothing per character on the common path: `TrySplit`, `IsQuoted` and `TextOf` allocate nothing for a plain or `"..."` part without `""`, as today.  `TextOf` of an escaped part allocates only when it holds `\` or `""`.
- Keep the change in `src/SqlSource/Settings/TypeKeys.cs` (and the remarks of `TypeOverrideGrammar.cs`).  The src-patterns unit may touch other files of `Settings/`.
- A plain part and a `"..."` part keep their meaning and their normalized text; the escaped form is a feature of quoted parts only, so phase 6 can decide SQL Server's brackets against an unchanged grammar.
- An escaped part that is not valid makes the key not valid: `SQLSRC014` in a property or metadata, `SQLSRC111` in a marker.  No new diagnostic.
- User docs name no id, no internals; `docs/manual` samples of SQL are fenced `sql <dialect>`, `sql fragment` or `sql error`.
- Every commit passes `./pre-commit-validation.sh`.  Tests are named `Method_Scenario_Outcome`, classes `sealed`, assertions Shouldly.  `tests/SqlSource.Tests/Generator/*.cs` compile on Roslyn 4.8.0 and C# 12.

## The escaped form

```
escaped-part = ("U" | "u") "&" '"' { character | '""' | escape } '"'
escape       = "\\" | "\" hex{4} | "\+" hex{6}
```

- `hex` is `0-9`, `a-f`, `A-F`.  A `\` followed by anything else, or by too few hex digits, is not valid.
- A code point must be from U+0001 to U+10FFFF.  A surrogate, U+D800 to U+DFFF, is valid only as a high surrogate escape followed at once by a low surrogate escape (either escape form), which together are one character, as PostgreSQL reads them.
- The part's text is not empty (`U&""` is not valid, as `""` is not).
- `U&` is followed by `"` at once; anything else after `U` is a plain part, as today (`U&x` is not a key).
- `WritePart` writes the escaped form with `\XXXX`, four upper-case hex digits, for each character it must escape, `\\` for a backslash and `""` for a quote; every character it escapes is in the BMP.
- A text needs the escaped form when it holds: a `char.IsControl` character; a `char.IsWhiteSpace` character other than U+0020; a U+0020 whose previous character in the text is U+0020; `;`; or `#`.  Only those characters are escaped: `a  b` is written `U&"a \0020b"`.

## Review Focus

- **An escaped part that decodes to a `"`, `U&"a\0022b"`**: one key with `"a""b"`, normalized to `"a""b"`, and the grammar's quote counting is not thrown off (Task 1, `TryNormalize_EscapedPart_IsItsQuotedForm` and `ReadList_EscapedKey_IsOneEntryAndTheRestApply`).
- **A character outside the BMP**: `U&"\+01F600"` and `U&"\D83D\DE00"` are one text; a lone `\D800`, `\DE00\D83D` and `\+110000` are not keys (Task 1).
- **Two spellings of one escaped text in one value**: `U&"a\000Ab"` and `U&"a\000ab"` are one key, and the last wins (Task 1, `ReadList_TwoSpellingsOfOneEscapedText_TheLastWins`).
- **An escaped catalog name**: `pg_catalog.U&"_int4"` is `int4[]`, `U&"int4"` is `int4`, `U&"Int4"` is not a key (Task 1).
- **`TextOf` on a damaged escaped part does not throw** (Task 1, `TextOf_EscapedPartThatIsNotWellFormed_DoesNotThrow`).

---

### Task 1: Read the escaped form

**Files:**
- Modify: `src/SqlSource/Settings/TypeKeys.cs` (`TryNormalize`, `TrySplit`, `IsQuoted`, `TextOf`, `TryReadPart`, `NormalizeQuotedCatalogName`, their doc comments)
- Modify: `src/SqlSource/Settings/TypeOverrideGrammar.cs` (class remarks only: a key's quoted part may be `U&"..."`)
- Test: `tests/SqlSource.Tests/Settings/TypeKeysTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrideGrammarTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrideMapTests.cs`
- Create: this plan, committed with the task.

**Interfaces:**
- Produces: `TypeKeys.IsQuoted(ReadOnlySpan<char> part)` true for `"..."` and `U&"..."` / `u&"..."`; `TypeKeys.TextOf(ReadOnlySpan<char> part)` decodes an escaped part; a private `static string Quote(string text)` that Task 2 makes `WritePart` use: `"..."` with `"` doubled — Task 1 writes it as that alone, and Task 2 adds the escaped branch.  `TryNormalize` gives an escaped part as `Quote(TextOf(part))`.

- [ ] **Step 1: Write the failing tests** (add to the existing classes; keep the style of the theories there)

`TypeKeysTests`:

```csharp
// An escaped part is read to its text, and given in the quoted form of that text.
[Theory]
[InlineData("public.U&\"st\\0061tus\"", "public.\"status\"")]
[InlineData("public.u&\"St\\+000061tus\"", "public.\"Status\"")]
[InlineData("U&\"my schema\".x", "\"my schema\".x")]
[InlineData("s.U&\"a\\\\b\"", "s.\"a\\b\"")]
[InlineData("s.U&\"a\\0022b\"", "s.\"a\"\"b\"")]
[InlineData("s.U&\"a\"\"b\"", "s.\"a\"\"b\"")]
[InlineData("s.U&\"x\"[]", "s.\"x\"[]")]
[InlineData("U&\"int4\"", "int4")]
[InlineData("U&\"_int4\"", "int4[]")]
[InlineData("pg_catalog.U&\"_int4\"", "int4[]")]
[InlineData("pg_catalog.U&\"New Type\"", "\"New Type\"")]
[InlineData("U&\"pg_catalog\".int4", "int4")]
public void TryNormalize_EscapedPart_IsItsQuotedForm(string key, string expected) => ShouldNormalize(key, expected);

[Theory]
[InlineData("U&\"Int4\"")]
[InlineData("U&\"\"")]
[InlineData("s.U&\"\"")]
[InlineData("s.U&\"\\0000\"")]
[InlineData("s.U&\"\\00\"")]
[InlineData("s.U&\"\\00G1\"")]
[InlineData("s.U&\"a\\\"")]
[InlineData("s.U&\"\\x\"")]
[InlineData("s.U&\"\\+11FFFF\"")]
[InlineData("s.U&\"\\+00006\"")]
[InlineData("s.U&\"\\D800\"")]
[InlineData("s.U&\"\\DE00\\D83D\"")]
[InlineData("s.U&\"\\D83Dx\"")]
[InlineData("s.U&x")]
[InlineData("s.U& \"x\"")]
[InlineData("s.U&\"x\" UESCAPE '!'")]
[InlineData("s.U&\"unclosed")]
public void TryNormalize_EscapedPartThatIsNotValid_IsNotAKey(string key) =>
    TypeKeys.TryNormalize(key.AsSpan(), out _).ShouldBeFalse();

[Theory]
[InlineData("U&\"a\\000Ab\"", "a\nb")]
[InlineData("u&\"\\\\\"", "\\")]
[InlineData("U&\"\\+01F600\"", "\U0001F600")]
[InlineData("U&\"\\D83D\\DE00\"", "\U0001F600")]
[InlineData("U&\"\\D83D\\+00DE00\"", "\U0001F600")]
[InlineData("U&\"a\"\"b\"", "a\"b")]
[InlineData("U&\"plain\"", "plain")]
public void TextOf_EscapedPart_IsItsTextDecoded(string part, string expected) =>
    TypeKeys.TextOf(part.AsSpan()).ToString().ShouldBe(expected);

// TextOf is given only parts TrySplit read, but nothing a user writes may make the generator throw.
[Theory]
[InlineData("U&\"\\\"")]
[InlineData("U&\"\\+12\"")]
[InlineData("U&\"\\D800\"")]
public void TextOf_EscapedPartThatIsNotWellFormed_DoesNotThrow(string part) =>
    Should.NotThrow(() => TypeKeys.TextOf(part.AsSpan()).ToString());
```

Add rows `[InlineData("U&\"x\"", true)]` and `[InlineData("u&\"x\"", true)]` to `IsQuoted_Part_IsWhetherItHasQuotes`, and `[InlineData("U&\"a.b\".c[]", "U&\"a.b\"", "c", true)]` to `TrySplit_WrittenKey_GivesItsParts`.

`TypeOverrideGrammarTests` (use the existing helpers and assertion style of the `ReadList_*` tests):

- `TryReadEntry_EscapedKeyInAMarker_IsItsQuotedForm`: `"public.U&\"st\\0061tus\" = int"` with `spacesAroundEquals: true` reads `new TypeOverride("public.\"status\"", "int", SettingPlace.Query)`.  (A text that needs the escape is Task 2's test, since its normalized form is the escaped one.)
- `ReadList_EscapedKey_IsOneEntryAndTheRestApply`: `"s.U&\"a=b\\0022 c\"=int int4=long s.U&\"\\00\"=int"` gives entries `int4`→`long` and `s."a=b"" c"`→`int` (in map order), and `invalid` is exactly `["s.U&\"\\00\"=int"]`.
- `ReadList_TwoSpellingsOfOneEscapedText_TheLastWins`: `"s.U&\"st\\0061tus\"=int s.\"status\"=long"` gives one entry, key `s."status"`, type `long`.

`TypeOverrideMapTests`:

- `Find_EscapedPart_MatchesTheQuotedPartOfItsText`: a map read from `"public.\"Status\"=int"`; `Find(map, "public.U&\"St\\0061tus\"")` is that entry, and `Find(map, "public.U&\"st\\0061tus\"")` is null.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class "SqlSource.Tests.Settings.TypeKeysTests" --filter-class "SqlSource.Tests.Settings.TypeOverrideGrammarTests" --filter-class "SqlSource.Tests.Settings.TypeOverrideMapTests"`
Expected: the new cases fail (escaped keys are not keys yet); the old ones pass.

- [ ] **Step 3: Implement in `TypeKeys`**

- `TryReadPart`: at `U`/`u` followed by `&"`, read the quoted region from the `"` exactly as a `"..."` part is read, then check its inside with the escape rules above; give the index after the closing quote.
- One private reader of an escape, used by the check and by `TextOf`, e.g. `static bool TryReadEscape(ReadOnlySpan<char> inside, int at, out int codePoint, out int next)` (at a `\`), so the two cannot disagree.  The check allocates nothing.
- `IsQuoted`: `"` first, or `U`/`u`, `&`, `"`.
- `TextOf`: for an escaped part, the inside is `part.Slice(3, part.Length - 4)`; return it when it holds neither `\` nor `"`, else decode into one `char[]` of the inside's length (decoding never lengthens it).  Where an escape does not read, copy the `\` as a character and go on.
- `TryNormalize`: an unqualified escaped part goes through `TryNormalizeQuoted(TextOf(name))` as a `"..."` one does; `IsSystemSchema` already uses `TextOf`.  Where a quoted part is now given "as written" — the qualified key outside `pg_catalog` (`key.ToString()`) and the three `name.ToString()` returns of `NormalizeQuotedCatalogName` — give an escaped part as `Quote(TextOf(part).ToString())` and any other part as written, allocating nothing new when no part is escaped.
- Update the doc comments of `TryNormalize`, `TrySplit`, `IsQuoted` and `TextOf` to say what the escaped form is and gives, and the remarks of `TypeOverrideGrammar`.

- [ ] **Step 4: Run the tests and see them pass**

Run: the command of Step 2.  Expected: all pass.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh` (expect it to pass), then:

```bash
git add src/SqlSource/Settings/TypeKeys.cs src/SqlSource/Settings/TypeOverrideGrammar.cs tests/SqlSource.Tests/Settings docs/superpowers/plans/2026-10-20-escape-for-type-names-that-hold-a-line-break.md
git commit -m "Read a key's part in PostgreSQL's Unicode-escape form"
```

### Task 2: Write the escaped form

**Files:**
- Modify: `src/SqlSource/Settings/TypeKeys.cs` (`WritePart`, `Quote`, their doc comments)
- Modify: `src/SqlSource/Generation/Types/PostgresTypeMap.cs` (the doc comment of `KeyOf` only, adding the escaped example)
- Test: `tests/SqlSource.Tests/Settings/TypeKeysTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrideGrammarTests.cs`, `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`

**Interfaces:**
- Consumes: Task 1's `Quote(string)`, `TextOf`, `TrySplit`.
- Produces: `TypeKeys.WritePart(string text)`: plain, `"..."`, or `U&"..."` by the rules of "The escaped form".

- [ ] **Step 1: Write the failing tests**

Add rows to `WritePart_Text_IsQuotedWhenItNeedsIt`:

```csharp
[InlineData("a\nb", "U&\"a\\000Ab\"")]
[InlineData("a\r\nb", "U&\"a\\000D\\000Ab\"")]
[InlineData("a\tb", "U&\"a\\0009b\"")]
[InlineData("a  b", "U&\"a \\0020b\"")]
[InlineData("a\u00A0b", "U&\"a\\00A0b\"")]
[InlineData("a;b#c", "U&\"a\\003Bb\\0023c\"")]
[InlineData("a\u0001b", "U&\"a\\0001b\"")]
[InlineData("say \"hi\"\n\\", "U&\"say \"\"hi\"\"\\000A\\\\\"")]
[InlineData("a\\b", "\"a\\b\"")]
[InlineData("a b", "\"a b\"")]
```

Add rows `[InlineData("s.U&\"a\\000Ab\"")]` and `[InlineData("s.U&\"x \\0020y;\"")]` to `WritePart_PartOfAKey_ReadsBackAsItsText`, and a theory `WritePart_TextThatNeedsTheEscape_SurvivesTheTrimAndALine` over `"a\nb"`, `"a\tb"`, `"a  b"`, `"a;b"`, `"x\u2028y"`: the written part holds no `char.IsWhiteSpace` character but single U+0020s none of them next to another, no `;`, `#` or control character, and `TextOf` of it is the text.

`TypeOverrideGrammarTests.TryReadEntry_EscapedKeyInAMarker_IsItsWrittenForm`: `"public.U&\"a\\000ab\" = MyApp.Tag"`, `spacesAroundEquals: true`, gives key `public.U&"a\000Ab"` (the canonical spelling) and type `global::MyApp.Tag`.

`QueryTypesTests` (beside `Run_CompositeWhoseNameNeedsQuotes_HelpQuotesIt`):

- `Run_CompositeWhoseNameHoldsALineBreak_HelpEscapesIt`: a composite `schema: "my schema"`, `name`/`internalName: "Order\nItem"`; the diagnostic is `SQLSRC246 {GetUserName}: The column 'item' has the type 'Order\nItem', which SqlSource cannot give a C# type.  Map it with the type override, as "my schema".U&"Order\000AItem"=<a C# type>.` (check how the message writes the type's name first and match it).
- `Run_CompositeWhoseNameHoldsALineBreak_IsMappedByTheEscapedKey`: the same composite, with the file `-- type-override: "my schema".U&"Order\000AItem"=MyApp.Tag\n-- name: GetUser\nSELECT 1;\n` (as `Run_CompositeOfTheCatalog_IsSqlsrc246WithTheQualifiedKey` builds its `mapped` file); no diagnostics, and `ColumnsOf(run)` is one `PropertyType("global::MyApp.Tag", false, true, NoWarnings, "\"my schema\".U&\"Order\\000AItem\"", "a -- type-override: marker in the file header")`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class "SqlSource.Tests.Settings.TypeKeysTests" --filter-class "SqlSource.Tests.Settings.TypeOverrideGrammarTests" --filter-class "SqlSource.Tests.Generator.QueryTypesTests"`
Expected: the new cases fail.

- [ ] **Step 3: Implement**

`Quote(text)` writes `U&"..."` when the text needs it (rules above), escaping only the characters that need it, `\` as `\\` and `"` as `""`; otherwise `"..."` as today.  `WritePart` is plain or `Quote(text)`.  One pass with a `StringBuilder` only for the escaped branch.  Update the doc comments of `WritePart` and `KeyOf`.

- [ ] **Step 4: Run the tests and see them pass**

Run: the command of Step 2, then the whole `tests/SqlSource.Tests` project and `tests/SqlSource.Tests.RoslynFloor` (`dotnet test --project tests/SqlSource.Tests.RoslynFloor --filter-class "SqlSource.Tests.Generator.QueryTypesTests"`).  Expected: all pass.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource/Settings/TypeKeys.cs src/SqlSource/Generation/Types/PostgresTypeMap.cs tests/SqlSource.Tests
git commit -m "Write a key's part that a value or a line would change in the escaped form"
```

### Task 3: Documentation and TD-0066

**Files:**
- Modify: `docs/manual/settings.md` (the key's parts under `#setting-type-overrides`; "In a property or metadata"; `#values-written-in-msbuild`; remove the known issue)
- Modify: `docs/manual/diagnostics.md` (the `SQLSRC246` key paragraph; the `-- type-override:` item of `SQLSRC111`)
- Modify: `docs/architecture/generator.md` (the grammar paragraph under the type override, the MSBuild-value item, the `KeyOf` sentence of types)
- Delete: `docs/tech-debt/TD-0066-type-name-with-a-line-break-has-no-key.md`, its row in `docs/tech-debt/README.md`

- [ ] **Step 1: settings.md**

- Add to the bullet "A part of a key ... is plain or in double quotes": a part in quotes may be written `U&"..."`, PostgreSQL's form with escapes, in which `\` and four hex digits (or `\+` and six) is a character and `\\` a backslash; it is the same part as the quotes of its text: `public.U&"st\0061tus"` is `public."status"`.  Example first, for a name with a line break: `"my schema".U&"Order\000AItem"`.  `UESCAPE` is not read.  In a project file, `&` is written `&amp;`: `U&amp;"Order\000AItem"`.
- `#values-written-in-msbuild`: replace "So a property or metadata cannot name a type whose schema or name has two spaces in a row, a tab, a `;` or a `#` ... A name with a line break cannot be named at all" with: write such a character with an escape, `\0009` for a tab, `\0020` for the second of two spaces, `\003B` for `;`, `\0023` for `#`, `\000A` for a line break; the build's error gives the key written so.
- The "In a property or metadata" sentence "A run of white space inside the quotes is read as one space" gains: "write it with escapes" and the link stays.
- Delete the known issue "A type whose schema or name holds a line break cannot be given a type override".
- Run the grep of `docs/manual/AGENTS.md` over the changed pages.

- [ ] **Step 2: diagnostics.md**

- `SQLSRC246`'s paragraph "A schema or a name that needs quotes is in them": add that a name with a line break, a tab, two spaces in a row, a `;` or a `#` is written with escapes, `"my schema".U&"Order\000AItem"`, so that the key can be written in a project file and in a marker alike.
- `SQLSRC111`'s `-- type-override:` item: add "or in `U&"..."` with escapes, `public.U&"Order\000AItem"`; an escape that is not `\\`, `\` and four hex digits or `\+` and six is not valid".

- [ ] **Step 3: generator.md**

- The grammar paragraph: the quoted part may be `U&"..."`, decoded by `TextOf`, normalized to `Quote` of its text (so no key holds a line break), with the reason; `WritePart` writes the escaped form for what the trim, the compiler's cut or a line comment changes.
- The "An MSBuild value" item: replace the sentence ending in the TD-0066 link with how such a name is named (with escapes) and why the grammar needs no change (`""` stays a quote).
- The types item 2/3 sentences that describe `KeyOf`/the help: add the escaped example.

- [ ] **Step 4: TD-0066**

`git rm docs/tech-debt/TD-0066-type-name-with-a-line-break-has-no-key.md`, remove its row from `docs/tech-debt/README.md` (leave `Next id`), and `grep -rn "TD-0066" --exclude-dir=.git .` gives nothing outside this plan.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh` (the docs tests check links and samples), then:

```bash
git add docs
git commit -m "Document the escaped form of a key's part and resolve TD-0066"
```

## After the tasks

- Time `TypeOverrideMap.Find` and `TypeKeys.TryNormalize` over ordinary keys in a `Release` build with the same scratch `Stopwatch` loop as before the change (baseline on `main` at 7347c7a: `Find` 141 ns and 0 B a call, `TryNormalize` 24 ns and 56 B a call, seven keys), and give both in the PR.
