namespace SqlSource.Tool.Tests.Spike10;

// What one replay gave, for the phase 10.0 spike.  Text is the sidecar for an Entry, "<id> <step>" for a Failure,
// "<method> <request>" for a Miss, and the message for an Unreadable or a Thrown.
internal enum SpikeKind
{
    Entry,
    Failure,
    Miss,
    Unreadable,
    Thrown,
}

internal sealed record SpikeOutcome(SpikeKind Kind, string Text);
