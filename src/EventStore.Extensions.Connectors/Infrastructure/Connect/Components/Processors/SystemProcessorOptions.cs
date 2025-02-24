// ReSharper disable CheckNamespace

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

	public IPublisher Publisher { get; init; }
	public new ConsumeFilter Filter { get; init; } = ConsumeFilter.ExcludeSystemEvents();

	public new RecordPosition? StartPosition { get; init; } = RecordPosition.Unset;
	public new SubscriptionInitialPosition InitialPosition { get; init; } = SubscriptionInitialPosition.Latest;

	public new AutoCommitOptions AutoCommit { get; init; } = new();
	public new bool SkipDecoding { get; init; }
}
