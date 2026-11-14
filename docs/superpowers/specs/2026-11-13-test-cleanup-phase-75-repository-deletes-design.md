# Test cleanup phase 75: tests/SqlSource.Repository.Tests, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Repository.Tests`
- Findings: 2 (2 delete)
- Kind: one kind, delete
- Changed lines: about 20
- Guard: covers and run time

## Files

- `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`
- `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Repository.Tests-1-02

- test: `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs` | `UserDocsLinksTests.Anchors_HeadingsOfAPage_AreTheirSlugs` | rows: all
- action: delete
- evidence: read (UserDocsLinksTests.cs:38-45 asserts only that the anchors of site/diagnostics.md include `sqlsrc001` and `sqlsrc236`. Three other tests already prove this:
  - That a heading becomes its slug anchor is proven on lines of the test's own by UserDocsReadingTests.cs:11-15.
  - That the real headings of site/diagnostics.md give their anchors is proven by RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist (UserDocsLinksTests.cs:62-68). Through `Follow` (:184-189) it checks every `#sqlsrcNNN` link of that page's index table against `UserDocs.Anchors`, and DiagnosticsDocumentTests.Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor (DiagnosticsDocumentTests.cs:46-55) makes the table hold a row for every descriptor, SQLSRC001 and SQLSRC236 among them.
  - Links_OfTheUserDocs_AreFound (UserDocsLinksTests.cs:47-55) guards against those links being found vacuously, since it requires links of site/diagnostics.md.
  The test also breaks without a behaviour change when SQLSRC236 is removed or renumbered. There is no comment above the test. A grep over AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds the class only in tests/SqlSource.Repository.Tests/Documentation/AGENTS.md:3, as the class that checks links and anchors, and the test's name nowhere.); blind: no
- cover: `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs` | `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix`; `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs` | `UserDocsLinksTests.RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist`; `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs` | `UserDocsLinksTests.Links_OfTheUserDocs_AreFound`
- proposal: Delete the test. The unit test proves a heading becomes its slug anchor, and the link-following test fails on every row of the diagnostics index if the anchors of the real page are not read.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs:38-45 asserts only that `UserDocs.Anchors("site/diagnostics.md")` contains `sqlsrc001` and `sqlsrc236`.  site/diagnostics.md gives those anchors only by heading slug (`## SQLSRC001` at :347, no `<a id>`), and its index rows link them as `#sqlsrcNNN` (:9 and :151, 161 rows).  For such a link `UserDocs.Resolve` returns the same file and the anchor (UserDocs.cs:169-171), and `Follow` (UserDocsLinksTests.cs:179-189) fails unless `UserDocs.Anchors(path)` holds it, which comes from the same `Load`/`Read` path (UserDocs.cs:74, :187-188, :237-243), so `RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist` (:62-68) fails on every row if the real page's headings stop giving their slugs.  `Document_Table_ListsEveryDescriptorWithItsTitleAndAnchor` (DiagnosticsDocumentTests.cs:46-55) makes those rows exist for every descriptor.  `Links_OfTheUserDocs_AreFound` (UserDocsLinksTests.cs:47-55) keeps the links of site/diagnostics.md from being found vacuously.  `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix` (UserDocsReadingTests.cs:11-15) proves on lines of its own that a heading becomes its slug anchor.  I read every cover, and none is a database test.  No comment sits above the test.  A grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ finds the class only in tests/SqlSource.Repository.Tests/Documentation/AGENTS.md:3, as the class that checks links and anchors, and the test's name nowhere, so it is not the record of anything.  No scope ran it (evidence: `None`), so there are no kills to read.

### F-SqlSource.Repository.Tests-2-01

