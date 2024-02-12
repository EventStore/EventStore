using System.IO;
using System.Security.Cryptography;
using DotNext;
using DotNext.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	internal sealed class WriterWorkItem : Disposable {
		public const int BufferSize = 8192;

		public Stream WorkingStream { get; private set; }

		private readonly Stream _fileStream;
		private UnmanagedMemoryStream _memStream;

		public readonly MemoryStream Buffer;
		public readonly BinaryWriter BufferWriter;
		public readonly HashAlgorithm MD5;

		public unsafe WriterWorkItem(nint memoryPtr, int length, HashAlgorithm md5) {
			WorkingStream = _memStream = new UnmanagedMemoryStream((byte*)memoryPtr, length, length, FileAccess.ReadWrite);
			Buffer = new(BufferSize);
			BufferWriter = new(Buffer);
			MD5 = md5;
		}

		public WriterWorkItem(SafeFileHandle handle, HashAlgorithm md5, bool unbuffered) {
			WorkingStream = _fileStream = unbuffered
				? handle.AsUnbufferedStream(FileAccess.ReadWrite)
				: new BufferedStream(handle.AsUnbufferedStream(FileAccess.ReadWrite), BufferSize);
			Buffer = new(BufferSize);
			BufferWriter = new(Buffer);
			MD5 = md5;
		}

		public void SetMemStream(UnmanagedMemoryStream memStream) {
			_memStream = memStream;
			if (_fileStream is null)
				WorkingStream = memStream;
		}

		public void AppendData(byte[] buf, int offset, int len) {
			// as we are always append-only, stream's position should be right here
			_fileStream?.Write(buf, offset, len);

			//MEMORY
			_memStream?.Write(buf, offset, len);
		}

		public void ResizeStream(int fileSize) {
			_fileStream?.SetLength(fileSize);
			_memStream?.SetLength(fileSize);
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				_fileStream?.Dispose();
				DisposeMemStream();
				Buffer.Dispose();
				BufferWriter.Dispose();
				MD5.Dispose();
			}

			base.Dispose(disposing);
		}

		public void FlushToDisk() => _fileStream?.Flush();

		public void DisposeMemStream() {
			_memStream?.Dispose();
			_memStream = null;
		}
	}
}
