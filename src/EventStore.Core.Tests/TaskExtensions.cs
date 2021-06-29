using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace EventStore.Core.Tests {
	public static class TaskExtensions {
		public static Task WithTimeout(this Task task, TimeSpan timeout, [CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
			=> task.WithTimeout(Convert.ToInt32(timeout.TotalMilliseconds), memberName, sourceFilePath, sourceLineNumber);

		public static async Task WithTimeout(this Task task, int timeoutMs = 10000,
			[CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", 
			[CallerLineNumber] int sourceLineNumber = 0) {
			
			if (Debugger.IsAttached) {
				timeoutMs = -1;
			}

			if (await Task.WhenAny(task, Task.Delay(timeoutMs)) != task)
				throw new TimeoutException($"Timed out waiting for task at: {memberName} {sourceFilePath}:{sourceLineNumber}");
			await task;
		}

		public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, [CallerMemberName] string memberName = "",
			[CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
			=> task.WithTimeout(Convert.ToInt32(timeout.TotalMilliseconds), memberName, sourceFilePath, sourceLineNumber);

		public static async Task<T> WithTimeout<T>(this Task<T> task, int timeoutMs = 10000,
			[CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "",
			[CallerLineNumber] int sourceLineNumber = 0) {
			
			if (Debugger.IsAttached) {
				timeoutMs = -1;
			}

			if (await Task.WhenAny(task, Task.Delay(timeoutMs)) == task)
				return await task;
			
			throw new TimeoutException($"Timed out waiting for task at: {memberName} {sourceFilePath}:{sourceLineNumber}");
		}
	}
}
