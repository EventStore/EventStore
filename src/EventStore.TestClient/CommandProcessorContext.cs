using System;
using System.Diagnostics;
using System.Threading;
using ILogger = Serilog.ILogger;
#pragma warning disable 1591

namespace EventStore.TestClient {
	/// <summary>
	/// This context is passed to the instances of <see cref="ICmdProcessor"/>
	/// when they are executed. It can also be used for async synchronization
	/// </summary>
	public class CommandProcessorContext {
		public int ExitCode;
		public Exception Error;
		public string Reason;

		/// <summary>
		/// Current logger of the test client
		/// </summary>
		public readonly ILogger Log;

		/// <summary>
		/// Stats logger for the test client
		/// </summary>
		public readonly ILogger StatsLogger;

		/// <summary>
		/// Whether stats should be CSV or Json
		/// </summary>
		public bool OutputCsv = false;

		public readonly TcpTestClient _tcpTestClient;
		public readonly GrpcTestClient _grpcTestClient;
		public readonly ClientApiTcpTestClient _clientApiTestClient;

		private readonly ManualResetEventSlim _doneEvent;
		private int _completed;
		private int _timeout;

		public CommandProcessorContext(TcpTestClient tcpTestClient, GrpcTestClient grpcTestClient, ClientApiTcpTestClient clientApiTestClient,
			int timeout, ILogger log, ILogger statsLogger, bool outputCsv, ManualResetEventSlim doneEvent) {
			_tcpTestClient = tcpTestClient;
			_grpcTestClient = grpcTestClient;
			_clientApiTestClient = clientApiTestClient;
			Log = log;
			StatsLogger = statsLogger;
			_doneEvent = doneEvent;
			_timeout = timeout;
			OutputCsv = outputCsv;
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
			if (_timeout < 0)
				_doneEvent.Wait();
			else {
				if (!_doneEvent.Wait(_timeout * 1000))
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
