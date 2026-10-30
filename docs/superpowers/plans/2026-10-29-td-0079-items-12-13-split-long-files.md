# TD-0079 items 12 and 13: split two long files - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split `MappedTypeResolver` and `QueryMethodWriter` by job, with no change to what is generated.

**Architecture:** Code moves to new files of the same namespaces, kept as written: the check of generic constraints to `GenericConstraints.cs`, the reading of a row to `RowReaderWriter.cs`, and the locals of a query's method to `QueryMethodLocals.cs`.  `ResolveType` is cut into its lookup, its arguments and its checks of use.  The existing tests, which drive both classes through `Resolve` and `Append`, pin that nothing changed.

**Tech Stack:** C# (`netstandard2.0`, Roslyn 4.8.0), xUnit.

**Spec:** `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md`, items 12 and 13 and their proposed fixes.

## Global Constraints

- "with no change to what is generated": no generated text, diagnostic or `MappedType` record changes.
- Move code without rewriting it: bodies keep their statements and comments; only signatures, accessibility and the calls between the files change.
- Touch only items 12 and 13 of TD-0079; keep the other items' text and numbers, and follow the run's convention: a fixed item struck through with "Fixed: …", its proposed fix struck with "Done.".
- `src/SqlSource/AGENTS.md` has almost no room left; a rule about these files goes in `src/SqlSource/Generation/Emitting/AGENTS.md` or `docs/architecture/generator.md`.

## Where the code differs from the spec

- `QueryMethodWriter` is 1229 lines now, not about 960: td-0085, td-0088 and td-0087 added the drain, the transaction checks and the streaming iterator's `atRow`.  The two moves of item 13 are still the whole fix; the method's other jobs stay, as the spec proposes.
- `MappedTypeResolver` is 925 lines and `ResolveType` 168, with td-0086's `TransitivePackageAssemblies` and `PackageBinding`, which the split carries.
- td-0079-binders (open beside this unit) reshapes the `binder.Append` call in `AppendBody` and deletes `SqlServerBinding.cs`; this unit leaves both alone, and `QueryMethodWriter.VectorType`, which `SqlServerBinding` and `QueryMethod` name, stays where it is.

## Review Focus

- A generic mapped type whose argument breaks a constraint must still be `NotAPropertyType`, and the first failure of a name the same: `MappedTypeResolverTests` covers it, unchanged.
- A short name inside an argument must still be written in full, and its `Package` be the first found: `MappedTypeResolverTests`, unchanged.
- A SQL Server vector column, a converted column and an array converted by element must read as before: `QueryMethodWriterTests` and the generated-source snapshots, unchanged.
- A token named like a local must still get a numbered name: the awkward-name tests, unchanged.
- No new test is needed: the change is a move, and the tests above are its proof.

---

### Task 1: `GenericConstraints` and the split of `ResolveType` (item 12)

**Files:**
- Create: `src/SqlSource/Generation/Types/MappedTypes/GenericConstraints.cs`
- Modify: `src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs`
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeResolverTests.cs` (run, unchanged)

**Interfaces:**
- Produces: `internal static class GenericConstraints` with `public static INamedTypeSymbol? Construct(Compilation compilation, INamedTypeSymbol definition, EquatableArray<ClrNamePart> parts, int partIndex, List<ITypeSymbol> arguments, ref int next)`.  Private: `MeetsConstraints`, `MeetsFlags`, `Converts`, `Substitute`, `HasPublicParameterlessConstructor`, `HasRequiredMembers`, `SetsRequiredMembers`, `IsNullable`, `IsNullableValue` and the constant `SetsRequiredMembersAttributeFullName`, moved as written.
- `CanBeAPropertyType` and `HasStaticAbstractMember` stay in the resolver: they judge a type's use in a name, not a constraint.

- [ ] **Step 1:** Move the members above into `GenericConstraints.cs`, with a summary and remarks taken from the third paragraph of the resolver's remarks (the constraints, the nullability rule and TD-0067).  The resolver's paragraph keeps one sentence that the type is constructed and checked by `<see cref="GenericConstraints" />`.
- [ ] **Step 2:** Split `ResolveType` into:
  - `LookUp(Compilation, TransitivePackageAssemblies, ClrTypeName, StringBuilder, out MappedTypeStatus status, out PackageBinding? package, out ImmutableArray<string> candidates)` → `INamedTypeSymbol?`: the full name's `FindNamed`, or the short name's switch; a short name of none is `NotFound`, of several `Ambiguous` with `FullNames` as `candidates`.
  - `ResolveArguments(Compilation, TransitivePackageAssemblies, ClrTypeName, StringBuilder, List<string> warningIds)` → a private `readonly record struct Arguments(MappedTypeStatus Status, bool IsPublic, List<ITypeSymbol>? Symbols, ImmutableArray<string> Ambiguous, PackageBinding? Package, ImmutableArray<ClrNamePart>.Builder? WrittenParts)`: the loop over the parts, as written, from `Found`, `true` and no package.  `First` is a minimum with `Found` as its identity, so `First(lookUp, arguments.Status)` is the loop's status.
  - `CanBeUsed(INamedTypeSymbol type, ClrTypeName name, Use use, ref bool isPublic, List<string> warningIds)` → `bool`: `CanBeAPropertyType` and the warnings of each containing type, the module and the assembly, as written.
  - `ResolveType` keeps the keyword, the calls, the construction through `GenericConstraints.Construct`, the accessibility and the result.
- [ ] **Step 3:** Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~MappedTypes|FullyQualifiedName~QueryTyper"`; expected: all pass, none changed.
- [ ] **Step 4:** Commit `refactor(types): move the check of generic constraints to GenericConstraints and split ResolveType`.

