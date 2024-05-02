#nullable enable

using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources {
	public static class EventStoreCommandLineConfigurationExtensions {
		public static IConfigurationBuilder AddEventStoreCommandLine(this IConfigurationBuilder builder, params string[] args) =>
			builder.Add(new EventStoreCommandLineConfigurationSource(args));
	}
}
