#nullable enable
using System;

namespace EventStore.Core.Protocol.Infrastructure.Serialization;

public interface ISerializer {
	EventToWrite Serialize(Message evt);
	bool TryDeserialize(Event evt, out Message? message, out Exception? exception);
}
