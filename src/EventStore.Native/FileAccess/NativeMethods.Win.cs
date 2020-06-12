using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Local

namespace EventStore.Native.FileAccess
{
    /// <summary>
    /// Windows native methods
    /// </summary>
    internal static unsafe partial class NativeMethods
    {


        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, IntPtr dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

      
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr lpAddress, IntPtr dwSize, FreeType dwFreeType);
       
        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            SafeFileHandle hFile,
            byte* lpBuffer,
            int nNumberOfBytesToRead,
            int* lpNumberOfBytesRead,
            NativeOverlapped* lpOverlapped);

        

        private static int OverlappedRead( SafeFileHandle file, IntPtr source, int length, NativeOverlapped* overlapped) {
            int bytesRead;
            if (!NativeMethods.ReadFile(file, (byte*)source, length, &bytesRead , overlapped)) {
                Win32Exception we = new Win32Exception();
                ApplicationException ae =
                    new ApplicationException($"{nameof(NativeMethods)}:OverlappedRead - Error occurred reading a file. - " + we.Message);
                throw ae;
            }

            return bytesRead;
        }

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            byte* lpBuffer,
            int nNumberOfBytesToWrite,
            int* lpNumberOfBytesWritten,
            NativeOverlapped* lpOverlapped);

        

        private static int OverlappedWrite(SafeFileHandle file, byte* source, int length, NativeOverlapped* overlapped) {
            int bytesWritten;
            if (!NativeMethods.WriteFile(file, source, length, &bytesWritten, overlapped)) {
                // This is an error condition.  The error msg can be obtained by creating a Win32Exception and
                // using the Message property to obtain a description of the error that was encountered.
                Win32Exception we = new Win32Exception();
                ApplicationException ae = new ApplicationException($"{nameof(NativeMethods)}:OverlappedWrite - Error occurred writing a file. - "
                                                                   + we.Message);
                throw ae;
            }
            return bytesWritten;
        }

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

      

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileSizeEx(SafeFileHandle hFile, out long lpFileSize);

        [DllImport("kernel32.dll", EntryPoint = "SetFilePointerEx", SetLastError = true)]
        static extern bool WinSeek(SafeFileHandle hFile, long offset, out IntPtr newPosition, SeekOrigin origin);

        [DllImport("kernel32.dll", SetLastError=true)]
        static extern bool SetEndOfFile(SafeFileHandle hFile);

        [DllImport("kernel32.dll", SetLastError=true)]
        private static extern bool FlushFileBuffers(SafeFileHandle hFile);

        [DllImport("msvcrt.dll", EntryPoint = "memcpy", SetLastError = true)]
        private static extern void CopyMemory(byte* dest, byte* src, uint count);

        [DllImport("msvcrt.dll", EntryPoint = "memcmp", SetLastError = true)]
        private static extern int Compare(IntPtr ptr1, IntPtr ptr2, UIntPtr count);

        [DllImport("msvcrt.dll", EntryPoint = "memset", SetLastError = true)]
        private static extern IntPtr MemSet(IntPtr dest, int c, int byteCount);

        private static FILE_STORAGE_INFO GetStorageInfo(SafeFileHandle fileHandle) {
            GetStorageInformationByHandle(
            fileHandle,
            FILE_INFO_BY_HANDLE_CLASS.FileStorageInfo,
            out var info,
            (uint)sizeof(FILE_STORAGE_INFO));
            return info;
        }

        [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
        private static extern bool GetStorageInformationByHandle(
            SafeFileHandle hFile,
            FILE_INFO_BY_HANDLE_CLASS infoClass,
            out FILE_STORAGE_INFO storageInfo,
            uint dwBufferSize);

        [Flags]
        private enum FreeType : uint
        {
            MEM_DECOMMIT = 0x4000,
            MEM_RELEASE = 0x8000
        }

        [Flags]
        private enum AllocationType : uint
        {
            COMMIT = 0x1000,
            RESERVE = 0x2000,
            RESET = 0x80000,
            LARGE_PAGES = 0x20000000,
            PHYSICAL = 0x400000,
            TOP_DOWN = 0x100000,
            WRITE_WATCH = 0x200000
        }

        [Flags]
        private enum MemoryProtection : uint
        {
            EXECUTE = 0x10,
            EXECUTE_READ = 0x20,
            EXECUTE_READWRITE = 0x40,
            EXECUTE_WRITECOPY = 0x80,
            NOACCESS = 0x01,
            READONLY = 0x02,
            READWRITE = 0x04,
            WRITECOPY = 0x08,
            GUARD_Modifierflag = 0x100,
            NOCACHE_Modifierflag = 0x200,
            WRITECOMBINE_Modifierflag = 0x400
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_STORAGE_INFO
        {
            public long LogicalBytesPerSector;
            public long PhysicalBytesPerSectorForAtomicity;
            public long PhysicalBytesPerSectorForPerformance;
            public long FileSystemEffectivePhysicalBytesPerSectorForAtomicity;
            public long Flags;
            public long ByteOffsetForSectorAlignment;
            public long ByteOffsetForPartitionAlignment;
        }




        private enum FILE_INFO_BY_HANDLE_CLASS
        {
            FileBasicInfo = 0,
            FileStandardInfo = 1,
            FileNameInfo = 2,
            FileRenameInfo = 3,
            FileDispositionInfo = 4,
            FileAllocationInfo = 5,
            FileEndOfFileInfo = 6,
            FileStreamInfo = 7,
            FileCompressionInfo = 8,
            FileAttributeTagInfo = 9,
            FileIdBothDirectoryInfo = 10,// 0x0A
            FileIdBothDirectoryRestartInfo = 11, // 0xB
            FileIoPriorityHintInfo = 12, // 0xC
            FileRemoteProtocolInfo = 13, // 0xD
            FileFullDirectoryInfo = 14, // 0xE
            FileFullDirectoryRestartInfo = 15, // 0xF
            FileStorageInfo = 16, // 0x10
            FileAlignmentInfo = 17, // 0x11
            FileIdInfo = 18, // 0x12
            FileIdExtdDirectoryInfo = 19, // 0x13
            FileIdExtdDirectoryRestartInfo = 20, // 0x14
            MaximumFileInfoByHandlesClass
        }

        private const uint INFINITE = unchecked((uint)-1);
        
        private const int ERROR_IO_PENDING = 997;
        private const uint ERROR_IO_INCOMPLETE = 996;
        private const uint ERROR_NOACCESS = 998;
        private const uint ERROR_HANDLE_EOF = 38;
        
        private const int ERROR_FILE_NOT_FOUND = 0x2;
        private const int ERROR_PATH_NOT_FOUND = 0x3;
        private const int ERROR_INVALID_DRIVE = 0x15;
        
        private enum MyEnum {
	        
        }
        private const uint FILE_BEGIN = 0;
        private const uint FILE_CURRENT = 1;
        private const uint FILE_END = 2;
        
        private const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;
        private const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        
        private const uint INVALID_HANDLE_VALUE = unchecked((uint)-1);
        
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint GENERIC_EXECUTE = 0x20000000;
        private const uint GENERIC_ALL = 0x10000000;
        
        private const uint READ_CONTROL = 0x00020000;
        private const uint FILE_READ_ATTRIBUTES = 0x0080;
        private const uint FILE_READ_DATA = 0x0001;
        private const uint FILE_READ_EA = 0x0008;
        private const uint STANDARD_RIGHTS_READ = READ_CONTROL;
        private const uint FILE_APPEND_DATA = 0x0004;
        private const uint FILE_WRITE_ATTRIBUTES = 0x0100;
        private const uint FILE_WRITE_DATA = 0x0002;
        private const uint FILE_WRITE_EA = 0x0010;
        private const uint STANDARD_RIGHTS_WRITE = READ_CONTROL;
        
        private const uint FILE_GENERIC_READ =
            FILE_READ_ATTRIBUTES
            | FILE_READ_DATA
            | FILE_READ_EA
            | STANDARD_RIGHTS_READ;
        internal const uint FILE_GENERIC_WRITE =
            FILE_WRITE_ATTRIBUTES
            | FILE_WRITE_DATA
            | FILE_WRITE_EA
            | STANDARD_RIGHTS_WRITE
            | FILE_APPEND_DATA;

        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        
        private const uint CREATE_ALWAYS = 2;
        private const uint CREATE_NEW = 1;
        private const uint OPEN_ALWAYS = 4;
        private const uint OPEN_EXISTING = 3;
        private const uint TRUNCATE_EXISTING = 5;
        
        private const uint FILE_FLAG_DELETE_ON_CLOSE = 0x04000000;
        private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        private const uint FILE_FLAG_OPEN_NO_RECALL = 0x00100000;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
        private const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;
        private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x4000;

        private static void SetPosition(this ref NativeOverlapped overlapped, long position) {
            overlapped.OffsetLow = (int)(position & 0xFFFFFFFF);
            overlapped.OffsetHigh = (int)(position >> 32);
        }
        private static long GetPosition(this ref NativeOverlapped overlapped) {
            return (long)overlapped.OffsetHigh << 32 | (uint)overlapped.OffsetLow;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct Overlapped
        {
            private Overlapped(long position) {
                InternalLow = (IntPtr)0;
                InternalHigh = (IntPtr)0;
                OffsetLow = 0;
                OffsetHigh = 0;
                EventHandle = (IntPtr)0;
                SetPosition(position);
            }

            private void SetPosition(long position) {
                OffsetLow = (int)(position & 0xFFFFFFFF);
                OffsetHigh = (int)(position >> 32);
            }

            private long GetPosition() {
                return (long)OffsetHigh << 32 | (uint)OffsetLow;
            }

            private IntPtr InternalLow;
            private IntPtr InternalHigh;
            private int OffsetLow;
            private int OffsetHigh;
            private IntPtr EventHandle;
        }
    }
}
