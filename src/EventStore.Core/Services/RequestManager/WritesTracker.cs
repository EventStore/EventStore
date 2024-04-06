using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Storage;

namespace EventStore.Core.Services.RequestManager;

public class WritesTracker : IWritesTracker {
	private readonly CounterSubMetric _writtenBytes;
	private readonly CounterSubMetric _writtenEvents;
	private readonly CounterSubMetric _writes;

	public WritesTracker(
		CounterSubMetric writtenBytes,
		CounterSubMetric writtenEvents,
		CounterSubMetric writes) {

		_writtenBytes = writtenBytes;
		_writtenEvents = writtenEvents;
		_writes = writes;
	}

	public void OnWrite(ClientMessage.WriteEvents msg) {
		_writes.Add(1);
		_writtenEvents.Add(msg.Events.Length);

		var count = msg.Events.Sum(@event => @event.Data.Length + @event.Metadata.Length);
		_writtenBytes.Add(count);
	}

	public class NoOp : IWritesTracker {
		public void OnWrite(ClientMessage.WriteEvents msg) {
		}
	}
}
