# Test cleanup phase 84: tests/DatabaseTests/Postgres/Realistic, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/Realistic`
- Findings: 1 (1 delete)
- Kind: one kind, delete
- Changed lines: about 4
- Guard: covers and run time

## Files

- `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticClaimsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres-2-01

- test: `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticClaimsTests.cs` | `RealisticClaimsTests.Of_CaseWithModels_IsItsClass` | rows: all
- action: delete
- evidence: read (the test asserts only that `RealisticClaims.Of(corpus, "cte-chain").TypeName` is `"CteChainMethod"`, through `RealisticClaimChecks.ShouldBeItsClass`, tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs:19-20.  `RealisticModels.MethodOf`, tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs:104-115, calls the same `RealisticClaims.Of(corpus, found)` and looks up `claim.TypeName + "Extensions"`; `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`, tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelsTests.cs:41-47, runs it on the same case and asserts the declaring type is `CteChainMethodExtensions` and the method `ListCustomerLifetimeValue`, so a wrong type name fails there too.  `Of`'s failure on a count other than one is the row `cte-chain` of `All_Claims_AreTheCasesWithModelsOnce`, and the reading of a class's name from its attribute is `Parse_ClassWithPathAttribute_IsItsClaim` in tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs:13-22.  The test is the remnant of `ShouldBeItsClassAtEachOutput`, which pinned a class at each of two outputs before the single claim (docs/superpowers/plans/2026-11-03-realistic-corpus-single-claim.md:150-154).  Search: neither the test's name nor `ShouldBeItsClass` is in any `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`; the class is named in tests/DatabaseTests/Postgres/Realistic/AGENTS.md:5,10 and tests/DatabaseTests/Postgres/README.md:79,86, which describe the per-case and subset checks and not this one, and the test has no comment of its own.  No server; 0.022 s.); blind: no
- cover: `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelsTests.cs` | `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`; `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs` | `RealisticClaimsTests.Parse_ClassWithPathAttribute_IsItsClaim`
- proposal: Delete the test.  SQL Server's twin of the same name is another slice's; if it goes too, `RealisticClaimChecks.ShouldBeItsClass` loses its last caller and goes with it.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence/DatabaseTests.Postgres-2.md:33), so there are no kills to read and the finding is held to the named-cover rule, by reading.  The test, tests/DatabaseTests/Postgres/Realistic/Generation/RealisticClaimsTests.cs:19-21, asserts only `RealisticClaims.Of(corpus, "cte-chain").TypeName == "CteChainMethod"` (tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs:19-20).  The cover `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim` (tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelsTests.cs:40-47) runs `RealisticModels.MethodOf`, which calls the same `RealisticClaims.Of(corpus, found)` (tests/SqlSource.Tool.Testing/Realistic/RealisticModels.cs:106), resolves `claim.TypeName + "Extensions"` by a case-sensitive `Assembly.GetType` in the project's namespace (lines 107-110) and asserts the declaring type is `CteChainMethodExtensions`: only a `TypeName` of exactly `CteChainMethod` passes it, so it proves the same fact on the same case and the same engine's corpus, and more.  `Of`'s count failure is also reached by every row of `All_Claims_AreTheCasesWithModelsOnce` (RealisticClaimsTests.cs:14-17, RealisticClaimChecks.cs:10-17), and the reading of a class name from its attribute by `Parse_ClassWithPathAttribute_IsItsClaim` (tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs:13-22); the claim itself is tests/DatabaseTests/Postgres/Realistic/Generation/RealisticMethods.cs:31-32.  Record search (grep over AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt, docs/deferred): neither `Of_CaseWithModels_IsItsClass` nor `ShouldBeItsClass` is named; `RealisticClaimsTests` is named at tests/DatabaseTests/Postgres/Realistic/AGENTS.md:5,10 and tests/DatabaseTests/Postgres/README.md:7,79,86, which list the class's body rule, its no-server status and the four failures of the theory and the parser (count, class without models, folder not a case, more than Source), none of them this test.  The class comment (RealisticClaimsTests.cs:6-11) names `MethodOf_Entry_IsTheMethodOfItsMethodsClaim` as the pin of the claim's output and makes this test the record of nothing; the test has no comment of its own.  It needs no server, so the cover's being a reflection test of the same project loses no engine behaviour.
- condition: `RealisticClaimChecks.ShouldBeItsClass` in `tests/SqlSource.Tool.Testing/Realistic/RealisticClaimChecks.cs` stays for SQL Server's twin; phase 87 removes it.  The review corrected `.claude/skills/reviewing-test-quality/broad-tests.txt`, which named the whole namespace `SqlSource.DatabaseTests.Postgres.Realistic.` as broad, to the classes that run the corpus.  `RealisticClaimsTests` is one of them, so its theory `All_Claims_AreTheCasesWithModelsOnce` is no cover and is dropped from the list above; `RealisticModelsTests` is not, and its `MethodOf_Entry_IsTheMethodOfItsMethodsClaim`, which asserts the exact class, is the cover, with the tool's parse test.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres-2-01 | delete | `RealisticClaimsTests.Of_CaseWithModels_IsItsClass` | `tests/DatabaseTests/Postgres/Realistic/Generation/RealisticModelsTests.cs`: `RealisticModelsTests.MethodOf_Entry_IsTheMethodOfItsMethodsClaim`; `tests/SqlSource.Tool.Tests/Realistic/RealisticClaimsTests.cs`: `RealisticClaimsTests.Parse_ClassWithPathAttribute_IsItsClaim` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/DatabaseTests/Postgres/README.md`: 7, 79, 86
- `tests/DatabaseTests/Postgres/Realistic/AGENTS.md`: 5, 10

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
