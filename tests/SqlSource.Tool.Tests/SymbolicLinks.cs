namespace SqlSource.Tool.Tests;

// A test that makes a symbolic link skips on Windows, where making one needs a privilege, and whose working folder
// keeps the spelling of a link anyway.
internal static class SymbolicLinks
{
    public const string SkipReason = "Making a symbolic link needs a privilege on Windows.";
}
