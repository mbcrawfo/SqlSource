# TD-0119: A static source or holder of a base type that has the repository's name is reached through the member

## Problem

[`ConnectionResolver`](../../src/SqlSource/Generation/Repositories/ConnectionResolver.Resolving.cs) writes the access of a static source, and of a static holder of a held factory, as the repository's name, then a dot, then the member's name: `Sample.Sample` for a static member `Sample` of a base type of `Sample`.  Inside the repository the simple name `Sample` binds to the inherited member before the type, so `Sample.Sample` reads a member of the member's value.  The generated method fails to compile with `CS1061`.  A member cannot have its own type's name (`CS0542`), so only a member that the repository inherits can.

## Why it exists

The access of a static member has been written from the type's name since the connection sources were first built, and the case was found only while the review of held factories checked `SQLSRC275`'s message for a static holder.  Fixing it changes how every static source is reached, which is beyond the change that found it.

## Impact

Low.  A base type must declare a static source or factory holder with the name of a type derived from it.  The build fails in the generated code, at the method, with an error that does not name the cause.  Renaming the member is the workaround.

## Proposed fix

Write a static access through the type in full, `global::App.Sample.Sample`, as a global's is, when the member has the type's name, or always, with the type's name in full from the `TargetType`.  A global's access has no leading name, so a token of the type's name would then hide nothing and `SQLSRC275` for it would go too.  Add a compile test to `MethodCompileTests` of a static source and of a static holder of a base type that has the repository's name.  Remove the limit "A static member of a base type with the repository's name" from `site/reference/repository-connections.md` and its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of `CS1061` in a repository's generated methods, or the next change to how a static source is reached.
