using System;
using System.IO;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkDbTruncator {
		private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkDbTruncator>();

		private readonly TFChunkDbConfig _config;

		public TFChunkDbTruncator(TFChunkDbConfig config) {
			Ensure.NotNull(config, "config");
			_config = config;
		}

		public void TruncateDb(long truncateChk) {
			var writerChk = _config.WriterCheckpoint.Read();
			var oldLastChunkNum = (int)(writerChk / _config.ChunkSize);
			var newLastChunkNum = (int)(truncateChk / _config.ChunkSize);

			var excessiveChunks = _config.FileNamingStrategy.GetAllVersionsFor(oldLastChunkNum + 1);
			if (excessiveChunks.Length > 0)
				throw new Exception(string.Format("During truncation of DB excessive TFChunks were found:\n{0}.",
					string.Join("\n", excessiveChunks)));

			ChunkHeader newLastChunkHeader = null;
			string newLastChunkFilename = null;
			for (int chunkNum = 0; chunkNum <= newLastChunkNum;) {
				var chunks = _config.FileNamingStrategy.GetAllVersionsFor(chunkNum);
				if (chunks.Length == 0) {
					if (chunkNum != newLastChunkNum)
						throw new Exception(string.Format("Could not find any chunk #{0}.", chunkNum));
					break;
				}

				using (var fs = File.OpenRead(chunks[0])) {
					var chunkHeader = ChunkHeader.FromStream(fs);
					if (chunkHeader.ChunkEndNumber >= newLastChunkNum) {
						newLastChunkHeader = chunkHeader;
						newLastChunkFilename = chunks[0];
						break;
					}

					chunkNum = chunkHeader.ChunkEndNumber + 1;
				}
			}

			// we need to remove excessive chunks from largest number to lowest one, so in case of crash
			// mid-process, we don't end up with broken non-sequential chunks sequence.
			for (int i = oldLastChunkNum; i > newLastChunkNum; i -= 1) {
				foreach (var chunkFile in _config.FileNamingStrategy.GetAllVersionsFor(i)) {
					Log.Info("File {chunk} will be deleted during TruncateDb procedure.", chunkFile);
					File.SetAttributes(chunkFile, FileAttributes.Normal);
					File.Delete(chunkFile);
				}
			}

			// it's not bad if there is no file, it could have been deleted on previous run
			if (newLastChunkHeader != null) {
				// if the chunk we want to truncate into is already scavenged 
				// we have to truncate (i.e., delete) the whole chunk, not just part of it
				if (newLastChunkHeader.IsScavenged) {
					truncateChk = newLastChunkHeader.ChunkStartPosition;

					// we need to delete EVERYTHING from ChunkStartNumber up to newLastChunkNum, inclusive
					Log.Info(
						"Setting TruncateCheckpoint to {truncateCheckpoint} and deleting ALL chunks from #{chunkStartNumber} inclusively "
						+ "as truncation position is in the middle of scavenged chunk.",
						truncateChk, newLastChunkHeader.ChunkStartNumber);
					for (int i = newLastChunkNum; i >= newLastChunkHeader.ChunkStartNumber; --i) {
						var chunksToDelete = _config.FileNamingStrategy.GetAllVersionsFor(i);
						foreach (var chunkFile in chunksToDelete) {
							Log.Info("File {chunk} will be deleted during TruncateDb procedure.", chunkFile);
							File.SetAttributes(chunkFile, FileAttributes.Normal);
							File.Delete(chunkFile);
						}
					}
				} else {
					TruncateChunkAndFillWithZeros(newLastChunkHeader, newLastChunkFilename, truncateChk);
				}
			}

			if (_config.EpochCheckpoint.Read() >= truncateChk) {
				var epochChk = _config.EpochCheckpoint.Read();
				Log.Info("Truncating epoch from {epochFrom} (0x{epochFrom:X}) to {epochTo} (0x{epochTo:X}).", epochChk,
					epochChk, -1, -1);
				_config.EpochCheckpoint.Write(-1);
				_config.EpochCheckpoint.Flush();
			}

			if (_config.ChaserCheckpoint.Read() > truncateChk) {
				var chaserChk = _config.ChaserCheckpoint.Read();
				Log.Info(
					"Truncating chaser from {chaserCheckpoint} (0x{chaserCheckpoint:X}) to {truncateCheckpoint} (0x{truncateCheckpoint:X}).",
					chaserChk, chaserChk, truncateChk, truncateChk);
				_config.ChaserCheckpoint.Write(truncateChk);
				_config.ChaserCheckpoint.Flush();
			}

			if (_config.WriterCheckpoint.Read() > truncateChk) {
				var writerCheckpoint = _config.WriterCheckpoint.Read();
				Log.Info(
					"Truncating writer from {writerCheckpoint} (0x{writerCheckpoint:X}) to {truncateCheckpoint} (0x{truncateCheckpoint:X}).",
					writerCheckpoint, writerCheckpoint, truncateChk, truncateChk);
				_config.WriterCheckpoint.Write(truncateChk);
				_config.WriterCheckpoint.Flush();
			}

			Log.Info("Resetting TruncateCheckpoint to {epoch} (0x{epoch:X}).", -1, -1);
			_config.TruncateCheckpoint.Write(-1);
			_config.TruncateCheckpoint.Flush();
		}

		private void TruncateChunkAndFillWithZeros(ChunkHeader chunkHeader, string chunkFilename, long truncateChk) {
			if (chunkHeader.IsScavenged
			    || chunkHeader.ChunkStartNumber != chunkHeader.ChunkEndNumber
			    || truncateChk < chunkHeader.ChunkStartPosition
			    || truncateChk >= chunkHeader.ChunkEndPosition) {
				throw new Exception(
					string.Format(
						"Chunk #{0}-{1} ({2}) is not correct unscavenged chunk. TruncatePosition: {3}, ChunkHeader: {4}.",
						chunkHeader.ChunkStartNumber, chunkHeader.ChunkEndNumber, chunkFilename, truncateChk,
						chunkHeader));
			}

			File.SetAttributes(chunkFilename, FileAttributes.Normal);
			using (var fs = new FileStream(chunkFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
				fs.SetLength(ChunkHeader.Size + chunkHeader.ChunkSize + ChunkFooter.Size);
				fs.Position = ChunkHeader.Size + chunkHeader.GetLocalLogPosition(truncateChk);
				var zeros = new byte[65536];
				var leftToWrite = fs.Length - fs.Position;
				while (leftToWrite > 0) {
					var toWrite = (int)Math.Min(leftToWrite, zeros.Length);
					fs.Write(zeros, 0, toWrite);
					leftToWrite -= toWrite;
				}

				fs.FlushToDisk();
			}
		}
	}
}
