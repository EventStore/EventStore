using System;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class QueuedEmittedStreamsWriter : IEmittedStreamsWriter {
		private IODispatcher _ioDispatcher;
		private Guid _writeQueueId;

		public QueuedEmittedStreamsWriter(IODispatcher ioDispatcher, Guid writeQueueId) {
			_ioDispatcher = ioDispatcher;
			_writeQueueId = writeQueueId;
		}

		public void WriteEvents(string streamId, long expectedVersion, Event[] events, IPrincipal writeAs,
			Action<ClientMessage.WriteEventsCompleted> complete) {
			_ioDispatcher.QueueWriteEvents(_writeQueueId, streamId, expectedVersion, events, writeAs, complete);
		}
	}
}
