using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Unbuffered
{
    internal unsafe static class NativeFile
    {
        public static uint GetDriveSectorSize(string path)
        {
            uint size;
            uint dontcare;
            WinNative.GetDiskFreeSpace(Path.GetPathRoot(path), out dontcare, out size, out dontcare, out dontcare);
            return size;
        }

        public static void SetFileSize(SafeFileHandle handle, long aligned)
        {
            WinNative.SetFilePointer(handle, (int)aligned, null, WinNative.EMoveMethod.Begin);
            if (!WinNative.SetEndOfFile(handle))
            {
                throw new Win32Exception();
            }
            FSync(handle);
        }

        private static void FSync(SafeFileHandle handle)
        {
            WinNative.FlushFileBuffers(handle);
        }
        
        //TODO UNBUFF byte* instead of byte[]?
        public static void Write(SafeFileHandle handle, byte[] buffer, uint count, ref int written)
        {
            if (!WinNative.WriteFile(handle, buffer, count, ref written, IntPtr.Zero))
            {
                throw new Win32Exception();
            }

        }
        //TODO UNBUFF use FileAccess etc or do custom?
        public static SafeFileHandle Create(string path, FileAccess acc, FileShare readWrite, FileMode mode, int flags)
        {
            var handle = WinNative.CreateFile(path,
                (int) acc,
                FileShare.ReadWrite,
                IntPtr.Zero,
                mode,
                flags,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception();
            }
            return handle;
        }

        public static void Seek(SafeFileHandle handle, int position, WinNative.EMoveMethod begin)
        {
            WinNative.SetFilePointer(handle, position, null, WinNative.EMoveMethod.Begin);
        }
    }
}