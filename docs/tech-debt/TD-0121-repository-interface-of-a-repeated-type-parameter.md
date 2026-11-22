# TD-0121: The interface of a repository that repeats a type parameter is another constructed type

## Problem

[`MethodEmitter.InterfaceOf`](../../src/SqlSource/Generation/Emitting/MethodEmitter.cs) writes the repository's interface in its base list from `global::`, through each type around it with its type parameters.  When a type parameter repeats one of an outer type, as in `Outer<T>.Repo<T>`, both `T`s in `global::App.Outer<T>.IRepo<T>` are `Repo`'s.  So `Outer<int>.Repo<string>` implements `Outer<string>.IRepo<string>` and not `Outer<int>.IRepo<string>`.  The generated declaration of the interface repeats the parameter too, which is the compiler's warning CS0693 in generated code.

## Why it exists

The reference was written for the usual case, where no type parameter repeats an outer one.  The review of TD-0119's fix found it, and the owner chose to record it, since the interface is separate generated code from the connection access that change made.

## Impact

Low.  The shape is legal but rare, and C# already warns on the user's own declaration.  A caller that uses the interface of the repository's own constructed type gets a compile error, or an `InvalidCastException` from an explicit cast.

## Proposed fix

Name the interface in the base list by its simple name with the repository's type parameters when `TypeDeclaration.RepeatsATypeParameter` holds at any level, so that the outer parameters stay the repository's own, and check that an inherited type of that name cannot take the reference.  Stop the generated interface from raising CS0693, then add a compile test of each shape to `MethodCompileTests` and remove the limit "A type parameter that repeats one of an outer type" from `site/reference/repositories.md` with its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of CS0693 in a generated file, or of a repository that does not implement its interface.
