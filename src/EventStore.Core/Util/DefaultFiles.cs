using System.Runtime.InteropServices;

namespace EventStore.Core.Util {
	public static class DefaultFiles {
		public static readonly string DefaultConfigFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			? string.Empty
			: "eventstore.conf";
	}
}
