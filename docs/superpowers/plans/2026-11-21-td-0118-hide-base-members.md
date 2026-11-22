# TD-0118: Hide Base Members as C# Does Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A repository's connection candidates and holders, and a factory's members, are hidden only where the C# compiler would bind the generated call or read to another member.

**Architecture:** A new `MemberHiding` static class holds the one hiding rule, moved out of `HeldFactoryReader`.  `ConnectionCandidateReader.ReachableMembersOf` and `HeldFactoryReader` both call it, each with the list of types its lookup searches, most derived first.

**Tech Stack:** C#, Roslyn 4.14 symbols, `netstandard2.0`, xUnit, Shouldly.

**Spec:** `docs/tech-debt/TD-0118-own-candidates-hide-base-members-by-name.md`

## Global Constraints

- No Roslyn API newer than 4.14.0; nothing a user writes may make the generator throw.
- `RepositoryShapes` runs on every compilation, so the PR states the read's time and allocation, before and after, from a `Stopwatch` loop over a `Release` build.
- `Read_BaseMemberHiddenByTheRepository_IsNotACandidate` keeps passing unchanged.
- The PR description states that tier 1 can now use a different member in an existing repository.
- Validate with `./format.sh`, then `./pre-commit-validation.sh`, before each commit, through `zsh -lc`.

## Where the code differs from the spec

Compiled on .NET 8, the spec's rule differs from C# in three ways.  They are owner questions Q1 to Q3, and Tasks 2 and 3 wait for the answers:

- **Q1:** a derived method of another signature that can take the generated call hides the base method at that call.
- **Q2:** a non-invocable member does not hide a method at a call, and a generic nested type hides nothing.
- **Q3:** a class constraint's method hides an interface's method by signature, or by taking the call, not by name.

## Review Focus

- A marked base `Open()` beside a derived `Open(int retries = 3)`: the generated call must never run the derived method (Task 2).
- A `new` property with a `private` getter over a base property: C# binds to it and fails, so the base property is not a candidate (Task 1).
- A marked constructor or destructor of a base type: neither is inherited, so neither is a candidate (Task 1).
- A marked accessor of a base property that a derived property hides: it is hidden with its property (Task 1).
- Marked base indexers beside a derived indexer: only one of the same signature hides (Task 1).

---

### Task 1: Share the hiding, by what the repository can reach

**Files:**
- Create: `src/SqlSource/Generation/Repositories/MemberHiding.cs`
- Modify: `src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs`, `HeldFactoryReader.cs`, `AGENTS.md` of that folder if a named member moves
- Test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs`, `HeldFactoryReaderTests.cs`

**Interfaces:**
- Produces: `internal static bool MemberHiding.IsHidden(ISymbol member, INamedTypeSymbol declaring, IReadOnlyList<INamedTypeSymbol> lookedIn, INamedTypeSymbol type, Compilation compilation, ITypeSymbol? throughType)`.
- `ConnectionCandidateReader.OwnerOf` becomes `internal`.

- [ ] **Step 1: Write the failing tests**

In `ConnectionCandidateReaderTests`, each through `RepositoryShapes.Of`:

- `Read_BaseMethodBesideARequiredOverload_IsACandidate`: repository `Open(int retries)`, marked base `Open()`; one candidate `Open`, `IsInherited` true.
- `Read_PrivateMemberOfABaseTypeBetween_HidesNothing`: marked `Open()` on the lowest base, `private` `Open()` on the middle; one candidate.
- `Read_PropertyWithAnUnreachableGetter_HidesTheBases`: base `protected string ConnectionString`, repository's base `public new string ConnectionString { private get; set; }`; no candidates.
- `Read_MarkedConstructorOfABaseType_IsNotACandidate`: base with a marked `protected` constructor taking a `string`, repository with a parameterless one; no candidates.
- `Read_MarkedAccessorOfAHiddenProperty_IsNotACandidate`: base property with a marked getter, repository `new` property of the name; no candidates.
- `Read_MarkedBaseIndexerOfAnotherSignature_IsACandidate`: marked base `this[int]`, repository `this[string]`; one `Unsupported` candidate.

In `HeldFactoryReaderTests`:

- `Read_BaseHolderBehindAPrivateMember_IsAHolder`: lowest base holds a factory in `protected` `Connections`, middle has `private` `Connections`; one held candidate.
- `Read_BaseHolderHiddenByAMethod_IsNotAHolder`: repository method `Connections()` over the base field; no held candidates.
- `Read_FactoryPropertyWithAnUnreachableGetter_HidesTheBases`: factory `public new DbConnection Connection { private get; set; }` over a marked base property; no held candidates.

- [ ] **Step 2: Run them to verify they fail**

Run: `zsh -lc 'dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~ConnectionCandidateReaderTests|FullyQualifiedName~HeldFactoryReaderTests"'`
Expected: the new tests that change behaviour FAIL.

- [ ] **Step 3: Implement `MemberHiding`**

Move `IsHidden`, `DerivesFrom`, `HaveOneSignature`, `AreOneType` and `AreOneContainer` from `HeldFactoryReader`, which calls `MemberHiding.IsHidden`.  The rule:

- An accessor or backing field is asked as its owner (`OwnerOf`).
- A hiding member counts when `compilation.IsSymbolAccessibleWithin(hiding, type, throughType)`, never its getter's access, since C# binds to the property and then fails.
- It must be referable by name (`CanBeReferencedByName`), or an indexer against an indexer of one signature.
- `ReachableMembersOf` skips an inherited constructor, static constructor or destructor, and calls `IsHidden` with the type and its base types.

- [ ] **Step 4: Run the tests to verify they pass**

Same command.  Expected: PASS, `Read_BaseMemberHiddenByTheRepository_IsNotACandidate` included.

- [ ] **Step 5: Commit** as `fix: hide a repository's base members by what it can reach`.

