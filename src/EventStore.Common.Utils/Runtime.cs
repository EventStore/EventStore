using System;
using System.Runtime.InteropServices;

namespace EventStore.Common.Utils {
	public static class Runtime {
		public static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

		public static readonly bool IsUnixOrMac = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) |
		                                          RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

		public static readonly bool IsWindows = !IsUnixOrMac;

		public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
	}
}
