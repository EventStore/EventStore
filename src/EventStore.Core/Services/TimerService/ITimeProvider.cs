using System;

namespace EventStore.Core.Services.TimerService {
	public interface ITimeProvider {
		DateTime UtcNow { get; }
		DateTime LocalTime { get; }
	}
}