- test: `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs` | `TestScopesTests.Parse_ProjectLine_IsKeyedByThePrefixedProjectName` | rows: all
- action: delete
- evidence: read (TestScopesTests.cs:103-110 parses `project postgres tests/DatabaseTests/Postgres/Postgres.csproj` and asserts the keys are `["SqlSource.DatabaseTests.Postgres"]` and the project files that one path.  `Parse_ProjectLine_MapsTheProjectToItsEngine` at TestScopesTests.cs:90-99 already proves the key is `TestScopes.AssemblyPrefix` plus the project file's name, and that `ProjectFiles` keeps the path as written.  `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects` at TestScopesFileTests.cs:78-88 parses the real tools/test-scopes.txt, whose line 14 is this same `project postgres tests/DatabaseTests/Postgres/Postgres.csproj`, and pins the literal key `SqlSource.DatabaseTests.Postgres`, so the prefix's value is pinned too.  No test of the three tells a folder's name from a file's, since every path used has the two equal, so this test adds nothing there either.  The agreement with the real assembly names, which the comment at TestScopesTests.cs:101-102 cites, is checked by `RunningProjectTests` of each container project (tests/DatabaseTests/Postgres/RunningProjectTests.cs:14), not here.  A grep of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name and `TestScopesTests` finds nothing: it is the record of no limit or decision, and only the plan docs/superpowers/plans/2026-10-28-database-tests-1-move-and-rename.md names it, as the TDD step that introduced the prefix); blind: no
- cover: `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs` | `TestScopesTests.Parse_ProjectLine_MapsTheProjectToItsEngine`; `tests/SqlSource.Repository.Tests/Validation/TestScopesFileTests.cs` | `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects`
- proposal: Delete the test.  If the owner wants the prefix's value pinned in the unit tests too, keep one assertion of the literal `SqlSource.DatabaseTests.X` key in `Parse_ProjectLine_MapsTheProjectToItsEngine` instead.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence/SqlSource.Repository.Tests-2.md:14), so there are no kills to read and the test is held to the named-cover rule.  Read the test (tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs:101-110), its subject (tests/SqlSource.Repository/TestScopes.cs:32-36, 161-177) and both covers.  The key is `AssemblyPrefix + Path.GetFileNameWithoutExtension(path)` (TestScopes.cs:35-36, 170).  `Parse_ProjectLine_MapsTheProjectToItsEngine` (TestScopesTests.cs:90-99) proves that a project line is keyed by the prefix plus the file name with the folder and `.csproj` dropped, and that `ProjectFiles` keeps the path as written (line 97).  The one thing it cannot see, the prefix's literal value since it reads `TestScopes.AssemblyPrefix`, is pinned by `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects` (TestScopesFileTests.cs:78-88), which parses tools/test-scopes.txt, whose line 14 is the same `project postgres tests/DatabaseTests/Postgres/Postgres.csproj`, and expects the literal `SqlSource.DatabaseTests.Postgres`.  No test of the three tells a folder name from a file name, since every path has the two equal, so the test adds nothing there either.  The agreement with the real assembly names that the comment at TestScopesTests.cs:101-102 cites is guarded by `RunningProjectTests.RunningProject_ThisProject_IsAContainerProjectOfTheScopes` in each of the four container projects (e.g. tests/DatabaseTests/Postgres/RunningProjectTests.cs:13-14), as TestScopes.cs:30-31 says; the naming rule itself is recorded in tests/DatabaseTests/AGENTS.md:7 and tests/DatabaseTests/Directory.Build.props:2-12, neither of which names this test.  A grep of every AGENTS.md, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for `Parse_ProjectLine_IsKeyedByThePrefixedProjectName` and `TestScopesTests` finds nothing; the only document naming the test is docs/superpowers/plans/2026-10-28-database-tests-1-move-and-rename.md:54, where it is the failing-first step of the TDD task that added the prefix, not the record of a limit, regression or decision.  The comment above the test explains the naming and does not call the test a record; the class comment (TestScopesTests.cs:7-8) is about the reader's grammar.  The proposal's fallback, one literal assertion in `Parse_ProjectLine_MapsTheProjectToItsEngine`, is optional and not needed for the cover to hold.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Repository.Tests-1-02 | delete | `UserDocsLinksTests.Anchors_HeadingsOfAPage_AreTheirSlugs` | `tests/SqlSource.Repository.Tests/Documentation/UserDocsReadingTests.cs`: `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix`; `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`: `UserDocsLinksTests.RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist`; `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`: `UserDocsLinksTests.Links_OfTheUserDocs_AreFound` |
| F-SqlSource.Repository.Tests-2-01 | delete | `TestScopesTests.Parse_ProjectLine_IsKeyedByThePrefixedProjectName` | `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs`: `TestScopesTests.Parse_ProjectLine_MapsTheProjectToItsEngine`; `tests/SqlSource.Repository.Tests/Validation/TestScopesFileTests.cs`: `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Repository.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md`: 3

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
