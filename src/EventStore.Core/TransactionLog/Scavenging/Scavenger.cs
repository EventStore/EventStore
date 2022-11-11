using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class Scavenger {
		protected static readonly ILogger Log = LogManager.GetLoggerFor<Scavenger>();
	}

	public class Scavenger<TStreamId> : Scavenger, IScavenger {
		private readonly Action _checkPreconditions;
		private readonly IScavengeState<TStreamId> _state;
		private readonly IAccumulator<TStreamId> _accumulator;
		private readonly ICalculator<TStreamId> _calculator;
		private readonly IChunkExecutor<TStreamId> _chunkExecutor;
		private readonly IChunkMerger _chunkMerger;
		private readonly IIndexExecutor<TStreamId> _indexExecutor;
		private readonly ICleaner _cleaner;
		private readonly IScavengePointSource _scavengePointSource;
		private readonly ITFChunkScavengerLog _scavengerLogger;
		private readonly int _thresholdForNewScavenge;
		private readonly bool _syncOnly;
		private readonly Func<string> _getThrottleStats;

		private readonly Dictionary<string, TimeSpan> _recordedTimes =
			new Dictionary<string, TimeSpan>();

		public Scavenger(
			Action checkPreconditions,
			IScavengeState<TStreamId> state,
			IAccumulator<TStreamId> accumulator,
			ICalculator<TStreamId> calculator,
			IChunkExecutor<TStreamId> chunkExecutor,
			IChunkMerger chunkMerger,
			IIndexExecutor<TStreamId> indexExecutor,
			ICleaner cleaner,
			IScavengePointSource scavengePointSource,
			ITFChunkScavengerLog scavengerLogger,
			int thresholdForNewScavenge,
			bool syncOnly,
			Func<string> getThrottleStats) {

			_checkPreconditions = checkPreconditions;
			_state = state;
			_accumulator = accumulator;
			_calculator = calculator;
			_chunkExecutor = chunkExecutor;
			_chunkMerger = chunkMerger;
			_indexExecutor = indexExecutor;
			_cleaner = cleaner;
			_scavengePointSource = scavengePointSource;
			_scavengerLogger = scavengerLogger;
			_thresholdForNewScavenge = thresholdForNewScavenge;
			_syncOnly = syncOnly;
			_getThrottleStats = getThrottleStats;
		}

		public string ScavengeId => _scavengerLogger.ScavengeId;

		public void Dispose() {
			_state.Dispose();
		}

		// following old scavenging design the returned task must complete successfully
		public async Task ScavengeAsync(CancellationToken cancellationToken) {
			await Task.Yield(); // get off the main queue

			_recordedTimes.Clear();
			var stopwatch = Stopwatch.StartNew();
			var result = ScavengeResult.Success;
			string error = null;
			try {
				Log.Debug("SCAVENGING: initializing scavenge state.");
				_state.Init();
				_state.LogStats();
				Log.Debug("SCAVENGING: started scavenging DB.");
				LogCollisions();

				_scavengerLogger.ScavengeStarted();

				await RunInternal(_scavengerLogger, stopwatch, cancellationToken);

				Log.Trace(
					"SCAVENGING: total time taken: {elapsed}, total space saved: {spaceSaved}.",
					stopwatch.Elapsed, _scavengerLogger.SpaceSaved);

			} catch (OperationCanceledException) {
				Log.Info("SCAVENGING: Scavenge cancelled.");
				result = ScavengeResult.Stopped;
			} catch (Exception exc) {
				result = ScavengeResult.Failed;
				Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
				error = string.Format("Error while scavenging DB: {0}.", exc.Message);
			} finally {
				try {
					_scavengerLogger.ScavengeCompleted(result, error, stopwatch.Elapsed);
					LogCollisions();
					LogTimes();
				} catch (Exception ex) {
					Log.ErrorException(
						ex,
						"SCAVENGING: Error whilst recording scavenge completed. " +
						"Scavenge result: {result}, Elapsed: {elapsed}, Original error: {e}",
						result, stopwatch.Elapsed, error);
				}
			}
		}

		private void LogCollisions() {
			var collisions = _state.AllCollisions().ToArray();
			Log.Trace("SCAVENGING: {count} KNOWN COLLISIONS", collisions.Length);

			foreach (var collision in collisions) {
				Log.Trace("SCAVENGING: KNOWN COLLISION: \"{collision}\"", collision);
			}
		}

		private async Task RunInternal(
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			_checkPreconditions();

			// each component can be started with either
			//  (i) a checkpoint that it wrote previously (it will continue from there)
			//  (ii) fresh from a given scavengepoint
			//
			// so if we have a checkpoint from a component, start the scavenge by passing the checkpoint
			// into that component and then starting each subsequent component fresh for that
			// scavengepoint.
			//
			// otherwise, start the whole scavenge fresh from whichever scavengepoint is applicable.
			if (!_state.TryGetCheckpoint(out var checkpoint)) {
				// there is no checkpoint, so this is the first scavenge of this scavenge state
				// (not necessarily the first scavenge of this database, old scavenged may have been run
				// or new scavenges run and the scavenge state deleted)
				Log.Trace("SCAVENGING: Starting a new scavenge with no checkpoint");
				await StartNewAsync(
					prevScavengePoint: null,
					scavengerLogger,
					stopwatch,
					cancellationToken);

			} else if (checkpoint is ScavengeCheckpoint.Done done) {
				// start of a subsequent scavenge.
				Log.Trace("SCAVENGING: Starting a new scavenge after checkpoint {checkpoint}", checkpoint);
				await StartNewAsync(
					prevScavengePoint: done.ScavengePoint,
					scavengerLogger,
					stopwatch,
					cancellationToken);

			} else {
				// the other cases are continuing an incomplete scavenge
				Log.Trace("SCAVENGING: Continuing a scavenge from {checkpoint}", checkpoint);

				if (checkpoint is ScavengeCheckpoint.Accumulating accumulating) {
					Time(stopwatch, "Accumulation", () =>
						_accumulator.Accumulate(accumulating, _state, cancellationToken));
					AfterAccumulation(
						accumulating.ScavengePoint, scavengerLogger, stopwatch, cancellationToken);

				} else if (checkpoint is ScavengeCheckpoint.Calculating<TStreamId> calculating) {
					Time(stopwatch, "Calculation", () =>
						_calculator.Calculate(calculating, _state, cancellationToken));
					AfterCalculation(
						calculating.ScavengePoint, scavengerLogger, stopwatch, cancellationToken);

				} else if (checkpoint is ScavengeCheckpoint.ExecutingChunks executingChunks) {
					Time(stopwatch, "Chunk execution", () =>
						_chunkExecutor.Execute(executingChunks, _state, scavengerLogger, cancellationToken));
					AfterChunkExecution(
						executingChunks.ScavengePoint, scavengerLogger, stopwatch, cancellationToken);

				} else if (checkpoint is ScavengeCheckpoint.MergingChunks mergingChunks) {
					Time(stopwatch, "Chunk merging", () =>
						_chunkMerger.MergeChunks(mergingChunks, _state, scavengerLogger, cancellationToken));
					AfterChunkMerging(
						mergingChunks.ScavengePoint, scavengerLogger, stopwatch, cancellationToken);

				} else if (checkpoint is ScavengeCheckpoint.ExecutingIndex executingIndex) {
					Time(stopwatch, "Index execution", () =>
						_indexExecutor.Execute(executingIndex, _state, scavengerLogger, cancellationToken));
					AfterIndexExecution(
						executingIndex.ScavengePoint, stopwatch, cancellationToken);

				} else if (checkpoint is ScavengeCheckpoint.Cleaning cleaning) {
					Time(stopwatch, "Cleaning", () =>
						_cleaner.Clean(cleaning, _state, cancellationToken));
					AfterCleaning(cleaning.ScavengePoint);

				} else {
					throw new Exception($"Unexpected checkpoint: {checkpoint}");
				}
			}
		}

		private void Time(Stopwatch stopwatch, string name, Action f) {
			var start = stopwatch.Elapsed;
			f();
			var elapsed = stopwatch.Elapsed - start;
			_state.LogStats();
			Log.Trace($"SCAVENGING: {_getThrottleStats()}");
			Log.Trace("SCAVENGING: " + name + " took {elapsed}", elapsed);
			Log.Trace("");
			_recordedTimes[name] = elapsed;
		}

		private void LogTimes() {
			foreach (var key in _recordedTimes.Keys.OrderBy(x => x)) {
				Log.Trace("SCAVENGING: {name} took {elapsed}", key, _recordedTimes[key]);
			}
		}

		private async Task StartNewAsync(
			ScavengePoint prevScavengePoint,
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			// prevScavengePoint is the previous one that was completed
			// latestScavengePoint is the latest one in the database
			// nextScavengePoint is the one we are about to scavenge up to

			ScavengePoint nextScavengePoint;
			var latestScavengePoint = await _scavengePointSource
				.GetLatestScavengePointOrDefaultAsync(cancellationToken);
			if (latestScavengePoint == null) {
				if (_syncOnly) {
					Log.Trace("SCAVENGING: No existing scavenge point to sync with, nothing to do.");
					return;
				} else {
					Log.Trace("SCAVENGING: creating the first scavenge point.");
					// no latest scavenge point, create the first one
					nextScavengePoint = await _scavengePointSource
						.AddScavengePointAsync(
							ExpectedVersion.NoStream,
							threshold: _thresholdForNewScavenge,
							cancellationToken);
				}
			} else {
				// got the latest scavenge point
				if (prevScavengePoint == null ||
					prevScavengePoint.EventNumber < latestScavengePoint.EventNumber) {
					// the latest scavengepoint is suitable
					Log.Trace(
						"SCAVENGING: using existing scavenge point {scavengePointNumber}",
						latestScavengePoint.EventNumber);
					nextScavengePoint = latestScavengePoint;
				} else {
					if (_syncOnly) {
						Log.Trace("SCAVENGING: No existing scavenge point to sync with, nothing to do.");
						return;
					} else {
						// the latest scavengepoint is the prev scavenge point, so create a new one
						var expectedVersion = prevScavengePoint.EventNumber;
						Log.Trace(
							"SCAVENGING: creating the next scavenge point: {scavengePointNumber}",
							expectedVersion + 1);

						nextScavengePoint = await _scavengePointSource
							.AddScavengePointAsync(
								expectedVersion,
								threshold: _thresholdForNewScavenge,
								cancellationToken);
					}
				}
			}

			// we now have a nextScavengePoint.
			Time(stopwatch, "Accumulation", () =>
				_accumulator.Accumulate(prevScavengePoint, nextScavengePoint, _state, cancellationToken));
			AfterAccumulation(nextScavengePoint, scavengerLogger, stopwatch, cancellationToken);
		}

		private void AfterAccumulation(
			ScavengePoint scavengepoint,
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			LogCollisions();
			Time(stopwatch, "Calculation", () =>
				_calculator.Calculate(scavengepoint, _state, cancellationToken));
			AfterCalculation(scavengepoint, scavengerLogger, stopwatch, cancellationToken);
		}

		private void AfterCalculation(
			ScavengePoint scavengePoint,
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			Time(stopwatch, "Chunk execution", () =>
				_chunkExecutor.Execute(scavengePoint, _state, scavengerLogger, cancellationToken));
			AfterChunkExecution(scavengePoint, scavengerLogger, stopwatch, cancellationToken);
		}

		private void AfterChunkExecution(
			ScavengePoint scavengePoint,
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			Time(stopwatch, "Chunk merging", () =>
				_chunkMerger.MergeChunks(scavengePoint, _state, scavengerLogger, cancellationToken));
			AfterChunkMerging(scavengePoint, scavengerLogger, stopwatch, cancellationToken);
		}

		private void AfterChunkMerging(
			ScavengePoint scavengePoint,
			ITFChunkScavengerLog scavengerLogger,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			Time(stopwatch, "Index execution", () =>
				_indexExecutor.Execute(scavengePoint, _state, scavengerLogger, cancellationToken));
			AfterIndexExecution(scavengePoint, stopwatch, cancellationToken);
		}

		private void AfterIndexExecution(
			ScavengePoint scavengePoint,
			Stopwatch stopwatch,
			CancellationToken cancellationToken) {

			Time(stopwatch, "Cleaning", () =>
				_cleaner.Clean(scavengePoint, _state, cancellationToken));
			AfterCleaning(scavengePoint);
		}

		private void AfterCleaning(ScavengePoint scavengePoint) {
			_state.SetCheckpoint(new ScavengeCheckpoint.Done(scavengePoint));
		}
	}
}
