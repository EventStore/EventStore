using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Scavenging {
	public static class ParallelLoop {
		// passes each item in `source` to `process`, according to the `degreeOfParallelism.
		// processing is never done on the calling thread
		// emitCheckpoint is called on the calling thread with the latest checkpoint that is complete
		// items are queried for checkpoint
		//   - getCheckpointInclusive returns the checkpoint that can be emitted when all items up to
		//     and including this one have been completed.
		//   - getCheckpointExclusive returns the checkpoint that can be emitted when all items up to
		//     but excluding this one have been completed.
		public static void RunWithTrailingCheckpoint<T>(
			IEnumerable<T> source,
			int degreeOfParallelism,
			Func<T, int> getCheckpointInclusive,
			Func<T, int?> getCheckpointExclusive,
			Action<int, T> process,
			Action<int> emitCheckpoint,
			Action onConsiderEmit = null) {

			Ensure.Positive(degreeOfParallelism, nameof(degreeOfParallelism));

			// in each slot we store the checkpoint that can be emitted when every item before the one
			// being processed in that slot is completed.
			// null means it cannot emit a checkpoint at all.
			// int.max means it places no limit on the checkpoint.
			var checkpoints = new int?[degreeOfParallelism];
			var tasksInProgress = new Task[degreeOfParallelism];

			for (var i = 0; i < degreeOfParallelism; i++) {
				tasksInProgress[i] = DelayForever();
				checkpoints[i] = int.MaxValue;
			}

			// checkpoint of the last element, to emit at the end.
			var endCheckpoint = default(int?);
			var lastEmittedCheckpoint = default(int?);

			void PrepareProcessingItem(int slot, T item) {
				checkpoints[slot] = getCheckpointExclusive(item);
			}

			void StartProcessingItem(int slot, T item) {
				tasksInProgress[slot] = Task.Factory.StartNew(
					() => process(slot, item),
					TaskCreationOptions.PreferFairness);
			}

			int WaitForSlot() {
				var slot = Task.WaitAny(tasksInProgress);
				var task = tasksInProgress[slot];
				if (task.Status == TaskStatus.Faulted)
					throw task.Exception.InnerException;
				return slot;
			}

			void EmitCheckpoint() {
				onConsiderEmit?.Invoke();

				// find the the minimum checkpoint, we can emit it.
				if (checkpoints.Any(x => x == null))
					return;

				var checkpointToEmit = checkpoints.Min().Value;

				if (lastEmittedCheckpoint != null && checkpointToEmit <= lastEmittedCheckpoint)
					return;

				if (checkpointToEmit == int.MaxValue)
					checkpointToEmit = endCheckpoint.Value;

				emitCheckpoint(checkpointToEmit);
				lastEmittedCheckpoint = checkpointToEmit;
			}

			// process the source
			var slotsInUse = 0;
			foreach (var item in source) {
				endCheckpoint = getCheckpointInclusive(item);
				if (slotsInUse < tasksInProgress.Length) {
					PrepareProcessingItem(slotsInUse, item);
					StartProcessingItem(slotsInUse++, item);
				} else {
					var slot = WaitForSlot();
					PrepareProcessingItem(slot, item);
					EmitCheckpoint();
					StartProcessingItem(slot, item);
				}
			}

			// drain the tasks
			while (slotsInUse > 0) {
				var slot = WaitForSlot();
				checkpoints[slot] = int.MaxValue;
				EmitCheckpoint();
				tasksInProgress[slot] = DelayForever();
				slotsInUse--;
			}
		}

		private static async Task<int> DelayForever() {
			await Task.Delay(Timeout.InfiniteTimeSpan);
			throw new Exception("never getting here");
		}
	}
}
