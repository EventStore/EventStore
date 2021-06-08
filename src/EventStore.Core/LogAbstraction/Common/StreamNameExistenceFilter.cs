using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using EventStore.Core.TransactionLog.Checkpoint;
using Serilog;

namespace EventStore.Core.LogAbstraction.Common {
	public class StreamNameExistenceFilter :
		INameExistenceFilter {
		private readonly string _filterName;
		private readonly MemoryMappedFileStringBloomFilter _mmfStringBloomFilter;
		private readonly MemoryMappedFileCheckpoint _checkpoint;
		private readonly Debouncer _checkpointer;
		private readonly CancellationTokenSource _cancellationTokenSource;

		private long _addedSinceLoad;

		protected static readonly ILogger Log = Serilog.Log.ForContext<StreamNameExistenceFilter>();

		public long CurrentCheckpoint => _checkpoint.Read();

		public StreamNameExistenceFilter(
			string directory,
			string filterName,
			long size,
			int initialReaderCount,
			int maxReaderCount,
			TimeSpan checkpointInterval) {
			_filterName = filterName;

			if (!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}

			var bloomFilterFilePath = $"{directory}/{_filterName}.dat";
			var checkpointFilePath =  $"{directory}/{_filterName}.chk";

			//qq 
			try {
				_mmfStringBloomFilter = new MemoryMappedFileStringBloomFilter(bloomFilterFilePath, size, initialReaderCount, maxReaderCount);
			} catch (CorruptedFileException exc) {
				Log.Error(exc, "{filterName} is corrupted. Rebuilding...", _filterName);
				File.Delete(bloomFilterFilePath);
				File.Delete(checkpointFilePath);
				_mmfStringBloomFilter = new MemoryMappedFileStringBloomFilter(bloomFilterFilePath, size, initialReaderCount, maxReaderCount);
			}

			Log.Information("{filterName} has successfully loaded.", _filterName);

			const double p = MemoryMappedFileBloomFilter.RecommendedFalsePositiveProbability;
			Log.Debug("Optimal number of items for a {filterName} with a configured size of " +
			                "{size:N0} MB is approximately equal to: {n:N0} with false positive probability: {p:N2}",
				_filterName,
				size / 1000 / 1000,
				_mmfStringBloomFilter.CalculateOptimalNumItems(p),
				p);

			_checkpoint = new MemoryMappedFileCheckpoint(checkpointFilePath, _filterName, true);

			_cancellationTokenSource = new();
			_checkpointer = new Debouncer(
				checkpointInterval,
				token => {
					try {
						_mmfStringBloomFilter.Flush();
						_checkpoint.Flush();
						Log.Debug("{filterName} took checkpoint at position: {position}", _filterName, _checkpoint.Read());
					} catch (Exception ex) {
						Log.Error(ex, "{filterName} could not take checkpoint at position: {position}", _filterName, _checkpoint.Read());
					}
					return Task.CompletedTask;
				}, _cancellationTokenSource.Token);

		}

		public void Initialize(INameEnumerator source) {
			var startTime = DateTime.UtcNow;
			source.Initialize(this);
			Log.Debug("{filterName} rebuilding done: total processed {processed} records, time elapsed: {elapsed}.",
				_filterName, _addedSinceLoad, DateTime.UtcNow - startTime);
		}

		public void Add(string name, long checkpoint) {
			_mmfStringBloomFilter.Add(name);
			Log.Verbose("{filterName} added new entry: {name}", _filterName, name);
			OnAdded(checkpoint);
		}

		public void Add(ulong hash, long checkpoint) {
			_mmfStringBloomFilter.Add(hash);
			Log.Verbose("{filterName} added new entry from hash: {name}", _filterName, hash);
			OnAdded(checkpoint);
		}

		private void OnAdded(long checkpoint) {
			_addedSinceLoad++;
			_checkpoint.Write(checkpoint);
			_checkpointer.Trigger();
		}

		public bool MightExist(string name) => _mmfStringBloomFilter.MayExist(name);

		public void Dispose() {
			_cancellationTokenSource?.Cancel();
			_mmfStringBloomFilter?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
