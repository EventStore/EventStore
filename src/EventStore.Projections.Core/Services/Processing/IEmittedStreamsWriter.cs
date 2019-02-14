using System;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IEmittedStreamsWriter {
		void WriteEvents(string streamId, long expectedVersion, Event[] events, IPrincipal writeAs,
			Action<ClientMessage.WriteEventsCompleted> complete);
	}
}
