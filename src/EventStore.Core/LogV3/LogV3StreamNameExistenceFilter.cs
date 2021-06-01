using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.DataStructures.ProbabilisticFilter.MemoryMappedFileBloomFilter;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog.Checkpoint;
using Serilog;
using Value = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class NameExistenceFilter {
		protected static readonly ILogger Log = Serilog.Log.ForContext<NameExistenceFilter>();
	}

	public class LogV3StreamNameExistenceFilter :
		NameExistenceFilter,
		INameExistenceFilter<Value> {
		private readonly string _filterName;
		private readonly Encoding _utf8NoBom = new UTF8Encoding(false, true);
		private readonly MemoryMappedFileBloomFilter<string> _mmfBloomFilter;
		private readonly MemoryMappedFileCheckpoint _checkpoint;
		private readonly Debouncer _checkpointer;
		private readonly Value _firstValue;
		private readonly Value _valueInterval;
		private readonly CancellationTokenSource _cancellationTokenSource;

		public LogV3StreamNameExistenceFilter(
			string directory,
			string filterName,
			long size,
			Value firstValue,
			Value valueInterval,
			TimeSpan checkpointInterval) {
			_filterName = filterName;
			_firstValue = firstValue;
			_valueInterval = valueInterval;

			if (!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}

			var bloomFilterFilePath = $"{directory}/{_filterName}.dat";
			var checkpointFilePath =  $"{directory}/{_filterName}.chk";

			byte[] Serializer(string s) => _utf8NoBom.GetBytes(s);
			try {
				_mmfBloomFilter = new MemoryMappedFileBloomFilter<string>
					(bloomFilterFilePath, size, Serializer);
			} catch (CorruptedFileException exc) {
				Log.Error(exc, "{filterName} is corrupted. Rebuilding...", _filterName);
				File.Delete(bloomFilterFilePath);
				File.Delete(checkpointFilePath);
				_mmfBloomFilter = new MemoryMappedFileBloomFilter<string>
					(bloomFilterFilePath, size, Serializer);
			}

			Log.Information("{filterName} has successfully loaded.", _filterName);
			Log.Debug("Optimal maximum number of items that fits in the {filterName} with a configured size of " +
			                "{size:N0} MB is approximately equal to: {n:N0} with false positive probability: {p:N2}",
				_filterName,
				size / 1000 / 1000,
				_mmfBloomFilter.OptimalMaxItems,
				_mmfBloomFilter.FalsePositiveProbability);

			_checkpoint = new MemoryMappedFileCheckpoint(checkpointFilePath, _filterName, true);

			_cancellationTokenSource = new();
			_checkpointer = new Debouncer(
				checkpointInterval,
				token => {
					try {
						_mmfBloomFilter.Flush();
						_checkpoint.Flush();
						Log.Debug("{filterName} took checkpoint at position: {position}", _filterName, _checkpoint.Read());
					} catch (Exception ex) {
						Log.Error(ex, "{filterName} could not take checkpoint at position: {position}", _filterName, _checkpoint.Read());
					}
					return Task.CompletedTask;
				}, _cancellationTokenSource.Token);

		}

		public void InitializeWithExisting(INameLookup<Value> source) {
			if (!source.TryGetLastValue(out var lastValue)) return;
			var checkpoint = _checkpoint.Read();
			for (var value = lastValue; value >= _firstValue && value > checkpoint; value -= _valueInterval) {
				var name = source.LookupName(value);
				Add(name, value);
			}
			_checkpoint.Write(lastValue);
			_checkpointer.Trigger();
		}

		public void Add(string name, Value value) {
			_mmfBloomFilter.Add(name);
			Log.Verbose("{filterName} added new entry: {name}", _filterName, name);
			_checkpoint.Write(value);
			_checkpointer.Trigger();
		}

		public bool? Exists(string name) => !_mmfBloomFilter.MayExist(name) ? false : null;

		public void Dispose() {
			_cancellationTokenSource?.Cancel();
			_mmfBloomFilter?.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
