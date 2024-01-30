using System.Collections.Generic;
using EventStore.Core.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration;

public static class ConfigurationProviderExtensions {
	public static IEnumerable<string> GetChildKeys(this IConfigurationProvider provider) => 
		provider.GetChildKeys([], EventStoreConfigurationKeys.Prefix);
}
