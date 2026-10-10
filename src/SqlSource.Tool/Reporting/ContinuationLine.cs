using System;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// A line under the first line of an error: <c>    label: text</c>.
/// </summary>
/// <param name="Label">What the line holds, <c>help</c> for one.</param>
/// <param name="Text">The line's text.</param>
internal sealed record ContinuationLine(string Label, string Text) : IEquatable<ContinuationLine>;
