using System;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using NLog;
using NLog.Conditions;

namespace EventStore.Common.Log {
	public static class NLoggerHelperMethods {
		[ConditionMethod("is-dot-net")]
		public static bool IsDotNet() {
			return !Runtime.IsMono;
		}

		[ConditionMethod("is-mono")]
		public static bool IsMono() {
			return Runtime.IsMono;
		}

		[ConditionMethod("is-structured")]
		public static bool IsStructured() {
			return LogManager.StructuredLog;
		}
	}

	public class NLogger : ILogger {
		private readonly Logger _logger;

		public NLogger(string name) {
			_logger = NLog.LogManager.GetLogger(name);
		}

		public void Flush(TimeSpan? maxTimeToWait = null) {
			FlushLog(maxTimeToWait);
		}

		public void Fatal(string format, params object[] args) {
			_logger.Fatal(format, args);
		}

		public void Error(string format, params object[] args) {
			_logger.Error(format, args);
		}

		public void Info(string format, params object[] args) {
			_logger.Info(format, args);
		}

		public void Debug(string format, params object[] args) {
			_logger.Debug(format, args);
		}

		public void Warn(string format, params object[] args) {
			_logger.Warn(format, args);
		}

		public void Trace(string format, params object[] args) {
			_logger.Trace(format, args);
		}

		public void FatalException(Exception exc, string format, params object[] args) {
			_logger.Fatal(exc, format, args);
		}

		public void ErrorException(Exception exc, string format, params object[] args) {
			_logger.Error(exc, format, args);
		}

		public void InfoException(Exception exc, string format, params object[] args) {
			_logger.Info(exc, format, args);
		}

		public void DebugException(Exception exc, string format, params object[] args) {
			_logger.Debug(exc, format, args);
		}

		public void WarnException(Exception exc, string format, params object[] args) {
			_logger.Warn(exc, format, args);
		}

		public void TraceException(Exception exc, string format, params object[] args) {
			_logger.Trace(exc, format, args);
		}

		public static void FlushLog(TimeSpan? maxTimeToWait = null) {
			var config = NLog.LogManager.Configuration;
			if (config == null)
				return;
			var asyncs = config.AllTargets.OfType<NLog.Targets.Wrappers.AsyncTargetWrapper>().ToArray();
			var countdown = new CountdownEvent(asyncs.Length);
			foreach (var wrapper in asyncs) {
				wrapper.Flush(x => countdown.Signal());
			}

			countdown.Wait(maxTimeToWait ?? TimeSpan.FromMilliseconds(500));
		}
	}
}
