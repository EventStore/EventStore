using System;
using System.Collections.Concurrent;
using System.Threading;

namespace EventStore.Core.Bus {
	public class QueueStatsManager {
#if DEBUG
		private Func<long> _readWriterCheckpoint;
		private Func<long> _readWriterNonFlushedCheckpoint;
		private Func<long> _readChaserCheckpoint;
		private ConcurrentDictionary<QueueStatsCollector, bool> _queueStatsCollectors = new ConcurrentDictionary<QueueStatsCollector, bool>();
#endif
		public QueueStatsManager() {
		}

		public QueueStatsCollector CreateQueueStatsCollector(string name, string groupName = null) {
			var statsCollector = new QueueStatsCollector(name, groupName);
#if DEBUG
			_queueStatsCollectors.AddOrUpdate(statsCollector, (statsCollector) => true,
				(statsCollector, curValue) => throw new Exception("This should never happen"));
#endif
			return statsCollector;
		}

#if DEBUG
		private void WaitStop(int multiplier = 1) {
			var counter = 0;
			foreach (var kvp in _queueStatsCollectors) {
				var queueStatsCollector = kvp.Key;
				while (!queueStatsCollector.IsStopped()) {
					Console.WriteLine($"Waiting for STOP state for queue {queueStatsCollector.Name}...");
					counter++;
					if (counter > 150 * multiplier)
						throw new ApplicationException($"Infinite WaitStop() loop for queue {queueStatsCollector.Name}?");
					Thread.Sleep(100);
				}
			}
		}

		public void WaitIdle(bool waitForCheckpoints = true, bool waitForNonEmptyTf = false,
			int multiplier = 1) {
			var counter = 0;

			var singlePass = false;
			do {
				singlePass = true;
				foreach (var kvp in _queueStatsCollectors) {
					var queueStatsCollector = kvp.Key;
					while (!queueStatsCollector.IsIdle()) {
						Console.WriteLine($"Waiting for IDLE state for queue {queueStatsCollector.Name}...");
						counter++;
						singlePass = false;
						if (counter > 150 * multiplier)
							throw new ApplicationException($"Infinite WaitIdle() loop for queue: {queueStatsCollector.Name}?");
						Thread.Sleep(100);
					}
				}
			} while (!singlePass);
			//todo: the logic here around the flushed and non-flushed read on the reader checkpoint seem suspect
			//todo: this function hangs when running tests in debug
			var successes = 0;
			do {
				if ((waitForCheckpoints && AreCheckpointsDifferent()) ||
					(waitForNonEmptyTf && _readWriterCheckpoint() == 0)) {
					Console.WriteLine("Waiting for IDLE state on checkpoints...");
					counter++;
					if (counter > 150 * multiplier)
						throw new ApplicationException("Infinite WaitIdle() loop on checkpoints?");
					Thread.Sleep(100);
				} else {
					successes++;
				}
			} while (successes < 2);
		}

		private bool AreCheckpointsDifferent() {
			//todo: the logic here around the flushed and non-flushed read on the reader checkpoint seem suspect
			return _readWriterNonFlushedCheckpoint != null && _readChaserCheckpoint != null && _readWriterNonFlushedCheckpoint() != _readChaserCheckpoint();
		}

		public void InitializeCheckpoints(
			Func<long> readWriterCheckpoint,
			Func<long> readWriterNonFlushedCheckpoint,
			Func<long> readChaserCheckpoint) {
			_readChaserCheckpoint = readChaserCheckpoint;
			_readWriterNonFlushedCheckpoint = readWriterNonFlushedCheckpoint;
			_readWriterCheckpoint = readWriterCheckpoint;
		}
#endif
	}
}
