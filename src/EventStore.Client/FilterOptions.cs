using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client {
	public class FilterOptions {
		/// <summary>
		/// The <see cref="IEventFilter"/> to apply.
		/// </summary>
		public IEventFilter Filter { get; }

		/// <summary>
		/// Sets how often the checkpointReached callback is called.
		/// </summary>
		public uint CheckpointInterval { get; }

		/// <summary>
		/// A Task invoked and await when a checkpoint is reached.
		/// Set the checkpointInterval to define how often this method is called.
		/// </summary>
		public Func<StreamSubscription, Position, CancellationToken, Task> CheckpointReached { get; }

		/// <summary>
		///
		/// </summary>
		/// <param name="filter">The <see cref="IEventFilter"/> to apply.</param>
		/// <param name="checkpointInterval">Sets how often the checkpointReached callback is called.</param>
		/// <param name="checkpointReached">
		/// A Task invoked and await when a checkpoint is reached.
		/// Set the checkpointInterval to define how often this method is called.
		/// </param>
		/// <exception cref="ArgumentNullException"></exception>
		public FilterOptions(IEventFilter filter, uint checkpointInterval = 1,
			Func<StreamSubscription, Position, CancellationToken, Task> checkpointReached = default) {
			if (filter == null) {
				throw new ArgumentNullException(nameof(filter));
			}

			if (checkpointInterval == 0) {
				throw new ArgumentOutOfRangeException(nameof(checkpointInterval),
					checkpointInterval, $"{nameof(checkpointInterval)} must be greater than 0.");
			}

			Filter = filter;
			CheckpointInterval = checkpointInterval;
			CheckpointReached = checkpointReached ?? ((_, __, ct) => Task.CompletedTask);
		}
	}
}
