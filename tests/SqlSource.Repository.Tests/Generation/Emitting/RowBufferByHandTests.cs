using System;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Generation.Emitting;
using Xunit;

namespace SqlSource.Repository.Tests.Generation.Emitting;

// A file of another test project held to the generator's code.
public sealed class RowBufferByHandTests
{
    // The hand-written copy of the buffer that the allocation tests of tests/DatabaseTests/QueryMethodAllocation read
    // their rows into, and its declaration, in place of the generated one's.
    private static readonly string HandWrittenCopy = RepositoryRoot.Path(
        "tests",
        "DatabaseTests",
        "QueryMethodAllocation",
        "RowBufferByHand.cs"
    );

    private const string HandWrittenDeclaration = "internal struct RowBufferByHand<T>\n";

    // The copy is the generated buffer, from its declaration to its last brace, but its attribute and its name, and at
    // the depth of a file-scoped namespace, so that the hand-written methods of the allocation tests read their rows
    // as the generated methods do.  A change to the generated buffer changes the copy with it.
    [Fact]
    public void Source_HandWrittenCopyOfTheAllocationTests_IsTheGeneratedBufferButItsAttributeNameAndDepth()
    {
        var copy = File.ReadAllText(HandWrittenCopy).ReplaceLineEndings("\n");
        var start = copy.IndexOf(HandWrittenDeclaration, StringComparison.Ordinal);
        var end = copy.IndexOf("\n}\n", start, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        end.ShouldBeGreaterThan(start);

        const string Attribute = "    [global::Microsoft.CodeAnalysis.Embedded]\n";
        const string LastBrace = "\n    }\n";
        var source = RowBuffer.Source;
        var declaration = source.IndexOf(Attribute, StringComparison.Ordinal) + Attribute.Length;
        var lastBrace = source.IndexOf(LastBrace, declaration, StringComparison.Ordinal);
        declaration.ShouldBeGreaterThanOrEqualTo(Attribute.Length);
        lastBrace.ShouldBeGreaterThan(declaration);
        var generated = string.Join(
            "\n",
            source[declaration..(lastBrace + LastBrace.Length)]
                .Split('\n')
                .Select(line =>
                    line.StartsWith(GeneratedText.Indent, StringComparison.Ordinal)
                        ? line[GeneratedText.Indent.Length..]
                        : line
                )
        );

        generated
            .Replace("internal struct SqlSourceRowBuffer<T>\n", HandWrittenDeclaration, StringComparison.Ordinal)
            .ShouldBe(copy[start..(end + 3)]);
    }
}
