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
#if !__MonoCS__ && !USE_UNIX_IO
            uint size;
            uint dontcare;
            WinNative.GetDiskFreeSpace(Path.GetPathRoot(path), out dontcare, out size, out dontcare, out dontcare);
            return size;
#else
            return 0;
#endif
        }

        public static void SetFileSize(SafeFileHandle handle, long aligned)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            WinNative.SetFilePointer(handle, (int)aligned, null, WinNative.EMoveMethod.Begin);
            if (!WinNative.SetEndOfFile(handle))
            {
                throw new Win32Exception();
            }
            FSync(handle);
#else
#endif
        }

        private static void FSync(SafeFileHandle handle)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            WinNative.FlushFileBuffers(handle);
#endif
        }
        
        //TODO UNBUFF byte* instead of byte[]?
        public static void Write(SafeFileHandle handle, byte[] buffer, uint count, ref int written)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            if (!WinNative.WriteFile(handle, buffer, count, ref written, IntPtr.Zero))
            {
                throw new Win32Exception();
            }
#endif
        }
        //TODO UNBUFF use FileAccess etc or do custom?
        public static SafeFileHandle Create(string path, FileAccess acc, FileShare readWrite, FileMode mode, int flags)
        {
#if !__MonoCS__ && !USE_UNIX_IO
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
#else
            return new SafeFileHandle((IntPtr) 0, true);
#endif
        }

        public static void Seek(SafeFileHandle handle, int position, SeekOrigin origin)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            WinNative.SetFilePointer(handle, position, null, WinNative.EMoveMethod.Begin);
#endif
        }
    }
}