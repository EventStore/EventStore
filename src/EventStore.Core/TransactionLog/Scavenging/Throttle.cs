using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Core.TransactionLog.Scavenging {
	// Call Rest from time to time and this will rest a suitable amount of time
	// to achieve an overall % time spent working approximately equal to activePercent.
	public class Throttle {
		protected static readonly ILogger Log = LogManager.GetLoggerFor<Throttle>();
		private readonly Stopwatch _stopwatch;
		private readonly double _minimumRestMs;
		private readonly double _logThresholdMs;
		private readonly double _restFactor;
		private readonly ManualResetEventSlim _mres;

		private double _totalRestingTimeMs;

		public Throttle(
			TimeSpan minimumRest,
			TimeSpan restLoggingThreshold,
			double activePercent) {

			if (activePercent <= 0 || 100 < activePercent)
				throw new ArgumentOutOfRangeException(nameof(activePercent), activePercent, null);

			_stopwatch = new Stopwatch();
			_minimumRestMs = minimumRest.TotalMilliseconds;
			_logThresholdMs = restLoggingThreshold.TotalMilliseconds;
			_restFactor = 1 - (activePercent / 100);

			_mres = new ManualResetEventSlim();
			Reset();
		}

		public void Reset() {
			_totalRestingTimeMs = 0;
			_stopwatch.Restart();
		}

		public void Rest(CancellationToken cancellationToken) {
			if (_restFactor == 0)
				return;

			var totalTimeMs = _stopwatch.Elapsed.TotalMilliseconds;

			// we want to have achieved the right proportion after the rest
			var timeToRestMs = (_restFactor * totalTimeMs - _totalRestingTimeMs) / (1 - _restFactor);

			if (timeToRestMs < _minimumRestMs)
				return;

			var isLongRest = timeToRestMs >= _logThresholdMs;
			if (isLongRest)
				Log.Trace("SCAVENGING: Resting {timeToRestMs:N0}ms", timeToRestMs);

			_totalRestingTimeMs += timeToRestMs;

			// nothing will set the mres, we just wait until we timeout or are cancelled
			_mres.Wait((int)timeToRestMs, cancellationToken);

			if (isLongRest)
				Log.Trace(PrettyPrint());
		}

		public string PrettyPrint() {
			var totalMs = _stopwatch.Elapsed.TotalMilliseconds;
			var totalActiveTimeMs = totalMs - _totalRestingTimeMs;
			var activePercent = 100 * totalActiveTimeMs / totalMs;
			return
				$"Rested {TimeSpan.FromMilliseconds(_totalRestingTimeMs)} " +
				$"out of {TimeSpan.FromMilliseconds(totalMs)}. " +
				$"{activePercent:N2} percent active.";
		}
	}
}
