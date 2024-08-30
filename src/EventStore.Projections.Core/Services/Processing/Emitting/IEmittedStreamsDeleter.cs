using System;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public interface IEmittedStreamsDeleter {
	void DeleteEmittedStreams(Action onEmittedStreamsDeleted);
}
