# TD-0119: A static source or holder with the repository's name fails when a type parameter around it is hidden

## Problem

[`ConnectionResolver`](../../src/SqlSource/Generation/Repositories/ConnectionResolver.Resolving.cs) writes a static access through the repository's type in full from `global::`.  That path cannot name the repository when a nearer type repeats a type parameter of a type around it, or has a nested type of its name, `RepositoryShape.HidesATypeParameter`, since the outer parameter has no name inside the repository.  Such a repository is reached through its own simple name instead, as `Sample.Sample` for a static member `Sample` of a base type of `Sample`.  Inside the repository, `Sample` binds to the inherited member before the type, so the generated method fails to compile with `CS1061`.  A generic repository is hidden the same way only by a generic member or nested type of its name and arity, and an inherited nested type of that name and arity that has a static member of the source's name is read in its place.

## Why it exists

The full path fixed every other repository.  Reaching this one by the member's bare name would make a token of that name an error, and could not reach a member named as a method's argument or a type parameter, each of which builds today.

## Impact

Low.  The repository needs both a hidden type parameter and a base type's member with its name.  The build fails in the generated code, at the method, with an error that does not name the cause.  Renaming the member or the type parameter is the workaround.

## Proposed fix

Decide from the symbols whether the simple name binds to the repository, and when it does not, reach the member by its bare name if no argument or type parameter has that name, with a diagnostic for what remains.  Add a compile test to `MethodCompileTests` of each shape, and remove the limit "A static member of a base type with the repository's name" from `site/reference/repository-connections.md` with its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of `CS1061` in a repository's generated methods, or the next change to how a static source is reached.
