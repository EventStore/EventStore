using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.TestClient {
	/// <summary>
	/// This context is passed to the instances of <see cref="ICmdProcessor"/>
	/// when they are executed. It can also be used for async syncrhonization
	/// </summary>
	public class CommandProcessorContext {
		public int ExitCode;
		public Exception Error;
		public string Reason;

		/// <summary>
		/// Current logger of the test client
		/// </summary>
		public readonly ILogger Log;

		public readonly Client Client;

		private readonly ManualResetEventSlim _doneEvent;
		private int _completed;

		public CommandProcessorContext(Client client, ILogger log, ManualResetEventSlim doneEvent) {
			Client = client;
			Log = log;
			_doneEvent = doneEvent;
		}

		public void Completed(int exitCode = (int)Common.Utils.ExitCode.Success, Exception error = null,
			string reason = null) {
			if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0) {
				ExitCode = exitCode;

				Error = error;
				Reason = reason;

				_doneEvent.Set();
			}
		}

		public void Fail(Exception exc = null, string reason = null) {
			Completed((int)Common.Utils.ExitCode.Error, exc, reason);
		}

		public void Success() {
			Completed();
		}

		public void IsAsync() {
			_doneEvent.Reset();
		}

		public void WaitForCompletion() {
			if (Client.Options.Timeout < 0)
				_doneEvent.Wait();
			else {
				if (!_doneEvent.Wait(Client.Options.Timeout * 1000))
					throw new TimeoutException("Command didn't finished within timeout.");
			}
		}

		public TimeSpan Time(Action action) {
			var sw = Stopwatch.StartNew();
			action();
			sw.Stop();
			return sw.Elapsed;
		}
	}
}
