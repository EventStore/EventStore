using System;
using System.Threading;

namespace EventStore.Core.Tests.Bus.Helpers {
	public class TimeoutHelper {
		public static void CallWithTimeout(Action action, int timeoutMilliseconds, Action onSuccess, Action onTimeout) {
			Thread threadToKill = null;
			Action wrappedAction = () => {
				threadToKill = Thread.CurrentThread;
				action();
			};

			IAsyncResult result = wrappedAction.BeginInvoke(null, null);
			if (result.AsyncWaitHandle.WaitOne(timeoutMilliseconds)) {
				wrappedAction.EndInvoke(result);
				onSuccess();
			} else {
				threadToKill.Abort();
				onTimeout();
			}
		}
	}
}
