using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using Microsoft.Win32.SafeHandles;
using Mono.Unix.Native;

// ReSharper disable RedundantIfElseBlock

namespace positional_writes
{
    internal static unsafe partial class NativeMethods
    {

        public static int PageSize = 4096;

        public struct PageAlignedBuffer
        {
            public IntPtr Handle;
            public IntPtr Buffer;
            public int Length;
            public int PageCount;
        }

        internal static PageAlignedBuffer GetPageBuffer(int pageCount) {
            if (Runtime.IsWindows) {
                var handle = VirtualAlloc(
                    (IntPtr)0,
                    (IntPtr)(pageCount * PageSize),
                    AllocationType.COMMIT | AllocationType.RESERVE,
                    MemoryProtection.READWRITE);
                return new PageAlignedBuffer {
                    Handle = handle,
                    Buffer = handle,
                    Length = pageCount * PageSize,
                    PageCount = pageCount
                };
            }
            else {
                var handle = Stdlib.calloc((ulong)PageSize, (ulong)pageCount + 1);
                return new PageAlignedBuffer {
                    Handle = handle,
                    Buffer = handle + (int)(PageSize - (long)handle % PageSize),
                    Length = pageCount * PageSize,
                    PageCount = pageCount
                };
            }

        }
        internal static bool FreeBuffer(ref PageAlignedBuffer buffer) {

            bool free = false;
            if (buffer.Handle != IntPtr.Zero) {
                if (Runtime.IsWindows) {
                    free = VirtualFree(buffer.Handle, (IntPtr)buffer.Length,
                        FreeType.MEM_RELEASE);
                }
                else {
                    Stdlib.free(buffer.Handle);
                    free = true;
                }
            }
            if (!free) return false;

            buffer.Handle = IntPtr.Zero;
            buffer.Buffer = IntPtr.Zero;
            buffer.Length = 0;
            buffer.PageCount = 0;
            return true;
        }

        internal static int ReadAt(SafeFileHandle file, IntPtr target, int length, long position) {

            if (Runtime.IsWindows) {
                var overlapped = new NativeOverlapped();
                overlapped.SetPosition(position);
                return OverlappedRead(file, target, length, &overlapped);
            }
            else {
                return UnixPRead(file, target, (ulong)length, position);

            }
        }




        internal static int WriteAt(SafeFileHandle file, IntPtr source, int length, long position) {
            if (Runtime.IsWindows) {
                var overlapped = new NativeOverlapped();
                overlapped.SetPosition(position);
                return OverlappedWrite(file, (byte*)source, length, &overlapped);
            }
            else {
                return UnixPWrite(file, source, (ulong)length, position);
            }
        }

        internal static void Flush(SafeFileHandle file) {
            if (Runtime.IsWindows) {
                FlushFileBuffers(file);
            }
            else {
                UnixFSync(file);
            }
        }

        internal static SafeFileHandle OpenNative(string path) {
            SafeFileHandle file;
            if (Runtime.IsWindows) {
                file = CreateFileW(
                    path,
                    (uint)FileAccess.ReadWrite,
                    (uint)FileShare.ReadWrite,
                    IntPtr.Zero,
                    (uint)FileMode.Open,
                    (uint)FileAttributes.Normal | FILE_FLAG_WRITE_THROUGH,
                    IntPtr.Zero);
                if ((file?.IsInvalid ?? true) || file.IsClosed) {
                    throw new ApplicationException($"NativeMethods:OpenWriteThrough - Unable to open file {path}.");
                }
            }
            else {
                file = UnixCreateRW(path, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open);
            }

            return file;
        }
        internal static SafeFileHandle OpenUnbuffered(string path) {
            SafeFileHandle file;
            if (Runtime.IsWindows) {
                file = NativeMethods.CreateFileW(
                path,
                (uint)FileAccess.ReadWrite,
                (uint)FileShare.ReadWrite,
                IntPtr.Zero,
                (uint)FileMode.Open,
                (uint)FileAttributes.Normal | FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH,
                IntPtr.Zero);
                if ((file?.IsInvalid ?? true) || file.IsClosed) {
                    throw new ApplicationException($"NativeMethods:OpenUnbuffered - Unable to open file {path}.");
                }
            }
            else {
                file = UnixCreateUnbufferedRW(path,
                     FileAccess.ReadWrite,
                     FileShare.ReadWrite,
                     FileMode.Open);
            }

            return file;
        }


        internal static long GetFileSize(SafeFileHandle file) {
            long length;
            if (Runtime.IsWindows) {
                if (!NativeMethods.GetFileSizeEx(file, out length)) {
                    throw new InvalidOperationException($"NativeMethods:GetFileSize - Unable to get file length.");
                }
            }
            else {
                length = UnixGetFileSize(file);
            }

            return length;
        }

        internal static int Compare(IntPtr p1, IntPtr p2, int length) {
            if (Runtime.IsWindows) {
                return Compare(p1, p2, (UIntPtr)length);
            }
            else {
                return UnixCompare(p1, p2, (UIntPtr)length);
            }
        }
        internal static void Copy(IntPtr destination, IntPtr source, long length) {
            if (Runtime.IsWindows) {
                CopyMemory((byte*)destination, (byte*)source, (uint)length);
            }
            else {
                UnixCopyMemory((byte*)destination, (byte*)source, (uint)length);
            }
        }

        internal static void Clear(IntPtr buffer, int length) {
            if (Runtime.IsWindows) {
                MemSet(buffer, 0, length);
            }
            else {
                UnixMemSet(buffer, 0, length);
            }
        }
        internal static void Fill(IntPtr buffer, byte fill, int length) {
            if (Runtime.IsWindows) {
                MemSet(buffer, fill, length);
            }
            else {
                UnixMemSet(buffer, fill, length);
            }
        }
        internal static int GetPhysicalSectorSize(SafeFileHandle file) {
            if (Runtime.IsWindows) {
                FILE_STORAGE_INFO diskInfo = GetStorageInfo(file);
                return (int)diskInfo.PhysicalBytesPerSectorForAtomicity;
            }
            else {
                //todo: implement call
                return 4096;
            }
        }

    }
}