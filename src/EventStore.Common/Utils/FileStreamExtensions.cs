using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using EventStore.Common.Log;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Common.Utils {
	public static class FileStreamExtensions {
		private static readonly ILogger Log = LogManager.GetLogger("FileStreamExtensions");
		private static Action<FileStream> FlushSafe;
		private static Func<FileStream, SafeFileHandle> GetFileHandle;

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
				Log.Info("FlushToDisk: DISABLED");
				FlushSafe = f => f.Flush(flushToDisk: false);
				return;
			}

			if (Runtime.IsMono)
				FlushSafe = f => f.Flush(flushToDisk: true);
			else {
				try {
					ParameterExpression arg = Expression.Parameter(typeof(FileStream), "f");
					Expression expr = Expression.Field(arg,
						typeof(FileStream).GetField("_handle", BindingFlags.Instance | BindingFlags.NonPublic));
					GetFileHandle = Expression.Lambda<Func<FileStream, SafeFileHandle>>(expr, arg).Compile();
					FlushSafe = f => {
						f.Flush(flushToDisk: false);
						if (!FlushFileBuffers(GetFileHandle(f)))
							throw new Exception(string.Format("FlushFileBuffers failed with err: {0}",
								Marshal.GetLastWin32Error()));
					};
				} catch (Exception exc) {
					Log.ErrorException(exc, "Error while compiling sneaky SafeFileHandle getter.");
					FlushSafe = f => f.Flush(flushToDisk: true);
				}
			}
		}
	}
}
