#nullable enable

using System.Collections;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.Configuration.Sources {
	public class EventStoreEnvironmentVariablesSource(IDictionary? environment = null) : IConfigurationSource {
		public IConfigurationProvider Build(IConfigurationBuilder builder) =>
			new EventStoreEnvironmentVariablesConfigurationProvider(environment);
	}
}