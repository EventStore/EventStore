using System.Reflection;

namespace EventStore.Common.Utils {
	public static class VersionInfo {
		public const string DefaultVersion = "0.0.0.0"; 
		public static string Version => typeof(VersionInfo).Assembly
			.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? DefaultVersion;

		public static string Branch => ThisAssembly.Git.Branch;
		public static string Hashtag => ThisAssembly.Git.Commit;
		public static readonly string Timestamp = "Unknown";

		public static string Text => $"EventStoreDB version {Version} ({Branch}/{Hashtag}, {Timestamp})";
	}
}
