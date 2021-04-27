using System;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests {
	public class LogFormat {
		public class V2{}
		public class V3{}
	}

	internal static class LogFormatHelper<TLogFormat, TStreamId> {
		public static T Choose<T>(object v2, object v3) {
			if (typeof(TLogFormat) == typeof(LogFormat.V2)) {
				if (typeof(TStreamId) != typeof(string)) throw new InvalidOperationException();
				return (T)v2;
			}
			if(typeof(TLogFormat) == typeof(LogFormat.V3)) {
				if (typeof(TStreamId) != typeof(long)) throw new InvalidOperationException($"TStreamId was {typeof(TStreamId)} but expected long");
				return (T)v3;
			}
			throw new InvalidOperationException();
		}

		public static LogFormatAbstractor<TStreamId> LogFormat { get; } =
			Choose<LogFormatAbstractor<TStreamId>>(LogFormatAbstractor.V2, LogFormatAbstractor.V3);

		public static void CheckIfExplicitTransactionsSupported() {
			if (typeof(TLogFormat) == typeof(LogFormat.V3)) {
				throw new InvalidOperationException("Explicit transactions are not supported yet by Log V3");
			}
		}

		public static void EnsureV0PrepareSupported() {
			if (typeof(TLogFormat) == typeof(LogFormat.V3)) {
				throw new InvalidOperationException("No such thing as a V0 prepare in LogV3");
			}
		}
	}
}
		
