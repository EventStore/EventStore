// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Core.Transforms;
using EventStore.Core.Transforms.Identity;
using ChunkInfo = EventStore.Core.Data.ChunkInfo;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.TransactionLog.Chunks;

public class TFChunkManager : IThreadPoolWorkItem {
	private static readonly ILogger Log = Serilog.Log.ForContext<TFChunkManager>();

	// MaxChunksCount is currently capped at 400,000 since:
	// - the chunk file naming strategy supports only up to 6 digits for the chunk number.
	// - this class uses a fixed size array to keep the chunk list
	public const int MaxChunksCount = 400_000;

	public int ChunksCount {
		get { return _chunksCount; }
	}

	private readonly TFChunkDbConfig _config;
	private readonly TFChunk.TFChunk[] _chunks = new TFChunk.TFChunk[MaxChunksCount];
	private readonly ITransactionFileTracker _tracker;
	private readonly DbTransformManager _transformManager;

	private volatile int _chunksCount;
	private volatile bool _cachingEnabled;

	// protects _chunksCount and _chunks
	private readonly AsyncExclusiveLock _chunksLocker = new();
	private int _backgroundPassesRemaining;
	private int _backgroundRunning;
	public Action<ChunkInfo> OnChunkLoaded { get; init; }
	public Action<ChunkInfo> OnChunkCompleted { get; init; }
	public Action<ChunkInfo> OnChunkSwitched { get; init; }

	public TFChunkManager(
		TFChunkDbConfig config,
		ITransactionFileTracker tracker,
		DbTransformManager transformManager) {
		Ensure.NotNull(config, "config");
		_config = config;
		_tracker = tracker;
		_transformManager = transformManager;
	}

	public async ValueTask EnableCaching(CancellationToken token) {
		await _chunksLocker.AcquireAsync(token);
		try {
			_cachingEnabled = true;
		} finally {
			_chunksLocker.Release();
		}

		// trigger caching out of lock to avoid lock contention
		TriggerBackgroundCaching();
	}

	async void IThreadPoolWorkItem.Execute() {
		do {
			do {
				await CacheUncacheReadOnlyChunks();
			} while (Interlocked.Decrement(ref _backgroundPassesRemaining) > 0);

			Interlocked.Exchange(ref _backgroundRunning, 0);
		} while (Interlocked.CompareExchange(ref _backgroundPassesRemaining, 0, 0) > 0
		         && Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0);
	}

	private async ValueTask CacheUncacheReadOnlyChunks(CancellationToken token = default) {
		int lastChunkToCache;

		await _chunksLocker.AcquireAsync(token);
		try {
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
		} finally {
			_chunksLocker.Release();
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
				await chunk.CacheInMemory(token);
			chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
		}
	}

	public ValueTask<TFChunk.TFChunk> CreateTempChunk(ChunkHeader chunkHeader, int fileSize, CancellationToken token) {
		var chunkFileName = _config.FileNamingStrategy.CreateTempFilename();
		return TFChunk.TFChunk.CreateWithHeader(chunkFileName,
			chunkHeader,
			fileSize,
			_config.InMemDb,
			_config.Unbuffered,
			_config.WriteThrough,
			_config.ReduceFileCachePressure,
			_tracker,
			// temporary chunks are used for replicating raw (scavenged) chunks.
			// since the raw data being replicated is already transformed, we use
			// the identity transform as we don't want to transform the data again
			// when appending raw data to the chunk.
			new IdentityChunkTransformFactory(),
			ReadOnlyMemory<byte>.Empty,
			token);
	}

	public async ValueTask<TFChunk.TFChunk> AddNewChunk(CancellationToken token) {
		TFChunk.TFChunk chunk;
		bool triggerCaching;
		await _chunksLocker.AcquireAsync(token);
		try {
			var chunkNumber = _chunksCount;
			var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkNumber, 0);
			chunk = await TFChunk.TFChunk.CreateNew(chunkName,
				_config.ChunkSize,
				chunkNumber,
				chunkNumber,
				isScavenged: false,
				inMem: _config.InMemDb,
				unbuffered: _config.Unbuffered,
				writethrough: _config.WriteThrough,
				reduceFileCachePressure: _config.ReduceFileCachePressure,
				tracker: _tracker,
				transformFactory: _transformManager.GetFactoryForNewChunk(),
				token);
			AddChunk(chunk, isNew: true);
			triggerCaching = _cachingEnabled;
		} finally {
			_chunksLocker.Release();
		}

