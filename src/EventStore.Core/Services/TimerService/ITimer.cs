using System;

namespace EventStore.Core.Services.TimerService {
	public interface ITimer : IDisposable {
		void FireIn(int milliseconds, Action callback);
	}
}
