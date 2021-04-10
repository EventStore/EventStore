using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration.CommandLine;

namespace EventStore.Common.Configuration {
	internal class CommandLine : CommandLineConfigurationProvider {
		public CommandLine(IEnumerable<string> args, IDictionary<string, string> switchMappings = null)
			: base(args, switchMappings) {
		}

		public override void Load() {
			base.Load();

			Data = Data.Keys.ToDictionary(StringExtensions.Computerize, x => Data[x], StringComparer.OrdinalIgnoreCase);
		}
	}
}
