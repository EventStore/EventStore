using System;
using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.TransactionLog.Tests.Validation {
	public static class DbUtil {
		public static void CreateSingleChunk(TFChunkDbConfig config, int chunkNum, string filename,
			int? actualDataSize = null, bool isScavenged = false, byte[] contents = null) {
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum,
				isScavenged, Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var dataSize = actualDataSize ?? config.ChunkSize;
			var buf = new byte[ChunkHeader.Size + dataSize + ChunkFooter.Size];
			Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
			var chunkFooter = new ChunkFooter(true, true, dataSize, dataSize, 0, new byte[ChunkFooter.ChecksumSize]);
			chunkBytes = chunkFooter.AsByteArray();
			Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);

			if (contents != null) {
				if (contents.Length != dataSize)
					throw new Exception("Wrong contents size.");
				Buffer.BlockCopy(contents, 0, buf, ChunkHeader.Size, contents.Length);
			}

			File.WriteAllBytes(filename, buf);
		}

		public static void CreateMultiChunk(TFChunkDbConfig config, int chunkStartNum, int chunkEndNum, string filename,
			int? physicalSize = null, long? logicalSize = null) {
			if (chunkStartNum > chunkEndNum) throw new ArgumentException("chunkStartNum");

			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkStartNum, chunkEndNum,
				true, Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var physicalDataSize = physicalSize ?? config.ChunkSize;
			var logicalDataSize = logicalSize ?? (chunkEndNum - chunkStartNum + 1) * config.ChunkSize;
			var buf = new byte[ChunkHeader.Size + physicalDataSize + ChunkFooter.Size];
			Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);
			var chunkFooter = new ChunkFooter(true, true, physicalDataSize, logicalDataSize, 0,
				new byte[ChunkFooter.ChecksumSize]);
			chunkBytes = chunkFooter.AsByteArray();
			Buffer.BlockCopy(chunkBytes, 0, buf, buf.Length - ChunkFooter.Size, chunkBytes.Length);
			File.WriteAllBytes(filename, buf);
		}

		public static void CreateOngoingChunk(TFChunkDbConfig config, int chunkNum, string filename,
			int? actualSize = null, byte[] contents = null) {
			var chunkHeader = new ChunkHeader(TFChunk.CurrentChunkVersion, config.ChunkSize, chunkNum, chunkNum, false,
				Guid.NewGuid());
			var chunkBytes = chunkHeader.AsByteArray();
			var dataSize = actualSize ?? config.ChunkSize;
			var buf = new byte[ChunkHeader.Size + dataSize + ChunkFooter.Size];
			Buffer.BlockCopy(chunkBytes, 0, buf, 0, chunkBytes.Length);

			if (contents != null) {
				if (contents.Length != dataSize)
					throw new Exception("Wrong contents size.");
				Buffer.BlockCopy(contents, 0, buf, ChunkHeader.Size, contents.Length);
			}

			File.WriteAllBytes(filename, buf);
		}
	}
}
