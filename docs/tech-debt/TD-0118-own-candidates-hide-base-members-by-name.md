# TD-0118: A repository's own candidates hide a base type's members by name alone

## Problem

[`ConnectionCandidateReader.ReachableMembersOf`](../../src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs), which gives the members that tier 1 of a repository's connection sources reads, and the field and property holders of tier 2, skips a member of a base type when any member of a more derived type has its name.  C# hides a method only by a method of the same signature, and a member that the repository cannot reach hides nothing at its call.  So:

- a method `Open(int retries)` of the repository hides a marked `Open()` of its base type, which the repository could call;
- a `private` member of a base type between the two hides a marked member of the base type below it.

The factory walk of [`HeldFactoryReader`](../../src/SqlSource/Generation/Repositories/HeldFactoryReader.cs) hides almost as C# does: a member hides only the members of its name in the types its type derives from, a method hides a method only of its signature, and a member the repository cannot reach hides nothing.  One case differs.  For a holder whose type is a type parameter constrained to a class and to an interface that the class does not implement, C# drops an interface member that a class member of its name hides, and the walk keeps both as candidates, which can report `SQLSRC029` where the call would bind to the class's member alone.

## Why it exists

Name hiding is how tier 1 has read a repository's members since the connection sources were first built, and tests and users rely on what it finds.  Moving tier 1 to signature hiding can change which member a repository uses, so it was kept out of the work that added held factories and globals, which keeps tier 1 unchanged.

## Impact

Low.  A repository that declares a member of the name of its base type's marked member, or derives through a base type with a private member of that name, does not use the marked member: it reports `SQLSRC028`, or uses another source.  Renaming the member is the workaround.

## Proposed fix

Make `ReachableMembersOf` hide as `HeldFactoryReader.IsHidden` does: a member of a base type is hidden by a member of its name, of a more derived type, that the repository can reach, unless both are methods and their arity, parameter types and kinds of reference differ.  Share the hiding between the two walks.  In the factory walk, let a class constraint's member hide an interface constraint's members of its name, as a method or not, as C# does for a type parameter.  Add tests to `ConnectionCandidateReaderTests` of an overload in the repository and of a private member of a base type between two, and keep `Read_BaseMemberHiddenByTheRepository_IsNotACandidate`, in which a property hides a property, passing.  Remove the limit "A marked member of a base type is not found" from `site/reference/repository-connections.md` and its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of a marked base member that is not found, or the next change to how tier 1 reads a repository's members.
