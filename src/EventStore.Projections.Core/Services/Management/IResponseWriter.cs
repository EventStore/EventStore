using System;

namespace EventStore.Projections.Core.Services.Management {
	public interface IResponseWriter {
		void PublishCommand(string command, object body);
		void Reset();
	}
}
