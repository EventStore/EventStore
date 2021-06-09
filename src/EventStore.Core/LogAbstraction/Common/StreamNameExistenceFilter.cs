using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog.Checkpoint;
using Serilog;

namespace EventStore.Core.LogAbstraction.Common {
	public class StreamNameExistenceFilter :
		INameExistenceFilter {
		private readonly string _filterName;
		private readonly MemoryMappedFileStreamBloomFilter _mmfStreamBloomFilter;
		private readonly ICheckpoint _checkpoint;
		private readonly Debouncer _checkpointer;
		private readonly CancellationTokenSource _cancellationTokenSource;

		private bool _rebuilding;
		private long _addedSinceLoad;

		protected static readonly ILogger Log = Serilog.Log.ForContext<StreamNameExistenceFilter>();

		public long CurrentCheckpoint => _checkpoint.Read();

		public StreamNameExistenceFilter(
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
				token => {
					try {
						_mmfStreamBloomFilter.Flush();
						_checkpoint.Flush();
						Log.Debug("{filterName} took checkpoint at position: {position}", _filterName, _checkpoint.Read());
					} catch (Exception ex) {
						Log.Error(ex, "{filterName} could not take checkpoint at position: {position}", _filterName, _checkpoint.Read());
					}
					return Task.CompletedTask;
				}, _cancellationTokenSource.Token);

		}

		public void Initialize(INameExistenceFilterInitializer source) {
			_rebuilding = true;
			Log.Debug("{filterName} rebuilding started from checkpoint: {checkpoint} (0x{checkpoint:X}).",
				_filterName, CurrentCheckpoint, CurrentCheckpoint);
			var startTime = DateTime.UtcNow;
			source.Initialize(this);
			_mmfStreamBloomFilter.Flush();
			_checkpoint.Flush();

			Log.Debug("{filterName} rebuilding done: total processed {processed} records, time elapsed: {elapsed}.",
				_filterName, _addedSinceLoad, DateTime.UtcNow - startTime);
			_rebuilding = false;
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
			if (_rebuilding && _addedSinceLoad % 500000 == 0) {
				Log.Debug("{_filterName} rebuilding: processed {processed} records.", _filterName, _addedSinceLoad);
			}

			//qq might not want these while rebuilding, or might want to checkpoint after rebuilding or
			_checkpoint.Write(checkpoint);
			_checkpointer.Trigger();
		}

		public bool MightContain(string name) {
			return _mmfStreamBloomFilter.MightContain(name);
		}

		public void Dispose() {
			_cancellationTokenSource?.Cancel();
			_mmfStreamBloomFilter?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
