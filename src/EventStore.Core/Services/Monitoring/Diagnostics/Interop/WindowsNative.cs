using System.ComponentModel;
using System.Runtime.InteropServices;

namespace System.Diagnostics.Interop;

public static partial class WindowsNative {
    public static partial class IO {
        public static DiskIoData GetDiskIo(Process process) {
            if (GetProcessIoCounters(process.Handle, out var counters)) {
                return new() {
                    ReadBytes    = counters.ReadTransferCount,
                    WrittenBytes = counters.WriteTransferCount,
                    ReadOps      = counters.ReadOperationCount,
                    WriteOps     = counters.WriteOperationCount
                };
            }

            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != 0)
                throw new Win32Exception(errorCode);

            return new();
        }

        public static DiskIoData GetDiskIo(int processId) {
            using var process = Process.GetProcessById(processId);
            return GetDiskIo(process);
        }

        public static DiskIoData GetDiskIo() =>
            GetDiskIo(Environment.ProcessId);

        // http://msdn.microsoft.com/en-us/library/ms683218%28VS.85%29.aspx
        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial bool GetProcessIoCounters(IntPtr processHandle, out IO_COUNTERS ioCounters);
    }

    public static partial class Memory {
        public static ulong GetTotalMemory() {
            var status  = MemoryStatusEx.Create();
            var success = GlobalMemoryStatusEx(ref status);

            if (success)
                return status.TotalPhys;

            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, "Failed to retrieve total memory information.");
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx {
            public          int   Length;
            public readonly int   MemoryLoad;
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

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatusEx);
    }
}