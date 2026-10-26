# TD-0079: a file that another generator adds reaches settings files

## Problem

The build checks a settings file only when the chain of the folder of some `.sql` file or C# file of the project reaches it ([which files are checked](../architecture/generator.md#which-files-are-checked)).  The C# files are the paths of the compilation's syntax trees, `CompilePaths` in [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs), and the compiler adds each generator's post-initialization files to the compilation before any generator runs.  `NormalizedCompilePaths` leaves out SqlSource's own attribute file, by the end of its path, and takes a file that another generator adds the same way for a file of the project.  Its path is the compiler's: the folder for generated files, `obj/<configuration>/<framework>/` or `CompilerGeneratedFilesOutputPath`, then folders named after the generator's assembly and type.  So the chain of that folder can reach a settings file that no item reaches, and its problems are reported.

## Why it exists

Roslyn 4.8.0 does not mark a generated tree, and a generator cannot learn the folder for generated files or the names of other generators, so nothing tells such a tree from a `Compile` item.

## Impact

Low.  In a build the folder for generated files is inside the project's folder, so the chain of a generated file reaches the project's folder and the folders above it, which almost every project's own C# files reach too.  It differs only when every item's chain ends at a `"root": true` below the project's folder, or when `CompilerGeneratedFilesOutputPath` points outside the project, and then a settings file the owner's rule leaves unchecked is reported, and could fail the build.  The `sqlsource` tool reads the `Compile` items of the manifest, which hold no generated file, so it would not report that file.

## Proposed fix

Tell the project's own trees apart: for instance, pass the `Compile` items to the generator, as the build already passes the settings files, through an item the package lists and the compiler gives as additional files or as metadata, and reach the chains from those paths instead of the trees.  Or, when Roslyn gains a public way to know that a tree was generated, use it.

## Trigger

A report of a settings file's error from a build whose project has no item under it, or a change that gives the generator the list of `Compile` items for another reason.
