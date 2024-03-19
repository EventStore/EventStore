using System.IO;
using DotNext.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	internal sealed class ReaderWorkItem {
		public const int BufferSize = 8192;
		public readonly Stream Stream;
		public readonly BinaryReader Reader;

		public unsafe ReaderWorkItem(nint memoryPtr, int length) {
			Stream = new UnmanagedMemoryStream((byte*)memoryPtr, length, length, FileAccess.Read);
			Reader = new(Stream);
		}

		public ReaderWorkItem(SafeFileHandle handle) {
			Stream = new BufferedStream(handle.AsUnbufferedStream(FileAccess.Read), BufferSize);
			Reader = new(Stream);
		}

		public bool IsMemory => Stream is UnmanagedMemoryStream;
	}
}
