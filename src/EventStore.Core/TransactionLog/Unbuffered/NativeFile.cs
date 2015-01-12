using System;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;

#if __MonoCS__ || USE_UNIX_IO
using Mono.Unix.Native;
using Mono.Unix;
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

        public static long GetPageSize(string path){
#if !__MonoCS__ && !USE_UNIX_IO
            return GetDriveSectorSize(path);
#else
            return Syscall.sysconf(SysconfName._SC_PAGESIZE);
#endif
        }

        public static void SetFileSize(SafeFileHandle handle, long count)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            WinNative.SetFilePointer(handle, (int)count, null, WinNative.EMoveMethod.Begin);
            if (!WinNative.SetEndOfFile(handle))
            {
                throw new Win32Exception();
            }
#else
            int r;
            do {
                r = Syscall.ftruncate (handle.DangerousGetHandle().ToInt32(), count);
            } while (UnixMarshal.ShouldRetrySyscall (r));
            UnixMarshal.ThrowExceptionForLastErrorIf (r);
#endif
            FSync(handle);
        }

        private static void FSync(SafeFileHandle handle)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            WinNative.FlushFileBuffers(handle);
#else
            Syscall.fsync(handle.DangerousGetHandle().ToInt32());
#endif
        }
        
        public static void Write(SafeFileHandle handle, byte* buffer, uint count, ref int written)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            if (!WinNative.WriteFile(handle, buffer, count, ref written, IntPtr.Zero))
            {
                throw new Win32Exception();
            }
#else
            long ret = 0;
                do {
                ret = Syscall.write (handle.DangerousGetHandle().ToInt32(), buffer ,count);
            } while (Mono.Unix.UnixMarshal.ShouldRetrySyscall ((int) ret));
            if(ret == -1)
                Mono.Unix.UnixMarshal.ThrowExceptionForLastErrorIf ((int) ret);
            written = (int) count;
#endif
        }

        public static int Read(SafeFileHandle handle, byte* buffer, int offset, int count)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            var read = 0;

            if (!WinNative.ReadFile(handle, buffer, count, ref read, 0))
            {
                throw new Win32Exception();
            }
            return read;
#else
            long r;
            do {
                r = Syscall.read (handle.DangerousGetHandle().ToInt32(), buffer, (ulong) count);
            } while (UnixMarshal.ShouldRetrySyscall ((int) r));
            if (r == -1)
                UnixMarshal.ThrowExceptionForLastError ();
            return count;
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
#else
            return 0;
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
            //TODO convert flags or separate methods?
            return new SafeFileHandle((IntPtr) 0, true);
#endif
        }


        public static SafeFileHandle CreateUnbufferedRW(string path, FileMode mode)
        {
#if !__MonoCS__ && !USE_UNIX_IO
            var handle = WinNative.CreateFile(path,
                (int) FileAccess.ReadWrite,
                FileShare.ReadWrite,
                IntPtr.Zero,
                FileMode.OpenOrCreate,
                (int) ExtendedFileOptions.NoBuffering,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new Win32Exception();
            }
            return handle;
#else
            var flags = OpenFlags.O_RDWR | OpenFlags.O_DIRECT | OpenFlags.O_CREAT;
            //var f = NativeConvert.FromOpenFlags(flags); not needed?
            var han = Syscall.open(path, flags, FilePermissions.S_IRWXU);
            if(han < 0)
                throw new Win32Exception();

            var handle = new SafeFileHandle((IntPtr) han, true);
            if(han == 0) Console.WriteLine("ZERO HANDLE");
            if(handle.IsInvalid) throw new Exception("Invalid handle");
            return handle;
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
#else
            if(Syscall.lseek(handle.DangerousGetHandle().ToInt32(), position, SeekFlags.SEEK_SET) < 0) {
                throw new Win32Exception();
            }
#endif
        }
    }
}