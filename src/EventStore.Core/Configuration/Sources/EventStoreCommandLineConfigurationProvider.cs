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
				// // ignore args in subsections. we will use these for plugins.
				// .Where(x => !x.Contains(':'))
				.ToDictionary(
					EventStoreConfigurationKeys.Normalize,
					x => Data[x], OrdinalIgnoreCase
				);
		}
	}
}
