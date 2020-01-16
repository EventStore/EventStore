using Grpc.Core;

namespace EventStore.Client {
	internal static class MetadataExtensions {
		public static bool TryGetValue(this Metadata metadata, string key, out string value) {
			value = default;

			foreach (var entry in metadata) {
				if (entry.Key == key) {
					value = entry.Value;
					return true;
				}
			}

			return false;
		}

		public static long? GetLongValueOrDefault(this Metadata metadata, string key)
			=> metadata.TryGetValue(key, out var s) && long.TryParse(s, out var value)
				? value
				: default;

		public static int GetIntValueOrDefault(this Metadata metadata, string key)
			=> metadata.TryGetValue(key, out var s) && int.TryParse(s, out var value)
				? value
				: default;
	}
}
