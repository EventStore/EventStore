using System;
using EventStore.Common.Utils;

namespace EventStore.Common.Log {
	public class LazyLogger : ILogger {
		private readonly Lazy<ILogger> _logger;

		public LazyLogger(Func<ILogger> factory) {
			Ensure.NotNull(factory, "factory");
			_logger = new Lazy<ILogger>(factory);
		}

		public void Flush(TimeSpan? maxTimeToWait = null) {
			_logger.Value.Flush(maxTimeToWait);
		}

		public void Fatal(string format, params object[] args) {
			_logger.Value.Fatal(format, args);
		}

		public void Error(string format, params object[] args) {
			_logger.Value.Error(format, args);
		}

		public void Info(string format, params object[] args) {
			_logger.Value.Info(format, args);
		}

		public void Warn(string format, params object[] args) {
			_logger.Value.Warn(format, args);
		}

		public void Debug(string format, params object[] args) {
			_logger.Value.Debug(format, args);
		}

		public void Trace(string format, params object[] args) {
			_logger.Value.Trace(format, args);
		}

		public void FatalException(Exception exc, string format, params object[] args) {
			_logger.Value.FatalException(exc, format, args);
		}

		public void ErrorException(Exception exc, string format, params object[] args) {
			_logger.Value.ErrorException(exc, format, args);
		}

		public void InfoException(Exception exc, string format, params object[] args) {
			_logger.Value.InfoException(exc, format, args);
		}

		public void DebugException(Exception exc, string format, params object[] args) {
			_logger.Value.DebugException(exc, format, args);
		}

		public void WarnException(Exception exc, string format, params object[] args) {
			_logger.Value.WarnException(exc, format, args);
		}

		public void TraceException(Exception exc, string format, params object[] args) {
			_logger.Value.TraceException(exc, format, args);
		}
	}
}
