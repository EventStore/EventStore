using System.IO;
using System.Text;
using DotNext.IO;
using Microsoft.Win32.SafeHandles;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	internal sealed class ReaderWorkItem : BinaryReader {
		public const int BufferSize = 8192;

		public unsafe ReaderWorkItem(nint memoryPtr, int length)
			: base(new UnmanagedMemoryStream((byte*)memoryPtr, length, length, FileAccess.Read), Encoding.UTF8, leaveOpen: false) {
			IsMemory = true;
		}

		public ReaderWorkItem(SafeFileHandle handle)
			: base(new BufferedStream(handle.AsUnbufferedStream(FileAccess.Read), BufferSize), Encoding.UTF8, leaveOpen: false) {
		}

		public bool IsMemory { get; }
	}
}
