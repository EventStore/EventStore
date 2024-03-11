using System.Runtime.InteropServices;

namespace EventStore.Common.Utils {
	public static class Runtime {
		public static readonly bool IsUnix      = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
		public static readonly bool IsMacOS     = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		public static readonly bool IsWindows   = !IsUnix && !IsMacOS;
		public static readonly bool IsUnixOrMac = IsUnix || IsMacOS;
	}
}