### Task 2: Hide as the call binds (after Q1 and Q2)

**Files:** `MemberHiding.cs`, both test classes.

**Interfaces:** Consumes `MemberHiding.IsHidden`.  Its method check also takes the call the build writes: no arguments, or one `CancellationToken`.

- [ ] **Step 1: Write the failing tests**, as theories over the overloads compiled for Q1: `Open(int retries = 3)`, `Open(params int[] r)`, `Open(object o)`, `Open<T>(T o)`, `Open(in CancellationToken t)`, `Open(CancellationToken? t)` hide; `Open(ref CancellationToken t)` and `Open(int required)` do not.  For Q2: `new int Open` over a marked method does not hide, a delegate-typed field does, and a nested `Conn<T>` over a marked field does not.  Each theory also compiles the generated code in a driver test and asserts no errors.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement the owner's answers** in `MemberHiding`, with `compilation.ClassifyConversion` for a token argument.
- [ ] **Step 4: Run them to verify they pass.**
- [ ] **Step 5: Commit** as `fix: hide a base member only where the call binds elsewhere`.

### Task 3: A class constraint hides an interface's members (after Q3)

**Files:** `HeldFactoryReader.cs`, `MemberHiding.cs`, `HeldFactoryReaderTests.cs`.

- [ ] **Step 1: Write the failing tests**: `Read_ClassConstraintsMemberOfTheName_HidesTheInterfaces` for a same-signature method, a property over a method and a method over a property; and `Read_ClassMethodThatCannotTakeTheCall_LeavesTheInterfaces` for `Open(int required)`.  The first gives one candidate and no `SQLSRC029`.
- [ ] **Step 2: Run them to verify they fail.**
- [ ] **Step 3: Implement** the class-over-interface step in `MemberHiding.IsHidden`, for a `declaring` interface and a class in `lookedIn`.
- [ ] **Step 4: Run them to verify they pass.**
- [ ] **Step 5: Commit** as `fix: let a class constraint hide an interface's members`.

### Task 4: Resolve the item in the documents

**Files:** `site/reference/repository-connections.md`, `site/guides/troubleshooting.md`, `docs/tech-debt/README.md`, `docs/tech-debt/TD-0118-own-candidates-hide-base-members-by-name.md`, `docs/architecture/generator.md` if it states the old rule.

- [ ] **Step 1:** Remove the limits `limit-member-of-the-same-name` and `limit-type-parameter-holder` and their two troubleshooting rows.  Rewrite line 195's link as a plain statement that a member the repository's call would not reach is not used.
- [ ] **Step 2:** Delete the item file and its row, and confirm `git grep TD-0118 -- ':!docs/superpowers'` is empty.
- [ ] **Step 3:** Run `./format.sh` and `./pre-commit-validation.sh`.  Expected: pass.
- [ ] **Step 4: Commit** as `docs: resolve TD-0118`.
