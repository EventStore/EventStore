using System;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests {
	// thing that can choose between a V2 or V3 implementation according to the
	// TStreamId type parameter. Handy for tests; don't write anything like this in the main code.
	internal static class LogFormatHelper<TStreamId> {
		public static T WhenV2<T>(object v2) =>
			typeof(TStreamId) == typeof(string) ? (T)v2 : throw new NotImplementedException();

		public static T Choose<T>(object v2, object v3) =>
			typeof(TStreamId) == typeof(string) ? (T)v2 : (T)v3;

		public static LogFormatAbstractor<TStreamId> LogFormat { get; } =
			WhenV2<LogFormatAbstractor<TStreamId>>(LogFormatAbstractor.V2);
	}
}
		
