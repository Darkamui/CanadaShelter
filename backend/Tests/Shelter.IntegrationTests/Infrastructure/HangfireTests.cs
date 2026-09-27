namespace Shelter.IntegrationTests.Infrastructure;

/// <summary>
/// Test classes that enqueue or run Hangfire jobs. Hangfire keeps its log provider in process-wide static state,
/// and each host installs its own logger factory there; hosts built and disposed in parallel would leave Hangfire
/// logging through a disposed factory. Classes in this collection run alone, after the parallel ones.
/// (One host per process in production.)
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HangfireTests
{
    public const string Name = "Hangfire";
}
