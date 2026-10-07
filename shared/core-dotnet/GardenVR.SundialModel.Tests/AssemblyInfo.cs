using Xunit;

// SundialService has static seams (DevSeedOnFresh, FreshReducedMotion). A test that sets one restores it in a finally block,
// and the assembly runs serially so no other test reads it mid-flight.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
