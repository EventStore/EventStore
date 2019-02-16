using System;

namespace EventStore.Core.Services.TimerService {
	public interface IScheduler : IDisposable {
		void Stop();
		void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state);
	}
}
