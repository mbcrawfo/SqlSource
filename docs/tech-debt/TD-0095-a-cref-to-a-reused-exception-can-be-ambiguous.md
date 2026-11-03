# TD-0095 - A cref to a reused exception can be ambiguous or unresolved

## Problem

A project that uses a referenced copy of `UnexpectedRowCountException` and `TransactionRolledBackException` declares no copy in source ([`ExceptionProbe`](../../src/SqlSource/Generation/Exceptions/ExceptionProbe.cs) gives `Referenced`).  The compiler binds a `cref` without regard to accessibility.  So when the project also references another assembly built with SqlSource whose internal copy it cannot see, a bound cref in the project's own documentation comments finds both copies: one that names either exception, such as `<exception cref="UnexpectedRowCountException"/>`, gets CS0419, and one that names a member of either, such as `<see cref="UnexpectedRowCountException.QueryName"/>` or a constructor, gets CS1574.  Before the exceptions were reused, the project's own source copy always won the lookup and nothing was reported.

The generated methods avoid this by writing documentation IDs (`T:SqlSource.UnexpectedRowCountException`) in [`HeaderWriter`](../../src/SqlSource/Generation/Emitting/QueryMethods/HeaderWriter.cs).  A user's own crefs cannot be changed by the generator; the workaround is the documentation ID of what the cref names, `T:` for an exception, `P:` or `M:` for a member.  `ExceptionReuseTests.Build_UserCrefWithAStrangersCopyReferenced_IsAWarning` pins the behaviour, `Build_UserCrefWithTheFriendsCopyAlone_HasNoDiagnostic` that the stranger's copy is its cause, and `Build_UserCrefAsADocumentationIdWithAStrangersCopyReferenced_HasNoDiagnostic` the workaround.

## Why it exists

Reusing a referenced copy is how the exceptions avoid CS0436 between projects that share internals, and it makes the exceptions catchable by type across the friend boundary.  Cref binding ignoring accessibility is a property of the compiler that the generator cannot change, and the only copies it could hide are those of other assemblies, which this project's generator does not write.

## Impact

Only a project with all three: it reuses a friend's exceptions, it references another SqlSource assembly that grants it no `InternalsVisibleTo`, and it builds a documentation file with crefs to the exceptions.  It gets a warning, which fails the build under `TreatWarningsAsErrors`.  The manual's known issue gives the workaround.  No generated code is affected.

## Proposed fix

Investigate making the copies that a project cannot see invisible to cref binding.  One narrowing: a project that grants no `InternalsVisibleTo` and does not set `SqlSourcePublicExceptions` has no reader for its exceptions outside itself, so it could write them `[Microsoft.CodeAnalysis.Embedded]`, which other compilations do not see at all, crefs included.  The generator would read the project's `InternalsVisibleTo` attributes from the compilation (a cheap read of the assembly's attributes) beside `ExceptionProbe`, and `ExceptionSource.TextFor` would take the attribute as a third form.  This still misses a library that grants `InternalsVisibleTo` to its own tests, whose copy stays visible to cref binding everywhere, so it narrows the problem without closing it; check whether that is worth the extra input to the pipeline first.

## Trigger

A user report of CS0419 at a cref to `UnexpectedRowCountException` or `TransactionRolledBackException`, or of CS1574 at a cref to a member of either.
