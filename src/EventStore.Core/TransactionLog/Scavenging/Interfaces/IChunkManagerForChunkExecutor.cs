using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkManagerForChunkExecutor<TStreamId, TRecord> {
		IChunkWriterForExecutor<TStreamId, TRecord> CreateChunkWriter(
			IChunkReaderForExecutor<TStreamId, TRecord> sourceChunk);

		IChunkReaderForExecutor<TStreamId, TRecord> GetChunkReaderFor(long position);
	}

	public interface IChunkWriterForExecutor<TStreamId, TRecord> {
		string FileName { get; }

		void WriteRecord(RecordForExecutor<TStreamId, TRecord> record);

		void Complete(out string newFileName, out long newFileSize);

		void Abort(bool deleteImmediately);
	}

	public interface IChunkReaderForExecutor<TStreamId, TRecord> {
		string Name { get; }
		int FileSize { get; }
		int ChunkStartNumber { get; }
		int ChunkEndNumber { get; }
		bool IsReadOnly { get; }
		long ChunkStartPosition { get; }
		long ChunkEndPosition { get; }
		IEnumerable<bool> ReadInto(
			RecordForExecutor<TStreamId, TRecord>.NonPrepare nonPrepare,
			RecordForExecutor<TStreamId, TRecord>.Prepare prepare);
	}
}
