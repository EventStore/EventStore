using System;
using System.Runtime.InteropServices;

namespace EventStore.Native.Monitoring {
	public class WinNativeMemoryStatus {
		[StructLayout(LayoutKind.Sequential)]
		private struct MemoryStatusEx {
			public int Length;
			public readonly int MemoryLoad;
			public readonly ulong TotalPhys;
			public readonly ulong AvailPhys;
			public readonly ulong TotalPageFile;
			public readonly ulong AvailPageFile;
			public readonly ulong TotalVirtual;
			public readonly ulong AvailVirtual;
			public readonly ulong AvailExtendedVirtual;

			public static MemoryStatusEx Create() {
				var memoryStatus = new MemoryStatusEx {
					Length = Marshal.SizeOf(typeof(MemoryStatusEx))
				};
				return memoryStatus;
			}
		}

		[DllImport("kernel32", SetLastError = true)]
		private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatusEx);

		[DllImport("kernel32")]
		private static extern int GetLastError();

		public static ulong GetTotalMemory() {
			var memoryStatus = MemoryStatusEx.Create();
			bool success = GlobalMemoryStatusEx(ref memoryStatus);
			if (!success) {
				var errorCode = GetLastError();
				throw new Exception($"Failed to retrieve memory information. Error code: {errorCode}.");
			}

			return memoryStatus.TotalPhys;
		}
	}
}
