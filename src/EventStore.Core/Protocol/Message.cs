using System;

namespace EventStore.Core.Protocol;

public abstract record Message {
}

public record ContextMessage<T>(
	Guid MessageId,
	string EntityId,
	ulong EventNumber,
	T Payload) : Message;
