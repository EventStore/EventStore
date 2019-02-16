using System;

namespace EventStore.Projections.Core.Services.Management {
	public interface IMultiStreamMessageWriter {
		void PublishResponse(string command, Guid workerId, object body);
		void Reset();
	}
}
