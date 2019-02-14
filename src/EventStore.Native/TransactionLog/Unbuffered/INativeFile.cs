using System.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Unbuffered {
	public interface INativeFile {
		uint GetDriveSectorSize(string path);
		long GetPageSize(string path);
		void SetFileSize(SafeFileHandle handle, long count);
		unsafe void Write(SafeFileHandle handle, byte* buffer, uint count, ref int written);
		unsafe int Read(SafeFileHandle handle, byte* buffer, int offset, int count);
		long GetFileSize(SafeFileHandle handle);
		SafeFileHandle Create(string path, FileAccess acc, FileShare readWrite, FileMode mode, int flags);

		SafeFileHandle CreateUnbufferedRW(string path, FileAccess acc, FileShare share, FileMode mode,
			bool writeThrough);

		void Seek(SafeFileHandle handle, long position, SeekOrigin origin);
	}
}
