#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using static System.StringComparer;

namespace EventStore.Core.Configuration.Sources {
	public class EventStoreCommandLineConfigurationSource : IConfigurationSource {
		public EventStoreCommandLineConfigurationSource(string[] args) {
			Args = args.Select(NormalizeKeys).Select(NormalizeBooleans);

			return;

			static string NormalizeKeys(string x) => x[0] == '-' && x[1] != '-' ? $"-{x}" : x;

			string NormalizeBooleans(string x, int i) {
				if (!x.StartsWith("--"))
					return x;

				if (x.EndsWith('+'))
					return $"{x[..^1]}=true";

				if (x.EndsWith('-'))
					return $"{x[..^1]}=false";

				if (x.Contains('='))
					return x;

				if (i != args.Length - 1 && !args[i + 1].StartsWith("--"))
					return x;

				return $"{x}=true";
			}
		}

		private IEnumerable<string> Args { get; set; }

		public IConfigurationProvider Build(IConfigurationBuilder builder) =>
			new EventStoreCommandLineConfigurationProvider(Args);
	}

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

	public static class EventStoreCommandLineConfigurationExtensions {
		public static IConfigurationBuilder AddEventStoreCommandLine(this IConfigurationBuilder builder, params string[] args) =>
			builder.Add(new EventStoreCommandLineConfigurationSource(args));
	}
}
