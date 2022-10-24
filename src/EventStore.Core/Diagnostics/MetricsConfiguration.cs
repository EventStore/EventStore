using System.Collections.Generic;

namespace EventStore.Core.Diagnostics {
	//qq this would be populated from some configuration file
	/*
	 * i guess we want to be able to have a configuration involving names along the lines of
	 * Core:
	 *     ReadAllEventsForward: Reads
	 *     TimerMessage: Other
	 * Projections:
	 *     etcetc
	 *     
	 * i.e. with names instead of numbers so that we can check that the names are correct
	 * the respective plugin will have to do the checking thought because ES won't know at compile time what plugins are present
	 */
	public class MetricsConfiguration {
		private readonly Dictionary<string, MetricsGroupConfiguration> _configuration;

		public MetricsConfiguration() {
			var coreConfiguration = new MetricsGroupConfiguration();
//			coreConfiguration.Add("Schedule", "Other");

			var projectionsConfiguration = new MetricsGroupConfiguration();
			projectionsConfiguration.Add("ProjectionsTimerTickThing", "Other");

			_configuration = new() {
				{ "CoreMessage", coreConfiguration },
				{ "Projections", projectionsConfiguration },
			};
		}

		public bool TryGetValue(string key, out MetricsGroupConfiguration value) =>
			_configuration.TryGetValue(key, out value);
	}
}
