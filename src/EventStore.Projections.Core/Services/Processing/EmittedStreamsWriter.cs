using System;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class EmittedStreamsWriter : IEmittedStreamsWriter {
		private IODispatcher _ioDispatcher;

		public EmittedStreamsWriter(IODispatcher ioDispatcher) {
			_ioDispatcher = ioDispatcher;
		}

		public void WriteEvents(string streamId, long expectedVersion, Event[] events, IPrincipal writeAs,
			Action<ClientMessage.WriteEventsCompleted> complete) {
			_ioDispatcher.WriteEvents(streamId, expectedVersion, events, writeAs, complete);
		}
	}
}