### Task 2: `RowReaderWriter` and `QueryMethodLocals` (item 13)

**Files:**
- Create: `src/SqlSource/Generation/Emitting/RowReaderWriter.cs`, `src/SqlSource/Generation/Emitting/QueryMethodLocals.cs`
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs` and the generator tests (run, unchanged)

**Interfaces:**
- Produces: `internal sealed class QueryMethodLocals`, the nested `Locals` as written, with its constructor and properties `public`; its seed reads `QueryMethodWriter`'s argument names, which become `internal const`.
- Produces: `internal static class RowReaderWriter` with `public static void AppendVectorChecks(StringBuilder builder, string indent, MethodOutput output, QueryMethodLocals locals)` and `public static void AppendRow(StringBuilder builder, MethodOutput output, QueryMethodLocals locals)`; `AppendColumn`, `AppendByElement` and `AppendGetFieldValue` private, moved as written.
- `QueryMethodWriter.AppendIf` and `AppendEnd` become `internal`, since `AppendVectorChecks` writes an `if`; `VectorType` stays in `QueryMethodWriter`.

- [ ] **Step 1:** Move `Locals` to `QueryMethodLocals.cs` as `QueryMethodLocals`, and rename its uses in `QueryMethodWriter`.
- [ ] **Step 2:** Move `AppendVectorChecks` to `AppendGetFieldValue` to `RowReaderWriter.cs`, and call `RowReaderWriter.AppendVectorChecks` and `RowReaderWriter.AppendRow` from `AppendRows` and `AppendStream`.  The class summary of `QueryMethodWriter` names the two.
- [ ] **Step 3:** Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Emitting|FullyQualifiedName~Generator"`; expected: all pass, none changed.
- [ ] **Step 4:** Commit `refactor(emitting): move the reading of a row to RowReaderWriter and the locals to QueryMethodLocals`.

### Task 3: Documents

**Files:**
- Modify: `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md` (items 12 and 13 and their fixes), `docs/tech-debt/README.md` (TD-0079's row loses "`MappedTypeResolver` and `QueryMethodWriter` are long"), `docs/tech-debt/TD-0067-nullability-inside-a-mapped-type-argument-is-not-checked.md` (`GenericConstraints.MeetsConstraints`), `docs/architecture/generator.md` (the symbol readers in `Types/MappedTypes/` name `GenericConstraints`; the resolution of mapped types says `GenericConstraints` checks the arguments; a method's reading of a row names `RowReaderWriter`, and its locals `QueryMethodLocals`), `src/SqlSource/Generation/Emitting/AGENTS.md` (the rule on locals names `QueryMethodLocals` and both writers).

- [ ] **Step 1:** Make those edits; grep `docs src tests` for `Locals`, `MeetsConstraints`, `AppendVectorChecks` and `AppendRow` to find any other reference.
- [ ] **Step 2:** Run `dotnet csharpier format .` and `./pre-commit-validation.sh`; expected: pass.
- [ ] **Step 3:** Commit `docs: record TD-0079 items 12 and 13 as fixed`.
