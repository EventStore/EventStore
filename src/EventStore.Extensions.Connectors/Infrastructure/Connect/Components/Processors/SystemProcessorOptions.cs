// ReSharper disable CheckNamespace

#pragma warning disable CS0108, CS0114

using EventStore.Core.Bus;
using Kurrent.Surge;
using Kurrent.Surge.Configuration;
using Kurrent.Surge.Consumers;
using Kurrent.Surge.Consumers.Configuration;
using Kurrent.Surge.Processors.Configuration;

namespace EventStore.Connect.Processors.Configuration;

[PublicAPI]
public record SystemProcessorOptions : ProcessorOptions {
    public SystemProcessorOptions() {
        Logging = new LoggingOptions {
            LogName = "EventStore.Connect.SystemProcessor"
        };
    }

    public IPublisher    Publisher { get; init; }
    public ConsumeFilter Filter    { get; init; } = ConsumeFilter.ExcludeSystemEvents();

    public RecordPosition?             StartPosition   { get; init; } = RecordPosition.Unset;
    public SubscriptionInitialPosition InitialPosition { get; init; } = SubscriptionInitialPosition.Latest;

    public AutoCommitOptions AutoCommit   { get; init; } = new();
    public bool              SkipDecoding { get; init; }
}
