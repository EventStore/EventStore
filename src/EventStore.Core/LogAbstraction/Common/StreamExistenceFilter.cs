using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog.Checkpoint;
using Serilog;

namespace EventStore.Core.LogAbstraction.Common {
	// This connects a bloom filter datastructure to the rest of the system by
	// adding catchup and checkpointing.
	public class StreamExistenceFilter :
		INameExistenceFilter {
		private readonly string _filterName;
		private readonly MemoryMappedFileStreamBloomFilter _mmfStreamBloomFilter;
		private readonly ICheckpoint _checkpoint;
		private readonly Debouncer _checkpointer;
		private long _lastNonFlushedCheckpoint;
		private readonly CancellationTokenSource _cancellationTokenSource;

		private volatile bool _initialized;
		private bool _initializing;
		private long _addedSinceLoad;

		protected static readonly ILogger Log = Serilog.Log.ForContext<StreamExistenceFilter>();

		public long CurrentCheckpoint => _checkpoint.Read();

		public StreamExistenceFilter(
			string directory,
			ICheckpoint checkpoint,
			string filterName,
			long size,
			int initialReaderCount,
			int maxReaderCount,
			TimeSpan checkpointInterval,
			ILongHasher<string> hasher) {
			_filterName = filterName;
			_checkpoint = checkpoint;
			_lastNonFlushedCheckpoint = _checkpoint.Read();

			if (!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}

			var bloomFilterFilePath = $"{directory}/{_filterName}.dat";

			try {
				_mmfStreamBloomFilter = new MemoryMappedFileStreamBloomFilter(bloomFilterFilePath, size, initialReaderCount, maxReaderCount, hasher);
			} catch (Exception exc) when (exc is CorruptedFileException || exc is SizeMismatchException) {
				if (exc is CorruptedFileException) {
					Log.Error(exc, "{filterName} is corrupted. Rebuilding...", _filterName);
				} else if (exc is SizeMismatchException) {
					Log.Error(exc, "{filterName} does not have the expected size. Rebuilding...", _filterName);
				}

				File.Delete(bloomFilterFilePath);
				_lastNonFlushedCheckpoint = -1L;
				_checkpoint.Write(-1L);
				_checkpoint.Flush();
				_mmfStreamBloomFilter = new MemoryMappedFileStreamBloomFilter(bloomFilterFilePath, size, initialReaderCount, maxReaderCount, hasher);
			}

			Log.Information("{filterName} has successfully loaded.", _filterName);

			const double p = MemoryMappedFileBloomFilter.RecommendedFalsePositiveProbability;
			Log.Debug("Optimal number of items for a {filterName} with a configured size of " +
			                "{size:N0} MB is approximately equal to: {n:N0} with false positive probability: {p:N2}",
				_filterName,
				size / 1000 / 1000,
				_mmfStreamBloomFilter.CalculateOptimalNumItems(p),
				p);

			_cancellationTokenSource = new();
			_checkpointer = new Debouncer(
				checkpointInterval,
				_ => {
					TakeCheckpoint();
					return Task.CompletedTask;
				},
				_cancellationTokenSource.Token);
		}

		private void TakeCheckpoint() {
			try {
				var checkpoint = Interlocked.Read(ref _lastNonFlushedCheckpoint);
				var flushedCheckpoint = _checkpoint.Read();

				if (checkpoint == flushedCheckpoint) {
					// e.g. logV2 rebuilds from index only and therefore does not update the checkpoint
					// until it has finished rebuilding
					Log.Debug("{filterName} skipped taking checkpoint at position: {position}", _filterName, _checkpoint.Read());
					return;
				}

				_mmfStreamBloomFilter.Flush();
				_checkpoint.Write(checkpoint);
				_checkpoint.Flush();
				Log.Debug("{filterName} took checkpoint at position: {position}", _filterName, _checkpoint.Read());
			} catch (Exception ex) {
				Log.Error(ex, "{filterName} could not take checkpoint at position: {position}", _filterName, _checkpoint.Read());
			}
		}

		public void Initialize(INameExistenceFilterInitializer source) {
			_initializing = true;
			Log.Debug("{filterName} rebuilding started from checkpoint: {checkpoint} (0x{checkpoint:X}).",
				_filterName, CurrentCheckpoint, CurrentCheckpoint);
			var startTime = DateTime.UtcNow;
			source.Initialize(this);
			Log.Debug("{filterName} rebuilding done: total processed {processed} records, time elapsed: {elapsed}.",
				_filterName, _addedSinceLoad, DateTime.UtcNow - startTime);
			_initializing = false;
			_initialized = true;
		}

		public void Add(string name, long checkpoint) {
			_mmfStreamBloomFilter.Add(name);
			Log.Verbose("{filterName} added new entry: {name}", _filterName, name);
			OnAdded(checkpoint);
		}

		public void Add(ulong hash, long checkpoint) {
			_mmfStreamBloomFilter.Add(hash);
			Log.Verbose("{filterName} added new entry from hash: {name}", _filterName, hash);
			OnAdded(checkpoint);
		}

		private void OnAdded(long checkpoint) {
			_addedSinceLoad++;
			Interlocked.Exchange(ref _lastNonFlushedCheckpoint, checkpoint);
			_checkpointer.Trigger();

			if (_initializing && _addedSinceLoad % 500_000 == 0) {
				Log.Debug("{_filterName} rebuilding: processed {processed} new streams.", _filterName, _addedSinceLoad);
			}
		}

		public bool MightContain(string name) {
			if (!_initialized)
				throw new InvalidOperationException("Initialize the filter before querying");
			return _mmfStreamBloomFilter.MightContain(name);
		}

		public void Dispose() {
			_cancellationTokenSource?.Cancel();
			_mmfStreamBloomFilter?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
