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

        // public static DiskIoData GetDiskIo(int processId) {
        //     using var process = Process.GetProcessById(processId);
        //     return GetDiskIo(process);
        // }

        public static DiskIoData GetDiskIo() =>
            GetDiskIo(Process.GetCurrentProcess());
        
        #region . native .
        
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
        
        #endregion
    }
}