using System.Linq;

namespace EventStore.Common.Configuration {
	public static class StringExtensions {
		public static string Computerize(string value) =>
			string.Join(
				string.Empty,
				(value?.Replace("-", "_").ToLowerInvariant()
				 ?? string.Empty).Split('_')
				.Select(x => new string(x.Select((c, i) => i == 0 ? char.ToUpper(c) : c).ToArray())));
	}
}
