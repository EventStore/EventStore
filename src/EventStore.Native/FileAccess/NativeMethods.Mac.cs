using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Mono.Unix;

namespace EventStore.Native.FileAccess {
	internal static partial class NativeMethods {
		// ReSharper disable once InconsistentNaming
		private const uint MAC_F_NOCACHE = 48;

		[DllImport("libc")]
		static extern int fcntl(int fd, uint command, int arg);

		public static void MacDisableCache(SafeFileHandle handle) {
			if (!Runtime.IsMacOS) {
				return;
			}

			long r;
			do {
				r = fcntl(handle.DangerousGetHandle().ToInt32(), MAC_F_NOCACHE, 1);
			} while (UnixMarshal.ShouldRetrySyscall((int)r));

			if (r == -1) {
				UnixMarshal.ThrowExceptionForLastError();
			}
		}
	}
}
