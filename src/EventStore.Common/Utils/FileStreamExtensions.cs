using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ILogger = Serilog.ILogger;

namespace EventStore.Common.Utils {
	public static class FileStreamExtensions {
		private static readonly ILogger Log =
			Serilog.Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "FileStreamExtensions");
		private static Action<FileStream> FlushSafe;

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool FlushFileBuffers(SafeFileHandle hFile);

		//[DllImport("kernel32.dll", SetLastError = true)]
		//[return: MarshalAs(UnmanagedType.Bool)]
		//static extern bool FlushViewOfFile(IntPtr lpBaseAddress, UIntPtr dwNumberOfBytesToFlush);

		static FileStreamExtensions() {
			ConfigureFlush(disableFlushToDisk: false);
		}

		public static void FlushToDisk(this FileStream fs) {
			FlushSafe(fs);
		}

		public static void ConfigureFlush(bool disableFlushToDisk) {
			if (disableFlushToDisk) {
				Log.Information("FlushToDisk: DISABLED");
				FlushSafe = f => f.Flush(flushToDisk: false);
				return;
			}

			if (!Runtime.IsWindows)
				FlushSafe = f => f.Flush(flushToDisk: true);
			else {
				FlushSafe = f => {
					f.Flush(flushToDisk: false);
					if (!FlushFileBuffers(f.SafeFileHandle))
						throw new Exception($"FlushFileBuffers failed with err: {Marshal.GetLastWin32Error()}");
				};
			}
		}
	}
}
