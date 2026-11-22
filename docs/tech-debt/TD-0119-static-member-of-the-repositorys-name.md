# TD-0119: A repository nested in a generic type cannot use a static connection when a member takes its name

## Problem

[`ConnectionResolver`](../../src/SqlSource/Generation/Repositories/ConnectionResolver.Resolving.cs) writes a static access through the repository's type in full from `global::`, except for a repository nested in a generic type.  There, a nearer type parameter or nested type, a generated one too, can hide an outer type parameter, and the path would name another constructed type or fail to compile, so the access starts at the repository's simple name.  An inherited member of that name then takes the name from the type, and `Sample.ConnectionString` would read through the member.  [`RepositoryShapeReader`](../../src/SqlSource/Generation/Repositories/RepositoryShapeReader.cs) finds such a member, and `SQLSRC039` refuses the repository's static source or holder, unless the member is a field or property of the repository's own type, which C#'s Color Color rule lets through.

## Why it exists

The full path fixed every repository that is not nested in a generic type.  For the rest, the owner chose today's access over a check of every name a path could meet, since generated nested classes do not exist when the check runs, and chose to refuse the shape over a method that could silently read another member.

## Impact

Low.  The repository must be nested in a generic type, use a static source or holder, and inherit a member with its name.  The build reports the cause at the repository.  Renaming the member, using an instance source or moving the repository are the workarounds.

## Proposed fix

Give such a repository a way to reach its static member that no name can hide, such as a generated private accessor, and remove `SQLSRC039`'s refusal.  Add a compile test of each shape to `MethodCompileTests`, and remove the limit "A static member of a base type with the repository's name" from `site/reference/repository-connections.md` with its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of `SQLSRC039`, or the next change to how a static source is reached.
