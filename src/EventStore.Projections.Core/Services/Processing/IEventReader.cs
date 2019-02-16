using System;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IEventReader : IDisposable {
		void Resume();
		void Pause();
		void SendNotAuthorized();
	}
}
