using System.Collections;
using System.Collections.Generic;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// A sequence of ints that counts how many times it is enumerated, for a test that a query enumerates a list once.
internal sealed class CountingSequence(int[] items) : IEnumerable<int>
{
    public int Enumerations { get; private set; }

    public IEnumerator<int> GetEnumerator()
    {
        Enumerations++;
        return ((IEnumerable<int>)items).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
