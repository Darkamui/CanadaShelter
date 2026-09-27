using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shelter.BuildingBlocks.Logging;

namespace Shelter.IntegrationTests.Host;

public sealed partial class LogRedactionTests
{
    [Fact]
    public void Personal_values_are_erased_and_non_personal_values_kept()
    {
        var sink = new CapturingLoggerProvider();
        using var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(sink).AddShelterRedaction())
            .BuildServiceProvider();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("redaction-test");

        LogPersonLookup(logger, "marie.tremblay@example.com", "animal-42");

        var entry = Assert.Single(sink.Entries);
        Assert.DoesNotContain("marie.tremblay@example.com", entry, StringComparison.Ordinal);
        Assert.Contains("animal-42", entry, StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lookup by {Email} for {AnimalRef}")]
    private static partial void LogPersonLookup(ILogger logger, [PersonalData] string email, [NonPersonalData] string animalRef);

    /// <summary>Records the formatted message plus every state value a provider would see.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}"))
                    : string.Empty;
                entries.Add($"{formatter(state, exception)}|{values}");
            }
        }
    }
}
