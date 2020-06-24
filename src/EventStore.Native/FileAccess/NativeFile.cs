using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Native.FileAccess {
	internal sealed class NativeFile : IDisposable {
		public int SectorSize { get; private set; } = 4096;
		public int BlockSize => SectorSize * 16 * 64;
		public FileInfo Info { get; private set; }
		public long Length { get; private set; }

		private SafeFileHandle _file;

		private NativeFile() { }
		/// <summary>
		/// Creates and opens a new native file on the current OS
		/// </summary>
		/// <param name="path">full path to file</param>
		/// <param name="length">Should be a multiple of SectorSize (4096) to support unbuffered writes</param>
		/// <param name="replace">true to allow overwriting to target file</param>
		/// <returns>The native file for the current OS</returns>
		internal static NativeFile OpenNewFile(string path, long length = 0, bool replace = false) {
			NativeFile file = new NativeFile();
			if (length % file.SectorSize != 0) {
				length += file.SectorSize - length % file.SectorSize;
			}
			CreateFile(path, length, replace);
			file.Open(path);
			return file;
		}
		internal static NativeFile OpenFile(string path, System.IO.FileAccess access, FileMode mode, FileShare share) {
			NativeFile file = new NativeFile();
			file.Open(path, access, mode, share);
			return file;
		}
		internal static NativeFile OpenFile(string path) {
			NativeFile file = new NativeFile();
			
			file.Open(path);
			return file;
		}
		private void Open(string path, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite, FileMode mode = FileMode.OpenOrCreate, FileShare share = FileShare.ReadWrite) {
			if (_file != null)
				throw new InvalidOperationException($"{nameof(NativeFile)}:Open - File is already open!");
			if (_disposed)
				throw new ObjectDisposedException(nameof(NativeFile), $"{nameof(NativeFile)}:Open - Cannot reopen a disposed {nameof(NativeFile)}, create a new instance.");
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentOutOfRangeException($"{nameof(NativeFile)}:Open - Empty or Null Path");
			Info = new FileInfo(path);

			_file = NativeMethods.OpenNative(Info.FullName, mode, access, share);
			Length = NativeMethods.GetFileSize(_file);
			SectorSize = NativeMethods.GetPhysicalSectorSize(_file);
		}

		public int Read(long position, IntPtr buffer, int requestedBytes) {
			//todo: validate parms are all > 0
			if (!IsOpen())
				throw new InvalidOperationException($"{nameof(NativeFile)}:Read - Cannot read, file is not open.");
			if (position + requestedBytes > Length)
				throw new InvalidOperationException($"{nameof(NativeFile)}:Read - Cannot read from position past end of file");

			//unbuffered = (long)position % SectorSize == 0 &&
			//                (long)buffer % SectorSize == 0 &&
			//                requestedBytes % SectorSize == 0;

			int bytesRead = 0;

			// Do until there are no more bytes to read or the buffer is full.
			do {
				var blockByteSize = Math.Min(BlockSize, requestedBytes - bytesRead);

				var bytesReadInBlock = NativeMethods.ReadAt(_file, buffer, blockByteSize, position);
				if (bytesReadInBlock == 0)
					break;
				bytesRead += bytesReadInBlock;
				position += bytesReadInBlock;
				buffer += bytesReadInBlock;
			} while (bytesRead < requestedBytes);
			return bytesRead;
		}

		/// <summary>
		/// This function writes out blocks at a time instead of the entire file.  
		/// </summary>
		/// <param name="position"></param>
		/// <param name="buffer"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public int Write(long position, IntPtr buffer, int length) {
			//todo: validate parms are all > 0
			if (!IsOpen()) { throw new InvalidOperationException("WinFile:Write - Cannot write, file is not open."); }

			//unbuffered = position % SectorSize == 0 &&
			//                (long)buffer % SectorSize == 0 &&
			//                length % SectorSize == 0;

			int bytesWritten = 0;
			var remainingBytes = length;
			// Do until there are no more bytes to write.
			do {

				var bytesToWrite = Math.Min(remainingBytes, BlockSize);
				var bytesWrittenInBlock = NativeMethods.WriteAt(_file, buffer, bytesToWrite, position);

				position += bytesWrittenInBlock;
				buffer += bytesToWrite;
				bytesWritten += bytesToWrite;
				remainingBytes -= bytesToWrite;
			} while (remainingBytes > 0);

			NativeMethods.Flush(_file);
			if (position + length > Length) {
				Length = NativeMethods.GetFileSize(_file);
			}

			return bytesWritten;
		}

		public static void CreateFile(string path, long length, bool replace) {
			var fileInfo = new FileInfo(path);
			if (fileInfo.Exists && !replace)
				throw new InvalidOperationException($"NativeFile:CreateFile - File Exists");
			using var fs = fileInfo.Create();
			if (length > 0) {
				try {
					fs.SetLength(length);
				} catch (Exception ex) {
					throw new ApplicationException($"NativeFile:CreateFile - Cannot set Length", ex);
				}
			}
		}

		public void Flush() {
			if (!IsOpen())
				return;
			NativeMethods.Flush(_file);
		}

		public long Seek(long offset, SeekOrigin origin) {
			if(origin == SeekOrigin.End){
					offset = Length + offset;
					origin = SeekOrigin.Begin;
				}
			if (Length + offset < 0)
				offset = Length;
				
			return NativeMethods.Seek(_file, offset, origin);
		}

		public long AlignToSectorSize(long value, bool roundUp = true) {
			var sectorSize = NativeMethods.GetPhysicalSectorSize(_file);
			if (value % sectorSize == 0) { return value; }
			return value + (sectorSize - (value % sectorSize));
		}
		public long SetLength(long length) {
			Length = NativeMethods.SetFileLength(_file, AlignToSectorSize(length));
			return Length;
		}
		private bool _disposed;
		internal bool IsOpen() {
			return !(_disposed || (_file?.IsInvalid ?? true) || _file.IsClosed);
		}
		private void Dispose(bool disposing) {
			_disposed = true;
			_file?.Dispose();
			if (disposing) { }
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		~NativeFile() {
			Dispose(false);
		}
	}
}
