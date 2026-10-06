# TD-0013 - An edit to a `.sql` file makes the compiler read every attributed type again

## Problem

[`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs) adds its attribute in a post-initialization step.  With that step registered, every run of the generator runs the steps that depend on the compilation again, even when the project's compilation is the same object as in the run before and only a `.sql` file changed: [`TargetTypeReader.Read`](../../src/SqlSource/Generation/TargetTypeReader.cs) for every attributed type, and the framework check.  Their results are equal to the previous ones, so no step after them runs for a type whose files did not change, but the reads themselves are the cost.

Measured on a `Release` build with Roslyn 5.9.0, a driver that is given the same `Compilation` each time, and one `.sql` file replaced before each run:

| Types | Claimed files | SqlSource | A generator with the same attribute step that only reads each type's name | The same, with the attribute declared in the project |
|----|----|----|----|----|
| 200 | 1,000 | 7.6 ms, 2.6 MB | 4.3 ms, 1.7 MB | 0.5 ms, 0.1 MB |
| 1,000 | 3,000 | 32 ms, 11.3 MB | 27 ms, 7.9 MB | 0.6 ms, 0.4 MB |

The third generator read no type on an edit, and the second read every one.  Most of the cost is therefore the compiler finding the attributed types again, not `TargetTypeReader.Read`.  The cause was not confirmed in the compiler's source: the likely one is that the driver adds the post-initialization file to the compilation on each run, which makes a new compilation.

## Why it exists

The epic decided to generate the attribute instead of shipping it in an assembly, so that the package stays a development dependency with nothing in `lib/`; [TD-0006](TD-0006-attribute-conflicts-across-friend-assemblies.md) records another cost of that decision.  This cost was found while measuring the fix for the path resolution that grew with types times files, which had hidden it.

## Impact

None for a project of ordinary size: about 30 µs and 10 KB for each attributed type, on each run.  An edit to a C# file changes the compilation and pays the same in any generator that finds types by attribute, so only edits to `.sql` files pay something they need not.  Whether an IDE hands the generator the same compilation after an edit to a `.sql` file was not checked, and it decides whether this costs anything there.

## Proposed fix

1. Check the IDE first: if it builds a new compilation for an edit to an additional file, nothing can be saved and this item closes.
2. Measure `TargetTypeReader.Read` on its own and cut what it allocates.  At most the difference between the first two columns is SqlSource's, and that difference also holds the rest of the pipeline.
3. Check whether `AddEmbeddedAttributeDefinition`, which TD-0006 waits for, changes what the driver does with the compilation.
4. Shipping the attribute in an assembly removes the step and reverses the epic's decision.  It is not worth that for this cost alone.

## Trigger

A user reports a slow IDE while editing `.sql` files in a project with many attributed types.  The way the attribute is delivered changes for another reason, such as TD-0006.
