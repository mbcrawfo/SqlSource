# TD-0121: The interface of a repository around which a type parameter is hidden is another constructed type

## Problem

[`MethodEmitter.InterfaceOf`](../../src/SqlSource/Generation/Emitting/MethodEmitter.cs) writes the repository's interface in its base list from `global::`, through each type around it with its type parameters.  When a type parameter repeats one of an outer type, as in `Outer<T>.Repo<T>`, both `T`s in `global::App.Outer<T>.IRepo<T>` are `Repo`'s.  So `Outer<int>.Repo<string>` implements `Outer<string>.IRepo<string>` and not `Outer<int>.IRepo<string>`.  The generated declaration of the interface repeats the parameter too, which is the compiler's warning CS0693 in generated code.  A nearer type with a nested type of the outer parameter's name, its own or inherited, does the same with no warning: in `Outer<T>.Mid` with a nested class `T`, `Mid.Repo` implements `Outer<Outer<T>.Mid.T>.Mid.IRepo`.

## Why it exists

The reference was written for the usual case, where every type parameter around the repository keeps its name inside it.  A review of the static connection access found it, and the owner chose to record it, since the interface is separate generated code from that access.

## Impact

Low.  The shapes are legal but rare.  C# warns on a repeated type parameter in the user's own declaration, and on a nested type of that name it says nothing.  A caller that uses the interface of the repository's own constructed type gets a compile error, or an `InvalidCastException` from an explicit cast.

## Proposed fix

Name the interface in the base list by its simple name with the repository's type parameters when the repository is nested in a generic type, so that the outer parameters stay the repository's own, and check that an inherited type of that name cannot take the reference.  Stop the generated interface from raising CS0693, then add a compile test of each shape to `MethodCompileTests` and remove the limit "A type parameter of an outer type that is hidden" from `site/reference/repositories.md` with its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of CS0693 in a generated file, or of a repository that does not implement its interface.
