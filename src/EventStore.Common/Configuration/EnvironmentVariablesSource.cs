using System;
using System.Collections;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	public class EnvironmentVariablesSource : IConfigurationSource {
		private readonly IDictionary _environment;
		private const string Prefix = "EVENTSTORE";

		public EnvironmentVariablesSource(IDictionary environment) {
			if (environment == null) {
				throw new ArgumentNullException(nameof(environment));
			}

			_environment = environment;
		}

		public IConfigurationProvider Build(IConfigurationBuilder builder)
			=> new EnvironmentVariables(Prefix, _environment);
	}
}
