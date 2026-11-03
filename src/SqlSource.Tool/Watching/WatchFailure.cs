namespace SqlSource.Tool.Watching;

/// <summary>
/// A watch that could not see everything, as it tells its request.
/// </summary>
/// <param name="Folder">The full path of the watched folder.</param>
/// <param name="Overflow">
/// True for a batch of events that the system lost because the tool did not read them fast enough, which is never
/// reported; false for any other failure, which <paramref name="Message" /> describes.
/// </param>
/// <param name="Message">The message of the exception that the watch met.</param>
internal sealed record WatchFailure(string Folder, bool Overflow, string Message);
