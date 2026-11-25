# TD-0031 - The tool reads `[SqlSourceGenerate]` as syntax, and takes a type of the same name for it

## Problem

[`AttributeReader`](../../src/SqlSource.Tool/Planning/AttributeReader.cs) finds the attribute by its name, or by a `using` alias of it, in the text of a project's C# files and its `Using` items.  The generator finds it by its symbol.  So a project's own `SqlSourceGenerateAttribute` in another namespace is a claim for the tool and nothing for the generator, and the tool describes queries that nothing uses.  So is an alias of such a type, since an alias's target is matched by its last name alone.

A `Source` that is not a literal is not among the differences: the tool reports it, `SQLSRC208`.  Nor is an attribute inside `#if`, which the tool reads with the constants of the configuration it evaluates, chosen by `--configuration` as `dotnet build -c` does, nor the attribute on two partial declarations of one type, which the tool reads at the first, as the generator does.

## Why it exists

The tool has no compilation.  Making one means resolving every reference of the project, which is a design-time build: slow, and the kind of thing the tool avoids by reading a manifest.  The epic chose the syntax reader with this cost in view.

## Impact

Low.  It takes a project that names a type of its own after the attribute.  The manual lists it as a limit, [an attribute of your own named `SqlSourceGenerate`](../../site/reference/attributes.md#limit-attribute-of-the-same-name).

## Proposed fix

It needs what a name means in the compilation: the types each namespace holds.  A reader could follow `using` directives and namespaces to the namespace of the attribute's name, and refuse one that does not reach `SqlSource`, at the cost of reading every C# file of the project for the types it declares, and the types of its references for those it does not.

## Trigger

A user reports a type whose queries the tool describes and should not.
