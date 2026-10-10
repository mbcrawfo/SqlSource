# TD-0031 - The tool reads `[SqlSourceGenerate]` as syntax, and misreads what only a compilation can tell

## Problem

[`AttributeReader`](../../src/SqlSource.Tool/Planning/AttributeReader.cs) finds the attribute by its name in the text of a project's C# files.  The generator finds it by its symbol.  Four things are therefore read differently by the two:

- **An alias.**  `using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]` is a claim for the generator and nothing for the tool, so the type's queries are never described and, from phase 5, its build says that their entries are missing.
- **A type of the same name.**  A project's own `SqlSourceGenerateAttribute` in another namespace is a claim for the tool and nothing for the generator.  The tool describes queries that nothing uses.
- **`#if` under another configuration.**  The tool reads the files with the constants of the manifest, which are those of the configuration that `dotnet msbuild` evaluates, `Debug` unless the environment says otherwise, and of the first target framework.  An attribute inside `#if RELEASE` is not seen.
- **The attribute on two partial declarations of one type.**  The generator takes the first, and the tool takes each for a claim, so the type's files need what either attribute asks for.  The compiler rejects the second attribute, so only a project that does not build is read this way.

A value that is not a literal is not among them: the tool reports it, `SQLSRC208`.

## Why it exists

The tool has no compilation.  Making one means resolving every reference of the project, which is a design-time build: slow, and the kind of thing the tool avoids by reading a manifest.  The epic chose the syntax reader with this cost in view.

## Impact

Low.  Each case takes a project that does something unusual with the attribute's name, and the first is found at once from phase 5, when the build reports the missing entries.

## Proposed fix

For the alias: read the `using` aliases of a file, and of the project's global usings, and match an attribute through them.  For the configuration: let `describe` take the configuration to evaluate, as `dotnet build -c` does.  The type of the same name needs symbols, and is left.

## Trigger

A user reports a type whose queries the tool does not describe, or describes and should not.
