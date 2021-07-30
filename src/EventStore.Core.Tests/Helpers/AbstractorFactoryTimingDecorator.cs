using System;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.Tests.Helpers {
	public class AbstractorFactoryTimingDecorator<TStreamId> : ILogFormatAbstractorFactory<TStreamId> {
		private readonly ILogFormatAbstractorFactory<TStreamId> _wrapped;
		private readonly int _streamExistenceFilterCheckpointDelayMs;
		private readonly int _streamExistenceFilterCheckpointIntervalMs;
		public AbstractorFactoryTimingDecorator(
			ILogFormatAbstractorFactory<TStreamId> wrapped,
			int streamExistenceFilterCheckpointIntervalMs,
			int streamExistenceFilterCheckpointDelayMs) {
			_wrapped = wrapped;
			_streamExistenceFilterCheckpointIntervalMs = streamExistenceFilterCheckpointIntervalMs;
			_streamExistenceFilterCheckpointDelayMs = streamExistenceFilterCheckpointDelayMs;
		}

		public LogFormatAbstractor<TStreamId> Create(LogFormatAbstractorOptions options) {
			return _wrapped.Create(options with {
				StreamExistenceFilterCheckpointDelay = TimeSpan.FromMilliseconds(_streamExistenceFilterCheckpointDelayMs),
				StreamExistenceFilterCheckpointInterval = TimeSpan.FromMilliseconds(_streamExistenceFilterCheckpointIntervalMs),
			});
		}
	}
}