		// trigger caching out of lock to avoid lock contention
		if (triggerCaching)
			TriggerBackgroundCaching();
		return chunk;
	}

	public async ValueTask<TFChunk.TFChunk> AddNewChunk(ChunkHeader chunkHeader, ReadOnlyMemory<byte> transformHeader, int fileSize, CancellationToken token) {
		Ensure.NotNull(chunkHeader, "chunkHeader");
		Ensure.Positive(fileSize, "fileSize");

		TFChunk.TFChunk chunk;
		bool triggerCaching;
		await _chunksLocker.AcquireAsync(token);
		try {
			if (chunkHeader.ChunkStartNumber != _chunksCount)
				throw new Exception(string.Format(
					"Received request to create a new ongoing chunk #{0}-{1}, but current chunks count is {2}.",
					chunkHeader.ChunkStartNumber, chunkHeader.ChunkEndNumber, _chunksCount));

			var chunkName = _config.FileNamingStrategy.GetFilenameFor(chunkHeader.ChunkStartNumber, 0);
			chunk = await TFChunk.TFChunk.CreateWithHeader(chunkName,
				chunkHeader,
				fileSize,
				_config.InMemDb,
				unbuffered: _config.Unbuffered,
				writethrough: _config.WriteThrough,
				reduceFileCachePressure: _config.ReduceFileCachePressure,
				tracker: _tracker,
				transformFactory: _transformManager.GetFactoryForExistingChunk(chunkHeader.TransformType),
				transformHeader: transformHeader,
				token);
			AddChunk(chunk, isNew: true);
			triggerCaching = _cachingEnabled;
		} finally {
			_chunksLocker.Release();
		}

		// trigger caching out of lock to avoid lock contention
		if (triggerCaching)
			TriggerBackgroundCaching();
		return chunk;
	}

	private void AddChunk(TFChunk.TFChunk chunk, bool isNew) {
		Debug.Assert(chunk is not null);
		Debug.Assert(_chunksLocker.IsLockHeld);

		for (int i = chunk.ChunkHeader.ChunkStartNumber; i <= chunk.ChunkHeader.ChunkEndNumber; ++i) {
			_chunks[i] = chunk;
		}

		_chunksCount = Math.Max(chunk.ChunkHeader.ChunkEndNumber + 1, _chunksCount);

		if (isNew) {
			if (chunk.ChunkHeader.ChunkStartNumber > 0)
				OnChunkCompleted?.Invoke(_chunks[chunk.ChunkHeader.ChunkStartNumber - 1].ChunkInfo);
		} else {
			OnChunkLoaded?.Invoke(chunk.ChunkInfo);
		}
	}

	public async ValueTask AddChunk(TFChunk.TFChunk chunk, CancellationToken token) {
		Ensure.NotNull(chunk, "chunk");

		bool triggerCaching;
		await _chunksLocker.AcquireAsync(token);
		try {
			AddChunk(chunk, isNew: false);
			triggerCaching = _cachingEnabled;
		} finally {
			_chunksLocker.Release();
		}

		// trigger caching out of lock to avoid lock contention
		if (triggerCaching)
			TriggerBackgroundCaching();
	}

	public async ValueTask<TFChunk.TFChunk> SwitchChunk(TFChunk.TFChunk chunk, bool verifyHash,
		bool removeChunksWithGreaterNumbers,
		CancellationToken token) {
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
				_config.FileNamingStrategy.DetermineNewVersionFilenameForIndex(chunkHeader.ChunkStartNumber, defaultVersion: 1);
			Log.Information("File {oldFileName} will be moved to file {newFileName}", Path.GetFileName(oldFileName),
				Path.GetFileName(newFileName));
			try {
				File.Move(oldFileName, newFileName);
			} catch (IOException) {
				WindowsProcessUtil.PrintWhoIsLocking(oldFileName, Log);
				WindowsProcessUtil.PrintWhoIsLocking(newFileName, Log);
				throw;
			}

			newChunk = await TFChunk.TFChunk.FromCompletedFile(newFileName, verifyHash, _config.Unbuffered,
				_tracker, _transformManager.GetFactoryForExistingChunk,
				_config.ReduceFileCachePressure, token: token);
		}

		bool triggerCaching;
		await _chunksLocker.AcquireAsync(token);
		try {
			if (!ReplaceChunksWith(newChunk, "Old")) {
				Log.Information("Chunk {chunk} will be not switched, marking for remove...", newChunk);
				newChunk.MarkForDeletion();
			} else
				OnChunkSwitched?.Invoke(newChunk.ChunkInfo);

			if (removeChunksWithGreaterNumbers) {
				var oldChunksCount = _chunksCount;
				_chunksCount = newChunk.ChunkHeader.ChunkEndNumber + 1;
				RemoveChunks(chunkHeader.ChunkEndNumber + 1, oldChunksCount - 1, "Excessive");
				if (_chunks[_chunksCount] is not null)
					throw new Exception(string.Format("Excessive chunk #{0} found after raw replication switch.",
						_chunksCount));
			}
			triggerCaching = _cachingEnabled;
		} finally {
			_chunksLocker.Release();
		}

		// trigger caching out of lock to avoid lock contention
		if (triggerCaching)
			TriggerBackgroundCaching();
		return newChunk;
	}

	private bool ReplaceChunksWith(TFChunk.TFChunk newChunk, string chunkExplanation) {
		Debug.Assert(_chunksLocker.IsLockHeld);

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
		Debug.Assert(_chunksLocker.IsLockHeld);

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

	private void TriggerBackgroundCaching() {
		Interlocked.Increment(ref _backgroundPassesRemaining);
		if (Interlocked.CompareExchange(ref _backgroundRunning, 1, 0) == 0)
			ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
	}

	public bool TryGetChunkFor(long logPosition, out TFChunk.TFChunk chunk) {
		try {
			chunk = GetChunkFor(logPosition);
			return true;
		} catch {
			chunk = null;
			return false;
		}
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

		if (_chunks[chunkNum] is not { } chunk)
			throw new Exception(string.Format("Requested chunk #{0}, which is not present in TFChunkManager.",
				chunkNum));

		return chunk;
	}

	public async ValueTask<bool> TryClose(CancellationToken token) {
		var allChunksClosed = true;

		await _chunksLocker.AcquireAsync(token);
		try {
			for (int i = 0; i < _chunksCount; ++i) {
				if (_chunks[i] != null)
					allChunksClosed &= _chunks[i].TryClose();
			}
		} finally {
			_chunksLocker.Release();
		}

		return allChunksClosed;
	}
}
