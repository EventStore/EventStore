using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;

namespace EventStore.ClientAPI.Common.Utils.Threading {
	internal static class TaskExtensions {
		public static async Task<TResult> WithTimeout<TResult>(this Task<TResult> task, TimeSpan timeout) {
			if (await Task.WhenAny(task, Task.Delay(timeout)) != task) {
				throw new OperationTimedOutException(
					string.Format("The operation did not complete within the specified time of {0}", timeout));
			}

			return await task;
		}
	}
}
