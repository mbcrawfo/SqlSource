using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// One error of the tool, as data.  <see cref="Reporter" /> writes it.
/// </summary>
/// <param name="Descriptor">What is wrong.  Its id has a section in <c>docs/diagnostics.md</c>.</param>
/// <param name="Path">The full path of the file the error is about, or null when it is about no file.</param>
/// <param name="Position">Where in the file, counted from zero, or null.  Never set without a path.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
/// <param name="Lines">The lines under the first, in the order they are written.</param>
internal sealed record ToolDiagnostic(
    DiagnosticDescriptor Descriptor,
    string? Path,
    LinePosition? Position,
    EquatableArray<string> Arguments,
    EquatableArray<ContinuationLine> Lines
)
{
    /// <summary>
    /// An error about no file.
    /// </summary>
    public static ToolDiagnostic Create(DiagnosticDescriptor descriptor, params string[] arguments) =>
        new(
            descriptor,
            Path: null,
            Position: null,
            new EquatableArray<string>(ImmutableArray.Create(arguments)),
            EquatableArray<ContinuationLine>.Empty
        );

    /// <summary>
    /// An error about a file, at no position in it.
    /// </summary>
    /// <param name="descriptor">What is wrong.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="arguments">The text the descriptor's message quotes.</param>
    public static ToolDiagnostic ForFile(DiagnosticDescriptor descriptor, string path, params string[] arguments) =>
        Create(descriptor, arguments) with
        {
            Path = path,
        };

    /// <summary>
    /// An error at a position in a file.
    /// </summary>
    /// <param name="descriptor">What is wrong.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="position">Where in the file, counted from zero.</param>
    /// <param name="arguments">The text the descriptor's message quotes.</param>
    public static ToolDiagnostic At(
        DiagnosticDescriptor descriptor,
        string path,
        LinePosition position,
        params string[] arguments
    ) => Create(descriptor, arguments) with { Path = path, Position = position };

    /// <summary>
    /// An error at a position that the generator's code gave.
    /// </summary>
    public static ToolDiagnostic At(
        DiagnosticDescriptor descriptor,
        LocationInfo location,
        params string[] arguments
    ) => At(descriptor, location.Path, location.LineSpan.Start, arguments);

    /// <summary>
    /// An error that the generator's code found, in the tool's form: at the start of where the generator has it.
    /// </summary>
    public static ToolDiagnostic From(DiagnosticInfo diagnostic) =>
        new(
            diagnostic.Descriptor,
            diagnostic.Location.Path,
            diagnostic.Location.LineSpan.Start,
            diagnostic.Arguments,
            EquatableArray<ContinuationLine>.Empty
        );

    /// <summary>
    /// The same error with these lines under its first, in this order.
    /// </summary>
    public ToolDiagnostic WithLines(params ContinuationLine[] lines) =>
        this with
        {
            Lines = new EquatableArray<ContinuationLine>(ImmutableArray.Create(lines)),
        };
}
