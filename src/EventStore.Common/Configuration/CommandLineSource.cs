using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Configuration {
	public class CommandLineSource : IConfigurationSource {
		private readonly IEnumerable<string> _args;

		public CommandLineSource(string[] args) {
			_args = args.Select(NormalizeKeys).Select(NormalizeBooleans);

			static string NormalizeKeys(string x) => x[0] == '-' && x[1] != '-' ? $"-{x}" : x;

			string NormalizeBooleans(string x, int i) => (x[..2] == "--", x[^1]) switch {
				(true, '+') => $"{x[..^1]}=true",
				(true, '-') => $"{x[..^1]}=false",
				(true, _) => !x.Contains("=") && (i == args.Length - 1 || args[i + 1][..2] == "--")
					? $"{x}=true"
					: x,
				_ => x
			};
		}

		public IConfigurationProvider Build(IConfigurationBuilder builder) => new CommandLine(_args);
	}
}
