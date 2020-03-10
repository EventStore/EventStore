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
		/// A Task invoked and await when a checkpoint is reached.
		/// </summary>
		public Func<Position, CancellationToken, Task> CheckpointReached { get; }

		/// <summary>
		///
		/// </summary>
		/// <param name="filter">The <see cref="IEventFilter"/> to apply.</param>
		/// <param name="checkpointReached">
		/// A Task invoked and await when a checkpoint is reached.
		/// </param>
		/// <exception cref="ArgumentNullException"></exception>
		public FilterOptions(IEventFilter filter, Func<Position, CancellationToken, Task> checkpointReached = default) {
			if (filter == null) {
				throw new ArgumentNullException(nameof(filter));
			}

			Filter = filter;
			CheckpointReached = checkpointReached ?? ((_, ct) => Task.CompletedTask);
		}
	}
}
