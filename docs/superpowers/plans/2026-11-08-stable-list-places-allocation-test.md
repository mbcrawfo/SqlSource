# Stable allocation test of the list places Implementation Plan

**Goal:** `SqlListPlacesTests.Find_WithPlaces_AllocatesThePlacesAlone` holds on a busy runner.  It failed once in three `./pre-commit-validation.sh --full` runs ("with - without should be 800L but was 88L").

**Cause (confirmed):** `SqlListUses.Find` rents its arrays from `ArrayPool<T>.Shared`.  A gen-2 collection under memory pressure, which any thread's allocations can bring, trims the pool, thread-local arrays included, and the next `Find` allocates its arrays again.  A scratch test run with `DOTNET_GCHighMemPercent=0x14`, so that every gen-2 collection counts as high pressure, measured 384 bytes a call before `GC.Collect(2)` and 1,480 on the first call after it, then 384 again.  The failing run's 712 extra bytes fell in the measure without places.  `GC.GetAllocatedBytesForCurrentThread` itself is not disturbed: the trim only adds allocations to the measure it falls in.

**Fix:** a test helper, `PooledAllocations.Of` at the root of `tests/SqlSource.Tests`, measures 20 calls after 20 warm-up calls five times and returns the least.  A trim adds bytes to the one measure it falls in, and the next measure finds the pool full again; an allocation the code makes on its own is in every measure, so the test guarantees what it did.

**Scope:** the two tests that measure `Find`, the only code of the generator that rents from the pool: `SqlListPlacesTests.Find_WithPlaces_AllocatesThePlacesAlone` and `SqlListUsesTests.Find_QueryWithLists_AllocatesItsUsesAlone` (same exposure, not yet seen to fail).  `Find_TextWithoutIn_IsEmptyAndAllocatesNothing` rents nothing.  A rule in `tests/SqlSource.Tests/AGENTS.md` names the helper (`tests/AGENTS.md` has no room).

## Steps

- [x] Add `tests/SqlSource.Tests/PooledAllocations.cs`.
- [x] Use it in both tests, in place of their own measures.
- [x] Add the rule to `tests/SqlSource.Tests/AGENTS.md`.
- [x] Run both test classes 30 times in a loop, and both tests 300 times in a scratch test with `DOTNET_GCHighMemPercent=0x14` and a thread that collects every millisecond (the old measure was off in 103 of the 300, the new one in none), then `./format.sh` and `./pre-commit-validation.sh --full`.
