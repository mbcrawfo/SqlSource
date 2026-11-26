# TD-0095 - A cref to a reused exception can be ambiguous or unresolved

## Problem

A project that uses a referenced copy of `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` declares no copy in source ([`ExceptionProbe`](../../src/SqlSource/Generation/Exceptions/ExceptionProbe.cs) gives `Referenced`).  The compiler binds a `cref` without regard to accessibility.  So when the project also references another assembly built with SqlSource whose internal copy it cannot see, a bound cref in the project's own documentation comments finds both copies: one that names any of the three, such as `<exception cref="UnexpectedRowCountException"/>`, gets CS0419, and one that names a member of one, such as `<see cref="UnexpectedRowCountException.QueryName"/>` or a constructor, gets CS1574.  Before the exceptions were reused, the project's own source copy always won the lookup and nothing was reported.

The generated methods avoid this by writing documentation IDs (`T:SqlSource.UnexpectedRowCountException`) in [`HeaderWriter`](../../src/SqlSource/Generation/Emitting/QueryMethods/HeaderWriter.cs).  A user's own crefs cannot be changed by the generator; the workaround is the documentation ID of what the cref names, `T:` for an exception, `P:` or `M:` for a member.  `ExceptionReuseTests.Build_UserCrefWithAStrangersCopyReferenced_IsAWarning` pins the behaviour, `Build_UserCrefWithTheFriendsCopyAlone_HasNoDiagnostic` that the stranger's copy is its cause, and `Build_UserCrefAsADocumentationIdWithAStrangersCopyReferenced_HasNoDiagnostic` the workaround.

## Why it exists

Reusing a referenced copy is how the exceptions avoid CS0436 between projects that share internals, and it makes the exceptions catchable by type across the friend boundary.  Cref binding ignoring accessibility is a property of the compiler that the generator cannot change, and the only copies it could hide are those of other assemblies, which this project's generator does not write.

## Impact

Only a project with all three: it reuses a friend's exceptions, it references another SqlSource assembly that grants it no `InternalsVisibleTo`, and it builds a documentation file with crefs to the exceptions.  It gets a warning, which fails the build under `TreatWarningsAsErrors`.  The manual's limit, `site/reference/exceptions.md#limit-cref`, gives the workaround.  No generated code is affected.

## Proposed fix

Investigate making the copies that a project cannot see invisible to cref binding.  One narrowing: a project that grants no `InternalsVisibleTo` and does not set `SqlSourcePublicExceptions` has no reader for its exceptions outside itself, so it could write them `[Microsoft.CodeAnalysis.Embedded]`, which other compilations do not see at all, crefs included.  The generator would read whether the project grants `InternalsVisibleTo`, and `ExceptionSource.TextFor` would take the attribute as a third form.  This still misses a library that grants `InternalsVisibleTo` to its own tests, whose copy stays visible to cref binding everywhere, and a stranger built with an earlier SqlSource, so it narrows the problem without closing it.

The narrowing was built and measured on 2026-11-25, and was not kept, because it costs every edit of every project for a Low warning that has a workaround:

- **It works.**  With the stranger's copy embedded, the crefs of `Build_UserCrefWithAStrangersCopyReferenced_IsAWarning` bound with no warning, on an image and on a compilation, and a friend given `InternalsVisibleTo` still reused its copy.  A project without SqlSource that names a stranger's exception gets CS0234 instead of CS0122.
- **What it costs.**  A `Stopwatch` loop over a `Release` build of the generator on Roslyn 4.14.0 timed 1,000 edits of a method body in one file, after 50 to warm up, in a project whose `AssemblyInfo.cs` held MSBuild's eight attributes and an `InternalsVisibleTo`.  Reading the grant with `ForAttributeWithMetadataName` on `System.Runtime.CompilerServices.InternalsVisibleToAttribute`, collected to a `bool`, added about 360 µs and 173 KB to each edit of a project of 300 files of 150 lines, whose edit cost about 890 µs and 651 KB, and about 265 µs and 75 KB to one of 50 files of 3,000 lines, whose edit cost about 1,380 µs and 532 KB.  Reading it from `compilation.Assembly.GetAttributes()` in a `Select` of the compilation added about 190 µs and 56 KB to the first and 40 µs and 55 KB to the second, about a tenth of the generator's allocation on each edit, since every new compilation binds its assembly attributes again.
- **A risk it adds.**  An `InternalsVisibleTo` that another source generator writes is in neither read, so that project's copy would be embedded, and its friends could no longer name it: catching its exceptions by type across the two projects would break.  This case was not built.

A fix that closes the problem, or a read of the grant that costs an edit nothing when the assembly's attributes did not change, would be worth doing.

## Trigger

A user report of CS0419 at a cref to `UnexpectedRowCountException`, `TransactionRolledBackException` or `CollectionItemException`, or of CS1574 at a cref to a member of one of them.  A Roslyn API that tells cheaply whether a compilation's assembly grants `InternalsVisibleTo`.
