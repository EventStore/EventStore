using System;
using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public interface IEmittedStreamsWriter {
	void WriteEvents(string streamId, long expectedVersion, Event[] events, ClaimsPrincipal writeAs,
		Action<ClientMessage.WriteEventsCompleted> complete);
}
