using System;
using System.Reflection;
using EventStore.Common.Log;

namespace EventStore.Common.Utils {
	public enum OsFlavor {
		Unknown,
		Windows,
		Linux,
		BSD,
		MacOS
	}

	public class OS {
		private static readonly ILogger Log = LogManager.GetLoggerFor<OS>();

		public static readonly OsFlavor OsFlavor = DetermineOSFlavor();

		public static bool IsUnix {
			get {
				var platform = (int)Environment.OSVersion.Platform;
				return (platform == 4) || (platform == 6) || (platform == 128);
			}
		}

		public static string GetHomeFolder() {
			return IsUnix
				? Environment.GetEnvironmentVariable("HOME")
				: Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
		}

		public static string GetRuntimeVersion() {
			var type = Type.GetType("Mono.Runtime");
			if (type != null) {
				MethodInfo getDisplayNameMethod =
					type.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static);
				return getDisplayNameMethod != null
					? (string)getDisplayNameMethod.Invoke(null, null)
					: "Mono <UNKNOWN>";
			}

			// must be .NET
			return ".NET " + Environment.Version;
		}

		private static OsFlavor DetermineOSFlavor() {
			if (!IsUnix) // assume Windows
				return OsFlavor.Windows;

			string uname = null;
			try {
				uname = ShellExecutor.GetOutput("uname", "");
			} catch (Exception ex) {
				Log.ErrorException(ex, "Couldn't determine the flavor of Unix-like OS.");
			}

			uname = uname.Trim().ToLower();
			switch (uname) {
				case "linux":
					return OsFlavor.Linux;
				case "darwin":
					return OsFlavor.MacOS;
				case "freebsd":
				case "netbsd":
				case "openbsd":
					return OsFlavor.BSD;
				default:
					return OsFlavor.Unknown;
			}
		}
	}
}
