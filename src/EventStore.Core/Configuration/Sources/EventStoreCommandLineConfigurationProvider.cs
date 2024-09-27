#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration.CommandLine;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources {
	public class EventStoreCommandLineConfigurationProvider(IEnumerable<string> args)
		: CommandLineConfigurationProvider(args) {

		public override void Load() {
			base.Load();

			Data = Data.Keys
				.ToDictionary(
					EventStoreConfigurationKeys.Normalize,
					x => Data[x], OrdinalIgnoreCase
				);
		}
	}
}
