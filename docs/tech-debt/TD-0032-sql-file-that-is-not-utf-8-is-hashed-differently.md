# TD-0032 - A `.sql` file that is not UTF-8 is hashed differently by the tool and by the compiler

## Problem

[`SqlFileText`](../../src/SqlSource.Tool/Planning/SqlFileText.cs) reads a `.sql` file with `SourceText.From` over its bytes: a byte order mark decides the encoding, and without one the bytes are read as UTF-8, with a replacement character for a sequence that is not UTF-8.  The compiler reads an additional file another way: UTF-8 first, and when the bytes are not valid UTF-8 it reads the whole file again in a fallback encoding, the code page 1252 where the runtime has it, and in the one the project's `CodePage` names when it names one.

So a query that holds a character outside ASCII, in a file saved as Windows-1252, has one text in the tool and another in the generator.  The hash of `SqlQueryHash` is of that text.  From phase 5 the generator will call such a query's entry stale, whatever the tool writes.

[`AttributeReader`](../../src/SqlSource.Tool/Planning/AttributeReader.cs) reads a C# file the same way.  There it matters only for a `Path` that is written with a character outside ASCII, in a C# file that is not UTF-8: the tool then looks for another folder than the build does.

## Why it exists

The compiler's reader, `EncodedStringText`, is not public, and its fallback differs by runtime.  Copying it into the tool means copying code that can change under it.  The manifest does not carry `CodePage` either.

## Impact

Low.  It takes a `.sql` file that is not UTF-8 with a character outside ASCII in the SQL itself, not in a comment: the hash is of the SQL without comments.  Files written by any current editor are UTF-8.

## Proposed fix

Read the bytes as strict UTF-8, and on failure with the encoding the compiler falls back to; add `CodePage` to the manifest as a new key, which is an addition to its format.  Or report a `.sql` file that is not valid UTF-8 as an error in the tool, which is simpler and tells the user to save it as UTF-8.

## Trigger

Phase 5, when the generator first compares a hash; or a user whose entries are always stale for one file.
