using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;

#if __MonoCS__ || USE_UNIX_IO
using Mono.Unix.Native;
#endif

namespace EventStore.Core.TransactionLog.Unbuffered
{
    public enum ExtendedFileOptions
    {
        NoBuffering = unchecked((int) 0x20000000),
        Overlapped = unchecked((int) 0x40000000),
        SequentialScan = unchecked((int) 0x08000000),
        WriteThrough = unchecked((int) 0x80000000)

    }
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
#else
            Syscall.fsync(0);
#endif
        }
        
        public static void Write(SafeFileHandle handle, byte[] buffer, uint count, ref int written)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            fixed (byte* b = buffer)
            {
                if (!WinNative.WriteFile(handle, b, count, ref written, IntPtr.Zero))
                {
                    throw new Win32Exception();
                }
            }
#else
            if(!syscall.write(handle, buffer,count) {
                throw new Win32Exception();
            }
#endif
        }

        public static int Read(SafeFileHandle handle, byte[] buffer, int offset, int count)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            var read = 0;
            fixed (byte* b = buffer)
            {
                if (!WinNative.ReadFile(handle, b + offset, count, ref read, 0))
                {
                    throw new Win32Exception();
                }
            }
            return read;
#endif
        }

        public static long GetFileSize(SafeFileHandle handle)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            long size = 0;
            if (!WinNative.GetFileSizeEx(handle, out size))
            {
                throw new Win32Exception();
            }
            return size;
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
            var f = WinNative.SetFilePointer(handle, position, null, WinNative.EMoveMethod.Begin);
            if (f == WinNative.INVALID_SET_FILE_POINTER)
            {
                throw new Win32Exception();
            }
#endif
        }
    }
}