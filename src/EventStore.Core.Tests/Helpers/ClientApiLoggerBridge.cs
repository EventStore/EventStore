using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Helpers {
	public class ClientApiLoggerBridge : EventStore.ClientAPI.ILogger {
		public static readonly ClientApiLoggerBridge Default =
			new ClientApiLoggerBridge(LogManager.GetLogger("client-api"));

		private readonly ILogger _log;

		public ClientApiLoggerBridge(ILogger log) {
			Ensure.NotNull(log, "log");
			_log = log;
		}

		public void Error(string format, params object[] args) {
			if (args.Length == 0)
				_log.Error(format);
			else
				_log.Error(format, args);
		}

		public void Error(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.ErrorException(ex, format);
			else
				_log.ErrorException(ex, format, args);
		}

		public void Info(string format, params object[] args) {
			if (args.Length == 0)
				_log.Info(format);
			else
				_log.Info(format, args);
		}

		public void Info(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.InfoException(ex, format);
			else
				_log.InfoException(ex, format, args);
		}

		public void Debug(string format, params object[] args) {
			if (args.Length == 0)
				_log.Debug(format);
			else
				_log.Debug(format, args);
		}

		public void Debug(Exception ex, string format, params object[] args) {
			if (args.Length == 0)
				_log.DebugException(ex, format);
			else
				_log.DebugException(ex, format, args);
		}
	}
}
