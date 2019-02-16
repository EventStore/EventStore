using System.Reflection;
using EventStore.Common.Utils;

namespace EventStore.Core.Util {
	public class DefaultFiles {
		public static readonly string DefaultConfigFile;

		static DefaultFiles() {
			bool isTestClient = false;
			if (Assembly.GetEntryAssembly() != null)
				isTestClient = Assembly.GetEntryAssembly().FullName.StartsWith("EventStore.TestClient");

			switch (Platforms.GetPlatform()) {
				case Platform.Linux:
				case Platform.Mac:
					if (isTestClient)
						DefaultConfigFile = "testclient.conf";
					else
						DefaultConfigFile = "eventstore.conf";
					break;
				default:
					DefaultConfigFile = System.String.Empty;
					break;
			}
		}
	}
}
