using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Mono.Unix.Native;

// ReSharper disable RedundantIfElseBlock

namespace EventStore.Native.FileAccess {
	internal static unsafe partial class NativeMethods {

		public static int PageSize = 4096;

		public struct PageAlignedBuffer {
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
			} else {
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
				} else {
					Stdlib.free(buffer.Handle);
					free = true;
				}
			}
			if (!free)
				return false;

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
			} else {
				return UnixPRead(file, target, (ulong)length, position);

			}
		}

		internal static int WriteAt(SafeFileHandle file, IntPtr source, int length, long position) {
			if (Runtime.IsWindows) {
				var overlapped = new NativeOverlapped();
				overlapped.SetPosition(position);
				return OverlappedWrite(file, (byte*)source, length, &overlapped);
			} else {
				return UnixPWrite(file, source, (ulong)length, position);
			}
		}

		internal static void Flush(SafeFileHandle file) {
			if (Runtime.IsWindows) {
				FlushFileBuffers(file);
			} else {
				UnixFSync(file);
			}
		}
		public static long Seek(SafeFileHandle file, long offset, SeekOrigin origin) {
			if (Runtime.IsWindows) {
				if (!WinSeek(file, offset, out var position, origin)) {
					Win32Exception we = new Win32Exception();
					throw new IOException($"{nameof(NativeMethods)}:WinSeek Stream seek failed - {we.NativeErrorCode}:{we.Message}");
				}
				return (long)position;
			} else {
				return UnixSeek(file, offset, origin);
			}
		}
		internal static SafeFileHandle OpenNative(
							string path, 
							FileMode mode, 
							System.IO.FileAccess access,
							FileShare share) {
			SafeFileHandle file;
			if (Runtime.IsWindows) {
				file = CreateFileW(
					path,
					(uint)access,
					(uint)share,
					IntPtr.Zero,
					(uint)mode,
					(uint)FileAttributes.Normal | FILE_FLAG_WRITE_THROUGH,
					IntPtr.Zero);
				if ((file?.IsInvalid ?? true) || file.IsClosed) {
					Win32Exception we = new Win32Exception();
					ApplicationException ae = new ApplicationException($"NativeMethods:OpenWriteThrough - Unable to open file {path}. - {we.Message}");
					throw ae;
				}
			} else {
				var info = new FileInfo(path);
				if (!info.Exists) {	using (var _ = info.Create()){}	}
				file = UnixCreateRW(info.FullName, access, FileShare.ReadWrite, mode);
			}

			return file;
		}
		//internal static SafeFileHandle OpenUnbuffered(string path) {
		//	SafeFileHandle file;
		//	if (Runtime.IsWindows) {
		//		file = NativeMethods.CreateFileW(
		//		path,
		//		(uint)System.IO.FileAccess.ReadWrite,
		//		(uint)FileShare.ReadWrite,
		//		IntPtr.Zero,
		//		(uint)FileMode.Open,
		//		(uint)FileAttributes.Normal | FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH,
		//		IntPtr.Zero);
		//		if ((file?.IsInvalid ?? true) || file.IsClosed) {
		//			throw new ApplicationException($"NativeMethods:OpenUnbuffered - Unable to open file {path}.");
		//		}
		//	} else {
		//		file = UnixCreateUnbufferedRW(path,
		//			 System.IO.FileAccess.ReadWrite,
		//			 FileShare.ReadWrite,
		//			 FileMode.Open);
		//	}

		//	return file;
		//}


		internal static long GetFileSize(SafeFileHandle file) {
			long length;
			if (Runtime.IsWindows) {
				if (!NativeMethods.GetFileSizeEx(file, out length)) {
					throw new InvalidOperationException($"NativeMethods:GetFileSize - Unable to get file length.");
				}
			} else {
				length = UnixGetFileSize(file);
			}

			return length;
		}

		internal static int Compare(IntPtr p1, IntPtr p2, int length) {
			if (Runtime.IsWindows) {
				return Compare(p1, p2, (UIntPtr)length);
			} else {
				return UnixCompare(p1, p2, (UIntPtr)length);
			}
		}
		internal static void Copy(IntPtr destination, IntPtr source, long length) {
			if (Runtime.IsWindows) {
				CopyMemory((byte*)destination, (byte*)source, (uint)length);
			} else {
				UnixCopyMemory((byte*)destination, (byte*)source, (uint)length);
			}
		}

		internal static void Clear(IntPtr buffer, int length) {
			if (Runtime.IsWindows) {
				MemSet(buffer, 0, length);
			} else {
				UnixMemSet(buffer, 0, length);
			}
		}
		internal static void Fill(IntPtr buffer, byte fill, int length) {
			if (Runtime.IsWindows) {
				MemSet(buffer, fill, length);
			} else {
				UnixMemSet(buffer, fill, length);
			}
		}

		private static int _physicalSectorSize;
		internal static int GetPhysicalSectorSize(SafeFileHandle file) {
			if (_physicalSectorSize != 0) { return _physicalSectorSize; }

			if (Runtime.IsWindows) {
				FILE_STORAGE_INFO diskInfo = GetStorageInfo(file);
				_physicalSectorSize = (int)diskInfo.PhysicalBytesPerSectorForAtomicity;
				return _physicalSectorSize;
			} else {
				//todo: implement linux call
				_physicalSectorSize = 4096;
				return _physicalSectorSize;
			}
		}


		public static long SetFileLength(SafeFileHandle file, long length) {
			if (Runtime.IsWindows) {
				if (!WinSeek(file, 0, out var oldPosition, SeekOrigin.Current)) {
					Win32Exception we = new Win32Exception();
					throw new ApplicationException($"{nameof(NativeMethods)}:SetFileLength - unable to get current position: {we.NativeErrorCode}:{we.Message}");
				}
				if (!WinSeek(file, length, out _, SeekOrigin.Begin)) {
					Win32Exception we = new Win32Exception();
					throw new ApplicationException($"{nameof(NativeMethods)}:SetFileLength - unable to get current position: {we.NativeErrorCode}:{we.Message}");
				}
				if (!SetEndOfFile(file)) {
					var we = new Win32Exception();
					throw new ApplicationException($"{nameof(NativeMethods)}:SetFileLength - unable to set file length. {we.NativeErrorCode}:{we.Message}");

				}
				oldPosition = (IntPtr)Math.Min(length, (long)oldPosition);
				if (!WinSeek(file, (long)oldPosition, out _, SeekOrigin.Begin)) {
					Win32Exception we = new Win32Exception();
					throw new ApplicationException($"{nameof(NativeMethods)}:SetFileLength - unable to set current position: {we.NativeErrorCode}:{we.Message}");
				}
				return GetFileSize(file);
			} else {
				UnixSetFileSize(file, length);
				return length;
			}
		}
		public static class Runtime {
			public static readonly bool IsUnixOrMac = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) |
			                                          RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

			public static readonly bool IsWindows = !IsUnixOrMac;

			public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		}
	}
}
