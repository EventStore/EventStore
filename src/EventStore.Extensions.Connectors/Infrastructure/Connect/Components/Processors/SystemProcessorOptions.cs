// ReSharper disable CheckNamespace

#pragma warning disable CS0108, CS0114

using EventStore.Core.Bus;
using Kurrent.Surge.Configuration;
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
}
