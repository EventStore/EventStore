// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
