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

			Data = Data.Keys
				// ignore args in subsections. we will use these for plugins.
				.Where(x => !x.Contains(':'))
				.ToDictionary(StringExtensions.Computerize, x => Data[x], StringComparer.OrdinalIgnoreCase);
		}
	}
}
