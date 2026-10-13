using System.Text.Json;

namespace SqlSource.Tool.Describing.Capture;

/// <summary>
/// One call a describer made through its exchange, as a capture keeps it.
/// </summary>
/// <param name="Method">The name of the question.</param>
/// <param name="Request">What was asked, as <see cref="CaptureJson.Options" /> writes it.</param>
/// <param name="Answer">What was answered, as <see cref="CaptureJson.Options" /> writes it.</param>
internal sealed record CaptureCall(string Method, JsonElement Request, JsonElement Answer);
