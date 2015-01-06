using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Unbuffered
{
#if ! __MonoCS__ && !USE_UNIX_IO
    internal static class WinNative
    {
        [DllImport("KERNEL32", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        public static extern bool GetDiskFreeSpace(string path,
            out uint sectorsPerCluster,
            out uint bytesPerSector,
            out uint numberOfFreeClusters,
            out uint totalNumberOfClusters);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteFile(
            SafeFileHandle hFile,
            Byte[] aBuffer,
            UInt32 cbToWrite,
            ref int cbThatWereWritten,
            IntPtr pOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern UInt32 SetFilePointer(
            SafeFileHandle hFile,
            Int32 cbDistanceToMove,
            IntPtr pDistanceToMoveHigh,
            EMoveMethod fMoveMethod);

        [DllImport("KERNEL32", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        public static extern SafeFileHandle CreateFile(String fileName,
            int desiredAccess,
            FileShare shareMode,
            IntPtr securityAttrs,
            FileMode creationDisposition,
            int flagsAndAttributes,
            IntPtr templateFile);

        public enum EMoveMethod : uint
        {
            Begin = 0,
            Current = 1,
            End = 2
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetEndOfFile(
            SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FlushFileBuffers(SafeFileHandle filehandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern unsafe uint SetFilePointer(
            [In] SafeFileHandle hFile,
            [In] int lDistanceToMove,
            [Out] int* lpDistanceToMoveHigh,
            [In] EMoveMethod dwMoveMethod);


    }
#endif
}