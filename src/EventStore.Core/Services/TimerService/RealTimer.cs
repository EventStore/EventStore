using System;
using System.Threading;

namespace EventStore.Core.Services.TimerService {
	public class RealTimer : ITimer {
		private Action _callback;
		private readonly Timer _timer;

		public RealTimer() {
			_timer = new Timer(InvokeCallback, null, Timeout.Infinite, Timeout.Infinite);
		}

		private void InvokeCallback(object state) {
			if (_callback != null)
				_callback();
		}

		public void FireIn(int milliseconds, Action callback) {
			_callback = callback;
			var dueTime = milliseconds == Timeout.Infinite ? Timeout.Infinite : Math.Max(0, milliseconds);
			_timer.Change(dueTime, Timeout.Infinite);
		}

		public void Dispose() {
			_timer.Dispose();
		}
	}
}
