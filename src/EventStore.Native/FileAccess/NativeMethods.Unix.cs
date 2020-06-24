using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Mono.Unix;
using Mono.Unix.Native;

// ReSharper disable InconsistentNaming
// ReSharper disable CommentTypo

namespace EventStore.Native.FileAccess {
	internal static unsafe partial class NativeMethods {
		[DllImport("libc.so.6", EntryPoint = "memset", SetLastError = true)]
		private static extern IntPtr UnixMemSet(IntPtr dest, int c, int byteCount);
		[DllImport("libc.so.6", EntryPoint = "memcpy", SetLastError = true)]
		private static extern void UnixCopyMemory(byte* dest, byte* src, uint count);

		[DllImport("libc.so.6", EntryPoint = "memcmp", SetLastError = true)]
		private static extern int UnixCompare(IntPtr ptr1, IntPtr ptr2, UIntPtr count);

		public static uint UnixGetDriveSectorSize(string path) {
			return 0;
		}

		public static long UnixGetPageSize(string path) {
			int r;
			do {
				r = (int)Syscall.sysconf(SysconfName._SC_PAGESIZE);
			} while (UnixMarshal.ShouldRetrySyscall(r));

			UnixMarshal.ThrowExceptionForLastErrorIf(r);
			return r;
		}

		public static void UnixSetFileSize(SafeFileHandle handle, long count) {
			int r;
			do {
				r = Syscall.ftruncate(handle.DangerousGetHandle().ToInt32(), count);
			} while (UnixMarshal.ShouldRetrySyscall(r));

			UnixMarshal.ThrowExceptionForLastErrorIf(r);
			UnixFSync(handle);
		}

		private static void UnixFSync(SafeFileHandle handle) {
			Syscall.fsync(handle.DangerousGetHandle().ToInt32());
		}

		public static int UnixPWrite(SafeFileHandle handle, IntPtr buffer, ulong count, long position) {
			int ret;
			do {
				ret = (int)Syscall.pwrite(handle.DangerousGetHandle().ToInt32(), buffer, count, position);
			} while (UnixMarshal.ShouldRetrySyscall(ret));

			if (ret == -1) {
				UnixMarshal.ThrowExceptionForLastErrorIf(ret);
			}
			return (int)count;
		}

		public static int UnixPRead(SafeFileHandle handle, IntPtr buffer, ulong count, long position) {
			int r;
			do {
				r = (int)Syscall.pread(handle.DangerousGetHandle().ToInt32(), buffer, count, position);
			} while (UnixMarshal.ShouldRetrySyscall(r));

			if (r == -1) {
				UnixMarshal.ThrowExceptionForLastError();
			}

			return (int)count;
		}

		public static long UnixGetFileSize(SafeFileHandle handle) {
			Stat s;
			int r;
			do {
				r = Syscall.fstat(handle.DangerousGetHandle().ToInt32(), out s);
			} while (UnixMarshal.ShouldRetrySyscall(r));

			UnixMarshal.ThrowExceptionForLastErrorIf(r);
			return s.st_size;
		}		
		public static SafeFileHandle UnixCreateRW(string path, System.IO.FileAccess acc, FileShare share, FileMode mode) {
			var flags = UnixGetFlags(acc, mode);
			var han = Syscall.open(path, flags, FilePermissions.S_IRWXU);
			if (han < 0) {
				throw new Win32Exception();
			}

			var handle = new SafeFileHandle((IntPtr)han, true);
			if (handle.IsInvalid) {
				throw new Exception("Invalid handle");
			}
			MacDisableCache(handle);
			return handle;
		}
		public static SafeFileHandle UnixCreateUnbufferedRW(string path, System.IO.FileAccess acc, FileShare share, FileMode mode) {
			//O_RDONLY is 0
			var direct = Runtime.IsMacOS ? OpenFlags.O_RDONLY : OpenFlags.O_DIRECT;
			var flags = UnixGetFlags(acc, mode) | direct;
			var han = Syscall.open(path, flags, FilePermissions.S_IRWXU);
			if (han < 0) {
				throw new Win32Exception();
			}

			var handle = new SafeFileHandle((IntPtr)han, true);
			if (handle.IsInvalid) {
				throw new Exception("Invalid handle");
			}

			MacDisableCache(handle);

			return handle;
		}

		private static OpenFlags UnixGetFlags(System.IO.FileAccess acc, FileMode mode) {
			var flags = OpenFlags.O_RDONLY; //RDONLY is 0
			switch (acc) {
				case System.IO.FileAccess.Read:
					flags |= OpenFlags.O_RDONLY;
					break;
				case System.IO.FileAccess.Write:
					flags |= OpenFlags.O_WRONLY;
					break;
				case System.IO.FileAccess.ReadWrite:
					flags |= OpenFlags.O_RDWR;
					break;
			}

			switch (mode) {
				case FileMode.Append:
					flags |= OpenFlags.O_APPEND;
					break;
				case FileMode.Create:
				case FileMode.CreateNew:
					flags |= OpenFlags.O_CREAT;
					break;
				case FileMode.Truncate:
					flags |= OpenFlags.O_TRUNC;
					break;
			}

			return flags;
		}

		public static long UnixSeek(SafeFileHandle handle, long position, SeekOrigin origin) {
			long r;
			do {
				r = Syscall.lseek(handle.DangerousGetHandle().ToInt32(), position, (SeekFlags)origin);
			} while (r == -1 && UnixMarshal.ShouldRetrySyscall((int)r));

			if (r == -1) {
				UnixMarshal.ThrowExceptionForLastErrorIf((int)r);
			}
			return r;
		}
	}

}

