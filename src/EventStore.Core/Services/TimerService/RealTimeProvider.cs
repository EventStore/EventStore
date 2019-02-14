using System;

namespace EventStore.Core.Services.TimerService {
	public class RealTimeProvider : ITimeProvider {
		public DateTime Now {
			get { return DateTime.UtcNow; }
		}
	}
}
