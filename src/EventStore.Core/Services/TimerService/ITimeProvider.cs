using System;

namespace EventStore.Core.Services.TimerService {
	public interface ITimeProvider {
		DateTime Now { get; }
	}
}
