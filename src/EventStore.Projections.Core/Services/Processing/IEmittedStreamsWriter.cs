using System;
using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IEmittedStreamsWriter {
		void WriteEvents(string streamId, int streamIdSize, long expectedVersion, Event[] events, ClaimsPrincipal writeAs,
			Action<ClientMessage.WriteEventsCompleted> complete);
	}
}
