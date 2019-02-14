using System.Diagnostics;

namespace EventStore.Common.Utils {
	public static class ShellExecutor {
		public static string GetOutput(string command, string args = null) {
			var info = new ProcessStartInfo {
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				FileName = command,
				Arguments = args ?? string.Empty
			};

			using (var process = Process.Start(info)) {
				var res = process.StandardOutput.ReadToEnd();
				return res;
			}
		}
	}
}
