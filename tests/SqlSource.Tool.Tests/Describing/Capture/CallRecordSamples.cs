namespace SqlSource.Tool.Tests.Describing.Capture;

// Types that are named as the records of a call are and break the rule of one, for CallRecordTests: the search must
// find each, and the rule must refuse it.  They are in no namespace that the search of the real records reads.

internal readonly record struct SampleStructRequest(int Value);

internal sealed record SampleGenericAnswer<T>(T Value);
