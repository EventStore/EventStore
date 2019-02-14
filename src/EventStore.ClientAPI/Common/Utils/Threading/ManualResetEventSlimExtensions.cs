using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.ClientAPI.Common.Utils.Threading {
	internal static class ManualResetEventSlimExtensions {
		public static Task AsTask(this ManualResetEventSlim resetEvent) {
			return AsTask(resetEvent, Timeout.Infinite);
		}

		public static Task AsTask(this ManualResetEventSlim resetEvent, int timeoutMs) {
			TaskCompletionSource<object> tcs = TaskCompletionSourceFactory.Create<object>();

			RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(resetEvent.WaitHandle,
				(state, timedOut) => {
					if (timedOut) {
						tcs.SetCanceled();
					} else {
						tcs.SetResult(null);
					}
				}, null, timeoutMs, true);

			tcs.Task.ContinueWith(_ => registration.Unregister(null));

			return tcs.Task;
		}
	}
}
