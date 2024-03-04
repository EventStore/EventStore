using System;
using System.IO;
using System.Collections.Generic;

namespace EventStore.Common.Utils {
	public static class VersionInfo {
		public const string DefaultVersion = "0.0.0-prerelease";
		public const string OldVersion = "old_version";
		public const string UnknownVersion = "unknown_version";

		public static string BuildId { get; private set; } = "";
		public static string Edition { get; private set; } = "";
		public static string Version { get; private set; } = DefaultVersion;

		public static string CommitSha { get; private set; } = ThisAssembly.Git.Commit;
		public static string Timestamp { get; private set; } = ThisAssembly.Git.CommitDate;

		public static string Text => $"EventStoreDB version {Version} {Edition} ({BuildId}/{CommitSha})";

		static VersionInfo() {
			var versionFilePath = Path.Join(
				Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory),
				"version.properties");
			var properties = LoadProperties(versionFilePath);

			if (properties.TryGetValue("version", out var version))
				Version = version;

			if (properties.TryGetValue("commit_sha", out var commitSha))
				CommitSha = commitSha;

			if (properties.TryGetValue("timestamp", out var timestamp))
				Timestamp = timestamp;

			if (properties.TryGetValue("build_id", out var buildId))
				BuildId = buildId;

			if (properties.TryGetValue("edition", out var edition))
				Edition = edition;
		}

		private static Dictionary<string, string> LoadProperties(string file) {
			using var reader = new StreamReader(file);

			var properties = new Dictionary<string, string>();
			string line;
			while ((line = reader.ReadLine()) != null) {
				var parts = line.Split('=', 2);
				if (parts.Length == 2)
					properties[parts[0]] = parts[1];
			}

			return properties;
		}
	}
}
