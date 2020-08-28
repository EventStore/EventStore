using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using System.Linq;
using EventStore.Core.TransactionLog.Util;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.TransactionLog.Chunks {
	public class TFChunkManager : IDisposable {
		private static readonly ILogger Log = Serilog.Log.ForContext<TFChunkManager>();

		public const int MaxChunksCount = 100000; // that's enough for about 25 Tb of data

		public int ChunksCount {
			get { return _chunksCount; }
		}

		private readonly TFChunkDbConfig _config;
		private readonly TFChunk.TFChunk[] _chunks = new TFChunk.TFChunk[MaxChunksCount];
		private volatile int _chunksCount;
		private volatile bool _cachingEnabled;

		private readonly object _chunksLocker = new object();
		private int _backgroundPassesRemaining;
		private int _backgroundRunning;

		public TFChunkManager(TFChunkDbConfig config) {
			Ensure.NotNull(config, "config");
			_config = config;
		}

		public void EnableCaching() {
			if (_chunksCount == 0)
				throw new Exception("No chunks in DB.");

			lock (_chunksLocker) {
				_cachingEnabled = _config.MaxChunksCacheSize > 0;
				TryCacheChunk(_chunks[_chunksCount - 1]);
			}
		}

		private void BackgroundCachingProcess(object state) {
			do {
				do {
					CacheUncacheReadOnlyChunks();
				} while (Interlocked.Decrement(ref _backgroundPassesRemaining) > 0);

				Interlocked.Exchange(ref _backgroundRunning, 0);
			} while (Interlocked.CompareExchange(ref _backgroundPassesRemaining, 0, 0) > 0
			         && Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0);
		}

		private void CacheUncacheReadOnlyChunks() {
			int lastChunkToCache;
			lock (_chunksLocker) {
				long totalSize = 0;
				lastChunkToCache = _chunksCount;

				for (int chunkNum = _chunksCount - 1; chunkNum >= 0;) {
					var chunk = _chunks[chunkNum];
					var chunkSize = chunk.IsReadOnly
						? chunk.ChunkFooter.PhysicalDataSize + chunk.ChunkFooter.MapSize + ChunkHeader.Size +
						  ChunkFooter.Size
						: chunk.ChunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size;

					if (totalSize + chunkSize > _config.MaxChunksCacheSize)
						break;

					totalSize += chunkSize;
					lastChunkToCache = chunk.ChunkHeader.ChunkStartNumber;

					chunkNum = chunk.ChunkHeader.ChunkStartNumber - 1;
				}
			}

			for (int chunkNum = lastChunkToCache - 1; chunkNum >= 0;) {
				var chunk = _chunks[chunkNum];
				if (chunk.IsReadOnly)
					chunk.UnCacheFromMemory();
				chunkNum = chunk.ChunkHeader.ChunkStartNumber - 1;
			}

			for (int chunkNum = lastChunkToCache; chunkNum < _chunksCount;) {
				var chunk = _chunks[chunkNum];
				if (chunk.IsReadOnly)
					chunk.CacheInMemory();
				chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
			}
		}

		public TFChunk.TFChunk CreateTempChunk(ChunkHeader chunkHeader, int fileSize) {
			var chunkFileName = _config.FileNamingStrategy.GetTempFilename();
			return TFChunk.TFChunk.CreateWithHeader(chunkFileName,
				chunkHeader,
				fileSize,
				_config.InMemDb,
				_config.InitialReaderCount,
				_config.MaxReaderCount);
		}

		public TFChunk.TFChunk AddNewChunk() {
			lock (_chunksLocker) {
				var chunkNumber = _chunksCount;
				var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkNumber, 0);
				var chunk = TFChunk.TFChunk.CreateNew(chunkName,
					_config.ChunkSize,
					chunkNumber,
					chunkNumber,
					isScavenged: false,
					inMem: _config.InMemDb,
					initialReaderCount: _config.InitialReaderCount,
					maxReaderCount: _config.MaxReaderCount);
				AddChunk(chunk);
				return chunk;
			}
		}

		public TFChunk.TFChunk AddNewChunk(ChunkHeader chunkHeader, int fileSize) {
			Ensure.NotNull(chunkHeader, "chunkHeader");
			Ensure.Positive(fileSize, "fileSize");

			lock (_chunksLocker) {
				if (chunkHeader.ChunkStartNumber != _chunksCount)
					throw new Exception(string.Format(
						"Received request to create a new ongoing chunk #{0}-{1}, but current chunks count is {2}.",
						chunkHeader.ChunkStartNumber, chunkHeader.ChunkEndNumber, _chunksCount));

				var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkHeader.ChunkStartNumber, 0);
				var chunk = TFChunk.TFChunk.CreateWithHeader(chunkName,
					chunkHeader,
					fileSize,
					_config.InMemDb,
					initialReaderCount: _config.InitialReaderCount,
					maxReaderCount: _config.MaxReaderCount);
				AddChunk(chunk);
				return chunk;
			}
		}

		public void AddChunk(TFChunk.TFChunk chunk) {
			Ensure.NotNull(chunk, "chunk");

			lock (_chunksLocker) {
				for (int i = chunk.ChunkHeader.ChunkStartNumber; i <= chunk.ChunkHeader.ChunkEndNumber; ++i) {
					_chunks[i] = chunk;
				}

				_chunksCount = chunk.ChunkHeader.ChunkEndNumber + 1;

				TryCacheChunk(chunk);
			}
		}

		public TFChunk.TFChunk SwitchChunk(TFChunk.TFChunk chunk, bool verifyHash,
			bool removeChunksWithGreaterNumbers) {
			Ensure.NotNull(chunk, "chunk");
			if (!chunk.IsReadOnly)
				throw new ArgumentException(string.Format("Passed TFChunk is not completed: {0}.", chunk.FileName));

			var chunkHeader = chunk.ChunkHeader;
			var oldFileName = chunk.FileName;

			Log.Information("Switching chunk #{chunkStartNumber}-{chunkEndNumber} ({oldFileName})...",
				chunkHeader.ChunkStartNumber, chunkHeader.ChunkEndNumber, Path.GetFileName(oldFileName));
			TFChunk.TFChunk newChunk;

			if (_config.InMemDb)
				newChunk = chunk;
			else {
				chunk.Dispose();
				try {
					chunk.WaitForDestroy(0); // should happen immediately
				} catch (TimeoutException exc) {
					throw new Exception(
						string.Format("The chunk that is being switched {0} is used by someone else.", chunk), exc);
				}

				var newFileName =
					_config.FileNamingStrategy.DetermineBestVersionFilenameFor(chunkHeader.ChunkStartNumber);
				Log.Information("File {oldFileName} will be moved to file {newFileName}", Path.GetFileName(oldFileName),
					Path.GetFileName(newFileName));
				try {
					File.Move(oldFileName, newFileName);
				} catch (IOException) {
					ProcessUtil.PrintWhoIsLocking(oldFileName, Log);
					ProcessUtil.PrintWhoIsLocking(newFileName, Log);
					throw;
				}

				newChunk = TFChunk.TFChunk.FromCompletedFile(newFileName, verifyHash,
					_config.InitialReaderCount, _config.MaxReaderCount, _config.OptimizeReadSideCache);
			}

			lock (_chunksLocker) {
				if (!ReplaceChunksWith(newChunk, "Old")) {
					Log.Information("Chunk {chunk} will be not switched, marking for remove...", newChunk);
					newChunk.MarkForDeletion();
				}

				if (removeChunksWithGreaterNumbers) {
					var oldChunksCount = _chunksCount;
					_chunksCount = newChunk.ChunkHeader.ChunkEndNumber + 1;
					RemoveChunks(chunkHeader.ChunkEndNumber + 1, oldChunksCount - 1, "Excessive");
					if (_chunks[_chunksCount] != null)
						throw new Exception(string.Format("Excessive chunk #{0} found after raw replication switch.",
							_chunksCount));
				}

				TryCacheChunk(newChunk);
				return newChunk;
			}
		}

		private bool ReplaceChunksWith(TFChunk.TFChunk newChunk, string chunkExplanation) {
			var chunkStartNumber = newChunk.ChunkHeader.ChunkStartNumber;
			var chunkEndNumber = newChunk.ChunkHeader.ChunkEndNumber;
			for (int i = chunkStartNumber; i <= chunkEndNumber;) {
				var chunk = _chunks[i];
				if (chunk != null) {
					var chunkHeader = chunk.ChunkHeader;
					if (chunkHeader.ChunkStartNumber < chunkStartNumber || chunkHeader.ChunkEndNumber > chunkEndNumber)
						return false;
					i = chunkHeader.ChunkEndNumber + 1;
				} else {
					//Cover the case of initial replication of merged chunks where they were never set
					// in the map in the first place.
					i = i + 1;
				}
			}

			TFChunk.TFChunk previousRemovedChunk = null;
			for (int i = chunkStartNumber; i <= chunkEndNumber; i += 1) {
				var oldChunk = Interlocked.Exchange(ref _chunks[i], newChunk);
				if (!ReferenceEquals(previousRemovedChunk, oldChunk)) {
					// Once we've swapped all entries for the previousRemovedChunk we can safely delete it.
					if (previousRemovedChunk != null) {
						previousRemovedChunk.MarkForDeletion();
						Log.Information("{chunkExplanation} chunk #{oldChunk} is marked for deletion.", chunkExplanation,
							previousRemovedChunk);
					}

					previousRemovedChunk = oldChunk;
				}
			}

			if (previousRemovedChunk != null) {
				// Delete the last chunk swapped out now it's fully replaced.
				previousRemovedChunk.MarkForDeletion();
				Log.Information("{chunkExplanation} chunk #{oldChunk} is marked for deletion.", chunkExplanation,
					previousRemovedChunk);
			}

			return true;
		}

		private void RemoveChunks(int chunkStartNumber, int chunkEndNumber, string chunkExplanation) {
			TFChunk.TFChunk lastRemovedChunk = null;
			for (int i = chunkStartNumber; i <= chunkEndNumber; i += 1) {
				var oldChunk = Interlocked.Exchange(ref _chunks[i], null);
				if (oldChunk != null && !ReferenceEquals(lastRemovedChunk, oldChunk)) {
					oldChunk.MarkForDeletion();
					Log.Information("{chunkExplanation} chunk {oldChunk} is marked for deletion.", chunkExplanation, oldChunk);
				}

				lastRemovedChunk = oldChunk;
			}
		}

		private void TryCacheChunk(TFChunk.TFChunk chunk) {
			if (!_cachingEnabled)
				return;

			Interlocked.Increment(ref _backgroundPassesRemaining);
			if (Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(BackgroundCachingProcess);

			if (!chunk.IsReadOnly && chunk.ChunkHeader.ChunkSize + ChunkHeader.Size + ChunkFooter.Size <=
			    _config.MaxChunksCacheSize)
				chunk.CacheInMemory();
		}

		public TFChunk.TFChunk GetChunkFor(long logPosition) {
			var chunkNum = (int)(logPosition / _config.ChunkSize);
			if (chunkNum < 0 || chunkNum >= _chunksCount)
				throw new ArgumentOutOfRangeException("logPosition",
					string.Format("LogPosition {0} does not have corresponding chunk in DB.", logPosition));

			var chunk = _chunks[chunkNum];
			if (chunk == null)
				throw new Exception(string.Format(
					"Requested chunk for LogPosition {0}, which is not present in TFChunkManager.", logPosition));
			return chunk;
		}

		public TFChunk.TFChunk GetChunk(int chunkNum) {
			if (chunkNum < 0 || chunkNum >= _chunksCount)
				throw new ArgumentOutOfRangeException("chunkNum",
					string.Format("Chunk #{0} is not present in DB.", chunkNum));

			var chunk = _chunks[chunkNum];
			if (chunk == null)
				throw new Exception(string.Format("Requested chunk #{0}, which is not present in TFChunkManager.",
					chunkNum));
			return chunk;
		}

		public TFChunk.TFChunk GetChunkForOrDefault(string path) {
			return _chunks != null ? _chunks.FirstOrDefault(c => c != null && c.FileName == path) : null;
		}

		public void Dispose() {
			lock (_chunksLocker) {
				for (int i = 0; i < _chunksCount; ++i) {
					if (_chunks[i] != null)
						_chunks[i].Dispose();
				}
			}
		}
	}
}
